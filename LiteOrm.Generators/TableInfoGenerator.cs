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
    /// 增量源生成器：扫描带 [Table] 特性的实体类型，在编译期生成
    /// TableDefinition / DataReader 映射委托 / 属性访问器委托 / AOT 类型注册，
    /// 以替代运行时反射与 Expression.Compile()，支持 NativeAOT。
    /// <para>
    /// 仅在 AOT/裁剪模式下生成代码（如 <c>PublishAot=true</c>、<c>IsAotCompatible=true</c>、
    /// <c>PublishTrimmed=true</c> 或 <c>IsTrimmable=true</c>，经 SDK 的
    /// <c>enableaotanalyzer</c>/<c>enabletrimanalyzer</c> 等属性传递给分析器）；
    /// 非 AOT 模式下运行时回退到反射与 <see cref="System.Linq.Expressions.Expression.Compile()"/>，
    /// 不生成任何多余代码，避免额外的编译开销与程序集膨胀。
    /// </para>
    /// </summary>
    [Generator]
    public class TableInfoGenerator : IIncrementalGenerator
    {
        private const string TableAttributeFullTypeName = "LiteOrm.Common.TableAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // 1) 收集 class/record 声明，类型自身或基类链上带 [Table] 特性即视为实体/视图。
            //    视图模型自身没有 [Table]，通过继承基类实体的 [Table] 捕获（与运行时
            //    GetCustomAttribute<TableAttribute>(true) 的继承语义一致）。
            var entityTypes = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is TypeDeclarationSyntax,
                    transform: static (ctx, _) =>
                    {
                        if (ctx.Node is not TypeDeclarationSyntax tds) return null;
                        if (ctx.SemanticModel.GetDeclaredSymbol(tds) is not INamedTypeSymbol symbol || symbol.IsStatic || symbol.IsAbstract)
                            return null;
                        for (var current = symbol;
                             current != null && current.SpecialType != SpecialType.System_Object;
                             current = current.BaseType)
                        {
                            if (current.GetAttributes().Any(a =>
                                    a.AttributeClass != null &&
                                    a.AttributeClass.ToDisplayString() == TableAttributeFullTypeName))
                                return symbol;
                        }
                        return null;
                    })
                .Where(static t => t is not null);

            // 2) 检测 AOT/裁剪模式：仅当构建面向 NativeAOT（或启用裁剪分析）时才生成源码。
            //    使用 SDK 提供给分析器可见的属性（build_property.* 小写）：
            //    - enableaotanalyzer=true：PublishAot=true 或 IsAotCompatible=true
            //    - enabletrimanalyzer=true：PublishTrimmed=true 或 IsTrimmable=true
            var aotMode = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
            {
                bool IsTrue(string key) => provider.GlobalOptions.TryGetValue(key, out string? v) && v == "true";
                return IsTrue("build_property.enableaotanalyzer")
                    || IsTrue("build_property.enabletrimanalyzer")
                    || IsTrue("build_property.publishaot")
                    || IsTrue("build_property.isaotcompatible")
                    || IsTrue("build_property.publishtrimmed")
                    || IsTrue("build_property.istrimmable");
            });

            // 3) 收集所有实体类型符号，与当前 Compilation 组合
            var compilationAndEntities = entityTypes.Collect().Combine(context.CompilationProvider);

            var pipeline = compilationAndEntities.Combine(aotMode);

            // 4) 生成代码（仅 AOT 模式）
            context.RegisterSourceOutput(pipeline, static (spc, source) =>
            {
                var ((entities, compilation), isAot) = source;
                // 非 AOT 模式：运行时使用反射与 Expression.Compile()，无需生成额外代码
                if (!isAot)
                {
                    return;
                }
                if (entities.IsEmpty)
                {
                    return;
                }

                GenerateAll(spc, compilation, entities);
            });
        }

        // ──────────────────────────────────────────────────────────────
        // 辅助：从 NamedArguments (ImmutableArray<KeyValuePair>) 中按名查找
        // ──────────────────────────────────────────────────────────────
        private static bool TryGetNamedArg(ImmutableArray<KeyValuePair<string, TypedConstant>> namedArgs, string name, out TypedConstant value)
        {
            foreach (var pair in namedArgs)
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

        // ──────────────────────────────────────────────────────────────
        // 主入口：生成三个文件 + 模块初始化器
        // ──────────────────────────────────────────────────────────────
        private static void GenerateAll(SourceProductionContext spc, Compilation compilation, IReadOnlyList<INamedTypeSymbol> entities)
        {
            // 始终生成 AOT 类型注册代码（即使没有 [Table] 实体）
            GenerateAotTypeRegistration(spc, compilation);

            var distinctEntities = entities.Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>().ToList();
            distinctEntities.Sort((a, b) => string.Compare(a.ToDisplayString(), b.ToDisplayString(), StringComparison.Ordinal));

            // 解析符号引用
            var symbols = ResolveSymbols(compilation);
            if (symbols == null)
            {
                return;
            }
            var resolved = symbols.Value;

            // 收集每个实体的元数据描述
            var entityInfos = new List<EntityInfo>(distinctEntities.Count);
            foreach (var entity in distinctEntities)
            {
                // 跳过不可访问的类型（如嵌套私有类），源生成器无法访问它们
                if (entity.DeclaredAccessibility == Accessibility.Private ||
                    entity.DeclaredAccessibility == Accessibility.Protected ||
                    entity.DeclaredAccessibility == Accessibility.ProtectedOrInternal ||
                    entity.DeclaredAccessibility == Accessibility.ProtectedAndInternal)
                    continue;
                // 跳过抽象基类（如 ObjectBase），抽象类不是可映射的实体表
                if (entity.IsAbstract)
                    continue;

                var info = BuildEntityInfo(entity, compilation, resolved);
                if (info != null)
                    entityInfos.Add(info);
            }

            if (entityInfos.Count == 0)
                return;

            // 1. 生成 TableInfo.g.cs
            var tableInfoCode = GenerateTableInfoProvider(entityInfos, resolved);
            spc.AddSource("TableInfo.g.cs", tableInfoCode);

            // 2. 生成 DataReaderMappers.g.cs
            var mapperCode = GenerateDataReaderMappers(entityInfos, resolved);
            spc.AddSource("DataReaderMappers.g.cs", mapperCode);

            // 3. 生成 PropertyAccessors.g.cs
            var accessorCode = GeneratePropertyAccessors(entityInfos, resolved);
            spc.AddSource("PropertyAccessors.g.cs", accessorCode);

            // 4. 生成模块初始化器
            var initCode = GenerateModuleInitializer(entityInfos, resolved);
            spc.AddSource("ModuleInitializer.g.cs", initCode);
        }

        // ──────────────────────────────────────────────────────────────
        // 符号解析
        // ──────────────────────────────────────────────────────────────
        private record struct ResolvedSymbols(
            INamedTypeSymbol TableAttribute,
            INamedTypeSymbol ColumnAttribute,
            INamedTypeSymbol? ForeignColumnAttribute,
            INamedTypeSymbol? TableJoinAttribute,
            INamedTypeSymbol TableDefinition,
            INamedTypeSymbol ColumnDefinition,
            INamedTypeSymbol TableView,
            INamedTypeSymbol TableInfoProvider,
            INamedTypeSymbol? DataReaderConverter,
            INamedTypeSymbol PropertyAccessorExtension,
            INamedTypeSymbol SqlColumn,
            INamedTypeSymbol? JoinedTable,
            INamedTypeSymbol? ColumnRef,
            INamedTypeSymbol? ForeignTable,
            INamedTypeSymbol DbType,
            INamedTypeSymbol ColumnMode,
            INamedTypeSymbol? EnumUtil,
            INamedTypeSymbol? TypeResolverHelper,
            INamedTypeSymbol? DisplayNameAttribute,
            INamedTypeSymbol? DescriptionAttribute
        );

        private static ResolvedSymbols? ResolveSymbols(Compilation compilation)
        {
            var tableAttr = compilation.GetTypeByMetadataName("LiteOrm.Common.TableAttribute");
            var columnAttr = compilation.GetTypeByMetadataName("LiteOrm.Common.ColumnAttribute");
            var foreignColumnAttr = compilation.GetTypeByMetadataName("LiteOrm.Common.ForeignColumnAttribute");
            var tableJoinAttr = compilation.GetTypeByMetadataName("LiteOrm.Common.TableJoinAttribute");
            var tableDef = compilation.GetTypeByMetadataName("LiteOrm.Common.TableDefinition");
            var colDef = compilation.GetTypeByMetadataName("LiteOrm.Common.ColumnDefinition");
            var tableView = compilation.GetTypeByMetadataName("LiteOrm.Common.TableView");
            var provider = compilation.GetTypeByMetadataName("LiteOrm.Common.TableInfoProvider");
            var drConverter = compilation.GetTypeByMetadataName("LiteOrm.DataReaderConverter");
            var propExt = compilation.GetTypeByMetadataName("PropertyAccessorExtension");
            var sqlColumn = compilation.GetTypeByMetadataName("LiteOrm.Common.SqlColumn");
            var joinedTable = compilation.GetTypeByMetadataName("LiteOrm.Common.JoinedTable");
            var columnRef = compilation.GetTypeByMetadataName("LiteOrm.Common.ColumnRef");
            var foreignTable = compilation.GetTypeByMetadataName("LiteOrm.Common.ForeignTable");
            var dbType = compilation.GetTypeByMetadataName("LiteOrm.Common.DbValueType");
            var columnMode = compilation.GetTypeByMetadataName("LiteOrm.Common.ColumnMode");
            var enumUtil = compilation.GetTypeByMetadataName("LiteOrm.EnumUtil");
            var typeResolverHelper = compilation.GetTypeByMetadataName("LiteOrm.Common.TypeResolverHelper");
            var displayNameAttr = compilation.GetTypeByMetadataName("System.ComponentModel.DisplayNameAttribute");
            var descriptionAttr = compilation.GetTypeByMetadataName("System.ComponentModel.DescriptionAttribute");

            if (tableAttr == null || tableDef == null || colDef == null || tableView == null || provider == null
                || propExt == null || sqlColumn == null || dbType == null || columnMode == null)
                return null;

            return new ResolvedSymbols(
                tableAttr, columnAttr!, foreignColumnAttr, tableJoinAttr, tableDef, colDef, tableView, provider,
                drConverter, propExt, sqlColumn, joinedTable, columnRef, foreignTable,
                dbType, columnMode, enumUtil, typeResolverHelper, displayNameAttr, descriptionAttr
            );
        }

        // ──────────────────────────────────────────────────────────────
        // 实体信息收集
        // ──────────────────────────────────────────────────────────────
        internal sealed class EntityInfo
        {
            public INamedTypeSymbol Type { get; set; } = null!;
            public string FullName { get; set; } = null!;
            public string SafeName { get; set; } = null!;
            public string? TableName { get; set; }
            public string? DataSource { get; set; }
            public int SyncTableInt { get; set; } = 0;
            public bool IsView { get; set; } = false;
            public List<ColumnInfo> Columns { get; set; } = new();
            public List<JoinedTableInfo> JoinedTables { get; set; } = new();
            public List<ForeignColumnInfo> ForeignColumns { get; set; } = new();
        }

        internal sealed class ColumnInfo
        {
            public string PropertyName { get; set; } = null!;
            public string ColumnName { get; set; } = null!;
            public string PropertyType { get; set; } = null!;
            public bool IsPrimaryKey { get; set; }
            public bool IsIdentity { get; set; }
            public bool IsTimestamp { get; set; }
            public bool IsIndex { get; set; }
            public bool IsUnique { get; set; }
            public bool AllowNull { get; set; }
            public int Length { get; set; }
            public string? DbType { get; set; }
            public string? Expression { get; set; }
            public string ColumnMode { get; set; } = "7"; // ColumnMode.Full = Read|Update|Insert = 1|2|4 = 7
            public string? DefaultValue { get; set; }
            public string? IdentityExpression { get; set; }
            public long IdentityStart { get; set; } = 1;
            public int IdentityIncreasement { get; set; } = 1;
            public bool CanRead { get; set; }
            public bool CanWrite { get; set; }
            public IPropertySymbol Symbol { get; set; } = null!;
        }

        internal sealed class JoinedTableInfo
        {
            public string TargetTypeName { get; set; } = null!;
            public string Alias { get; set; } = null!;
            public string JoinType { get; set; } = "Inner";
            public bool AutoExpand { get; set; }
            public string ForeignKeys { get; set; } = "";
            public string? PrimeKeys { get; set; }
            public string? Source { get; set; }
        }

        internal sealed class ForeignColumnInfo
        {
            public string PropertyName { get; set; } = null!;
            public string ForeignTable { get; set; } = null!;
            public string? Property { get; set; }
        }

        // ──────────────────────────────────────────────────────────────
        // 获取类型及基类的所有实例属性（含继承层次）
        // ──────────────────────────────────────────────────────────────
        private static IEnumerable<IPropertySymbol> GetAllInstanceProperties(INamedTypeSymbol type)
        {
            var current = type;
            while (current != null && current.SpecialType != SpecialType.System_Object)
            {
                foreach (var member in current.GetMembers())
                {
                    if (member is IPropertySymbol prop && !prop.IsStatic && !prop.IsIndexer)
                        yield return prop;
                }
                current = current.BaseType;
            }
        }

        private static EntityInfo? BuildEntityInfo(INamedTypeSymbol type, Compilation compilation, ResolvedSymbols symbols)
        {
            var info = new EntityInfo
            {
                Type = type,
                FullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                SafeName = CodeGenHelper.SafeName(type.ToDisplayString().Replace("global::", "").Replace(".", "_").Replace(":", "_"))
            };

            // 读取 TableAttribute：视图模型本身没有 [Table]，通过基类链继承（与运行时
            // GetCustomAttribute<TableAttribute>(true) 的继承语义一致）。
            var tableAttr = FindTableAttribute(type, symbols);
            if (tableAttr == null)
                return null;
            // 仅当 [Table] 直接标注在当前类型上时才视为独立表实体；从基类继承的视为视图模型。
            bool isView = !type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.TableAttribute));
            info.IsView = isView;

            // TableName
            string? tableName = null;
            if (TryGetNamedArg(tableAttr.NamedArguments, "TableName", out var tn) && !tn.IsNull)
                tableName = tn.Value?.ToString();
            if (tableAttr.ConstructorArguments.Length == 1 && !tableAttr.ConstructorArguments[0].IsNull)
                tableName = tableAttr.ConstructorArguments[0].Value?.ToString();
            info.TableName = string.IsNullOrEmpty(tableName) ? type.Name : tableName;

            // DataSource
            if (TryGetNamedArg(tableAttr.NamedArguments, "DataSource", out var ds) && !ds.IsNull)
                info.DataSource = ds.Value?.ToString();

            // SyncTable
            if (TryGetNamedArg(tableAttr.NamedArguments, "SyncTable", out var st) && !st.IsNull)
                info.SyncTableInt = Convert.ToInt32(st.Value);

            // 收集列
            foreach (var prop in GetAllInstanceProperties(type))
            {
                // 视图模型上的 [ForeignColumn] 投影属性不是真实列，跳过
                if (symbols.ForeignColumnAttribute != null &&
                    prop.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.ForeignColumnAttribute)))
                    continue;

                var colAttr = prop.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.ColumnAttribute));
                if (colAttr != null)
                {
                    // 检查 IsColumn
                    bool isColumn = true;
                    if (TryGetNamedArg(colAttr.NamedArguments, "IsColumn", out var ic) && !ic.IsNull && ic.Value is bool b)
                        isColumn = b;
                    else if (colAttr.ConstructorArguments.Length == 1 && colAttr.ConstructorArguments[0].Value is bool b2)
                        isColumn = b2;

                    if (!isColumn)
                        continue;

                    info.Columns.Add(BuildColumnInfo(prop, colAttr, symbols));
                }
                else
                {
                    // 无 ColumnAttribute 的属性：根据类型推断
                    var dbType = InferDbType(prop.Type, symbols);
                    if (dbType == "Object")
                        continue;

                    info.Columns.Add(new ColumnInfo
                    {
                        PropertyName = prop.Name,
                        ColumnName = prop.Name,
                        PropertyType = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        DbType = dbType,
                        CanRead = prop.GetMethod != null,
                        CanWrite = prop.SetMethod != null,
                        Symbol = prop,
                        AllowNull = !prop.Type.IsValueType || (prop.Type is INamedTypeSymbol nts && nts.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T),
                        ColumnMode = ComputeDefaultColumnMode(prop.GetMethod != null, prop.SetMethod != null).ToString()
                    });
                }
            }

            return info;
        }

        /// <summary>
        /// 在类型自身及其基类链上查找 [Table] 特性，与运行时
        /// <c>GetCustomAttribute&lt;TableAttribute&gt;(true)</c> 的继承语义一致。
        /// </summary>
        private static AttributeData? FindTableAttribute(INamedTypeSymbol type, ResolvedSymbols symbols)
        {
            var current = type;
            while (current != null && current.SpecialType != SpecialType.System_Object)
            {
                var attr = current.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.TableAttribute));
                if (attr != null)
                    return attr;
                current = current.BaseType;
            }
            return null;
        }

        private static ColumnInfo BuildColumnInfo(IPropertySymbol prop, AttributeData colAttr, ResolvedSymbols symbols)
        {
            var info = new ColumnInfo
            {
                PropertyName = prop.Name,
                ColumnName = prop.Name,
                PropertyType = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                CanRead = prop.GetMethod != null,
                CanWrite = prop.SetMethod != null,
                Symbol = prop
            };

            // ColumnName
            if (TryGetNamedArg(colAttr.NamedArguments, "ColumnName", out var cn) && !cn.IsNull)
                info.ColumnName = cn.Value?.ToString() ?? prop.Name;
            else if (colAttr.ConstructorArguments.Length == 1 && !colAttr.ConstructorArguments[0].IsNull && colAttr.ConstructorArguments[0].Value is string colName)
                info.ColumnName = colName;

            if (TryGetNamedArg(colAttr.NamedArguments, "IsPrimaryKey", out var pk) && pk.Value is bool bPk) info.IsPrimaryKey = bPk;
            if (TryGetNamedArg(colAttr.NamedArguments, "IsIdentity", out var ii) && ii.Value is bool bIi) info.IsIdentity = bIi;
            if (TryGetNamedArg(colAttr.NamedArguments, "IsTimestamp", out var ts) && ts.Value is bool bTs) info.IsTimestamp = bTs;
            if (TryGetNamedArg(colAttr.NamedArguments, "IsIndex", out var idx) && idx.Value is bool bIdx) info.IsIndex = bIdx;
            if (TryGetNamedArg(colAttr.NamedArguments, "IsUnique", out var uq) && uq.Value is bool bUq) info.IsUnique = bUq;
            if (TryGetNamedArg(colAttr.NamedArguments, "AllowNull", out var an) && an.Value is bool bAn) info.AllowNull = bAn;
            if (TryGetNamedArg(colAttr.NamedArguments, "Length", out var len) && len.Value is int iLen) info.Length = iLen;
            if (TryGetNamedArg(colAttr.NamedArguments, "DefaultValue", out var dv) && !dv.IsNull) info.DefaultValue = dv.Value?.ToString();
            if (TryGetNamedArg(colAttr.NamedArguments, "IdentityExpression", out var ie) && !ie.IsNull) info.IdentityExpression = ie.Value?.ToString();
            if (TryGetNamedArg(colAttr.NamedArguments, "IdentityStart", out var ist) && ist.Value is long lIst) info.IdentityStart = lIst;
            if (TryGetNamedArg(colAttr.NamedArguments, "IdentityIncreasement", out var iic) && iic.Value is int iIic) info.IdentityIncreasement = iIic;

            // DbType: 空表示未显式指定，运行时由 SqlBuilder 推断
            if (TryGetNamedArg(colAttr.NamedArguments, "DbType", out var dt) && !dt.IsNull && dt.Value is int dbTypeInt)
                info.DbType = GetEnumMemberName(symbols.DbType, dbTypeInt) ?? dbTypeInt.ToString();
            else
                info.DbType = null;

            // 计算列表达式（非实际列）
            if (TryGetNamedArg(colAttr.NamedArguments, "Expression", out var ex) && !ex.IsNull)
                info.Expression = ex.Value?.ToString();

            int modeMask = ComputeDefaultColumnMode(info.CanRead, info.CanWrite);
            const int computedBit = 8; // ColumnMode.Computed
            if (TryGetNamedArg(colAttr.NamedArguments, "ColumnMode", out var cm) && !cm.IsNull)
            {
                // Roslyn 在处理枚举命名参数时可能返回装箱的枚举值（底层类型为 int）
                // 需要统一转换为整数字符串
                int rawMode;
                if (cm.Value is int cmInt)
                    rawMode = cmInt;
                else if (cm.Value is Enum cmEnum)
                    rawMode = (int)(object)cmEnum;
                else if (cm.Value != null)
                {
                    // 尝试通过 Convert.ToInt32 处理其他可能的数值类型
                    try { rawMode = Convert.ToInt32(cm.Value); }
                    catch { rawMode = 0; }
                }
                else
                    rawMode = 0;
                // 计算列位与读写掩码正交，需单独保留
                info.ColumnMode = ((rawMode & modeMask) | (rawMode & computedBit)).ToString();
            }
            else
                info.ColumnMode = modeMask.ToString();

            return info;
        }

        /// <summary>
        /// 计算默认列操作模式，与运行时 <c>AttributeTableInfoProvider</c> 保持一致：
        /// <c>Full &amp; ((CanRead ? Write : None) | (CanWrite ? Read : None))</c>。
        /// Write = Insert|Update = 6，Read = 1；可读可写属性默认即为 Full(7)。
        /// </summary>
        private static int ComputeDefaultColumnMode(bool canRead, bool canWrite)
        {
            const int Write = 6; // ColumnMode.Insert | ColumnMode.Update
            const int Read = 1;  // ColumnMode.Read
            return (canRead ? Write : 0) | (canWrite ? Read : 0);
        }

        /// <summary>
        /// 根据枚举成员的整数值获取成员名，供内部 DbType 名称转换使用。
        /// </summary>
        private static string? GetEnumMemberName(INamedTypeSymbol enumSymbol, int value)
        {
            foreach (var member in enumSymbol.GetMembers())
            {
                if (member is IFieldSymbol field && field.HasConstantValue && field.ConstantValue is int v && v == value)
                    return field.Name;
            }
            return null;
        }

        /// <summary>
        /// 根据枚举成员名获取其整数值，供内部 DbType 生成使用。
        /// 找不到时返回 -1，表示不生成 DbType。
        /// </summary>
        private static int GetEnumMemberValue(INamedTypeSymbol enumSymbol, string name)
        {
            foreach (var member in enumSymbol.GetMembers())
            {
                if (member is IFieldSymbol field && field.HasConstantValue && field.Name == name && field.ConstantValue is int v)
                    return v;
            }
            return -1;
        }

        /// <summary>
        /// 转义生成到 C# 字符串字面量中的文本（反斜杠与双引号）。
        /// </summary>
        private static string EscapeCSharpString(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string InferDbType(ITypeSymbol type, ResolvedSymbols symbols)
        {
            var t = type;
            if (t is INamedTypeSymbol nts && nts.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T && nts.IsGenericType)
                t = nts.TypeArguments[0];

            return t.SpecialType switch
            {
                SpecialType.System_Boolean => "Boolean",
                SpecialType.System_Byte => "Byte",
                SpecialType.System_SByte => "SByte",
                SpecialType.System_Int16 => "Int16",
                SpecialType.System_UInt16 => "UInt16",
                SpecialType.System_Int32 => "Int32",
                SpecialType.System_UInt32 => "UInt32",
                SpecialType.System_Int64 => "Int64",
                SpecialType.System_UInt64 => "UInt64",
                SpecialType.System_Single => "Single",
                SpecialType.System_Double => "Double",
                SpecialType.System_Decimal => "Decimal",
                SpecialType.System_String => "String",
                SpecialType.System_DateTime => "DateTime",
                SpecialType.System_Char => "String",
                SpecialType.System_Object => "Object",
                _ => t.TypeKind == TypeKind.Enum ? "Int32" : "Object"
            };
        }

        // ──────────────────────────────────────────────────────────────
        // 1. 生成 TableInfo Provider
        // ──────────────────────────────────────────────────────────────
        private static string GenerateTableInfoProvider(List<EntityInfo> entities, ResolvedSymbols symbols)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Data;");
            sb.AppendLine("using System.Reflection;");
            sb.AppendLine("using LiteOrm.Common;");
            sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
            sb.AppendLine();
            sb.AppendLine($"namespace {CodeGenHelper.ProviderFullNamespace}");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 源生成器在编译期生成的表信息提供者，替代基于反射的 AttributeTableInfoProvider。");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public class SourceGeneratedTableInfoProvider : TableInfoProvider");
            sb.AppendLine("    {");
            sb.AppendLine("        private static readonly Dictionary<Type, TableDefinition> _tables;");
            sb.AppendLine("        private static readonly Dictionary<Type, TableView> _views;");
            sb.AppendLine("        private readonly object _syncLock = new object();");
            sb.AppendLine();
            sb.AppendLine("        static SourceGeneratedTableInfoProvider()");
            sb.AppendLine("        {");
            sb.AppendLine("            _tables = new Dictionary<Type, TableDefinition>");
            sb.AppendLine("            {");

            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                var trailing = i < entities.Count - 1 ? "," : "";
                sb.AppendLine($"                [typeof({e.FullName})] = Build{e.SafeName}Table(){trailing}");
            }

            sb.AppendLine("            };");
            sb.AppendLine("            _views = new Dictionary<Type, TableView>();");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public override TableDefinition? GetTableDefinition([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] Type objectType)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (objectType is null) return null;");
            sb.AppendLine("            lock (_syncLock)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (_tables.TryGetValue(objectType, out var tableDef)) return tableDef;");
            sb.AppendLine("                return null;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public override TableView? GetTableView([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] Type objectType)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (objectType is null) return null;");
            sb.AppendLine("            lock (_syncLock)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (_views.TryGetValue(objectType, out var cachedView)) return cachedView;");
            sb.AppendLine("                // 构建视图（简化版：不含关联表）");
            sb.AppendLine("                var tableDef = GetTableDefinition(objectType);");
            sb.AppendLine("                if (tableDef is null) return null;");
            sb.AppendLine("                var cols = new List<SqlColumn>();");
            sb.AppendLine("                foreach (var c in tableDef.Columns) cols.Add(c);");
            sb.AppendLine("                var view = new TableView(tableDef, cols, new List<JoinedTable>());");
            sb.AppendLine("                _views[objectType] = view;");
            sb.AppendLine("                return view;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // 为每个实体生成 Build 方法
            foreach (var e in entities)
            {
                GenerateBuildTableMethod(sb, e, symbols);
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void GenerateBuildTableMethod(StringBuilder sb, EntityInfo e, ResolvedSymbols symbols)
        {
            sb.AppendLine();
            sb.AppendLine($"        private static TableDefinition Build{e.SafeName}Table()");
            sb.AppendLine("        {");
            sb.AppendLine($"            var properties = new PropertyInfo[]");
            sb.AppendLine("            {");
            for (int i = 0; i < e.Columns.Count; i++)
            {
                var c = e.Columns[i];
                var trailing = i < e.Columns.Count - 1 ? "," : "";
                sb.AppendLine($"                typeof({e.FullName}).GetProperty(\"{c.PropertyName}\")!{trailing}");
            }
            sb.AppendLine("            };");
            sb.AppendLine("            var columns = new List<ColumnDefinition>();");
            sb.AppendLine("            foreach (var prop in properties)");
            sb.AppendLine("            {");
            sb.AppendLine("                columns.Add(new ColumnDefinition(prop));");
            sb.AppendLine("            }");
            sb.AppendLine();
            // 设置每个列的属性
            for (int i = 0; i < e.Columns.Count; i++)
            {
                var c = e.Columns[i];
                sb.AppendLine($"            columns[{i}].Name = \"{c.ColumnName}\";");
                sb.AppendLine($"            columns[{i}].IsPrimaryKey = {c.IsPrimaryKey.ToString().ToLowerInvariant()};");
                sb.AppendLine($"            columns[{i}].IsIdentity = {c.IsIdentity.ToString().ToLowerInvariant()};");
                sb.AppendLine($"            columns[{i}].IsTimestamp = {c.IsTimestamp.ToString().ToLowerInvariant()};");
                sb.AppendLine($"            columns[{i}].IsIndex = {c.IsIndex.ToString().ToLowerInvariant()};");
                sb.AppendLine($"            columns[{i}].IsUnique = {c.IsUnique.ToString().ToLowerInvariant()};");
                sb.AppendLine($"            columns[{i}].AllowNull = {c.AllowNull.ToString().ToLowerInvariant()};");
                sb.AppendLine($"            columns[{i}].Length = {c.Length};");
                // DbType 为 Default（未显式指定）时留空，由 SqlBuilder 运行时推断
                if (!string.IsNullOrEmpty(c.DbType) && c.DbType != "Object" && c.DbType != "Default")
                {
                    var dbTypeInt = GetEnumMemberValue(symbols.DbType, c.DbType!);
                    if (dbTypeInt >= 0)
                        sb.AppendLine($"            columns[{i}].DbType = (DbValueType){dbTypeInt};");
                }
                if (!string.IsNullOrEmpty(c.Expression))
                    sb.AppendLine($"            columns[{i}].Expression = \"{EscapeCSharpString(c.Expression!)}\";");
                sb.AppendLine($"            columns[{i}].Mode = (ColumnMode){c.ColumnMode};");
                if (!string.IsNullOrEmpty(c.IdentityExpression))
                    sb.AppendLine($"            columns[{i}].IdentityExpression = \"{c.IdentityExpression}\";");
                if (!string.IsNullOrEmpty(c.DefaultValue))
                    sb.AppendLine($"            columns[{i}].DefaultValue = \"{c.DefaultValue}\";");
                sb.AppendLine($"            columns[{i}].IdentityStart = {c.IdentityStart}L;");
                sb.AppendLine($"            columns[{i}].IdentityIncreasement = {c.IdentityIncreasement};");
            }
            sb.AppendLine();
            sb.AppendLine("            var tableDef = new TableDefinition(typeof(" + e.FullName + "), columns)");
            sb.AppendLine("            {");
            sb.AppendLine($"                Name = \"{e.TableName}\",");
            sb.AppendLine($"                DataSource = {(string.IsNullOrEmpty(e.DataSource) ? "null" : $"\"{e.DataSource}\"")},");
            sb.AppendLine($"                SyncTable = (SyncTableMode){e.SyncTableInt}");
            sb.AppendLine("            };");
            sb.AppendLine("            return tableDef;");
            sb.AppendLine("        }");
        }

        // ──────────────────────────────────────────────────────────────
        // 2. 生成 DataReader Mappers
        // ──────────────────────────────────────────────────────────────
        private static string GenerateDataReaderMappers(List<EntityInfo> entities, ResolvedSymbols symbols)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using LiteOrm;");
            sb.AppendLine();
            sb.AppendLine($"namespace {CodeGenHelper.ProviderFullNamespace}");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 源生成的 DataReader 映射委托，替代运行时 Expression.Compile()。");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    internal static class SourceGeneratedDataReaderMappers");
            sb.AppendLine("    {");
            sb.AppendLine("        public static void RegisterAll()");
            sb.AppendLine("        {");

            foreach (var e in entities)
            {
                if (e.Columns.Count == 0) continue;
                if (symbols.DataReaderConverter != null)
                    sb.AppendLine($"            DataReaderConverter.RegisterMapper<{e.FullName}>({e.SafeName}_Mapper);");
            }

            sb.AppendLine("        }");
            sb.AppendLine();

            // 为每个实体生成映射委托
            foreach (var e in entities)
            {
                if (e.Columns.Count == 0) continue;
                GenerateMapperMethod(sb, e);
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void GenerateMapperMethod(StringBuilder sb, EntityInfo e)
        {
            sb.AppendLine();
            sb.AppendLine($"        private static {e.FullName} {e.SafeName}_Mapper(global::LiteOrm.AutoLockDataReader reader)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var entity = new {e.FullName}();");
            for (int i = 0; i < e.Columns.Count; i++)
            {
                var c = e.Columns[i];
                if (!c.CanWrite) continue;
                var readExpr = GenerateTypedReadCall(c.PropertyType, i);
                sb.AppendLine($"            entity.{c.PropertyName} = {readExpr};");
            }
            sb.AppendLine("            return entity;");
            sb.AppendLine("        }");
        }

        private static string GenerateTypedReadCall(string propertyType, int ordinal)
        {
            // 去掉 global:: 前缀
            var type = propertyType.Replace("global::", "");

            // 处理 Nullable<T> 或 T? 语法
            string? innerType = null;
            if (type.StartsWith("System.Nullable<"))
                innerType = type.Substring("System.Nullable<".Length).TrimEnd('>');
            else if (type.EndsWith("?") && !type.EndsWith("??"))
                innerType = type.TrimEnd('?');

            if (innerType != null)
            {
                var readCall = GenerateScalarReadCall(innerType, ordinal);
                return $"reader.IsDBNull({ordinal}) ? default({type}) : {readCall}";
            }

            var scalarRead = GenerateScalarReadCall(type, ordinal);
            return $"reader.IsDBNull({ordinal}) ? default({type})! : {scalarRead}";
        }

        private static string GenerateScalarReadCall(string type, int ordinal)
        {
            return type switch
            {
                "bool" or "System.Boolean" => $"reader.GetBoolean({ordinal})",
                "byte" or "System.Byte" => $"reader.GetByte({ordinal})",
                "char" or "System.Char" => $"reader.GetChar({ordinal})",
                "short" or "System.Int16" => $"reader.GetInt16({ordinal})",
                "int" or "System.Int32" => $"reader.GetInt32({ordinal})",
                "long" or "System.Int64" => $"reader.GetInt64({ordinal})",
                "float" or "System.Single" => $"reader.GetFloat({ordinal})",
                "double" or "System.Double" => $"reader.GetDouble({ordinal})",
                "decimal" or "System.Decimal" => $"reader.GetDecimal({ordinal})",
                "string" or "System.String" => $"reader.GetString({ordinal})",
                "System.DateTime" => $"reader.GetDateTime({ordinal})",
                "System.Guid" => $"reader.GetGuid({ordinal})",
                _ => $"({type})reader.ChangeType(reader.GetValue({ordinal}), typeof({type}))!"
            };
        }

        // ──────────────────────────────────────────────────────────────
        // 3. 生成 Property Accessors
        // ──────────────────────────────────────────────────────────────
        private static string GeneratePropertyAccessors(List<EntityInfo> entities, ResolvedSymbols symbols)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Reflection;");
            sb.AppendLine("using LiteOrm.Common;");
            sb.AppendLine();
            sb.AppendLine($"namespace {CodeGenHelper.ProviderFullNamespace}");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 源生成的属性访问器委托，替代运行时 Expression.Compile()。");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    internal static class SourceGeneratedPropertyAccessors");
            sb.AppendLine("    {");
            sb.AppendLine("        public static void RegisterAll()");
            sb.AppendLine("        {");

            foreach (var e in entities)
            {
                foreach (var c in e.Columns)
                {
                    if (!c.CanRead) continue;
                    sb.AppendLine($"            PropertyAccessorExtension.RegisterAccessor(");
                    sb.AppendLine($"                typeof({e.FullName}).GetProperty(\"{c.PropertyName}\")!,");
                    sb.AppendLine($"                {e.SafeName}_{c.PropertyName}_Getter,");
                    if (c.CanWrite)
                        sb.AppendLine($"                {e.SafeName}_{c.PropertyName}_Setter);");
                    else
                        sb.AppendLine($"                null);");
                }
            }

            sb.AppendLine("        }");
            sb.AppendLine();

            // 为每个属性生成 getter/setter
            foreach (var e in entities)
            {
                foreach (var c in e.Columns)
                {
                    GenerateAccessorMethods(sb, e, c);
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void GenerateAccessorMethods(StringBuilder sb, EntityInfo e, ColumnInfo c)
        {
            var entityType = e.FullName;
            var propType = c.PropertyType;

            // Getter
            if (c.CanRead)
            {
                sb.AppendLine();
                sb.AppendLine($"        private static object {e.SafeName}_{c.PropertyName}_Getter(object obj)");
                sb.AppendLine("        {");
                sb.AppendLine($"            return (({entityType})obj).{c.PropertyName}!;");
                sb.AppendLine("        }");
            }

            // Setter
            if (c.CanWrite)
            {
                sb.AppendLine();
                sb.AppendLine($"        private static void {e.SafeName}_{c.PropertyName}_Setter(object obj, object value)");
                sb.AppendLine("        {");
                sb.AppendLine($"            (({entityType})obj).{c.PropertyName} = ({propType})value;");
                sb.AppendLine("        }");
            }
        }

        // ──────────────────────────────────────────────────────────────
        // 4. 生成模块初始化器
        // ──────────────────────────────────────────────────────────────
        private static string GenerateModuleInitializer(List<EntityInfo> entities, ResolvedSymbols symbols)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using LiteOrm.Common;");
            sb.AppendLine();
            sb.AppendLine($"namespace {CodeGenHelper.ProviderFullNamespace}");
            sb.AppendLine("{");
            sb.AppendLine("    internal static class LiteOrmGeneratedInitializer");
            sb.AppendLine("    {");
            sb.AppendLine("        [ModuleInitializer]");
            sb.AppendLine("        internal static void Initialize()");
            sb.AppendLine("        {");
            sb.AppendLine("            TableInfoProvider.Set(() => new SourceGeneratedTableInfoProvider());");
            sb.AppendLine("            SourceGeneratedDataReaderMappers.RegisterAll();");
            sb.AppendLine("            SourceGeneratedPropertyAccessors.RegisterAll();");
            sb.AppendLine("            RegisterTypeResolverNames();");
            sb.AppendLine("            RegisterEnums();");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 注册所有实体/视图类型到 TypeResolverHelper，使 Expr JSON 反序列化在 NativeAOT 下");
            sb.AppendLine("        /// 无需 Type.GetType / 程序集扫描即可按名称解析类型。");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        private static void RegisterTypeResolverNames()");
            sb.AppendLine("        {");

            foreach (var e in entities)
            {
                if (symbols.TypeResolverHelper == null) break;
                // 先注册全名（Expr JSON 使用 DefaultTypeNameResolver.GetName = FullName），
                // 再注册短名（保持 TypeResolverHelper.GetName 返回短名的原有行为）。
                sb.AppendLine($"            TypeResolverHelper.Register(typeof({e.FullName}).FullName!, typeof({e.FullName}));");
                sb.AppendLine($"            TypeResolverHelper.Register(typeof({e.FullName}).Name, typeof({e.FullName}));");
            }

            sb.AppendLine("        }");
            sb.AppendLine();

            var enumInfos = CollectEnumInfos(entities, symbols);
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 预注册实体/视图列所用枚举类型的显示名称映射，替代 NativeAOT 下运行时反射扫描。");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        private static void RegisterEnums()");
            sb.AppendLine("        {");

            if (symbols.EnumUtil != null)
            {
                foreach (var enumInfo in enumInfos)
                {
                    sb.AppendLine($"            global::LiteOrm.EnumUtil.Register(typeof({enumInfo.FullName}), new global::System.Collections.Generic.KeyValuePair<global::System.Enum, string>[]");
                    sb.AppendLine("            {");
                    for (int i = 0; i < enumInfo.Fields.Count; i++)
                    {
                        var f = enumInfo.Fields[i];
                        var trailing = i < enumInfo.Fields.Count - 1 ? "," : "";
                        sb.AppendLine($"                new global::System.Collections.Generic.KeyValuePair<global::System.Enum, string>({enumInfo.FullName}.{f.Name}, \"{EscapeString(f.DisplayName)}\"){trailing}");
                    }
                    sb.AppendLine("            });");
                }
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// 收集实体/视图列中使用的所有枚举类型及其字段显示名称，
        /// 供生成器预注册到 <c>EnumUtil</c>。
        /// </summary>
        private static List<EnumInfo> CollectEnumInfos(List<EntityInfo> entities, ResolvedSymbols symbols)
        {
            var result = new List<EnumInfo>();
            var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var e in entities)
            {
                foreach (var c in e.Columns)
                {
                    var type = c.Symbol?.Type;
                    if (type is null) continue;
                    // 解包 Nullable<T>
                    if (type is INamedTypeSymbol nts && nts.IsGenericType &&
                        nts.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T &&
                        nts.TypeArguments.Length == 1)
                        type = nts.TypeArguments[0];
                    if (type is not INamedTypeSymbol named || named.TypeKind != TypeKind.Enum) continue;
                    if (!seen.Add(named)) continue;

                    var fields = new List<EnumFieldInfo>();
                    foreach (var member in named.GetMembers())
                    {
                        if (member is IFieldSymbol field &&
                            field.IsConst &&
                            field.HasConstantValue &&
                            field.DeclaredAccessibility == Accessibility.Public &&
                            field.Name != "value__")
                        {
                            fields.Add(new EnumFieldInfo(field.Name, GetEnumFieldDisplayName(field, symbols)));
                        }
                    }
                    result.Add(new EnumInfo(
                        named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        fields));
                }
            }
            return result;
        }

        private static string GetEnumFieldDisplayName(IFieldSymbol field, ResolvedSymbols symbols)
        {
            foreach (var attr in field.GetAttributes())
            {
                if (symbols.DisplayNameAttribute != null && SymbolEqualityComparer.Default.Equals(attr.AttributeClass, symbols.DisplayNameAttribute))
                {
                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string dn && !string.IsNullOrEmpty(dn))
                        return dn;
                }
            }
            foreach (var attr in field.GetAttributes())
            {
                if (symbols.DescriptionAttribute != null && SymbolEqualityComparer.Default.Equals(attr.AttributeClass, symbols.DescriptionAttribute))
                {
                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string de && !string.IsNullOrEmpty(de))
                        return de;
                }
            }
            return field.Name;
        }

        private static string EscapeString(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private sealed class EnumInfo
        {
            public EnumInfo(string fullName, List<EnumFieldInfo> fields) { FullName = fullName; Fields = fields; }
            public string FullName { get; }
            public List<EnumFieldInfo> Fields { get; }
        }

        private sealed class EnumFieldInfo
        {
            public EnumFieldInfo(string name, string displayName) { Name = name; DisplayName = displayName; }
            public string Name { get; }
            public string DisplayName { get; }
        }

        // ──────────────────────────────────────────────────────────────
        // 5. 生成 AOT 类型注册代码
        //    扫描编译中所有引用的程序集，查找继承自 SqlBuilder / DbConnection 的类型，
        //    在模块初始化器中生成 RegisterSqlBuilderType<T>() /
        //    RegisterDbConnectionType<T>() 调用，以支持 NativeAOT。
        //    通过基类继承关系自动发现派生类型，无需维护硬编码列表。
        // ──────────────────────────────────────────────────────────────
        private static void GenerateAotTypeRegistration(SourceProductionContext spc, Compilation compilation)
        {
            var registrationLines = new List<string>();

            // 查找 SqlBuilder 派生类型
            var sqlBuilderBaseType = compilation.GetTypeByMetadataName("LiteOrm.SqlBuilder");
            if (sqlBuilderBaseType != null &&
                compilation.GetTypeByMetadataName("LiteOrm.SqlBuilderFactory") != null)
            {
                foreach (var derivedType in FindDerivedTypes(compilation, sqlBuilderBaseType))
                {
                    var fullyQualified = derivedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    registrationLines.Add($"            SqlBuilderFactory.RegisterSqlBuilderType<{fullyQualified}>();");
                }
            }

            // 查找 DbConnection 派生类型
            var dbConnectionBaseType = compilation.GetTypeByMetadataName("System.Data.Common.DbConnection");
            if (dbConnectionBaseType != null &&
                compilation.GetTypeByMetadataName("LiteOrm.DAOContextPoolFactory") != null)
            {
                foreach (var derivedType in FindDerivedTypes(compilation, dbConnectionBaseType))
                {
                    var fullyQualified = derivedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    registrationLines.Add($"            DAOContextPoolFactory.RegisterDbConnectionType<{fullyQualified}>();");
                }
            }

            if (registrationLines.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using LiteOrm;");
            sb.AppendLine();
            sb.AppendLine($"namespace {CodeGenHelper.ProviderFullNamespace}");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 源生成的 AOT 类型注册器，在模块初始化时预注册 SqlBuilder 和 DbConnection 类型，");
            sb.AppendLine("    /// 以支持 NativeAOT 下的类型实例化。");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    internal static class LiteOrmAotTypeRegistration");
            sb.AppendLine("    {");
            sb.AppendLine("        [ModuleInitializer]");
            sb.AppendLine("        internal static void RegisterAotTypes()");
            sb.AppendLine("        {");
            foreach (var line in registrationLines)
            {
                sb.AppendLine(line);
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            spc.AddSource("AotTypeRegistration.g.cs", sb.ToString());
        }

        /// <summary>
        /// 在编译的所有程序集中查找继承自指定基类型的非抽象、具有公共无参构造函数的类型。
        /// <para>
        /// 仅扫描直接引用了基类型所在程序集的引用程序集，以避免遍历无关程序集。
        /// </para>
        /// </summary>
        private static List<INamedTypeSymbol> FindDerivedTypes(Compilation compilation, INamedTypeSymbol baseType)
        {
            var result = new List<INamedTypeSymbol>();
            var baseTypeAssembly = baseType.ContainingAssembly;
            var visited = new HashSet<IAssemblySymbol>(SymbolEqualityComparer.Default);

            // 扫描源程序集
            CollectDerivedTypes(compilation.Assembly.GlobalNamespace, baseType, result);
            visited.Add(compilation.Assembly);

            // 扫描引用程序集中直接引用了基类型所在程序集的
            foreach (var module in compilation.Assembly.Modules)
            {
                foreach (var refAssembly in module.ReferencedAssemblySymbols)
                {
                    if (visited.Contains(refAssembly)) continue;

                    // 基类型自身的程序集或引用了基类型程序集的程序集才可能包含派生类型
                    if (refAssembly.Equals(baseTypeAssembly, SymbolEqualityComparer.Default) ||
                        AssemblyReferencesAssembly(refAssembly, baseTypeAssembly))
                    {
                        visited.Add(refAssembly);
                        CollectDerivedTypes(refAssembly.GlobalNamespace, baseType, result);
                    }
                }
            }

            result.Sort((a, b) => string.Compare(a.ToDisplayString(), b.ToDisplayString(), StringComparison.Ordinal));
            return result;
        }

        /// <summary>
        /// 检查程序集是否直接引用了目标程序集。
        /// </summary>
        private static bool AssemblyReferencesAssembly(IAssemblySymbol assembly, IAssemblySymbol target)
        {
            foreach (var module in assembly.Modules)
            {
                foreach (var refAssembly in module.ReferencedAssemblySymbols)
                {
                    if (refAssembly.Equals(target, SymbolEqualityComparer.Default))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 递归遍历命名空间，收集继承自指定基类型的类型。
        /// </summary>
        private static void CollectDerivedTypes(INamespaceSymbol namespaceSymbol, INamedTypeSymbol baseType, List<INamedTypeSymbol> result)
        {
            foreach (var member in namespaceSymbol.GetMembers())
            {
                if (member is INamespaceSymbol ns)
                {
                    CollectDerivedTypes(ns, baseType, result);
                }
                else if (member is INamedTypeSymbol typeSymbol)
                {
                    CheckTypeAndNested(typeSymbol, baseType, result);
                }
            }
        }

        /// <summary>
        /// 检查类型及其嵌套类型是否继承自指定基类型且满足注册条件。
        /// </summary>
        private static void CheckTypeAndNested(INamedTypeSymbol typeSymbol, INamedTypeSymbol baseType, List<INamedTypeSymbol> result)
        {
            if (!typeSymbol.IsAbstract &&
                InheritsFrom(typeSymbol, baseType) &&
                HasPublicParameterlessConstructor(typeSymbol))
            {
                result.Add(typeSymbol);
            }

            // 递归检查嵌套类型
            foreach (var nested in typeSymbol.GetTypeMembers())
            {
                CheckTypeAndNested(nested, baseType, result);
            }
        }

        /// <summary>
        /// 检查类型是否继承自指定基类型（含间接继承）。
        /// </summary>
        private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            var current = type.BaseType;
            while (current != null)
            {
                if (current.Equals(baseType, SymbolEqualityComparer.Default))
                    return true;
                current = current.BaseType;
            }
            return false;
        }

        /// <summary>
        /// 检查类型是否具有公共无参构造函数。
        /// </summary>
        private static bool HasPublicParameterlessConstructor(INamedTypeSymbol typeSymbol)
        {
            foreach (var constructor in typeSymbol.InstanceConstructors)
            {
                if (constructor.Parameters.IsEmpty && constructor.DeclaredAccessibility == Accessibility.Public)
                    return true;
            }
            return false;
        }
    }
}
