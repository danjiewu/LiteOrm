using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace LiteOrm.Common
{
    /// <summary>
    /// 默认的类型解析器，实现 <see cref="ITypeNameResolver"/>。
    /// <para>
    /// 正向（<see cref="GetName"/>）返回 <see cref="TypeResolverHelper.GetName(Type)"/> 生成的短名
    /// （泛型使用 <c>Base&lt;T1,T2&gt;</c> 格式）。
    /// </para>
    /// <para>
    /// 反向（<see cref="GetType"/>）支持：
    /// 1. 非泛型类型名 → 直接查找，未找到时按 <see cref="Namespaces"/> 顺序依次拼接命名空间再试；
    /// 2. 泛型服务名（如 <c>IEntityService&lt;User&gt;</c>）→ 解析开放泛型定义，
    ///    类型参数同样按 <see cref="Namespaces"/> 顺序查找，最终构造闭合泛型类型。
    /// </para>
    /// <para>
    /// 所有解析结果按名称缓存。
    /// </para>
    /// </summary>
    public class DefaultTypeResolver : ITypeNameResolver
    {
        private readonly ConcurrentDictionary<string, Type?> _cache = new();

        /// <summary>
        /// 默认单例实例（命名空间列表为空，即全程序集按类型短名扫描）。
        /// </summary>
        public static readonly DefaultTypeResolver Instance = new();

        /// <summary>
        /// 类型名解析时依次拼接尝试的命名空间列表（按顺序匹配）。
        /// </summary>
        public IReadOnlyList<string> Namespaces { get; }

        /// <summary>
        /// 初始化 <see cref="DefaultTypeResolver"/> 类的新实例，使用全程序集短名扫描。
        /// </summary>
        public DefaultTypeResolver()
            : this(Array.Empty<string>())
        {
        }

        /// <summary>
        /// 初始化 <see cref="DefaultTypeResolver"/> 类的新实例，指定按顺序匹配的命名空间列表。
        /// </summary>
        /// <param name="namespaces">解析类型名时依次尝试拼接的命名空间（可为空，空项会被忽略）。
        /// 未指定任何命名空间时，回退为全程序集按类型短名扫描。</param>
        public DefaultTypeResolver(params string[] namespaces)
        {
            if (namespaces is null) throw new ArgumentNullException(nameof(namespaces));
            Namespaces = namespaces.Where(ns => !string.IsNullOrEmpty(ns)).ToArray();
        }

        /// <inheritdoc />
        public string GetName(Type type)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));
            return TypeResolverHelper.GetName(type);
        }

        /// <inheritdoc />
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2073", Justification = "ConcurrentDictionary.GetOrAdd returns a Type that is naturally available under JIT; under AOT, RequiresDynamicCode indicates this path is unavailable and closed generic types must be pre-registered.")]
#endif
        [return: DynamicallyAccessedMembers(Constants.RegistedMemberTypes)]
        public Type? GetType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
#pragma warning disable IL3050
            return _cache.GetOrAdd(name, ResolveCore);
#pragma warning restore IL3050
        }

        [RequiresDynamicCode("Pre-register all required closed generic types via TypeResolverHelper with typeof(T) to ensure trimming roots in AOT.")]
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2055",
            Justification = "MakeGenericType resolves generic service names under JIT; under AOT, RequiresDynamicCode indicates this path is unavailable and callers must pre-register closed generic types.")]
#endif
        private Type? ResolveCore(string name)
        {
            var ltIndex = name.IndexOf('<');

            // 非泛型：直接查找，未找到时按 Namespaces 顺序拼接命名空间再试
            if (ltIndex <= 0)
                return FindTypeWithNamespace(name);

            // 开放泛型：解析 "IEntityService<User>" → baseName="IEntityService", args=["User"]
            var parsed = TypeResolverHelper.TryParseGenericServiceName(name);
            if (parsed is null) return null;
            var (baseName, argNames) = parsed.Value;

            // 使用 CLR 泛型类型名格式 "Foo`1" 查找开放泛型定义，
            // 避免与同名的非泛型类型冲突（如同时存在 Foo 和 Foo<T> 时，Foo 会错误匹配非泛型类型）
            var genericTypeName = baseName + "`" + argNames.Length;
            var openGeneric = FindTypeWithNamespace(genericTypeName);
            if (openGeneric is null || !openGeneric.IsGenericTypeDefinition)
                return null;

            var genericParams = openGeneric.GetGenericArguments();
            if (genericParams.Length != argNames.Length) return null;

            var typeArgs = new Type[argNames.Length];
            for (int i = 0; i < argNames.Length; i++)
            {
                var argType = FindTypeWithNamespace(argNames[i]);
                if (argType is null) return null;
                typeArgs[i] = argType;
            }

            return openGeneric.MakeGenericType(typeArgs);
        }

        /// <summary>
        /// 先按 <paramref name="typeName"/> 直接查找；未找到且类型名不含点时，
        /// 按 <see cref="Namespaces"/> 顺序依次以 <c>ns + "." + typeName</c> 再次查找。
        /// </summary>
        private Type? FindTypeWithNamespace(string typeName)
        {
            var type = TypeResolverHelper.FindType(typeName);
            if (type is not null) return type;

            if (typeName.Contains('.')) return null;

            foreach (var ns in Namespaces)
            {
                if (string.IsNullOrEmpty(ns)) continue;
                type = TypeResolverHelper.FindType(ns + "." + typeName);
                if (type is not null) return type;
            }

            return null;
        }
    }
}