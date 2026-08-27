using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace LiteOrm.Common
{
    /// <summary>
    /// 通用的类型名称 ↔ <see cref="Type"/> 双向转换工具。
    /// <para>
    /// 提供：
    /// 1. <see cref="GetName(Type)"/>：生成类型的可序列化名称（短名，泛型使用 <c>Base&lt;T1,T2&gt;</c> 格式）；
    /// 2. <see cref="FindType(string)"/>：按名称查找类型（支持自定义注册、全名、全程序集短名扫描）；
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
        /// <summary>FindType 结果缓存：typeName → 类型（未找到为 null）。</summary>
        private static readonly ConcurrentDictionary<string, Type?> _findTypeCache = new(StringComparer.Ordinal);
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
        public static void Register(string name, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (type is null) throw new ArgumentNullException(nameof(type));

            name = StripNonSignificantChars(name);

            // 覆盖旧的类型→名称映射（同一类型可能换了新名字）
            _typeToName[type] = name;
            _nameToType[name] = type;

            // 失效缓存
            _getNameCache.TryRemove(type, out _);
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
            // 失效 FindType 缓存
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
        /// 优先返回自定义注册的名称；否则使用短名，
        /// 泛型类型返回 <c>基名&lt;参数短名1,参数短名2,...&gt;</c>（去除反引号 arity 后缀，递归处理嵌套泛型）。
        /// </para>
        /// </summary>
        /// <param name="type">类型。</param>
        /// <returns>类型名称；<paramref name="type"/> 为 null 时返回空字符串。</returns>
        public static string GetName(Type? type)
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
        /// 按名称查找类型。解析顺序：自定义注册 → 精确全名匹配 → 忽略大小写匹配。
        /// <para>
        /// 查找前会剥离名称中的空格/制表/换行等非实质性字符以避免输入格式差异导致的不匹配。
        /// </para>
        /// <para>
        /// 泛型类型应使用 CLR 名称格式（含反引号 arity 后缀），如 <c>IEntityService`1</c>，
        /// 避免与同名的非泛型类型冲突。
        /// </para>
        /// <para>
        /// AOT 模式下仅返回通过 <see cref="Register"/> 预注册的类型；非 AOT 模式下
        /// 回退到 <see cref="Type.GetType(string)"/> 与程序集扫描（返回的类型不保证构造函数被保留）。
        /// </para>
        /// </summary>
        /// <param name="typeName">类型名称，可以是全名、短名或程序集限定名。泛型类型应使用 <c>Foo`1</c> 格式。</param>
        /// <returns>匹配到的类型；未找到时返回 null。</returns>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2073",
            Justification = "GetOrAdd calls FindTypeCore; under AOT only the _nameToType path is reachable (returns null early when IsDynamicCodeSupported is false); pre-registered types preserve all members via the Register(name, [DynamicallyAccessedMembers] Type) annotation chain. JIT path type lookup is naturally available via reflection.")]
#endif
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        public static Type? FindType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            return _findTypeCache.GetOrAdd(typeName, t =>
            {
                // 剥离空白等非实质性字符后精确匹配，未命中则忽略大小写回退
                var normalized = StripNonSignificantChars(t);
                var result = FindTypeCore(normalized);
                if (result is null)
                    result = FindTypeCaseInsensitive(normalized);
                return result;
            });
        }

#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2057",
            Justification = "Type.GetType is only called when RuntimeFeature.IsDynamicCodeSupported is true (JIT mode); under AOT the method returns null early.")]
        [UnconditionalSuppressMessage("Trimming", "IL2026",
            Justification = "Assembly.GetType is only called when RuntimeFeature.IsDynamicCodeSupported is true (JIT mode); under AOT the method returns null early.")]
        [UnconditionalSuppressMessage("Trimming", "IL2073",
            Justification = "Return type annotation requirements are only satisfied on the AOT path (pre-registered types preserve all members); the Type returned on the JIT path is naturally available via reflection.")]
#endif
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        private static Type? FindTypeCore(string typeName)
        {
            // 1. 自定义注册
            if (_nameToType.TryGetValue(typeName, out var registered)) return registered;

            // AOT / 裁剪模式下 Type.GetType 与 AppDomain 程序集扫描不可用，
            // 仅使用预注册映射（由 LiteOrm.Generators 源生成器在编译期登记）。
            if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
                return null;

            // 2. 兼容程序集限定名（AssemblyQualifiedName）与全名：Type.GetType 支持这两种格式
            var byGetType = Type.GetType(typeName);
            if (byGetType != null) return byGetType;

            // 3. 精确全名匹配（跨程序集遍历）
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            return null;
        }

        /// <summary>
        /// 剥离类型名称中的非实质性字符（空格、制表符、换行、回车），用于忽略输入格式差异的匹配。
        /// </summary>
        /// <param name="typeName">原始类型名称。</param>
        /// <returns>剥离非实质性字符后的名称（无变化时返回原串）。</returns>
        private static string StripNonSignificantChars(string typeName)
        {
            if (typeName.IndexOfAny(WhitespaceChars) < 0) return typeName;
            var chars = new char[typeName.Length];
            int write = 0;
            foreach (char c in typeName)
            {
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') continue;
                chars[write++] = c;
            }
            return new string(chars, 0, write);
        }

        private static readonly char[] WhitespaceChars = { ' ', '\t', '\r', '\n' };

        /// <summary>
        /// 忽略大小写查找类型：对自定义注册做不区分大小写匹配；
        /// AOT/裁剪模式与 <see cref="FindTypeCore"/> 一致仅查预注册映射。
        /// </summary>
        /// <param name="typeName">规范化后的类型名称。</param>
        /// <returns>匹配到的类型；未找到时返回 null。</returns>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2057",
            Justification = "Type.GetType is only called when RuntimeFeature.IsDynamicCodeSupported is true (JIT mode); under AOT the method returns early via the pre-registered map.")]
        [UnconditionalSuppressMessage("Trimming", "IL2096",
            Justification = "Case-insensitive GetType is only called when RuntimeFeature.IsDynamicCodeSupported is true (JIT mode); under AOT the method returns early via the pre-registered map.")]
        [UnconditionalSuppressMessage("Trimming", "IL2026",
            Justification = "Assembly.GetType is only called when RuntimeFeature.IsDynamicCodeSupported is true (JIT mode); under AOT the method returns early via the pre-registered map.")]
        [UnconditionalSuppressMessage("Trimming", "IL2073",
            Justification = "Return type annotation is satisfied on the AOT path (pre-registered types preserve all members); the Type returned on the JIT path is naturally available via reflection.")]
#endif
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        private static Type? FindTypeCaseInsensitive(string typeName)
        {
            // 不区分大小写的注册匹配（AOT/JIT 均适用）
            var registered = _nameToType
                .FirstOrDefault(kv => string.Equals(kv.Key, typeName, StringComparison.OrdinalIgnoreCase)).Value;
            if (registered != null) return registered;

            // AOT/裁剪模式下动态查找不可用，仅依赖预注册映射
            if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
                return null;

            // 不区分大小写的程序集限定名/全名匹配
            var byGetType = Type.GetType(typeName, throwOnError: false, ignoreCase: true);
            if (byGetType != null) return byGetType;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName, throwOnError: false, ignoreCase: true);
                if (type != null) return type;
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
