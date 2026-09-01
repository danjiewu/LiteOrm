using Autofac;
using Autofac.Builder;
using Autofac.Extensions.DependencyInjection;
using Autofac.Extras.DynamicProxy;
using Castle.DynamicProxy;
using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using InterceptAttribute = LiteOrm.Common.InterceptAttribute;

namespace LiteOrm.DependencyInjection
{
    /// <summary>
    /// LiteOrm 服务提供者扩展方法集合。
    /// </summary>
    /// <remarks>
    /// LiteOrmServiceExtensions 提供了将 LiteOrm 框架集成到 Autofac DI 容器的扩展方法，
    /// 同时启用 Castle DynamicProxy 拦截器支持。
    ///
    /// 主要功能包括：
    /// 1. Autofac 容器集成 - 通过 <see cref="RegisterLiteOrm(IHostBuilder, Action{LiteOrmOptions})"/>
    ///    使用 Autofac 作为 DI 容器；
    /// 2. 自动服务注册 - 扫描程序集中带 [AutoRegister] 特性的类型并注册到 Autofac；
    /// 3. 拦截器应用 - 读取 <see cref="InterceptAttribute"/> 并自动应用 Castle DynamicProxy 拦截；
    /// 4. SqlBuilder 注册 - 注册自定义 SqlBuilder 到 SqlBuilderFactory；
    /// 5. 作用域跟踪 - 通过 Autofac ILifetimeScope 事件自动跟踪作用域变化。
    ///
    /// 使用示例：
    /// <code>
    /// var builder = Host.CreateDefaultBuilder(args)
    ///     .RegisterLiteOrm(options =>
    ///     {
    ///         options.Assemblies = new[] { typeof(MyService).Assembly };
    ///     });
    /// </code>
    /// </remarks>
    public static class LiteOrmServiceExtensions
    {
        /// <summary>
        /// 共享的 <see cref="ProxyGenerator"/> 单例。Castle DynamicProxy 会在实例内部缓存生成的代理类型，
        /// 复用同一实例可避免每次创建代理时重复生成类型，显著提升性能。
        /// </summary>
        private static readonly ProxyGenerator _proxyGenerator = new ProxyGenerator();

        /// <summary>
        /// 注册 LiteOrm 框架到主机构建器（Autofac 容器 + Castle DynamicProxy 拦截器）。
        /// </summary>
        /// <param name="hostBuilder">主机构建器。</param>
        /// <returns>配置后的主机构建器。</returns>
        public static IHostBuilder RegisterLiteOrm(this IHostBuilder hostBuilder)
        {
            return RegisterLiteOrm(hostBuilder, null);
        }

        /// <summary>
        /// 注册 LiteOrm 框架到主机构建器，并允许配置选项。
        /// <para>
        /// 通过 <paramref name="configureOptions"/> 回调驱动注册期配置；若要使用注入工厂方式配置，
        /// 可调用 <see cref="RegisterLiteOrmOptions"/> 注册 <see cref="LiteOrmOptions"/> 工厂，
        /// 运行时可通过 DI 解析到该工厂构造的选项（覆盖 <paramref name="configureOptions"/>）。
        /// </para>
        /// </summary>
        /// <param name="hostBuilder">主机构建器。</param>
        /// <param name="configureOptions">配置选项的回调函数。</param>
        /// <returns>配置后的主机构建器。</returns>
        public static IHostBuilder RegisterLiteOrm(this IHostBuilder hostBuilder, Action<LiteOrmOptions>? configureOptions)
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

