using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace LiteOrm.Common
{
    /// <summary>
    /// 默认的服务类型解析器，实现 <see cref="ITypeNameResolver"/>。
    /// <para>
    /// 正向（<see cref="GetName"/>）返回 <see cref="TypeResolverHelper.GetName(Type)"/> 生成的短名
    /// （泛型使用 <c>Base&lt;T1,T2&gt;</c> 格式）。
    /// </para>
    /// <para>
    /// 反向（<see cref="GetType"/>）支持：
    /// 1. 非泛型类型名 → 直接查找，未找到时拼接 <see cref="ServiceNamespace"/> 再试；
    /// 2. 泛型服务名（如 <c>IEntityService&lt;User&gt;</c>）→ 解析开放泛型定义，
    ///    泛型参数通过 <see cref="ModelNamespace"/> 查找，最终构造闭合泛型类型。
    /// </para>
    /// <para>
    /// 所有解析结果按名称缓存。
    /// </para>
    /// </summary>
    public class DefaultServiceTypeResolver : ITypeNameResolver
    {
        private readonly ConcurrentDictionary<string, Type?> _cache = new();

        /// <summary>
        /// 默认单例实例（<see cref="ServiceNamespace"/> 和 <see cref="ModelNamespace"/> 均为 null）。
        /// </summary>
        public static readonly DefaultServiceTypeResolver Instance = new();

        /// <summary>
        /// 服务接口类型所在的命名空间。为 null 时不拼接命名空间。
        /// </summary>
        public string? ServiceNamespace { get; }

        /// <summary>
        /// 实体/模型类型所在的命名空间。为 null 时不拼接命名空间。
        /// </summary>
        public string? ModelNamespace { get; }

        /// <summary>
        /// 初始化 <see cref="DefaultServiceTypeResolver"/> 类的新实例，使用全程序集短名扫描。
        /// </summary>
        public DefaultServiceTypeResolver()
            : this(null, null)
        {
        }

        /// <summary>
        /// 初始化 <see cref="DefaultServiceTypeResolver"/> 类的新实例，指定 Service 和 Model 命名空间。
        /// </summary>
        /// <param name="serviceNamespace">服务接口类型所在的命名空间（可选，为 null 时不拼接）。</param>
        /// <param name="modelNamespace">实体/模型类型所在的命名空间（可选，为 null 时不拼接）。</param>
        public DefaultServiceTypeResolver(string? serviceNamespace, string? modelNamespace)
        {
            ServiceNamespace = serviceNamespace;
            ModelNamespace = modelNamespace;
        }

        /// <inheritdoc />
        public string GetName(Type type)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));
            return TypeResolverHelper.GetName(type);
        }

        /// <inheritdoc />
        public Type? GetType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
#pragma warning disable IL3050
            return _cache.GetOrAdd(name, ResolveCore);
#pragma warning restore IL3050
        }

        [RequiresDynamicCode("Pre-register all required closed generic types via TypeResolverHelper with typeof(T) to ensure trimming roots in AOT.")]
        private Type? ResolveCore(string name)
        {
            var ltIndex = name.IndexOf('<');

            // 非泛型：直接查找，未找到时拼接 ServiceNamespace 再试
            if (ltIndex <= 0)
                return FindTypeWithNamespace(name, ServiceNamespace);

            // 开放泛型：解析 "IEntityService<User>" → baseName="IEntityService", args=["User"]
            var parsed = TypeResolverHelper.TryParseGenericServiceName(name);
            if (parsed is null) return null;
            var (baseName, argNames) = parsed.Value;

            // 使用 CLR 泛型类型名格式 "Foo`1" 查找开放泛型定义，
            // 避免与同名的非泛型类型冲突（如同时存在 Foo 和 Foo<T> 时，Foo 会错误匹配非泛型类型）
            var genericTypeName = baseName + "`" + argNames.Length;
            var openGeneric = FindTypeWithNamespace(genericTypeName, ServiceNamespace);
            if (openGeneric is null || !openGeneric.IsGenericTypeDefinition)
                return null;

            var genericParams = openGeneric.GetGenericArguments();
            if (genericParams.Length != argNames.Length) return null;

            var typeArgs = new Type[argNames.Length];
            for (int i = 0; i < argNames.Length; i++)
            {
                var argType = FindTypeWithNamespace(argNames[i], ModelNamespace);
                if (argType is null) return null;
                typeArgs[i] = argType;
            }

            return openGeneric.MakeGenericType(typeArgs);
        }

        /// <summary>
        /// 先按 <paramref name="typeName"/> 直接查找；未找到且 <paramref name="ns"/> 非空时，
        /// 按 <c>ns + "." + typeName</c> 再次查找。
        /// </summary>
        private static Type? FindTypeWithNamespace(string typeName, string? ns)
        {
            var type = TypeResolverHelper.FindType(typeName);
            if (type is not null) return type;

            if (!string.IsNullOrEmpty(ns) && !typeName.Contains('.'))
            {
                return TypeResolverHelper.FindType(ns + "." + typeName);
            }

            return null;
        }
    }
}
