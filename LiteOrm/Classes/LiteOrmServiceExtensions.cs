using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm 服务提供者扩展方法集合。
    /// </summary>
    /// <remarks>
    /// LiteOrmServiceExtensions 提供了将 LiteOrm 框架集成到 Microsoft.Extensions.DependencyInjection (MS DI)
    /// 容器的扩展方法，简化 LiteOrm 框架与 .NET 通用主机 (IHostBuilder) 的集成过程。
    ///
    /// 主要功能包括：
    /// 1. 框架初始化 - 在宿主构建时通过 ConfigureServices 初始化 LiteOrm 框架；
    /// 2. 自动服务注册 - 扫描程序集中带 [AutoRegister] 特性的类型并注册到 MS DI；
    /// 3. SqlBuilder 注册 - 注册自定义 SqlBuilder 到 SqlBuilderFactory。
    ///
    /// 使用示例：
    /// <code>
    /// var builder = Host.CreateDefaultBuilder(args)
    ///     .RegisterLiteOrm(options =&gt;
    ///     {
    ///         options.Assemblies = new[] { typeof(MyService).Assembly };
    ///     });
    /// </code>
    /// </remarks>
    public static class LiteOrmServiceExtensions
    {
        /// <summary>
        /// 注册 LiteOrm 框架到主机构建器。
        /// </summary>
        /// <param name="hostBuilder">主机构建器。</param>
        /// <returns>配置后的主机构建器。</returns>
        public static IHostBuilder RegisterLiteOrm(this IHostBuilder hostBuilder)
        {
            return RegisterLiteOrm(hostBuilder, null);
        }

        /// <summary>
        /// 注册 LiteOrm 框架到主机构建器，并允许配置选项。
        /// </summary>
        /// <param name="hostBuilder">主机构建器。</param>
        /// <param name="configureOptions">配置选项的回调函数。</param>
        /// <returns>配置后的主机构建器。</returns>
        public static IHostBuilder RegisterLiteOrm(this IHostBuilder hostBuilder, Action<LiteOrmOptions> configureOptions)
        {
            var options = new LiteOrmOptions();
            try
            {
                configureOptions?.Invoke(options);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize LiteOrm options", ex);
            }

            return hostBuilder.ConfigureServices(services =>
            {
                var logger = options.LoggerFactory?.CreateLogger(nameof(LiteOrmServiceExtensions));

                // 显式注册核心服务（不再依赖 AutoRegister 特性）
                services.AddCoreLiteOrmServices();

                // 自动扫描并注册标记 [AutoRegister] 的服务
                try
                {
                    if (options.Assemblies != null && options.Assemblies.Length > 0)
                    {
                        services.RegisterAutoService(logger, options.Assemblies);
                    }
                    else
                    {
                        services.RegisterAutoService(logger);
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Failed to register LiteOrm services automatically", ex);
                }

                // 注册自定义 SqlBuilder（按数据源名称），直接在 ConfigureServices 中完成（不再使用 RegisterBuildCallback）
                foreach (var kvp in options.SqlBuilders)
                {
                    try
                    {
                        SqlBuilderFactory.Instance.RegisterSqlBuilder(kvp.Key, kvp.Value);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Failed to register SqlBuilder for data source '{kvp.Key}'", ex);
                    }
                }

                // 注册自定义 SqlBuilder（按连接类型）
                foreach (var kvp in options.SqlBuildersByType)
                {
                    try
                    {
                        SqlBuilderFactory.Instance.RegisterSqlBuilder(kvp.Key, kvp.Value);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Failed to register SqlBuilder for connection type '{kvp.Key.FullName}'", ex);
                    }
                }
            });
        }

        /// <summary>
        /// 显式注册 LiteOrm 核心服务。
        /// 这些服务不再使用 [AutoRegister] 特性，而是通过此方法手动注册，确保注册行为的确定性。
        /// </summary>
        /// <remarks>
        /// 注册的核心服务包括：
        /// 1. <see cref="SqlBuilderFactory"/> - 单例，使用静态 <see cref="SqlBuilderFactory.Instance"/> 确保 DI 解析与静态访问一致；
        /// 2. <see cref="DAOContextPoolFactory"/> - 单例，数据库连接池工厂；
        /// 3. <see cref="SessionManager"/> - Scoped，每作用域一个会话管理器实例；
        /// 4. <see cref="LiteOrmCoreInitializer"/> - HostedService，启动时自动同步数据库表结构。
        /// 同时触发 <see cref="LiteOrmSqlFunctionInitializer.Initialize"/> 以注册 SQL 函数映射。
        /// </remarks>
        /// <param name="services">服务集合。</param>
        /// <returns>服务集合。</returns>
        public static IServiceCollection AddCoreLiteOrmServices(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            // SqlBuilderFactory 使用静态 Instance，确保 DI 解析的实例与静态访问 (SqlBuilderFactory.Instance) 一致
            services.AddSingleton<SqlBuilderFactory>(sp => SqlBuilderFactory.Instance);
            services.AddSingleton<ISqlBuilderFactory>(sp => sp.GetRequiredService<SqlBuilderFactory>());

            // 连接池工厂 - 单例
            services.AddSingleton<DAOContextPoolFactory>();

            // 会话管理器 - 每作用域一个实例
            services.AddScoped<SessionManager>();

            // 启动时自动同步数据库表结构
            services.AddHostedService<LiteOrmCoreInitializer>();

            // 触发 SQL 函数初始化（静态构造函数仅执行一次，多次调用安全）
            LiteOrmSqlFunctionInitializer.Initialize();

            return services;
        }

        /// <summary>
        /// LiteOrm 配置选项。
        /// </summary>
        public class LiteOrmOptions
        {
            /// <summary>
            /// 注册的 SqlBuilder 映射（按数据源名称）。
            /// </summary>
            internal Dictionary<string, SqlBuilder> SqlBuilders { get; } = new Dictionary<string, SqlBuilder>();

            /// <summary>
            /// 注册的 SqlBuilder 映射（按连接类型）。
            /// </summary>
            internal Dictionary<Type, SqlBuilder> SqlBuildersByType { get; } = new Dictionary<Type, SqlBuilder>();

            /// <summary>
            /// 是否注册 Scope 跟踪（默认为 true）。
            /// <para>Scope 跟踪逻辑已迁移到 LiteOrm.Framework 项目，此选项由 Framework 读取。</para>
            /// </summary>
            public bool RegisterScope { get; set; } = true;

            /// <summary>
            /// 要扫描的程序集列表。
            /// </summary>
            public Assembly[] Assemblies { get; set; }

            /// <summary>
            /// 日志工厂，用于记录服务注册过程中的程序集扫描日志（可选）。
            /// </summary>
            public ILoggerFactory LoggerFactory { get; set; }

            /// <summary>
            /// 注册自定义 SqlBuilder（按数据源名称）。
            /// </summary>
            /// <param name="dataSourceName">数据源名称。</param>
            /// <param name="sqlBuilder">SqlBuilder 实例。</param>
            public void RegisterSqlBuilder(string dataSourceName, SqlBuilder sqlBuilder)
            {
                SqlBuilders[dataSourceName] = sqlBuilder;
            }

            /// <summary>
            /// 注册自定义 SqlBuilder（按连接类型）。
            /// </summary>
            /// <param name="providerType">数据库连接类型。</param>
            /// <param name="sqlBuilder">SqlBuilder 实例。</param>
            public void RegisterSqlBuilder(Type providerType, SqlBuilder sqlBuilder)
            {
                SqlBuildersByType[providerType] = sqlBuilder;
            }
        }

        /// <summary>
        /// 扫描指定程序集，自动注册标记 [AutoRegister] 的类型。
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="assemblies">目标程序集（为空则扫描当前域所有引用程序集）。</param>
        /// <returns>服务集合。</returns>
        public static IServiceCollection RegisterAutoService(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            return RegisterAutoService(services, null, assemblies);
        }

        /// <summary>
        /// 扫描指定程序集，自动注册标记 [AutoRegister] 的类型，并通过 <paramref name="logger"/> 输出扫描日志。
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="logger">日志记录器（为 null 时跳过日志输出）。</param>
        /// <param name="assemblies">目标程序集（为空则扫描当前域所有引用程序集）。</param>
        /// <returns>服务集合。</returns>
        public static IServiceCollection RegisterAutoService(
            this IServiceCollection services,
            ILogger logger,
            params Assembly[] assemblies)
        {
            var assemblyList = new HashSet<Assembly>();

            // 自动加上 LiteOrm 和 LiteOrm.Common 的 Assembly
            assemblyList.Add(typeof(LiteOrmServiceExtensions).Assembly);
            assemblyList.Add(typeof(AutoRegisterAttribute).Assembly);

            // 若指定了程序集，则加入指定列表；否则扫描引用程序集
            if (assemblies != null && assemblies.Any())
            {
                foreach (var assembly in assemblies)
                {
                    assemblyList.Add(assembly);
                }
            }
            else
            {
                foreach (var assembly in AssemblyAnalyzer.GetAllReferencedAssemblies())
                {
                    assemblyList.Add(assembly);
                }
            }

            logger?.LogDebug("Scanning {Count} assemblies to register LiteOrm services", assemblyList.Count);

            // netstandard2.0 不具备编译期可用的 MS DI Keyed Service 支持，使用注册表 + 工厂作为回退方案。
            KeyedServiceRegistry keyedRegistry = null;
#if !(NET8_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER)
            keyedRegistry = new KeyedServiceRegistry();
            services.AddSingleton(typeof(KeyedServiceRegistry), keyedRegistry);
#endif

            var autoActivateTypes = new List<Type>();
            var totalRegistered = 0;

            foreach (var assembly in assemblyList)
            {
                IEnumerable<Type> types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    logger?.LogWarning(ex, "Failed to load types from assembly '{Assembly}', some types will be skipped", assembly.FullName);
                    types = ex.Types.Where(t => t != null);
                }

                var registrableTypes = types
                    .Where(t => !t.IsAbstract && !t.IsInterface && (t.GetCustomAttribute<AutoRegisterAttribute>(true)?.Enabled ?? false))
                    .ToList();

                logger?.LogDebug("Scanned assembly '{Assembly}': found {Count} registrable type(s)",
                    assembly.GetName().Name, registrableTypes.Count);

                foreach (var t in registrableTypes)
                {
                    var attr = t.GetCustomAttribute<AutoRegisterAttribute>(true);
                    logger?.LogDebug(
                        "Registering {Kind} service '{Type}' [Lifetime={Lifetime}, AutoActivate={AutoActivate}]",
                        t.IsGenericTypeDefinition ? "generic" : "regular",
                        t.FullName,
                        attr?.Lifetime ?? Lifetime.Scoped,
                        attr?.AutoActivate ?? false);

                    RegisterSingleType(services, t, attr, keyedRegistry, autoActivateTypes, logger);
                }

                totalRegistered += registrableTypes.Count;
            }

            // 将所有 AutoActivate 类型注册为一个 IHostedService，在宿主启动时统一解析以触发实例化。
            if (autoActivateTypes.Count > 0)
            {
                var typesToActivate = autoActivateTypes.ToArray();
                services.AddHostedService(sp => new AutoActivateHostedService(sp, typesToActivate));
                logger?.LogDebug("Registered {Count} auto-activate type(s) via IHostedService", typesToActivate.Length);
            }

            logger?.LogInformation(
                "LiteOrm service registration complete: scanned {AssemblyCount} assemblies, registered {Total} type(s)",
                assemblyList.Count, totalRegistered);

            return services;
        }

        /// <summary>
        /// 将单个带 [AutoRegister] 的类型注册到 MS DI。
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="implementationType">实现类型。</param>
        /// <param name="attr">自动注册特性。</param>
        /// <param name="keyedRegistry">netstandard2.0 下用于键控服务回退查找的注册表（net8.0+/netstandard2.1+ 下为 null）。</param>
        /// <param name="autoActivateTypes">收集需要自动激活的类型列表。</param>
        /// <param name="logger">日志记录器。</param>
        private static void RegisterSingleType(
            IServiceCollection services,
            Type implementationType,
            AutoRegisterAttribute attr,
            KeyedServiceRegistry keyedRegistry,
            List<Type> autoActivateTypes,
            ILogger logger)
        {
            var lifetime = ToServiceLifetime(attr?.Lifetime ?? Lifetime.Scoped);
            var key = attr?.Key;
            bool isGenericDefinition = implementationType.IsGenericTypeDefinition;
            var serviceTypes = GetServiceTypes(implementationType, attr, out bool hasIntercept);

            if (hasIntercept)
            {
                // 拦截由 LiteOrm.Framework 项目负责应用，此处仅记录不处理。
                logger?.LogDebug(
                    "Type '{Type}' is decorated with InterceptAttribute; interception will be applied by LiteOrm.Framework.",
                    implementationType.FullName);
            }

            // 自身注册：使实现类型可被直接解析，并作为多个服务类型之间共享同一实例的锚点。
            // MS DI 内置容器不支持基于工厂的开放泛型，因此开放泛型使用类型描述符。
            if (!services.Any(d => d.ServiceType == implementationType && d.ImplementationType == implementationType))
            {
                services.Add(new ServiceDescriptor(implementationType, implementationType, lifetime));
            }

            foreach (var serviceType in serviceTypes)
            {
                if (serviceType == implementationType)
                {
                    // 已通过自身注册完成。
                    continue;
                }

                if (isGenericDefinition)
                {
                    // 开放泛型：MS DI 不支持基于工厂的开放泛型，使用类型描述符。
                    // 键控的开放泛型在 MS DI 中不被可靠支持，回退为非键控注册 +
                    // (netstandard2.0) 注册表映射。
                    if (key != null)
                    {
                        keyedRegistry?.Register(serviceType, key, implementationType);
                    }
                    services.Add(new ServiceDescriptor(serviceType, implementationType, lifetime));
                }
                else
                {
                    // 闭合类型：使用工厂从自身注册解析实例，保证多个服务类型共享同一实例。
                    if (key != null)
                    {
#if NET8_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                        services.Add(CreateKeyedDescriptor(
                            serviceType,
                            key,
                            (sp, _) => sp.GetRequiredService(implementationType),
                            lifetime));
#else
                        // netstandard2.0 回退：注册非键控工厂描述符（供 IEnumerable<T> 枚举）并记录键映射。
                        keyedRegistry?.Register(serviceType, key, implementationType);
                        services.Add(CreateDescriptor(serviceType, sp => sp.GetRequiredService(implementationType), lifetime));
#endif
                    }
                    else
                    {
                        services.Add(CreateDescriptor(serviceType, sp => sp.GetRequiredService(implementationType), lifetime));
                    }
                }
            }

            if (attr?.AutoActivate ?? false)
            {
                autoActivateTypes.Add(implementationType);
            }
        }

        /// <summary>
        /// 计算类型应注册的服务类型集合，并检测是否存在 <see cref="InterceptAttribute"/>。
        /// </summary>
        private static List<Type> GetServiceTypes(Type implementationType, AutoRegisterAttribute attr, out bool hasIntercept)
        {
            hasIntercept = false;
            var serviceTypes = new List<Type>();

            // 若特性指定了 ServiceTypes，直接使用
            if (attr?.ServiceTypes is not null && attr.ServiceTypes.Any())
            {
                serviceTypes.AddRange(attr.ServiceTypes);
            }
            else
            {
                // 否则自动获取所有实现的接口（排除 System.* 命名空间下的接口）
                foreach (var serviceType in implementationType.GetInterfaces()
                    .Where(i => i.Namespace != null
                             && !i.Namespace.StartsWith("System.")
                             && i.Namespace != "System"
                             && (i.GetCustomAttribute<AutoRegisterAttribute>(true)?.Enabled ?? true)))
                {
                    if (implementationType.IsGenericTypeDefinition && serviceType.IsGenericType)
                    {
                        if (implementationType.GetGenericArguments().Length == serviceType.GenericTypeArguments.Length
                            && serviceType.GenericTypeArguments.All(t => t.DeclaringType == implementationType))
                        {
                            serviceTypes.Add(serviceType.GetGenericTypeDefinition());
                        }
                    }
                    else if (!implementationType.IsGenericTypeDefinition)
                    {
                        // 只有当实现类型不是泛型类型定义时，才添加非泛型接口
                        serviceTypes.Add(serviceType);
                    }
                }
            }

            var interceptAttribute = implementationType.GetCustomAttribute<InterceptAttribute>()
                ?? implementationType.GetInterfaces()
                    .Select(i => i.GetCustomAttribute<InterceptAttribute>())
                    .FirstOrDefault(a => a is not null);

            if (interceptAttribute is null)
            {
                // 没有拦截特性时，将实现类型自身也注册为服务。
                serviceTypes.Add(implementationType);
            }
            else
            {
                // 存在拦截特性：此处不应用拦截（由 Framework 处理），仅记录。
                hasIntercept = true;
            }

            return serviceTypes;
        }

        /// <summary>
        /// 将 LiteOrm 的 <see cref="Lifetime"/> 枚举映射为 MS DI 的 <see cref="ServiceLifetime"/>。
        /// </summary>
        private static ServiceLifetime ToServiceLifetime(Lifetime lifetime)
        {
            return lifetime switch
            {
                Lifetime.Singleton => ServiceLifetime.Singleton,
                Lifetime.Scoped => ServiceLifetime.Scoped,
                Lifetime.Transient => ServiceLifetime.Transient,
                _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null)
            };
        }

        /// <summary>
        /// 创建基于工厂的非键控 <see cref="ServiceDescriptor"/>。
        /// </summary>
        private static ServiceDescriptor CreateDescriptor(Type serviceType, Func<IServiceProvider, object> factory, ServiceLifetime lifetime)
        {
            return lifetime switch
            {
                ServiceLifetime.Singleton => ServiceDescriptor.Singleton(serviceType, factory),
                ServiceLifetime.Scoped => ServiceDescriptor.Scoped(serviceType, factory),
                _ => ServiceDescriptor.Transient(serviceType, factory),
            };
        }