            return hostBuilder
                // 将选项注册进 DI（IServiceCollection 通道，TryAddSingleton）：
                // 若用户已通过 RegisterLiteOrmOptions 注册了工厂，则工厂优先（运行时覆盖 configureOptions）。
                .ConfigureServices((_, services) => services.TryAddSingleton(options))
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureContainer<ContainerBuilder>(builder =>
                {
                    var logger = options.LoggerFactory?.CreateLogger(nameof(LiteOrmServiceExtensions));

                    // 注册核心服务（Autofac 原生注册）
                    builder.RegisterCoreServices();

                    // 自动扫描并注册标记 [AutoRegister] 的服务（Autofac 版，含拦截器支持）
                    if (options.AutoRegisterServices)
                    {
                        try
                        {
                            builder.RegisterAutoService(logger, options.Assemblies ?? Array.Empty<Assembly>());
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException("Failed to register LiteOrm services automatically", ex);
                        }
                    }
                    else
                    {
                        // 显式注册核心实体服务与数据访问对象， 注册顺序决定了 IEntityViewService<> 解析到 EntityViewService<>，
                        // IEntityService<>/IEntityServiceAsync<> 解析到 EntityService<>。
                        // 注意：启用 EnableInterfaceInterceptors 时不能注册 AsSelf（具体类型非接口）
                        builder.RegisterGeneric(typeof(EntityService<>))
                            .As(typeof(IEntityService<>))
                            .As(typeof(IEntityServiceAsync<>))
                            .EnableInterfaceInterceptors()
                            .InterceptedBy(typeof(ServiceInvokeInterceptor))
                            .InstancePerLifetimeScope();

                        builder.RegisterGeneric(typeof(EntityViewService<>))
                            .As(typeof(IEntityViewService<>))
                            .As(typeof(IEntityViewServiceAsync<>))
                            .EnableInterfaceInterceptors()
                            .InterceptedBy(typeof(ServiceInvokeInterceptor))
                            .InstancePerLifetimeScope();

                        builder.RegisterGeneric(typeof(ObjectDAO<>))
                            .AsSelf()
                            .As(typeof(IObjectDAO<>))
                            .InstancePerLifetimeScope();

                        builder.RegisterGeneric(typeof(ObjectViewDAO<>))
                            .AsSelf()
                            .As(typeof(IObjectViewDAO<>))
                            .InstancePerLifetimeScope();

                        builder.RegisterGeneric(typeof(DataDAO<>))
                            .AsSelf()
                            .InstancePerLifetimeScope();

                        builder.RegisterGeneric(typeof(DataViewDAO<>))
                            .AsSelf()
                            .As(typeof(IDataViewDAO<>))
                            .InstancePerLifetimeScope();
                    }

                    // 注册自定义 SqlBuilder
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

                    // 注册作用域跟踪和全局实例初始化（始终启用，无需额外配置）
                    builder.RegisterBuildCallback(container =>
                    {
                        // 设置根作用域的会话工厂，使 SessionManager.Current 在根作用域可用
                        SessionManager.SetCurrent(() => container.Resolve<SessionManager>());

                        // 注册子作用域跟踪
                        ScopeExtensions.RegisterScope(container);
                    });
                });
        }

        /// <summary>
        /// 以工厂方式注册 <see cref="LiteOrmOptions"/> 到 DI 容器，便于在配置服务(<see cref="IConfiguration"/>)或其他 DI 服务基础上构造参数。
        /// <para>
        /// 工厂接收 <see cref="IServiceProvider"/>，可从其中解析 <see cref="IConfiguration"/> 等依赖后返回选项。
        /// 若同时调用了 <see cref="RegisterLiteOrm(IHostBuilder, Action{LiteOrmOptions})"/>，
        /// 则运行时解析 <see cref="LiteOrmOptions"/> 时以本工厂构造的选项为准（覆盖 configureOptions）。
        /// </para>
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="optionsFactory">选项工厂，接收 <see cref="IServiceProvider"/>，返回 <see cref="LiteOrmOptions"/>。</param>
        /// <returns>返回修改后的服务集合以支持链式调用。</returns>
        /// <example>
        /// <code>
        /// builder.ConfigureServices(services =>
        ///     services.RegisterLiteOrmOptions(sp =>
        ///     {
        ///         var config = sp.GetRequiredService&lt;IConfiguration&gt;();
        ///         return new LiteOrmServiceExtensions.LiteOrmOptions
        ///         {
        ///             AutoRegisterServices = config.GetValue&lt;bool&gt;("LiteOrm:AutoRegisterServices"),
        ///         };
        ///     }));
        /// </code>
        /// </example>
        public static IServiceCollection RegisterLiteOrmOptions(
            this IServiceCollection services,
            Func<IServiceProvider, LiteOrmOptions> optionsFactory)
        {
            if (optionsFactory is null)
                throw new ArgumentNullException(nameof(optionsFactory));
            services.AddSingleton(optionsFactory);
            return services;
        }

