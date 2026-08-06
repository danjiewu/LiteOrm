using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LiteOrm.Generators
{
    /// <summary>
    /// 增量源生成器：扫描编译单元中带 <c>[AutoRegister]</c> 特性的自定义服务与 DAO，
    /// 在编译期生成注册代码，通过模块初始化器登记到 <c>LiteOrm.LiteOrmAutoRegistration</c>，
    /// 使 <c>AddLiteOrm()</c> 能够自动注册自定义服务与 DAO。
    /// <para>
    /// 与运行时反射扫描（Autofac 的 <c>RegisterAutoService</c>）等价，但注册信息在编译期确定，
    /// 无需 <c>Assembly.GetTypes()</c> / 反射，支持 NativeAOT 裁剪。
    /// </para>
    /// </summary>
    [Generator]
    public class AutoRegisterGenerator : IIncrementalGenerator
    {
        private const string AutoRegisterAttributeFullTypeName = "LiteOrm.Common.AutoRegisterAttribute";
        private const string ServiceLifetimeFullTypeName = "LiteOrm.Common.Lifetime";
        private const string RegistryFullTypeName = "LiteOrm.LiteOrmAutoRegistration";
        private const string ServiceCollectionTypeName = "Microsoft.Extensions.DependencyInjection.IServiceCollection";

        /// <summary>
        /// 非泛型标记接口（仅作为约定标记，不作为服务注册类型），与运行时
        /// <c>LiteOrmServiceExtensions.IsExcludedMarkerInterface</c> 保持一致。
        /// </summary>
        private static readonly string[] ExcludedMarkerInterfaces =
        {
            "LiteOrm.Common.IObjectViewDAO",
            "LiteOrm.Common.IObjectDAO",
            "LiteOrm.Common.IObjectDAOAsync",
            "LiteOrm.Service.IEntityService",
            "LiteOrm.Service.IEntityServiceAsync",
            "LiteOrm.Service.IEntityViewService",
            "LiteOrm.Service.IEntityViewServiceAsync",
        };

        private sealed class Candidate
        {
            public INamedTypeSymbol Type { get; set; } = null!;
            public AttributeData Attr { get; set; } = null!;
        }

        private sealed class Registration
        {
            public string ImplRef { get; set; } = null!;
            public string LifetimeMethod { get; set; } = null!;
            public bool IsGenericDefinition { get; set; }
            public List<string> ServiceRefs { get; set; } = new();
            public bool Keyed { get; set; }
            public string? KeyLiteral { get; set; }
        }

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var candidates = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is TypeDeclarationSyntax,
                    transform: static (ctx, _) =>
                    {
                        if (ctx.Node is not TypeDeclarationSyntax tds) return null;
                        if (ctx.SemanticModel.GetDeclaredSymbol(tds) is not INamedTypeSymbol symbol) return null;
                        if (symbol.IsStatic || symbol.IsAbstract || symbol.TypeKind != TypeKind.Class) return null;
                        var attr = GetAutoRegisterAttribute(symbol);
                        if (attr == null) return null;
                        return new Candidate { Type = symbol, Attr = attr };
                    })
                .Where(static t => t is not null)
                .Collect()
                .Combine(context.CompilationProvider);

            context.RegisterSourceOutput(candidates, static (spc, source) =>
            {
                var (items, compilation) = source;

                // 运行时注册中心（LiteOrm 核心程序集）必须可用，且编译需引用 DI 抽象
                if (compilation.GetTypeByMetadataName(RegistryFullTypeName) == null) return;
                if (compilation.GetTypeByMetadataName(ServiceCollectionTypeName) == null) return;

                var registrations = new List<Registration>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in items)
                {
                    if (item is null) continue;
                    if (!IsEnabled(item.Attr)) continue;
                    var key = item.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (!seen.Add(key)) continue;
                    var registration = BuildRegistration(item.Type, item.Attr);
                    if (registration != null)
                        registrations.Add(registration);
                }

                if (registrations.Count == 0) return;

                spc.AddSource("LiteOrmAutoRegister.g.cs", GenerateCode(registrations));
            });
        }

        // ──────────────────────────────────────────────────────────────
        // 特性解析
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 获取类型上的 <c>[AutoRegister]</c> 特性，若类型自身未声明则沿基类链向上查找
        /// （与运行时 <c>GetCustomAttribute&lt;AutoRegisterAttribute&gt;(true)</c> 的继承语义一致）。
        /// </summary>
        private static AttributeData? GetAutoRegisterAttribute(INamedTypeSymbol symbol)
        {
            for (INamedTypeSymbol? current = symbol; current != null; current = current.BaseType)
            {
                var attr = current.GetAttributes().FirstOrDefault(a =>
                    a.AttributeClass != null &&
                    a.AttributeClass.ToDisplayString() == AutoRegisterAttributeFullTypeName);
                if (attr != null) return attr;
            }
            return null;
        }

        private static bool TryGetNamedArg(AttributeData attr, string name, out TypedConstant value)
        {
            foreach (var pair in attr.NamedArguments)
            {
                if (pair.Key == name)
                {
                    value = pair.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }

        /// <summary>
        /// 判断特性是否启用。仅 <c>AutoRegisterAttribute(bool enabled)</c> 构造函数会显式设置
        /// <c>Enabled</c>；其余构造函数默认启用。
        /// </summary>
        private static bool IsEnabled(AttributeData attr)
        {
            foreach (var arg in attr.ConstructorArguments)
            {
                if (arg.Type?.SpecialType == SpecialType.System_Boolean)
                    return arg.Value is true;
            }
            return true;
        }

        /// <summary>
        /// 读取生命周期：命名参数 <c>Lifetime</c> 优先，其次构造函数参数，默认 Singleton(0)。
        /// </summary>
        private static int GetLifetime(AttributeData attr)
        {
            if (TryGetNamedArg(attr, "Lifetime", out var tc) && !tc.IsNull && tc.Value is int i)
                return i;
            foreach (var arg in attr.ConstructorArguments)
            {
                if (arg.Type?.ToDisplayString() == ServiceLifetimeFullTypeName && arg.Value is int li)
                    return li;
            }
            return 0;
        }

        private static string LifetimeMethodName(int lifetime) => lifetime switch
        {
            1 => "Scoped",
            2 => "Transient",
            _ => "Singleton"
        };

        /// <summary>
        /// 读取显式指定的服务类型（命名参数 <c>ServiceTypes</c> 或 <c>params Type[]</c> 构造函数参数）。
        /// </summary>
        private static List<INamedTypeSymbol> GetExplicitServiceTypes(AttributeData attr)
        {
            var result = new List<INamedTypeSymbol>();
            TypedConstant? arrayTc = null;
            if (TryGetNamedArg(attr, "ServiceTypes", out var tc) && tc.Kind == TypedConstantKind.Array)
                arrayTc = tc;
            else
            {
                foreach (var arg in attr.ConstructorArguments)
                {
                    if (arg.Kind == TypedConstantKind.Array)
                    {
                        arrayTc = arg;
                        break;
                    }
                }
            }

            if (arrayTc != null)
            {
                foreach (var element in arrayTc.Value.Values)
                {
                    if (element.Value is INamedTypeSymbol typeSymbol)
                        result.Add(typeSymbol);
                }
            }
            return result;
        }

        /// <summary>
        /// 计算注册的服务类型集合，与运行时 <c>GetServiceTypes</c> 语义一致：
        /// 显式 <c>ServiceTypes</c> 优先；否则取所有非 System、非标记接口的已实现接口；始终包含实现类型自身。
        /// </summary>
        private static List<ITypeSymbol> GetServiceTypes(INamedTypeSymbol implementationType, AttributeData attr)
        {
            var serviceTypes = new List<ITypeSymbol>();

            var explicitTypes = GetExplicitServiceTypes(attr);
            if (explicitTypes.Any())
            {
                serviceTypes.AddRange(explicitTypes);
            }
            else
            {
                foreach (var iface in implementationType.AllInterfaces)
                {
                    var ns = iface.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                    if (ns.Length == 0 || ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal))
                        continue;
                    if (IsExcludedMarkerInterface(iface))
                        continue;

                    var ifaceAttr = iface.GetAttributes().FirstOrDefault(a =>
                        a.AttributeClass != null && a.AttributeClass.ToDisplayString() == AutoRegisterAttributeFullTypeName);
                    if (ifaceAttr != null && !IsEnabled(ifaceAttr))
                        continue;

                    if (implementationType.IsGenericType)
                    {
                        if (iface.IsGenericType &&
                            implementationType.TypeParameters.Length == iface.TypeArguments.Length &&
                            iface.TypeArguments.All(t => t.TypeKind == TypeKind.TypeParameter &&
                                                         SymbolEqualityComparer.Default.Equals(
                                                             ((ITypeParameterSymbol)t).DeclaringType, implementationType)))
                        {
                            serviceTypes.Add(iface.OriginalDefinition);
                        }
                    }
                    else
                    {
                        serviceTypes.Add(iface);
                    }
                }
            }

            // 始终将实现类型自身注册为服务（MS.DI 无拦截器，无需因 Intercept 跳过自身）
            if (!serviceTypes.Any(s => SymbolEqualityComparer.Default.Equals(s, implementationType)))
                serviceTypes.Add(implementationType);

            return serviceTypes;
        }

        private static bool IsExcludedMarkerInterface(INamedTypeSymbol iface)
        {
            if (iface.IsGenericType) return false;
            var name = iface.ToDisplayString();
            return ExcludedMarkerInterfaces.Contains(name);
        }

        // ──────────────────────────────────────────────────────────────
        // 构建注册描述
        // ──────────────────────────────────────────────────────────────

        private static Registration? BuildRegistration(INamedTypeSymbol type, AttributeData attr)
        {
            var lifetime = GetLifetime(attr);
            var serviceTypes = GetServiceTypes(type, attr);
            if (serviceTypes.Count == 0) return null;

            var registration = new Registration
            {
                ImplRef = RenderTypeRef(type),
                LifetimeMethod = LifetimeMethodName(lifetime),
                IsGenericDefinition = type.IsGenericType,
            };

            if (TryGetNamedArg(attr, "Key", out var keyTc) && !keyTc.IsNull)
            {
                var keyLiteral = RenderKeyLiteral(keyTc, type);
                if (keyLiteral != null)
                {
                    registration.Keyed = true;
                    registration.KeyLiteral = keyLiteral;
                }
            }

            foreach (var serviceType in serviceTypes)
            {
                registration.ServiceRefs.Add(RenderTypeRef(serviceType));
            }
            return registration;
        }

        /// <summary>
        /// 将类型符号渲染为 <c>typeof(...)</c> 内部可用的完全限定引用。
        /// 开放泛型渲染为 <c>global::NS.Type&lt;&gt;</c>。
        /// </summary>
        private static string RenderTypeRef(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol named && named.IsGenericType &&
                named.TypeArguments.Length > 0 &&
                named.TypeArguments.All(t => t.TypeKind == TypeKind.TypeParameter))
            {
                // 开放泛型：手动构造 global::NS.Outer<>...Inner<>
                var sb = new StringBuilder("global::");
                if (!named.ContainingNamespace.IsGlobalNamespace)
                    sb.Append(named.ContainingNamespace.ToDisplayString()).Append('.');
                var parts = new Stack<string>();
                for (var cur = named; cur != null; cur = cur.ContainingType)
                {
                    parts.Push(cur.Name + (cur.IsGenericType ? "<>" : ""));
                }
                sb.Append(string.Join(".", parts));
                return sb.ToString();
            }
            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        private static string? RenderKeyLiteral(TypedConstant key, INamedTypeSymbol context)
        {
            switch (key.Kind)
            {
                case TypedConstantKind.Primitive:
                    if (key.Value is string s) return "\"" + EscapeString(s) + "\"";
                    if (key.Value is bool b) return b ? "true" : "false";
                    if (key.Value is char c) return "'" + c + "'";
                    if (key.Value is null) return null;
                    // 数值
                    return Convert.ToString(key.Value, System.Globalization.CultureInfo.InvariantCulture);
                case TypedConstantKind.Enum:
                    if (key.Type is INamedTypeSymbol enumType && key.Value != null)
                    {
                        foreach (var member in enumType.GetMembers())
                        {
                            if (member is IFieldSymbol field && field.HasConstantValue &&
                                Equals(field.ConstantValue, key.Value))
                            {
                                return enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + field.Name;
                            }
                        }
                    }
                    return null;
                case TypedConstantKind.Type:
                    if (key.Value is ITypeSymbol typeSymbol)
                        return "typeof(" + RenderTypeRef(typeSymbol) + ")";
                    return null;
                default:
                    return null;
            }
        }

        private static string EscapeString(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        // ──────────────────────────────────────────────────────────────
        // 代码生成
        // ──────────────────────────────────────────────────────────────

        private static string GenerateCode(List<Registration> registrations)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine();
            sb.AppendLine($"namespace {CodeGenHelper.ProviderFullNamespace}");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 源生成的自定义服务与 DAO 注册器，由 [AutoRegister] 特性驱动。");
            sb.AppendLine("    /// 在模块初始化时登记到 LiteOrmAutoRegistration，由 AddLiteOrm 应用。");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    internal static class LiteOrmGeneratedAutoRegister");
            sb.AppendLine("    {");
            sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
            sb.AppendLine("        internal static void Register()");
            sb.AppendLine("        {");
            sb.AppendLine("            global::LiteOrm.LiteOrmAutoRegistration.Add(RegisterServices);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        internal static void RegisterServices(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
            sb.AppendLine("        {");
            foreach (var reg in registrations)
            {
                AppendRegistration(sb, reg);
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void AppendRegistration(StringBuilder sb, Registration reg)
        {
            // 实现类型自身注册（开放泛型用 typeof，普通类型直接泛型语法）
            if (reg.IsGenericDefinition)
            {
                AppendService(sb, reg, null);
            }
            else
            {
                AppendService(sb, reg, reg.ImplRef);
            }

            // 服务类型注册
            foreach (var serviceRef in reg.ServiceRefs)
            {
                if (string.Equals(serviceRef, reg.ImplRef, StringComparison.Ordinal)) continue;
                AppendService(sb, reg, serviceRef);
            }
        }

        private static void AppendService(StringBuilder sb, Registration reg, string? serviceRef)
        {
            if (reg.Keyed)
            {
                sb.AppendLine("#if NET8_0_OR_GREATER");
                if (serviceRef == null)
                    sb.AppendLine($"                services.AddKeyed{reg.LifetimeMethod}({reg.KeyLiteral}, typeof({reg.ImplRef}));");
                else
                    sb.AppendLine($"                services.AddKeyed{reg.LifetimeMethod}({reg.KeyLiteral}, typeof({serviceRef}), typeof({reg.ImplRef}));");
                sb.AppendLine("#else");
            }

            if (serviceRef == null)
                sb.AppendLine($"                services.Add{reg.LifetimeMethod}(typeof({reg.ImplRef}));");
            else
                sb.AppendLine($"                services.Add{reg.LifetimeMethod}(typeof({serviceRef}), typeof({reg.ImplRef}));");

            if (reg.Keyed)
                sb.AppendLine("#endif");
        }
    }
}