#if NET8_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        /// <summary>
        /// 创建基于工厂的键控 <see cref="ServiceDescriptor"/>（net8.0+/netstandard2.1+ 可用）。
        /// </summary>
        private static ServiceDescriptor CreateKeyedDescriptor(Type serviceType, object key, Func<IServiceProvider, object, object> factory, ServiceLifetime lifetime)
        {
            return lifetime switch
            {
                ServiceLifetime.Singleton => ServiceDescriptor.KeyedSingleton(serviceType, key, factory),
                ServiceLifetime.Scoped => ServiceDescriptor.KeyedScoped(serviceType, key, factory),
                _ => ServiceDescriptor.KeyedTransient(serviceType, key, factory),
            };
        }
#endif

        /// <summary>
        /// 在宿主启动时解析所有标记为 AutoActivate 的类型，触发其实例化。
        /// </summary>
        internal sealed class AutoActivateHostedService : IHostedService
        {
            private readonly IServiceProvider _serviceProvider;
            private readonly Type[] _types;

            public AutoActivateHostedService(IServiceProvider serviceProvider, Type[] types)
            {
                _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
                _types = types ?? Type.EmptyTypes;
            }

            /// <inheritdoc/>
            public Task StartAsync(CancellationToken cancellationToken)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Task.FromCanceled(cancellationToken);
                }

                foreach (var type in _types)
                {
                    // 解析以触发实例化（构造失败将抛出，终止启动）。
                    _serviceProvider.GetService(type);
                }

                return Task.CompletedTask;
            }

            /// <inheritdoc/>
            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }

        /// <summary>
        /// netstandard2.0 下键控服务的注册表，保存 (服务类型, 键) -> 实现类型 的映射，
        /// 供 <see cref="LiteOrmKeyedServiceExtensions.ResolveKeyed"/> 解析使用。
        /// </summary>
        internal sealed class KeyedServiceRegistry
        {
            private readonly Dictionary<(Type ServiceType, object Key), Type> _map =
                new Dictionary<(Type ServiceType, object Key), Type>();

            public void Register(Type serviceType, object key, Type implementationType)
            {
                _map[(serviceType, key)] = implementationType;
            }

            public bool TryGet(Type serviceType, object key, out Type implementationType)
            {
                return _map.TryGetValue((serviceType, key), out implementationType);
            }
        }
    }