        /// <summary>
        /// 显式注册 LiteOrm 核心服务到 Autofac 容器（原生注册）。
        /// 这些服务不再使用 [AutoRegister] 特性，而是通过此方法手动注册，确保注册行为的确定性。
        /// </summary>
        /// <remarks>
        /// 注册的核心服务包括：
        /// 1. <see cref="DataSourceProvider"/> - 单例，从 <c>LiteOrm</c> 配置节点加载数据源；
        /// 2. <see cref="SqlBuilderFactory"/> - 单例，使用静态 <see cref="SqlBuilderFactory.Instance"/> 确保 DI 解析与静态访问一致；
        /// 3. <see cref="DAOContextPoolFactory"/> - 单例，数据库连接池工厂；
        /// 4. <see cref="SessionManager"/> - Scoped，每作用域一个会话管理器实例；
        /// 5. <see cref="LiteOrmCoreInitializer"/> - HostedService，启动时自动同步数据库表结构。
        /// </remarks>
        /// <param name="builder">Autofac 容器构建器。</param>
        /// <returns>容器构建器。</returns>
        public static ContainerBuilder RegisterCoreServices(this ContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            // 数据源提供程序 - 单例，从宿主 IConfiguration 的 LiteOrm 节点加载连接配置
            builder.Register(sp =>
                {
                    var configuration = sp.Resolve<IConfiguration>();
                    var provider = new DataSourceProvider();
                    provider.LoadConfiguration(configuration.GetSection("LiteOrm"));
                    return provider;
                })
                .As<IDataSourceProvider>()
                .As<DataSourceProvider>()
                .SingleInstance();

            //注册服务调用拦截器，为服务方法提供事务、日志和性能监控
            builder.RegisterType<ServiceInvokeInterceptor>()
                .InstancePerLifetimeScope();

            //注册服务生成拦截器，根据接口自动生成服务
            builder.RegisterType<ServiceGenerateInterceptor>()
                .InstancePerLifetimeScope();

            // SqlBuilderFactory 使用静态 Instance，确保 DI 解析的实例与静态访问 (SqlBuilderFactory.Instance) 一致
            builder.Register(_ => SqlBuilderFactory.Instance)
                .As<SqlBuilderFactory>()
                .As<ISqlBuilderFactory>()
                .SingleInstance();

            // 连接池工厂 - 单例
            builder.RegisterType<DAOContextPoolFactory>()
                .SingleInstance();

            // 会话管理器 - 每作用域一个实例
            builder.RegisterType<SessionManager>()
                .InstancePerLifetimeScope();

            // 表信息提供程序 - 单例
            builder.RegisterType<AttributeTableInfoProvider>()
                .As<TableInfoProvider>()
                .SingleInstance();

            // 初始化 - HostedService
            builder.RegisterType<LiteOrmCoreInitializer>()
                .As<IHostedService>()
                .SingleInstance();

            return builder;
        }

        /// <summary>
        /// 注册服务生成器代理（通过 ServiceGenerateInterceptor 从 DI 容器解析返回类型）
        /// </summary>
        /// <typeparam name="T">服务生成工厂类</typeparam>
        /// <param name="services">DI 容器</param>
        /// <param name="lifetime">服务生命周期，默认为 Scoped</param>
        /// <returns>DI 容器</returns>
        public static IServiceCollection AddServiceGenerator<T>(
            this IServiceCollection services,
            ServiceLifetime lifetime = ServiceLifetime.Scoped) where T : class
        {
            services.Add(new ServiceDescriptor(typeof(T), sp =>
            {
                var interceptor = sp.GetRequiredService<ServiceGenerateInterceptor>();
                return _proxyGenerator.CreateInterfaceProxyWithoutTarget<T>(interceptor);
            }, lifetime));
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
            /// 是否自动扫描程序集注册标记 <c>[AutoRegister]</c> 的类型到 Autofac 容器（含拦截器支持）。
            /// 默认为 <c>true</c>；设为 <c>false</c> 时跳过自动扫描，需手动注册服务。
            /// </summary>
            public bool AutoRegisterServices { get; set; } = true;

            /// <summary>
            /// 要扫描的程序集列表。
            /// </summary>
            public Assembly[]? Assemblies { get; set; }

            /// <summary>
            /// 日志工厂，用于记录服务注册过程中的程序集扫描日志（可选）。
            /// </summary>
            public ILoggerFactory? LoggerFactory { get; set; }

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
        /// 扫描指定程序集，自动注册标记 [AutoRegister] 的类型到 Autofac 容器（含拦截器支持）。
        /// </summary>
        /// <param name="builder">Autofac 容器构建器。</param>
        /// <param name="assemblies">目标程序集（为空则扫描当前域所有引用程序集）。</param>
        /// <returns>容器构建器。</returns>
        public static ContainerBuilder RegisterAutoService(
            this ContainerBuilder builder,
            params Assembly[] assemblies)
        {
            return RegisterAutoService(builder, null, assemblies);
        }

        /// <summary>
        /// 扫描指定程序集，自动注册标记 [AutoRegister] 的类型到 Autofac 容器（含拦截器支持），
        /// 并通过 <paramref name="logger"/> 输出扫描日志。
        /// </summary>
        /// <param name="builder">Autofac 容器构建器。</param>
        /// <param name="logger">日志记录器（为 null 时跳过日志输出）。</param>
        /// <param name="assemblies">目标程序集（为空则扫描当前域所有引用程序集）。</param>
        /// <returns>容器构建器。</returns>
        public static ContainerBuilder RegisterAutoService(
            this ContainerBuilder builder,
            ILogger? logger,
            params Assembly[] assemblies)
        {
            var assemblyList = new HashSet<Assembly>();
            assemblyList.Add(typeof(LiteOrmServiceExtensions).Assembly);
            assemblyList.Add(typeof(AutoRegisterAttribute).Assembly);

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

            logger?.LogDebug("Scanning {Count} assemblies to register LiteOrm services (Autofac)", assemblyList.Count);

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
                    types = ex.Types.OfType<Type>();
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

                    RegisterSingleType(builder, t, attr, logger);
                }
            }

