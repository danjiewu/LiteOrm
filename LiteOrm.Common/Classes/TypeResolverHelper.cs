using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace LiteOrm.Common
{
    /// <summary>
    /// 通用的类型名称 ↔ <see cref="Type"/> 双向转换工具。
    /// <para>
    /// 提供：
    /// 1. <see cref="GetName(Type)"/>：生成类型的可序列化名称（短名，泛型使用 <c>Base&lt;T1,T2&gt;</c> 格式）；
    /// 2. <see cref="FindType(string, string?)"/>：按名称查找类型（支持自定义注册、全名、命名空间+短名、全程序集短名扫描）；
    /// 3. <see cref="Register(string, Type)"/>/<see cref="Unregister(string)"/>：自定义名称 ↔ 类型的双向静态注册；
    /// 4. <see cref="TryParseGenericServiceName"/>：解析泛型服务名。
    /// </para>
    /// <para>
    /// 所有查找结果均缓存，自定义注册优先于扫描结果。
    /// </para>
    /// </summary>
    public static class TypeResolverHelper
    {
        /// <summary>自定义注册：名称 → 类型。</summary>
        private static readonly ConcurrentDictionary<string, Type> _nameToType = new(StringComparer.Ordinal);
        /// <summary>自定义注册：类型 → 名称。</summary>
        private static readonly ConcurrentDictionary<Type, string> _typeToName = new();
        /// <summary>FindType 结果缓存：typeName → (defaultNamespace → 类型，未找到为 null)。双层字典便于按 typeName 维度整体失效。</summary>
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string?, Type?>> _findTypeCache = new(StringComparer.Ordinal);
        /// <summary>GetName 结果缓存：类型 → 名称。</summary>
        private static readonly ConcurrentDictionary<Type, string> _getNameCache = new();

        /// <summary>
        /// 注册自定义的类型名称双向映射。注册后 <see cref="GetName"/> 返回 <paramref name="name"/>，
        /// <see cref="FindType"/> 优先返回 <paramref name="type"/>。
        /// <para>
        /// 若 <paramref name="name"/> 或 <paramref name="type"/> 已被注册，将覆盖原映射。
        /// </para>
        /// </summary>
        /// <param name="name">自定义名称。</param>
        /// <param name="type">对应的类型。</param>
        public static void Register(string name, Type type)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (type is null) throw new ArgumentNullException(nameof(type));

            // 覆盖旧的类型→名称映射（同一类型可能换了新名字）
            _typeToName[type] = name;
            _nameToType[name] = type;

            // 失效缓存
            _getNameCache.TryRemove(type, out _);
            // 失效该名称所有命名空间下的 FindType 缓存（双层字典：直接移除整个内层字典）
            _findTypeCache.TryRemove(name, out _);
        }

        /// <summary>
        /// 注销指定名称的自定义映射。
        /// </summary>
        /// <param name="name">已注册的名称。</param>
        /// <returns>是否成功移除。</returns>
        public static bool Unregister(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var removed = _nameToType.TryRemove(name, out var type);
            if (removed && type is not null)
            {
                _typeToName.TryRemove(type, out _);
                _getNameCache.TryRemove(type, out _);
            }
            // 失效该名称所有命名空间下的 FindType 缓存
            _findTypeCache.TryRemove(name, out _);
            return removed;
        }

        /// <summary>
        /// 清除所有自定义注册与缓存。
        /// </summary>
        public static void Clear()
        {
            _nameToType.Clear();
            _typeToName.Clear();
            _findTypeCache.Clear();
            _getNameCache.Clear();
        }

        /// <summary>
        /// 生成类型的可序列化名称。
        /// <para>
        /// 优先返回自定义注册的名称；否则使用短名（<see cref="Type.Name"/>），
        /// 泛型类型返回 <c>基名&lt;参数短名1,参数短名2,...&gt;</c>（去除反引号 arity 后缀，递归处理嵌套泛型）。
        /// </para>
        /// </summary>
        /// <param name="type">类型。</param>
        /// <returns>类型名称；<paramref name="type"/> 为 null 时返回空字符串。</returns>
        public static string GetName(Type type)
        {
            if (type is null) return string.Empty;
            // 自定义注册优先（实时查询，确保 Register 后立即生效）
            if (_typeToName.TryGetValue(type, out var customName)) return customName;
            return _getNameCache.GetOrAdd(type, t =>
            {
                if (t.IsGenericType)
                {
                    int backtickIndex = t.Name.IndexOf('`');
                    var baseName = backtickIndex > 0
                        ? t.Name.Substring(0, backtickIndex)
                        : t.Name;
                    var argNames = t.GetGenericArguments().Select(a => GetName(a));
                    return baseName + "<" + string.Join(",", argNames) + ">";
                }
                return t.Name;
            });
        }

        /// <summary>
        /// 按名称查找类型。解析顺序：
        /// 1. 自定义注册（<see cref="Register"/>）；
        /// 2. 精确全名匹配（含命名空间或程序集限定名）；
        /// 3. 若 <paramref name="defaultNamespace"/> 已设置且 <paramref name="typeName"/> 为短名（不含 '.'），
        ///    尝试 <c>defaultNamespace + "." + typeName</c> 精确匹配；
        /// 4. 回退到全程序集短名（<see cref="Type.Name"/>）扫描。
        /// <para>
        /// 结果按 (typeName, defaultNamespace) 缓存。
        /// </para>
        /// <para>
        /// 泛型类型应使用 CLR 名称格式（含反引号 arity 后缀），如 <c>IEntityService`1</c>，
        /// 避免与同名的非泛型类型冲突。
        /// </para>
        /// </summary>
        /// <param name="typeName">类型名称，可以是全名、短名或程序集限定名。泛型类型应使用 <c>Foo`1</c> 格式。</param>
        /// <param name="defaultNamespace">默认命名空间（可选），用于将短名组合为全名进行精确匹配。</param>
        /// <returns>匹配到的类型；未找到时返回 null。</returns>
        public static Type? FindType(string typeName, string? defaultNamespace = null)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            // 双层字典：外层按 typeName 索引，内层按 defaultNamespace 索引
            var inner = _findTypeCache.GetOrAdd(typeName, _ => new ConcurrentDictionary<string?, Type?>(StringComparer.Ordinal));
            return inner.GetOrAdd(defaultNamespace, ns => FindTypeCore(typeName, ns));
        }

        private static Type? FindTypeCore(string typeName, string? defaultNamespace)
        {
            // 1. 自定义注册
            if (_nameToType.TryGetValue(typeName, out var registered)) return registered;

            // 2. 兼容程序集限定名（AssemblyQualifiedName）与全名：Type.GetType 支持这两种格式
            var byGetType = Type.GetType(typeName);
            if (byGetType != null) return byGetType;

            // 3. 精确全名匹配（跨程序集遍历）
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            // 4. 默认命名空间 + 短名
            if (!string.IsNullOrEmpty(defaultNamespace) && !typeName.Contains('.'))
            {
                var fullName = defaultNamespace + "." + typeName;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType(fullName);
                    if (type != null) return type;
                }
            }

            // 5. 短名匹配
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var match = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                    if (match != null) return match;
                }
                catch (ReflectionTypeLoadException)
                {
                    // 跳过加载失败的程序集
                }
            }

            return null;
        }

        /// <summary>
        /// 尝试将服务名/类型名解析为开放泛型基名与类型参数名列表。
        /// 例如 "IEntityService&lt;User&gt;" → ("IEntityService", ["User"])。
        /// 非泛型名称返回 null。
        /// </summary>
        /// <param name="serviceName">服务名称或类型名称。</param>
        /// <returns>解析结果（基名 + 类型参数名数组）；非泛型时返回 null。</returns>
        public static (string BaseName, string[] ArgNames)? TryParseGenericServiceName(string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName)) return null;
            var ltIndex = serviceName.IndexOf('<');
            if (ltIndex <= 0) return null;
            var gtIndex = serviceName.LastIndexOf('>');
            if (gtIndex <= ltIndex) return null;

            var baseName = serviceName.Substring(0, ltIndex);
            var argsPart = serviceName.Substring(ltIndex + 1, gtIndex - ltIndex - 1);
            var argNames = argsPart.Split(',').Select(s => s.Trim()).ToArray();
            return (baseName, argNames);
        }
    }
}