#if !(NET8_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER)
    /// <summary>
    /// netstandard2.0 下的键控服务解析扩展。
    /// <para>MS DI 的 Keyed Service 仅在 net8.0+/netstandard2.1+ 下启用；netstandard2.0 通过
    /// <see cref="LiteOrmServiceExtensions.KeyedServiceRegistry"/> + 本扩展方法提供等价的键控解析能力。</para>
    /// </summary>
    public static class LiteOrmKeyedServiceExtensions
    {
        /// <summary>
        /// 按键解析服务（找不到时返回 default）。
        /// </summary>
        public static T ResolveKeyed<T>(this IServiceProvider provider, object key)
        {
            return (T)provider.ResolveKeyed(typeof(T), key);
        }

        /// <summary>
        /// 按键解析服务（找不到时返回 null）。
        /// </summary>
        public static object ResolveKeyed(this IServiceProvider provider, Type serviceType, object key)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

            var registry = provider.GetService(typeof(LiteOrmServiceExtensions.KeyedServiceRegistry))
                as LiteOrmServiceExtensions.KeyedServiceRegistry;

            if (registry != null && registry.TryGet(serviceType, key, out var implementationType))
            {
                return provider.GetService(implementationType);
            }

            return null;
        }

        /// <summary>
        /// 按键解析必需的服务（找不到时抛出异常）。
        /// </summary>
        public static T ResolveRequiredKeyed<T>(this IServiceProvider provider, object key)
        {
            var service = provider.ResolveKeyed<T>(key);
            if (service == null)
            {
                throw new InvalidOperationException(
                    $"No keyed service of type '{typeof(T).FullName}' with key '{key}' is registered.");
            }
            return service;
        }
    }
#endif
}