            return builder;
        }

        /// <summary>
        /// 将单个带 [AutoRegister] 的类型注册到 Autofac 容器（含拦截器支持）。
        /// </summary>
        private static void RegisterSingleType(
            ContainerBuilder builder,
            Type implementationType,
            AutoRegisterAttribute? attr,
            ILogger? logger)
        {
            if (implementationType.IsGenericTypeDefinition)
            {
                var registration = builder.RegisterGeneric(implementationType);
                ApplyRegistrationSettings(registration, implementationType, attr, logger);
            }
            else
            {
                var registration = builder.RegisterType(implementationType);
                ApplyRegistrationSettings(registration, implementationType, attr, logger);
            }
        }

        /// <summary>
        /// 将生命周期、拦截器和服务类型等通用配置应用到注册构建器上。
        /// </summary>
        private static void ApplyRegistrationSettings<TLimit, TActivatorData, TRegistrationStyle>(
            IRegistrationBuilder<TLimit, TActivatorData, TRegistrationStyle> registration,
            Type implementationType,
            AutoRegisterAttribute? attr,
            ILogger? logger)
        {
            // 设置生命周期
            var lifetime = attr?.Lifetime ?? Lifetime.Scoped;
            switch (lifetime)
            {
                case Lifetime.Singleton:
                    registration.SingleInstance();
                    break;
                case Lifetime.Scoped:
                    registration.InstancePerLifetimeScope();
                    break;
                case Lifetime.Transient:
                    registration.InstancePerDependency();
                    break;
            }

            // 设置自动激活
            if (attr?.AutoActivate ?? false)
            {
                registration.AutoActivate();
            }

            // 注册服务类型
            // 注意：Autofac 中 .As() 会覆盖默认的自身注册，因此需要显式注册 AsSelf() 以保证具体类型可解析
            var serviceTypes = GetServiceTypes(implementationType, attr);
            bool hasAdditionalServices = false;
            foreach (var serviceType in serviceTypes)
            {
                if (serviceType == implementationType)
                    continue;

                hasAdditionalServices = true;

                if (attr?.Key != null)
                {
                    registration.Keyed(attr.Key, serviceType);
                }
                else
                {
                    registration.As(serviceType);
                }
            }

            // 检测 InterceptAttribute
            var interceptAttribute = implementationType.GetCustomAttribute<InterceptAttribute>()
                ?? implementationType.GetInterfaces()
                    .Select(i => i.GetCustomAttribute<InterceptAttribute>())
                    .FirstOrDefault(a => a is not null);

            if (interceptAttribute != null)
            {
                // 应用 Castle DynamicProxy 拦截
                registration.EnableInterfaceInterceptors()
                    .InterceptedBy(interceptAttribute.InterceptorType);
                logger?.LogDebug(
                    "Applied interception to '{Type}' with interceptor '{Interceptor}'",
                    implementationType.FullName, interceptAttribute.InterceptorType.FullName);
            }
            else if (HasServiceAttribute(implementationType))
            {
                // 带 [Service] 特性的类型自动应用服务调用拦截器
                registration.EnableInterfaceInterceptors()
                    .InterceptedBy(typeof(ServiceInvokeInterceptor));
                logger?.LogDebug(
                    "Applied interception to '{Type}' with interceptor 'ServiceInvokeInterceptor' ([Service])",
                    implementationType.FullName);
            }
            else if (hasAdditionalServices)
            {
                // 当有额外的服务类型注册且未启用拦截器时，显式注册自身以保证具体类型可解析
                // （Autofac 中 .As() 会覆盖默认的自身注册）
                // 注意：启用 EnableInterfaceInterceptors 时不能注册非接口类型作为服务，
                // 否则 EnsureInterfaceInterceptionApplies 会抛出异常
                registration.As(implementationType);
            }
        }

        /// <summary>
        /// 判断是否为 LiteOrm 的非泛型标记接口（这些接口仅作为约定标记，不作为服务注册类型）。
        /// <para>原先通过接口上的 <c>[AutoRegister(false)]</c> 排除，特性迁移后改为按接口名判断。</para>
        /// </summary>
        private static bool IsExcludedMarkerInterface(Type serviceType)
        {
            if (serviceType.IsGenericType) return false;
            return serviceType.FullName is "LiteOrm.Common.IObjectViewDAO"
                or "LiteOrm.Common.IObjectDAO"
                or "LiteOrm.Common.IObjectDAOAsync"
                or "LiteOrm.Service.IEntityService"
                or "LiteOrm.Service.IEntityServiceAsync"
                or "LiteOrm.Service.IEntityViewService"
                or "LiteOrm.Service.IEntityViewServiceAsync";
        }

        /// <summary>
        /// 计算类型应注册的服务类型集合。
        /// <para>
        /// <see cref="RegisterPolicy.All"/>（默认）：非 System、非标记接口 + 实现类型自身；
        /// <see cref="RegisterPolicy.Interface"/>：仅已实现接口（排除带 <c>[AutoRegister(false)]</c> 的接口）；
        /// <see cref="RegisterPolicy.Self"/>：仅实现类型自身。
        /// </para>
        /// </summary>
        private static List<Type> GetServiceTypes(Type implementationType, AutoRegisterAttribute? attr)
        {
            var mode = attr?.Policy ?? RegisterPolicy.All;
            var serviceTypes = new List<Type>();

            // 没有拦截特性时，将实现类型自身也注册为服务（Autofac 拦截时不能注册非接口自身）
            var hasIntercept = HasInterceptAttribute(implementationType);

            if (mode == RegisterPolicy.Self)
            {
                if (!hasIntercept) serviceTypes.Add(implementationType);
                return serviceTypes;
            }

            foreach (var serviceType in implementationType.GetInterfaces()
                .Where(i => i.Namespace != null
                         && !i.Namespace.StartsWith("System.")
                         && i.Namespace != "System"
                         && !IsExcludedMarkerInterface(i)
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
                    serviceTypes.Add(serviceType);
                }
            }

            if (mode == RegisterPolicy.All && !hasIntercept && !serviceTypes.Contains(implementationType))
                serviceTypes.Add(implementationType);

            return serviceTypes;
        }

        /// <summary>
        /// 判断实现类型或其接口是否声明了 <see cref="InterceptAttribute"/>。
        /// </summary>
        private static bool HasInterceptAttribute(Type implementationType)
        {
            return implementationType.GetCustomAttribute<InterceptAttribute>() != null
                || implementationType.GetInterfaces().Any(i => i.GetCustomAttribute<InterceptAttribute>() != null);
        }

        /// <summary>
        /// 判断实现类型或其接口是否带 <see cref="ServiceAttribute"/> 且 <see cref="ServiceAttribute.IsService"/> 为 <c>true</c>。
        /// </summary>
        private static bool HasServiceAttribute(Type implementationType)
        {
            return implementationType.GetCustomAttributes<ServiceAttribute>(true).Any(a => a.IsService)
                || implementationType.GetInterfaces()
                    .Any(i => i.GetCustomAttributes<ServiceAttribute>(true).Any(a => a.IsService));
        }
    }
}
