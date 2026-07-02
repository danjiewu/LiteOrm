using LiteOrm.Common;
using System.Collections.Concurrent;

namespace LiteOrm.Remote.Server
{
    /// <summary>
    /// 远程服务类型解析器抽象。服务端通过此接口根据请求中的 ServiceName 解析目标服务接口类型。
    /// </summary>
    public interface IRemoteServiceTypeResolver
    {
        /// <summary>
        /// 根据 ServiceName 解析服务接口类型。
        /// </summary>
        /// <param name="serviceName">服务名称（由客户端 <see cref="ServiceNameUtil.GetServiceName"/> 生成）。</param>
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
                return TypeResolverHelper.FindType(serviceName, ServiceNamespace);

            // 开放泛型：解析 "IEntityService<User>" → baseName="IEntityService", args=["User"]
            var parsed = TypeResolverHelper.TryParseGenericServiceName(serviceName);
            if (parsed is null) return null;
            var (baseName, argNames) = parsed.Value;

            // 使用 CLR 泛型类型名格式 "Foo`1" 查找开放泛型定义，
            // 避免与同名的非泛型类型冲突（如同时存在 Foo 和 Foo<T> 时，Foo 会错误匹配非泛型类型）
            var genericTypeName = baseName + "`" + argNames.Length;
            var openGeneric = TypeResolverHelper.FindType(genericTypeName, ServiceNamespace);
            if (openGeneric is null || !openGeneric.IsGenericTypeDefinition)
                return null;

            var genericParams = openGeneric.GetGenericArguments();
            if (genericParams.Length != argNames.Length) return null;

            var typeArgs = new Type[argNames.Length];
            for (int i = 0; i < argNames.Length; i++)
            {
                var argType = TypeResolverHelper.FindType(argNames[i], ModelNamespace);
                if (argType is null) return null;
                typeArgs[i] = argType;
            }

            return openGeneric.MakeGenericType(typeArgs);
        }
    }
}
