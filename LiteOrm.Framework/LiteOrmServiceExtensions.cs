using Autofac;
using Autofac.Builder;
using Autofac.Extensions.DependencyInjection;
using Autofac.Extras.DynamicProxy;
using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using InterceptAttribute = LiteOrm.Common.InterceptAttribute;

namespace LiteOrm.Framework
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

            return hostBuilder.UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureServices(services =>
                {
                    // 显式注册核心服务（MS DI 注册会被 AutofacServiceProviderFactory 转移到 Autofac 容器）
                    services.AddCoreLiteOrmServices();
                })
                .ConfigureContainer<ContainerBuilder>(builder =>
                {
                    var logger = options.LoggerFactory?.CreateLogger(nameof(LiteOrmServiceExtensions));

                    // 自动扫描并注册标记 [AutoRegister] 的服务（Autofac 版，含拦截器支持）
                    try
                    {
                        builder.RegisterAutoService(logger, options.Assemblies ?? Array.Empty<Assembly>());
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Failed to register LiteOrm services automatically", ex);
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

                    // 注册作用域跟踪和全局实例初始化
                    if (options.RegisterScope)
                    {
                        builder.RegisterBuildCallback(container =>
                        {
                            var lifetimeScope = container as ILifetimeScope;
                            if (lifetimeScope != null)
                            {
                                var sp = new AutofacServiceProvider(lifetimeScope);

                                // 设置全局 TableInfoProvider（DI 解析的实例优先于默认的 AttributeTableInfoProvider）
                                var tableInfoProvider = sp.GetService<TableInfoProvider>();
                                if (tableInfoProvider != null)
                                {
                                    TableInfoProvider.Default = tableInfoProvider;
                                }

                                // 设置根作用域的 ServiceProvider，使 SessionManager.Current 在根作用域可用
                                SessionManager.SetCurrentServiceProvider(sp);

                                // 注册子作用域跟踪
                                ScopeExtensions.RegisterScope(lifetimeScope);
                            }
                        });
                    }
                });
        }

        /// <summary>
        /// 显式注册 LiteOrm 核心服务。
        /// 这些服务不再使用 [AutoRegister] 特性，而是通过此方法手动注册，确保注册行为的确定性。
        /// </summary>
        /// <remarks>
        /// 注册的核心服务包括：
        /// 1. <see cref="DataSourceProvider"/> - 单例，从 <c>LiteOrm</c> 配置节点加载数据源；
        /// 2. <see cref="SqlBuilderFactory"/> - 单例，使用静态 <see cref="SqlBuilderFactory.Instance"/> 确保 DI 解析与静态访问一致；
        /// 3. <see cref="DAOContextPoolFactory"/> - 单例，数据库连接池工厂；
        /// 4. <see cref="SessionManager"/> - Scoped，每作用域一个会话管理器实例；
        /// 5. <see cref="LiteOrmCoreInitializer"/> - HostedService，启动时自动同步数据库表结构。
        /// 同时触发 <see cref="LiteOrmSqlFunctionInitializer.Initialize"/> 以注册 SQL 函数映射。
        /// </remarks>
        /// <param name="services">服务集合。</param>
        /// <returns>服务集合。</returns>
        public static IServiceCollection AddCoreLiteOrmServices(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            // 数据源提供程序 - 单例，从宿主 IConfiguration 的 LiteOrm 节点加载连接配置
            services.AddSingleton<IDataSourceProvider>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var provider = new DataSourceProvider();
                provider.LoadConfiguration(configuration.GetSection("LiteOrm"));
                return provider;
            });
            services.AddSingleton<DataSourceProvider>(sp => (DataSourceProvider)sp.GetRequiredService<IDataSourceProvider>());

            // SqlBuilderFactory 使用静态 Instance，确保 DI 解析的实例与静态访问 (SqlBuilderFactory.Instance) 一致
            services.AddSingleton<SqlBuilderFactory>(sp => SqlBuilderFactory.Instance);
            services.AddSingleton<ISqlBuilderFactory>(sp => sp.GetRequiredService<SqlBuilderFactory>());

            // 连接池工厂 - 单例
            services.AddSingleton<DAOContextPoolFactory>();

            // 会话管理器 - 每作用域一个实例
            services.AddScoped<SessionManager>();

            // 显式注册核心实体服务与数据访问对象（不再依赖 [AutoRegister] 特性扫描）。
            // 注意：注册顺序决定了 IEntityViewService<> 解析到 EntityViewService<>，
            // IEntityService<>/IEntityServiceAsync<> 解析到 EntityService<>。
            services.AddScoped(typeof(EntityService<>));
            services.AddScoped(typeof(IEntityService<>), typeof(EntityService<>));
            services.AddScoped(typeof(IEntityServiceAsync<>), typeof(EntityService<>));
            services.AddScoped(typeof(EntityViewService<>));
            services.AddScoped(typeof(IEntityViewService<>), typeof(EntityViewService<>));
            services.AddScoped(typeof(IEntityViewServiceAsync<>), typeof(EntityViewService<>));
            services.AddScoped(typeof(ObjectDAO<>));
            services.AddScoped(typeof(IObjectDAO<>), typeof(ObjectDAO<>));
            services.AddScoped(typeof(ObjectViewDAO<>));
            services.AddScoped(typeof(IObjectViewDAO<>), typeof(ObjectViewDAO<>));
            services.AddScoped(typeof(DataDAO<>));
            services.AddScoped(typeof(DataViewDAO<>));
            services.AddScoped(typeof(IDataViewDAO<>), typeof(DataViewDAO<>));

            // 表信息提供程序 - 单例
            services.AddSingleton<TableInfoProvider, AttributeTableInfoProvider>();

            // 批量插入提供程序工厂 - 单例
            services.AddSingleton<BulkProviderFactory>();

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
            /// <para>Scope 跟踪逻辑由本框架读取。</para>
            /// </summary>
            public bool RegisterScope { get; set; } = true;

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

            // 当有额外的服务类型注册且未启用拦截器时，显式注册自身以保证具体类型可解析
            // （Autofac 中 .As() 会覆盖默认的自身注册）
            // 注意：启用 EnableInterfaceInterceptors 时不能注册非接口类型作为服务，
            // 否则 EnsureInterfaceInterceptionApplies 会抛出异常
            if (hasAdditionalServices && interceptAttribute == null)
            {
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
        /// </summary>
        private static List<Type> GetServiceTypes(Type implementationType, AutoRegisterAttribute? attr)
        {
            var serviceTypes = new List<Type>();

            if (attr?.ServiceTypes is not null && attr.ServiceTypes.Any())
            {
                serviceTypes.AddRange(attr.ServiceTypes);
            }
            else
            {
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
            }

            // 没有拦截特性时，将实现类型自身也注册为服务
            var hasIntercept = implementationType.GetCustomAttribute<InterceptAttribute>() != null
                || implementationType.GetInterfaces()
                    .Any(i => i.GetCustomAttribute<InterceptAttribute>() != null);

            if (!hasIntercept && !serviceTypes.Contains(implementationType))
            {
                serviceTypes.Add(implementationType);
            }

            return serviceTypes;
        }
    }
}
