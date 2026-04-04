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
    /// LiteOrm 服务集成扩展。
    /// </summary>
    public static class LiteOrmServiceExtensions
    {
        /// <summary>
        /// 注册 LiteOrm 到主机构建器。
        /// </summary>
        public static IHostBuilder RegisterLiteOrm(this IHostBuilder hostBuilder)
        {
            return RegisterLiteOrm(hostBuilder, null);
        }

        /// <summary>
        /// 注册 LiteOrm 到主机构建器，并允许配置选项。
        /// </summary>
        public static IHostBuilder RegisterLiteOrm(this IHostBuilder hostBuilder, Action<LiteOrmOptions> configureOptions)
        {
            if (hostBuilder is null) throw new ArgumentNullException(nameof(hostBuilder));

            var options = new LiteOrmOptions();
            try
            {
                configureOptions?.Invoke(options);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize LiteOrm options", ex);
            }

            return hostBuilder.UseServiceProviderFactory(new LiteOrmServiceProviderFactory(options));
        }

        /// <summary>
        /// LiteOrm 配置选项。
        /// </summary>
        public class LiteOrmOptions
        {
            internal Dictionary<string, SqlBuilder> SqlBuilders { get; } = new Dictionary<string, SqlBuilder>();

            internal Dictionary<Type, SqlBuilder> SqlBuildersByType { get; } = new Dictionary<Type, SqlBuilder>();

            public Assembly[] Assemblies { get; set; }

            public ILoggerFactory LoggerFactory { get; set; }

            public void RegisterSqlBuilder(string dataSourceName, SqlBuilder sqlBuilder)
            {
                SqlBuilders[dataSourceName] = sqlBuilder;
            }

            public void RegisterSqlBuilder(Type providerType, SqlBuilder sqlBuilder)
            {
                SqlBuildersByType[providerType] = sqlBuilder;
            }
        }

        /// <summary>
        /// 扫描指定程序集，自动注册标记 <see cref="AutoRegisterAttribute"/> 的类型。
        /// </summary>
        internal static IServiceCollection RegisterAutoService(this IServiceCollection services, LiteOrmRuntimeRegistry runtimeRegistry, params Assembly[] assemblies)
        {
            return RegisterAutoService(services, runtimeRegistry, null, assemblies);
        }

        /// <summary>
        /// 扫描指定程序集，自动注册标记 <see cref="AutoRegisterAttribute"/> 的类型，并输出扫描日志。
        /// </summary>
        internal static IServiceCollection RegisterAutoService(this IServiceCollection services, LiteOrmRuntimeRegistry runtimeRegistry, ILogger logger, params Assembly[] assemblies)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (runtimeRegistry is null) throw new ArgumentNullException(nameof(runtimeRegistry));

            var assemblyList = new HashSet<Assembly>
            {
                typeof(LiteOrmServiceExtensions).Assembly,
                typeof(AutoRegisterAttribute).Assembly
            };

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

                foreach (var implementationType in registrableTypes)
                {
                    var attr = implementationType.GetCustomAttribute<AutoRegisterAttribute>(true);
                    logger?.LogDebug(
                        "Registering {Kind} service '{Type}' [Lifetime={Lifetime}, AutoActivate={AutoActivate}]",
                        implementationType.IsGenericTypeDefinition ? "generic" : "regular",
                        implementationType.FullName,
                        attr?.Lifetime ?? Lifetime.Scoped,
                        attr?.AutoActivate ?? false);

                    RegisterType(services, runtimeRegistry, implementationType);
                }

                totalRegistered += registrableTypes.Count;
            }

            logger?.LogInformation(
                "LiteOrm service registration complete: scanned {AssemblyCount} assemblies, registered {Total} type(s)",
                assemblyList.Count, totalRegistered);

            return services;
        }

        /// <summary>
        /// 注册服务生成器，将接口调用转发到当前作用域的服务提供者。
        /// </summary>
        public static IServiceCollection AddServiceGenerator<TService>(this IServiceCollection services, Lifetime lifetime = Lifetime.Scoped)
            where TService : class
        {
            if (services is null) throw new ArgumentNullException(nameof(services));

            var serviceDescriptor = new ServiceDescriptor(
                typeof(TService),
                sp => new ProxyGenerator().CreateInterfaceProxyWithoutTarget<TService>(new ServiceGenerateInterceptor(sp)),
                ToServiceLifetime(lifetime));

            services.Add(serviceDescriptor);
            return services;
        }

        private static void RegisterType(IServiceCollection services, LiteOrmRuntimeRegistry runtimeRegistry, Type implementationType)
        {
            var attribute = implementationType.GetCustomAttribute<AutoRegisterAttribute>(true);
            var lifetime = ToServiceLifetime(attribute?.Lifetime ?? Lifetime.Scoped);
            var serviceTypes = GetServiceTypes(implementationType, attribute);
            var interceptAttribute = implementationType.GetCustomAttribute<InterceptAttribute>(true)
                ?? implementationType.GetInterfaces().Select(i => i.GetCustomAttribute<InterceptAttribute>(true)).FirstOrDefault(a => a != null);

            AddSelfRegistration(services, implementationType, lifetime);

            if (attribute?.AutoActivate == true)
            {
                runtimeRegistry.AutoActivateTypes.Add(implementationType);
            }

            if (attribute?.Key != null)
            {
                foreach (var serviceType in serviceTypes)
                {
                    runtimeRegistry.KeyedServices.Add(serviceType, attribute.Key, implementationType);
                }
            }

            if (interceptAttribute != null)
            {
                RegisterInterceptedServices(services, runtimeRegistry, implementationType, serviceTypes, interceptAttribute, lifetime);
                return;
            }

            if (!serviceTypes.Contains(implementationType))
            {
                serviceTypes.Add(implementationType);
            }

            foreach (var serviceType in serviceTypes)
            {
                AddServiceMapping(services, serviceType, implementationType, lifetime);
            }
        }

        private static void RegisterInterceptedServices(
            IServiceCollection services,
            LiteOrmRuntimeRegistry runtimeRegistry,
            Type implementationType,
            List<Type> serviceTypes,
            InterceptAttribute interceptAttribute,
            ServiceLifetime lifetime)
        {
            foreach (var serviceType in serviceTypes)
            {
                runtimeRegistry.AddInterceptedService(new LiteOrmInterceptedServiceRegistration
                {
                    ServiceType = serviceType,
                    ImplementationType = implementationType,
                    InterceptorTypes = interceptAttribute.InterceptorTypes,
                    Lifetime = lifetime
                });

                if (!serviceType.IsGenericTypeDefinition)
                {
                    services.Add(new ServiceDescriptor(
                        serviceType,
                        sp => CreateInterceptedService(sp, serviceType, implementationType, interceptAttribute.InterceptorTypes),
                        lifetime));
                }
                else
                {
                    AddOpenGenericFallbackMapping(services, serviceType, implementationType, lifetime);
                }
            }
        }

        private static object CreateInterceptedService(IServiceProvider serviceProvider, Type serviceType, Type implementationType, Type[] interceptorTypes)
        {
            var runtimeRegistry = serviceProvider.GetRequiredService<LiteOrmRuntimeRegistry>();
            var closedImplementationType = implementationType;
            if (closedImplementationType.IsGenericTypeDefinition && serviceType.IsConstructedGenericType)
            {
                closedImplementationType = closedImplementationType.MakeGenericType(serviceType.GenericTypeArguments);
            }

            var target = serviceProvider.GetRequiredService(closedImplementationType);
            var interceptors = interceptorTypes.Select(interceptorType =>
            {
                var interceptor = serviceProvider.GetRequiredService(interceptorType);
                if (interceptor is IAsyncInterceptor asyncInterceptor)
                    return asyncInterceptor.ToInterceptor();
                return (IInterceptor)interceptor;
            }).ToArray();

            return runtimeRegistry.ProxyGenerator.CreateInterfaceProxyWithTarget(serviceType, target, interceptors);
        }

        private static List<Type> GetServiceTypes(Type implementationType, AutoRegisterAttribute attribute)
        {
            var serviceTypes = new List<Type>();

            if (attribute?.ServiceTypes is not null && attribute.ServiceTypes.Any())
            {
                serviceTypes.AddRange(attribute.ServiceTypes);
                return serviceTypes;
            }

            foreach (var serviceType in implementationType.GetInterfaces()
                .Where(i => !string.IsNullOrEmpty(i.Namespace)
                         && !i.Namespace.StartsWith("System.", StringComparison.Ordinal)
                         && i.Namespace != "System"
                         && (i.GetCustomAttribute<AutoRegisterAttribute>(true)?.Enabled ?? true)))
            {
                if (implementationType.IsGenericTypeDefinition && serviceType.IsGenericType)
                {
                    if (implementationType.GetGenericArguments().Length == serviceType.GenericTypeArguments.Length &&
                        serviceType.GenericTypeArguments.All(t => t.DeclaringType == implementationType))
                    {
                        serviceTypes.Add(serviceType.GetGenericTypeDefinition());
                    }
                }
                else if (!implementationType.IsGenericTypeDefinition)
                {
                    serviceTypes.Add(serviceType);
                }
            }

            return serviceTypes;
        }

        private static void AddSelfRegistration(IServiceCollection services, Type implementationType, ServiceLifetime lifetime)
        {
            if (implementationType.IsGenericTypeDefinition)
            {
                services.Add(new ServiceDescriptor(implementationType, implementationType, lifetime));
            }
            else
            {
                services.Add(new ServiceDescriptor(implementationType, implementationType, lifetime));
            }
        }

        private static void AddServiceMapping(IServiceCollection services, Type serviceType, Type implementationType, ServiceLifetime lifetime)
        {
            if (serviceType == implementationType)
                return;

            if (serviceType.IsGenericTypeDefinition && implementationType.IsGenericTypeDefinition)
            {
                services.Add(new ServiceDescriptor(serviceType, implementationType, lifetime));
                return;
            }

            services.Add(new ServiceDescriptor(serviceType, sp => sp.GetRequiredService(implementationType), lifetime));
        }

        private static void AddOpenGenericFallbackMapping(IServiceCollection services, Type serviceType, Type implementationType, ServiceLifetime lifetime)
        {
            if (serviceType.IsGenericTypeDefinition && implementationType.IsGenericTypeDefinition)
            {
                services.Add(new ServiceDescriptor(serviceType, implementationType, lifetime));
            }
        }

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
    }
}
