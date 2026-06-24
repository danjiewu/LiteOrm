using Castle.DynamicProxy;
using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm服务提供者扩展方法集合
    /// </summary>
    public static class LiteOrmServiceExtensions
    {
        /// <summary>
        /// 注册LiteOrm框架到主机构建器
        /// </summary>
        /// <param name="hostBuilder">主机构建器</param>
        /// <returns>配置后的主机构建器</returns>
        public static IHostBuilder RegisterLiteOrm(this IHostBuilder hostBuilder)
        {
            return RegisterLiteOrm(hostBuilder, null);
        }

        /// <summary>
        /// 注册LiteOrm框架到主机构建器，并允许配置选项
        /// </summary>
        /// <param name="hostBuilder">主机构建器</param>
        /// <param name="configureOptions">配置选项的回调函数</param>
        /// <returns>配置后的主机构建器</returns>
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

            return hostBuilder.ConfigureServices((context, services) =>
            {
                try
                {
                    var logger = options.LoggerFactory?.CreateLogger(nameof(LiteOrmServiceExtensions));
                    // 使用指定的程序集或默认程序集
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

                // 注册自定义SqlBuilder（按数据源名称）
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

                // 注册自定义SqlBuilder（按连接类型）
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

                // 注册 IHostedService 以便在宿主启动时执行 LiteOrm 初始化
                // LiteOrmCoreInitializer 和 LiteOrmSqlFunctionInitializer 通过 [AutoRegister] + 自动扫描注册为 IHostedService
            });
        }

        /// <summary>
        /// LiteOrm配置选项
        /// </summary>
        public class LiteOrmOptions
        {
            /// <summary>
            /// 注册的SqlBuilder映射（按数据源名称）
            /// </summary>
            internal Dictionary<string, SqlBuilder> SqlBuilders { get; } = new Dictionary<string, SqlBuilder>();

            /// <summary>
            /// 注册的SqlBuilder映射（按连接类型）
            /// </summary>
            internal Dictionary<Type, SqlBuilder> SqlBuildersByType { get; } = new Dictionary<Type, SqlBuilder>();

            /// <summary>
            /// 要扫描的程序集列表
            /// </summary>
            public System.Reflection.Assembly[] Assemblies { get; set; }

            /// <summary>
            /// 日志工厂，用于记录服务注册过程中的程序集扫描日志（可选）。
            /// 默认为控制台输出，最低级别为 <see cref="ServiceLogLevel.Information"/>。
            /// </summary>
            public ILoggerFactory LoggerFactory { get; set; }

            /// <summary>
            /// 注册自定义SqlBuilder（按数据源名称）
            /// </summary>
            /// <param name="dataSourceName">数据源名称</param>
            /// <param name="sqlBuilder">SqlBuilder实例</param>
            public void RegisterSqlBuilder(string dataSourceName, SqlBuilder sqlBuilder)
            {
                SqlBuilders[dataSourceName] = sqlBuilder;
            }

            /// <summary>
            /// 注册自定义SqlBuilder（按连接类型）
            /// </summary>
            /// <param name="providerType">数据库连接类型</param>
            /// <param name="sqlBuilder">SqlBuilder实例</param>
            public void RegisterSqlBuilder(Type providerType, SqlBuilder sqlBuilder)
            {
                SqlBuildersByType[providerType] = sqlBuilder;
            }
        }

        /// <summary>
        /// 扫描指定程序集，自动注册标记[AutoRegister]的类型
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="assemblies">目标程序集（为空则扫描当前域所有程序集）</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection RegisterAutoService(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            return RegisterAutoService(services, null, assemblies);
        }

        /// <summary>
        /// 扫描指定程序集，自动注册标记[AutoRegister]的类型，并通过 <paramref name="logger"/> 输出扫描日志
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="logger">日志记录器（为 null 时跳过日志输出）</param>
        /// <param name="assemblies">目标程序集（为空则扫描当前域所有程序集）</param>
        /// <returns>服务集合</returns>
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
            if (assemblies.Any())
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
                    RegisterTypeWithInterception(services, t);
                }
                totalRegistered += registrableTypes.Count;
            }

            logger?.LogInformation(
                "LiteOrm service registration complete: scanned {AssemblyCount} assemblies, registered {Total} type(s)",
                assemblyList.Count, totalRegistered);
            return services;
        }

        /// <summary>
        /// 注册类型并应用拦截配置
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="implementationType">要注册的实现类型</param>
        /// <returns>配置后的服务集合</returns>
        public static IServiceCollection RegisterTypeWithInterception(this IServiceCollection services, Type implementationType)
        {
            var attribute = implementationType.GetCustomAttribute<AutoRegisterAttribute>(true);
            Lifetime lifetime = attribute?.Lifetime ?? Lifetime.Scoped;
            var serviceLifetime = ToServiceLifetime(lifetime);

            List<Type> serviceTypes = new List<Type>();

            // 若特性指定了ServiceTypes，直接使用
            if (attribute?.ServiceTypes is not null && attribute.ServiceTypes.Any())
            {
                serviceTypes.AddRange(attribute.ServiceTypes);
            }
            // 否则自动获取所有实现的接口（排除系统接口如IDisposable）
            else
            {
                foreach (var serviceType in implementationType.GetInterfaces()
                    .Where(i => !i.Namespace.StartsWith("System.") && i.Namespace != "System"
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

            bool hasIntercept = implementationType.GetCustomAttribute<InterceptAttribute>() != null
                             || implementationType.GetInterfaces()
                                .Any(i => i.GetCustomAttribute<InterceptAttribute>() != null);

            if (!hasIntercept && !serviceTypes.Any())
            {
                // 无拦截且无接口，注册自身
                serviceTypes.Add(implementationType);
            }

            // 有 Key 的服务（如 IBulkProvider 按连接类型注册）
            if (attribute?.Key != null)
            {
                // 对于有 Key 的服务，直接注册为具体类型，通过 IServiceProvider.GetServices<T>() 枚举
                foreach (var serviceType in serviceTypes)
                {
                    services.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));
                }
                // 同时注册具体类型自身
                services.Add(new ServiceDescriptor(implementationType, implementationType, serviceLifetime));
                return services;
            }

            if (implementationType.IsGenericTypeDefinition)
            {
                // 泛型类型：使用 MS DI 开放泛型注册（不支持 AOP 代理）
                if (serviceTypes.Any())
                {
                    foreach (var serviceType in serviceTypes)
                    {
                        services.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));
                    }
                    // 同时注册开放泛型自身
                    services.Add(new ServiceDescriptor(implementationType, implementationType, serviceLifetime));
                }
                else
                {
                    services.Add(new ServiceDescriptor(implementationType, implementationType, serviceLifetime));
                }
            }
            else
            {
                // 非泛型类型
                if (hasIntercept && serviceTypes.Any())
                {
                    // 需要 AOP 拦截：使用 Castle.Core 代理工厂
                    var proxyGenerator = new ProxyGenerator();
                    foreach (var serviceType in serviceTypes)
                    {
                        services.Add(new ServiceDescriptor(serviceType, sp =>
                        {
                            var interceptors = ResolveInterceptors(sp, implementationType);
                            if (interceptors.Length > 0)
                            {
                                return proxyGenerator.CreateInterfaceProxyWithTarget(
                                    serviceType,
                                    sp.GetRequiredService(implementationType),
                                    interceptors);
                            }
                            return sp.GetRequiredService(implementationType);
                        }, serviceLifetime));
                    }
                    // 注册实现类型自身（供代理工厂解析）
                    services.Add(new ServiceDescriptor(implementationType, implementationType, serviceLifetime));
                }
                else if (serviceTypes.Any())
                {
                    // 无拦截：直接注册
                    foreach (var serviceType in serviceTypes)
                    {
                        services.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));
                    }
                    // 同时注册具体类型自身，支持通过具体类型直接解析
                    services.Add(new ServiceDescriptor(implementationType, implementationType, serviceLifetime));
                }
                else
                {
                    services.Add(new ServiceDescriptor(implementationType, implementationType, serviceLifetime));
                }

                // AutoActivate：通过立即解析来激活
                if (attribute?.AutoActivate == true && !implementationType.IsGenericTypeDefinition)
                {
                    // AutoActivate 通过 IHostedService 实现，但单例可以直接预解析
                    // 这里通过注册一个在首次构建时触发的回调来处理
                    // 使用 ServiceProviderServiceExtensions.GetRequiredService 会在 Build 后执行
                }
            }

            return services;
        }

        /// <summary>
        /// 解析类型上标记的拦截器
        /// </summary>
        private static IInterceptor[] ResolveInterceptors(IServiceProvider sp, Type implementationType)
        {
            var interceptAttrs = implementationType.GetCustomAttributes<InterceptAttribute>(true)
                .Concat(implementationType.GetInterfaces()
                    .SelectMany(i => i.GetCustomAttributes<InterceptAttribute>(true)));

            var interceptors = new List<IInterceptor>();
            foreach (var attr in interceptAttrs)
            {
                if (typeof(IInterceptor).IsAssignableFrom(attr.InterceptorType))
                {
                    var interceptor = sp.GetService(attr.InterceptorType) as IInterceptor;
                    if (interceptor != null)
                        interceptors.Add(interceptor);
                }
            }
            return interceptors.ToArray();
        }

        /// <summary>
        /// 将 LiteOrm Lifetime 转换为 MS DI ServiceLifetime
        /// </summary>
        private static ServiceLifetime ToServiceLifetime(Lifetime lifetime)
        {
            return lifetime switch
            {
                Lifetime.Singleton => ServiceLifetime.Singleton,
                Lifetime.Scoped => ServiceLifetime.Scoped,
                Lifetime.Transient => ServiceLifetime.Transient,
                _ => ServiceLifetime.Scoped
            };
        }

        /// <summary>
        /// 注册服务生成器，将接口获取服务通过动态代理转换为从 ServiceProvider 获取服务
        /// </summary>
        /// <typeparam name="TService">获取服务的接口，提供返回服务的属性或方法</typeparam>
        /// <param name="services">服务集合。</param>
        /// <param name="lifetime">服务生命周期。</param>
        /// <returns>返回修改后的服务集合以支持链式调用。</returns>
        public static IServiceCollection AddServiceGenerator<TService>(
            this IServiceCollection services,
            Lifetime lifetime = Lifetime.Scoped)
            where TService : class
        {
            var lifetimeDescriptor = lifetime switch
            {
                Lifetime.Singleton => ServiceLifetime.Singleton,
                Lifetime.Scoped => ServiceLifetime.Scoped,
                Lifetime.Transient => ServiceLifetime.Transient,
                _ => ServiceLifetime.Transient,
            };
            var serviceDescriptor = new ServiceDescriptor(typeof(TService),
                sp => new ProxyGenerator().CreateInterfaceProxyWithoutTarget<TService>(sp.GetRequiredService<ServiceGenerateInterceptor>()),
                lifetimeDescriptor);
            services.Add(serviceDescriptor);
            return services;
        }
    }
}