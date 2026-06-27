using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LiteOrm.Service
{
    /// <summary>
    /// 远程服务类型解析器抽象。服务端通过此接口根据请求中的 ServiceName 解析目标服务接口类型。
    /// </summary>
    public interface IRemoteServiceTypeResolver
    {
        /// <summary>
        /// 根据 ServiceName 解析服务接口类型。
        /// </summary>
        /// <param name="serviceName">服务名称（由客户端 <see cref="RemoteServiceNameUtil.GetServiceName"/> 生成）。</param>
        /// <returns>匹配到的服务接口类型；未找到时返回 null。</returns>
        Type? ResolveService(string serviceName);
    }

    /// <summary>
    /// 通过委托构造的远程服务类型解析器。允许用户提供任意自定义解析逻辑。
    /// </summary>
    public class DelegateRemoteServiceTypeResolver : IRemoteServiceTypeResolver
    {
        private readonly Func<string, Type?> _resolver;

        /// <summary>
        /// 初始化 <see cref="DelegateRemoteServiceTypeResolver"/> 类的新实例。
        /// </summary>
        /// <param name="resolver">解析委托，接收 ServiceName 返回服务接口类型（未找到返回 null）。</param>
        public DelegateRemoteServiceTypeResolver(Func<string, Type?> resolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        /// <inheritdoc />
        public Type? ResolveService(string serviceName)
            => string.IsNullOrEmpty(serviceName) ? null : _resolver(serviceName);
    }

    /// <summary>
    /// 默认的远程服务类型解析器。通过指定 Service 和 Model 命名空间解析服务类型，命名空间可选。
    /// <para>
    /// 服务接口类型（如 <c>IEntityService</c>）从 <see cref="ServiceNamespace"/> 解析；
    /// 实体/模型类型（泛型参数，如 <c>User</c>）从 <see cref="ModelNamespace"/> 解析。
    /// 支持开放泛型接口（如 <c>IEntityService&lt;&gt;</c>）的闭合构造。
    /// </para>
    /// <para>
    /// 命名空间为 null 或空时，回退到全程序集短名（<see cref="Type.Name"/>）扫描；
    /// 设置命名空间时，先按 <c>命名空间 + "." + 类型名</c> 精确匹配，失败再回退到全程序集短名扫描。
    /// 若类型名已含命名空间（包含 '.'），直接按全名查找。
    /// </para>
    /// <para>
    /// 该类是 <see cref="IRemoteServiceTypeResolver"/> 的默认实现，
    /// <see cref="RemoteServerOptions.ServiceTypeResolver"/> 未显式设置时使用此实例。
    /// </para>
    /// </summary>
    public class DefaultServiceTypeResolver : IRemoteServiceTypeResolver
    {
        private readonly ConcurrentDictionary<string, Type?> _cache = new();

        /// <summary>
        /// 服务接口类型所在的命名空间。为 null 或空时回退到全程序集短名扫描。
        /// </summary>
        public string? ServiceNamespace { get; }

        /// <summary>
        /// 实体/模型类型所在的命名空间。为 null 或空时回退到全程序集短名扫描。
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
        /// <param name="serviceNamespace">服务接口类型所在的命名空间（可选，为 null 时全程序集扫描）。</param>
        /// <param name="modelNamespace">实体/模型类型所在的命名空间（可选，为 null 时全程序集扫描）。</param>
        public DefaultServiceTypeResolver(string? serviceNamespace, string? modelNamespace)
        {
            ServiceNamespace = serviceNamespace;
            ModelNamespace = modelNamespace;
        }

        /// <inheritdoc />
        public Type? ResolveService(string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName)) return null;
            return _cache.GetOrAdd(serviceName, ResolveCore);
        }

        private Type? ResolveCore(string serviceName)
        {
            var ltIndex = serviceName.IndexOf('<');

            // 非泛型：从 ServiceNamespace 解析
            if (ltIndex <= 0)
                return RemoteServiceTypeResolverHelper.FindType(serviceName, ServiceNamespace);

            // 开放泛型：解析 "IEntityService<User>" → baseName="IEntityService", args=["User"]
            var parsed = RemoteServiceTypeResolverHelper.TryParseGenericServiceName(serviceName);
            if (parsed is null) return null;
            var (baseName, argNames) = parsed.Value;

            var openGeneric = RemoteServiceTypeResolverHelper.FindType(baseName, ServiceNamespace);
            if (openGeneric is null || !openGeneric.IsGenericTypeDefinition)
                return null;

            var genericParams = openGeneric.GetGenericArguments();
            if (genericParams.Length != argNames.Length) return null;

            var typeArgs = new Type[argNames.Length];
            for (int i = 0; i < argNames.Length; i++)
            {
                var argType = RemoteServiceTypeResolverHelper.FindType(argNames[i], ModelNamespace);
                if (argType is null) return null;
                typeArgs[i] = argType;
            }

            return openGeneric.MakeGenericType(typeArgs);
        }
    }

    /// <summary>
    /// 远程服务名称解析辅助方法。提供泛型 ServiceName 的解析与类型查找工具，供自定义解析器复用。
    /// </summary>
    public static class RemoteServiceTypeResolverHelper
    {
        /// <summary>
        /// 尝试将 ServiceName 解析为开放泛型基名与类型参数名列表。
        /// 例如 "IEntityService&lt;User&gt;" → ("IEntityService", ["User"])。
        /// 非泛型 ServiceName 返回 null。
        /// </summary>
        /// <param name="serviceName">服务名称。</param>
        /// <returns>解析结果（基名 + 类型参数名数组）；非泛型时返回 null。</returns>
        public static (string BaseName, string[] ArgNames)? TryParseGenericServiceName(string serviceName)
        {
            var ltIndex = serviceName.IndexOf('<');
            if (ltIndex <= 0) return null;
            var gtIndex = serviceName.LastIndexOf('>');
            if (gtIndex <= ltIndex) return null;

            var baseName = serviceName.Substring(0, ltIndex);
            var argsPart = serviceName.Substring(ltIndex + 1, gtIndex - ltIndex - 1);
            var argNames = argsPart.Split(',').Select(s => s.Trim()).ToArray();
            return (baseName, argNames);
        }

        /// <summary>
        /// 按类型名称查找类型。解析顺序：
        /// 1. 精确全名匹配（含命名空间或程序集限定名）；
        /// 2. 若 <paramref name="defaultNamespace"/> 已设置且 <paramref name="typeName"/> 为短名（不含 '.'），
        ///    尝试 <c>defaultNamespace + "." + typeName</c> 精确匹配；
        /// 3. 回退到全程序集短名（<see cref="Type.Name"/>）扫描。
        /// </summary>
        /// <param name="typeName">类型名称，可以是全名、短名或程序集限定名。</param>
        /// <param name="defaultNamespace">默认命名空间（可选），用于将短名组合为全名。</param>
        /// <returns>匹配到的类型；未找到时返回 null。</returns>
        public static Type? FindType(string typeName, string? defaultNamespace = null)
        {
            // 1. 精确全名匹配
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            // 2. 默认命名空间 + 短名
            if (!string.IsNullOrEmpty(defaultNamespace) && !typeName.Contains('.'))
            {
                var fullName = defaultNamespace + "." + typeName;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType(fullName);
                    if (type != null) return type;
                }
            }

            // 3. 短名匹配
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
    }
}
