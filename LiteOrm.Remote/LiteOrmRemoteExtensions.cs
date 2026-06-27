using Autofac;
using Autofac.Builder;
using Autofac.Core.Lifetime;
using Autofac.Extensions.DependencyInjection;
using Autofac.Extras.DynamicProxy;
using Castle.DynamicProxy;
using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm服务提供者扩展方法集合
    /// </summary>
    /// <remarks>
    /// LiteOrmServiceProviderExtensions 提供了用于集成 LiteOrm 框架到依赖注入容器的扩展方法。
    /// 它简化了 LiteOrm 框架与 ASP.NET Core 宿主的集成过程。
    /// 
    /// 主要功能包括：
    /// 1. 框架初始化 - 在宿主构建时初始化 LiteOrm 框架
    /// 2. Autofac集成 - 将 Autofac 集成到依赖注入系统
    /// 3. 服务注册 - 注册所有LiteOrm相关的服务
    /// 
    /// 使用示例：
    /// <code>
    /// var builder = Host.CreateDefaultBuilder(args)
    ///     .RegisterLiteOrm()
    ///     .ConfigureServices(services =>
    ///         ...
    ///     );
    /// </code>
    /// </remarks>
    public static class LiteOrmRemoteExtensions
    {
        /// <summary>
        /// 注册LiteOrm框架到主机构建器
        /// </summary>
        /// <param name="hostBuilder">主机构建器</param>
        /// <returns>配置后的主机构建器</returns>
        public static IHostBuilder RegisterLiteOrmRemote(this IHostBuilder hostBuilder)
        {
            return RegisterLiteOrmRemote(hostBuilder, null);
        }

        /// <summary>
        /// 注册LiteOrm框架到主机构建器，并允许配置选项
        /// </summary>
        /// <param name="hostBuilder">主机构建器</param>
        /// <param name="configureOptions">配置选项的回调函数</param>
        /// <returns>配置后的主机构建器</returns>
        public static IHostBuilder RegisterLiteOrmRemote(this IHostBuilder hostBuilder, Action<LiteOrmOptions> configureOptions)
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

            // 应用短名/全名配置到共享的 RemoteServiceNameUtil，影响客户端发出的 ServiceName 格式
            RemoteServiceNameUtil.UseShortTypeName = options.UseShortTypeName;

            // 先在 IServiceCollection 注册 IRemoteServiceTransport（HttpRemoteServiceTransport 或用户自定义实现）
            hostBuilder = hostBuilder.ConfigureServices((hostContext, services) =>
            {
                if (options.Transport is not null)
                {
                    services.AddSingleton<IRemoteServiceTransport>(options.Transport);
                }
                else if (options.RemoteServiceUri is not null)
                {
                    services.AddSingleton<IRemoteServiceTransport>(sp =>
                    {
                        var httpClient = new HttpClient { BaseAddress = options.RemoteServiceUri };
                        options.ConfigureHttpClient?.Invoke(httpClient);
                        return new HttpRemoteServiceTransport(httpClient, options.RemoteServicePath);
                    });
                }
                else
                {
                    throw new InvalidOperationException(
                        "LiteOrm.Remote requires either LiteOrmOptions.Transport or LiteOrmOptions.RemoteServiceUri to be set. " +
                        "Configure one of them in RegisterLiteOrmRemote(opts => { ... }).");
                }
            });

            return hostBuilder.UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureContainer<ContainerBuilder>((builder, containerBuilder) =>
                {
                    try
                    {
                        var logger = options.LoggerFactory?.CreateLogger(nameof(LiteOrmRemoteExtensions));
                        // 使用指定的程序集或默认程序集
                        if (options.Assemblies != null && options.Assemblies.Length > 0)
                        {
                            containerBuilder.RegisterAutoService(logger, options.Assemblies);
                        }
                        else
                        {
                            containerBuilder.RegisterAutoService(logger);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Failed to register LiteOrm services automatically", ex);
                    }
                });
        }

        /// <summary>
        /// LiteOrm配置选项
        /// </summary>
        public class LiteOrmOptions
        {
            /// <summary>
            /// 要扫描的程序集列表
            /// </summary>
            public System.Reflection.Assembly[]? Assemblies { get; set; }

            /// <summary>
            /// 日志工厂，用于记录服务注册过程中的程序集扫描日志（可选）。
            /// 默认为控制台输出，最低级别为 <see cref="ServiceLogLevel.Information"/>。
            /// </summary>
            public ILoggerFactory? LoggerFactory { get; set; }

            /// <summary>
            /// 远程服务的基础地址。设置该值将自动注册基于 HttpClient 的 <see cref="HttpRemoteServiceTransport"/>。
            /// 若同时设置了 <see cref="Transport"/>，则 <see cref="Transport"/> 优先。
            /// </summary>
            public Uri? RemoteServiceUri { get; set; }

            /// <summary>
            /// 相对于 <see cref="RemoteServiceUri"/> 的请求路径，默认为 <c>api/remote/invoke</c>。
            /// 仅在使用默认 <see cref="HttpRemoteServiceTransport"/> 时生效。
            /// </summary>
            public string RemoteServicePath { get; set; } = "api/remote/invoke";

            /// <summary>
            /// 用于配置默认 <see cref="HttpRemoteServiceTransport"/> 内部 HttpClient 的回调（如超时、默认请求头等）。
            /// </summary>
            public Action<HttpClient>? ConfigureHttpClient { get; set; }

            /// <summary>
            /// 自定义的远程调用传输层实例。若设置则优先使用，覆盖 <see cref="RemoteServiceUri"/> 的默认 HTTP 注册。
            /// </summary>
            public IRemoteServiceTransport? Transport { get; set; }

            /// <summary>
            /// 获取或设置是否使用类型短名（不含命名空间）作为远程调用 ServiceName。默认为 true。
            /// 设为 false 时将使用类型全名（含命名空间），适用于服务端存在同名短类型需要消歧、
            /// 或客户端与服务端实体命名空间不一致的场景。
            /// 该设置在 <see cref="RegisterLiteOrmRemote"/> 初始化时应用到 <see cref="RemoteServiceNameUtil.UseShortTypeName"/>。
            /// </summary>
            public bool UseShortTypeName { get; set; } = true;
        }

        /// <summary>
        /// 扫描指定程序集，自动注册标记[AutoRegister]的类型
        /// </summary>
        /// <param name="builder">服务集合</param>
        /// <param name="assemblies">目标程序集（为空则扫描当前域所有程序集）</param>
        /// <returns>服务集合</returns>
        public static ContainerBuilder RegisterAutoService(
            this ContainerBuilder builder,
            params Assembly[] assemblies)
        {
            return RegisterAutoService(builder, null, assemblies);
        }

        /// <summary>
        /// 扫描指定程序集，自动注册标记[AutoRegister]的类型，并通过 <paramref name="logger"/> 输出扫描日志
        /// </summary>
        /// <param name="builder">服务集合</param>
        /// <param name="logger">日志记录器（为 null 时跳过日志输出）</param>
        /// <param name="assemblies">目标程序集（为空则扫描当前域所有程序集）</param>
        /// <returns>服务集合</returns>
        public static ContainerBuilder RegisterAutoService(
            this ContainerBuilder builder,
            ILogger logger,
            params Assembly[] assemblies)
        {
            var assemblyList = new HashSet<Assembly>();

            // 自动加上 LiteOrm 和 LiteOrm.Common 的 Assembly
            assemblyList.Add(typeof(LiteOrmRemoteExtensions).Assembly);
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
                    RegisterTypeWithInterception(builder, t);
                }
                totalRegistered += registrableTypes.Count;
            }

            logger?.LogInformation(
                "LiteOrm service registration complete: scanned {AssemblyCount} assemblies, registered {Total} type(s)",
                assemblyList.Count, totalRegistered);
            return builder;
        }

        /// <summary>
        /// 注册类型并应用拦截配置
        /// </summary>
        /// <param name="builder">容器构建器</param>
        /// <param name="implementationType">要注册的实现类型</param>
        /// <returns>配置后的容器构建器</returns>
        public static ContainerBuilder RegisterTypeWithInterception(this ContainerBuilder builder, Type implementationType)
        {
            if (implementationType.IsGenericTypeDefinition)
            {
                builder.RegisterGeneric(implementationType).AddInterception(implementationType);
            }
            else
            {
                builder.RegisterType(implementationType).AddInterception(implementationType);
            }
            return builder;
        }

        /// <summary>
        /// 为类型注册添加拦截配置
        /// </summary>
        /// <typeparam name="TLimit">限制类型</typeparam>
        /// <typeparam name="TActivatorData">激活器数据类型</typeparam>
        /// <typeparam name="TRegistrationStyle">注册风格类型</typeparam>
        /// <param name="registration">注册构建器</param>
        /// <param name="implementationType">实现类型</param>
        /// <returns>配置后的注册构建器</returns>
        public static IRegistrationBuilder<TLimit, TActivatorData, TRegistrationStyle> AddInterception<TLimit, TActivatorData, TRegistrationStyle>(
           this IRegistrationBuilder<TLimit, TActivatorData, TRegistrationStyle> registration,
           Type implementationType) where TActivatorData : ReflectionActivatorData
        {
            var attribute = implementationType.GetCustomAttribute<AutoRegisterAttribute>(true);
            Lifetime lifetime = attribute?.Lifetime ?? Lifetime.Scoped;
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
                        if (implementationType.GetGenericArguments().Length == serviceType.GenericTypeArguments.Length && serviceType.GenericTypeArguments.All(t => t.DeclaringType == implementationType))
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

            var interceptAttribute = implementationType.GetCustomAttribute<InterceptAttribute>() ??
                                   implementationType.GetInterfaces()
                                       .Select(i => i.GetCustomAttribute<InterceptAttribute>())
                                       .FirstOrDefault(a => a is not null);
            if (interceptAttribute is not null)
            {
                registration.EnableInterfaceInterceptors();
            }
            else
            {
                serviceTypes.Add(implementationType);
            }

            if (attribute?.Key != null)
            {
                foreach (var serviceType in serviceTypes)
                {
                    registration.Keyed(attribute.Key, serviceType);
                }
            }
            else
            {
                registration.As(serviceTypes.ToArray());
            }

            registration.PropertiesAutowired()
            .SetLifetime(lifetime);
            if (attribute.AutoActivate)
                registration.AutoActivate();
            return registration;
        }

        /// <summary>
        /// 设置服务的生命周期
        /// </summary>
        /// <typeparam name="TLimit">限制类型</typeparam>
        /// <typeparam name="TActivatorData">激活器数据类型</typeparam>
        /// <typeparam name="TRegistrationStyle">注册风格类型</typeparam>
        /// <param name="registration">注册构建器</param>
        /// <param name="lifetime">服务生命周期</param>
        /// <returns>配置后的注册构建器</returns>
        public static IRegistrationBuilder<TLimit, TActivatorData, TRegistrationStyle> SetLifetime<TLimit, TActivatorData, TRegistrationStyle>(
           this IRegistrationBuilder<TLimit, TActivatorData, TRegistrationStyle> registration,
           Lifetime lifetime)
        {
            return lifetime switch
            {
                Lifetime.Singleton => registration.SingleInstance(),
                Lifetime.Scoped => registration.InstancePerLifetimeScope(),
                Lifetime.Transient => registration.InstancePerDependency(),
                _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null)
            };
        }

        /// <summary>
        /// 注册服务生成器，将接口获取服务通过动态代理转换为从 <see cref="IServiceProvider"/> 获取远程服务
        /// </summary>
        /// <typeparam name="TService">获取服务的接口，提供返回服务的属性或方法</typeparam>
        /// <param name="services">服务集合。</param>
        /// <param name="lifetime">服务生命周期。</param>
        /// <returns>返回修改后的服务集合以支持链式调用。</returns>
        public static IServiceCollection AddRemoteServiceGenerator<TService>(
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
                sp => new ProxyGenerator().CreateInterfaceProxyWithoutTarget<TService>(sp.GetRequiredService<RemoteServiceGenerateInterceptor>()),
                lifetimeDescriptor);
            services.Add(serviceDescriptor);
            return services;
        }
    }
}
