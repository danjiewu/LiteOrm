using Castle.DynamicProxy;
using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;


namespace LiteOrm.Remote
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
        public static IHostBuilder RegisterLiteOrmRemote(this IHostBuilder hostBuilder, Action<LiteOrmOptions>? configureOptions)
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

            // IServiceCollection 注册 IRemoteServiceTransport（HttpRemoteServiceTransport 或用户自定义实现）
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
#if NET8_0_OR_GREATER
                        var handler = new SocketsHttpHandler
                        {
                            PooledConnectionLifetime = TimeSpan.FromMinutes(2), // 2分钟后重建连接，重解析DNS
                            UseCookies = true,
                            CookieContainer = new System.Net.CookieContainer(),
                        };
#else
                        var handler = new HttpClientHandler
                        {
                            UseCookies = true,
                            CookieContainer = new System.Net.CookieContainer(),
                        };                        
#endif
                        var httpClient = new HttpClient(handler) { BaseAddress = options.RemoteServiceUri };
                        options.ConfigureHttpClient?.Invoke(httpClient);
                        return new HttpRemoteServiceTransport(httpClient, options.Credentials, options.RemoteServicePath, options.RemoteConnectPath);
                    });
                }
                else
                {
                    throw new InvalidOperationException(
                        "LiteOrm.Remote requires either LiteOrmOptions.Transport or LiteOrmOptions.RemoteServiceUri to be set. " +
                        "Configure one of them in RegisterLiteOrmRemote(opts => { ... }).");
                }

                services.AddSingleton<RemoteServiceInvokeInterceptor>();
                services.AddScoped<RemoteServiceGenerateInterceptor>();
                if (options.AutoRegisterEntityServices)
                {
                    // 1. 扫描程序集，将带 [Service] 特性的接口通过 TypeResolverHelper.Register 注册，
                    //    并注册为远程代理，确保客户端和服务端使用一致的 ServiceName 进行匹配
                    AutoRegisterServiceTypes(services, options.Assemblies);

                    // 2. 注册 4 个开放泛型接口的具体代理实现类
                    //    当解析 IEntityService<T>、IEntityServiceAsync<T>、IEntityViewService<T>、IEntityViewServiceAsync<T> 时，
                    //    DI 容器自动构造对应的代理类（内部持有 RemoteServiceInvokeInterceptor 创建的动态代理）
                    services.AddScoped(typeof(IEntityService<>), typeof(RemoteServiceProxy<>));
                    services.AddScoped(typeof(IEntityServiceAsync<>), typeof(RemoteServiceAsyncProxy<>));
                    services.AddScoped(typeof(IEntityViewService<>), typeof(RemoteViewServiceProxy<>));
                    services.AddScoped(typeof(IEntityViewServiceAsync<>), typeof(RemoteViewServiceAsyncProxy<>));
                }
            });

            return hostBuilder;
        }

        /// <summary>
        /// LiteOrm配置选项
        /// </summary>
        public class LiteOrmOptions
        {
            /// <summary>
            /// 要扫描的程序集列表（用于 <see cref="AutoRegisterEntityServices"/> 扫描带 <see cref="ServiceAttribute"/> 特性的接口）。
            /// 未设置时扫描所有引用的程序集。
            /// </summary>
            public Assembly[]? Assemblies { get; set; }

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
            /// 相对于 <see cref="RemoteServiceUri"/> 的连接路径，默认为 <c>api/remote/connect</c>。
            /// 仅在使用默认 <see cref="HttpRemoteServiceTransport"/> 时生效。
            /// </summary>
            public string RemoteConnectPath { get; set; } = "api/remote/connect";

            /// <summary>
            /// 用于配置默认 <see cref="HttpRemoteServiceTransport"/> 内部 HttpClient 的回调（如超时、默认请求头等）。
            /// </summary>
            public Action<HttpClient>? ConfigureHttpClient { get; set; }

            /// <summary>
            /// 远程调用凭据（可选）。设置后将在首次调用前通过 Connect 端点建立已认证会话。
            /// <para>
            /// 根据 <see cref="RemoteCredentials.GrantType"/> 区分授权模式：
            /// <list type="bullet">
            /// <item><see cref="AuthGrantType.Password"/>（默认）：使用 Username/Password 认证</item>
            /// <item><see cref="AuthGrantType.ClientCredentials"/>：使用 ClientId/ClientSecret 认证</item>
            /// </list>
            /// </para>
            /// </summary>
            public RemoteCredentials? Credentials { get; set; }

            /// <summary>
            /// 自定义的远程调用传输层实例。若设置则优先使用，覆盖 <see cref="RemoteServiceUri"/> 的默认 HTTP 注册。
            /// </summary>
            public IRemoteServiceTransport? Transport { get; set; }

            /// <summary>
            /// 是否自动注册所有实体服务为远程代理。默认为 true。
            /// <para>
            /// 设置为 true 时，将完成以下注册：
            /// <list type="bullet">
            /// <item>扫描程序集，将标记了 <see cref="ServiceAttribute"/>（且 <c>IsService == true</c>）的接口：
            /// 通过 <see cref="TypeResolverHelper.Register"/> 注册名称映射，同时注册为远程代理</item>
            /// <item>注册 4 个开放泛型接口的具体代理实现类：
            /// <c>IEntityService&lt;T&gt;</c> → <see cref="RemoteServiceProxy{T}"/>、
            /// <c>IEntityServiceAsync&lt;T&gt;</c> → <see cref="RemoteServiceAsyncProxy{T}"/>、
            /// <c>IEntityViewService&lt;T&gt;</c> → <see cref="RemoteViewServiceProxy{T}"/>、
            /// <c>IEntityViewServiceAsync&lt;T&gt;</c> → <see cref="RemoteViewServiceAsyncProxy{T}"/></item>
            /// </list>
            /// </para>
            /// <para>
            /// 启用后无需手动调用 <see cref="AddRemoteService{TService}"/> 逐个注册，
            /// 任何带 <c>[Service]</c> 特性的服务接口均可直接从 DI 容器解析。
            /// </para>
            /// </summary>
            public bool AutoRegisterEntityServices { get; set; } = true;
        }

        /// <summary>
        /// 手动将单个服务接口注册为远程代理。
        /// </summary>
        /// <typeparam name="TService">远程服务接口类型。</typeparam>
        /// <param name="services">服务集合。</param>
        /// <param name="lifetime">服务生命周期，默认为 <see cref="Lifetime.Scoped"/>。</param>
        /// <returns>返回修改后的服务集合以支持链式调用。</returns>
        /// <remarks>
        /// 创建无目标对象的接口代理，所有方法调用由 <see cref="RemoteServiceInvokeInterceptor"/>
        /// 拦截并通过 <see cref="IRemoteServiceTransport"/> 转发到远程服务端。
        /// <para>
        /// 与 <see cref="AddRemoteServiceGenerator{TService}"/> 不同，本方法注册的是单个业务服务接口本身
        /// （如 <c>IUserService</c>），解析时直接返回可调用远程服务的代理实例；
        /// 而 <see cref="AddRemoteServiceGenerator{TService}"/> 注册的是返回服务的工厂接口
        /// （如 <c>RemoteServiceFactory</c>），访问其属性时由
        /// <see cref="RemoteServiceGenerateInterceptor"/> 从 DI 容器解析对应服务。
        /// </para>
        /// <para>
        /// 使用示例：
        /// <code>
        /// services.AddRemoteService&lt;IUserService&gt;()
        ///         .AddRemoteService&lt;ISalesService&gt;();
        /// var userService = sp.GetRequiredService&lt;IUserService&gt;();
        /// await userService.GetByUserNameAsync("alice");
        /// </code>
        /// </para>
        /// </remarks>
        public static IServiceCollection AddRemoteService<TService>(
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
                sp => new ProxyGenerator().CreateInterfaceProxyWithoutTarget<TService>(
                    sp.GetRequiredService<RemoteServiceInvokeInterceptor>().ToInterceptor()),
                lifetimeDescriptor);
            services.Add(serviceDescriptor);
            return services;
        }

        /// <summary>
        /// 注册服务生成器，将接口获取服务通过动态代理转换为从 <see cref="IServiceProvider"/> 获取远程服务。
        /// 同时自动扫描工厂接口所有属性与方法的返回类型，将其中未注册的接口类型自动注册为远程代理。
        /// </summary>
        /// <typeparam name="TService">获取服务的工厂类，提供返回服务的属性或方法</typeparam>
        /// <param name="services">服务集合。</param>
        /// <param name="lifetime">服务生命周期。</param>
        /// <returns>返回修改后的服务集合以支持链式调用。</returns>
        /// <remarks>
        /// <para>
        /// 工厂接口（如 <c>RemoteServiceFactory</c>）的每个属性/方法返回一个业务服务接口，
        /// <see cref="RemoteServiceGenerateInterceptor"/> 访问属性时通过
        /// <c>ServiceProvider.GetRequiredService(返回类型)</c> 解析对应服务。
        /// </para>
        /// <para>
        /// 本方法自动扫描 <typeparamref name="TService"/> 的所有属性与方法返回类型，
        /// 将满足以下条件的类型自动注册为远程代理（通过 <see cref="RemoteServiceInvokeInterceptor"/> 转发）：
        /// 1. 为接口类型；
        /// 2. 命名空间不属于 <c>System</c>；
        /// 3. 未在 DI 容器中注册（避免覆盖手动注册）。
        /// </para>
        /// <para>
        /// 使用后无需再手动调用 <see cref="AddRemoteService{TService}"/> 注册各业务接口：
        /// <code>
        /// services.AddRemoteServiceGenerator&lt;RemoteServiceFactory&gt;();
        /// // IUserService、ISalesService 等已自动注册为远程代理
        /// </code>
        /// </para>
        /// </remarks>
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

            // 自动扫描工厂接口的属性与方法返回类型，注册为远程代理
            AutoRegisterRemoteServices(services, typeof(TService), lifetimeDescriptor);

            return services;
        }

        /// <summary>
        /// 扫描工厂接口的所有属性与方法返回类型，将未注册的接口类型自动注册为远程代理。
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="factoryType">工厂接口类型。</param>
        /// <param name="lifetimeDescriptor">服务生命周期。</param>
        private static void AutoRegisterRemoteServices(
            IServiceCollection services,
            Type factoryType,
            ServiceLifetime lifetimeDescriptor)
        {
            var serviceTypes = new HashSet<Type>();

            // 从属性收集返回类型
            foreach (var prop in factoryType.GetProperties())
            {
                CollectRemoteServiceType(prop.PropertyType, serviceTypes);
            }

            // 从方法收集返回类型（排除返回 void 的方法）
            foreach (var method in factoryType.GetMethods())
            {
                if (method.ReturnType != typeof(void))
                    CollectRemoteServiceType(method.ReturnType, serviceTypes);
            }

            // 注册未注册的服务接口为远程代理
            foreach (var serviceType in serviceTypes)
            {
                if (services.Any(d => d.ServiceType == serviceType))
                    continue;

                var capturedType = serviceType;
                services.Add(new ServiceDescriptor(serviceType,
                    sp => new ProxyGenerator().CreateInterfaceProxyWithoutTarget(
                        capturedType,
                        sp.GetRequiredService<RemoteServiceInvokeInterceptor>().ToInterceptor()),
                    lifetimeDescriptor));
            }
        }

        /// <summary>
        /// 判断类型是否为可注册的远程服务接口，若满足条件则加入集合。
        /// 条件：为接口、命名空间不属于 System。
        /// </summary>
        private static void CollectRemoteServiceType(Type type, HashSet<Type> serviceTypes)
        {
            if (type is null) return;
            if (!type.IsInterface) return;
            var ns = type.Namespace ?? string.Empty;
            if (ns == "System" || ns.StartsWith("System.")) return;
            serviceTypes.Add(type);
        }

        /// <summary>
        /// 扫描程序集，将标记了 <see cref="ServiceAttribute"/>（且 <c>IsService == true</c>）的接口：
        /// <list type="number">
        /// <item>通过 <see cref="TypeResolverHelper.Register"/> 注册到全局名称映射，确保客户端与服务端使用一致的 ServiceName</item>
        /// <item>注册为远程代理（Castle DynamicProxy），所有方法调用由 <see cref="RemoteServiceInvokeInterceptor"/> 拦截并转发到远程服务端</item>
        /// </list>
        /// <para>
        /// 若 <see cref="ServiceAttribute.Name"/> 非空，使用该名称注册；否则使用 <see cref="TypeResolverHelper.GetName"/> 生成的短名。
        /// 已注册的服务接口不会被覆盖。
        /// </para>
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="assemblies">要扫描的程序集列表。为 null 时扫描所有引用的程序集。</param>
        private static void AutoRegisterServiceTypes(IServiceCollection services, Assembly[]? assemblies)
        {
            var scanAssemblies = assemblies ?? AssemblyAnalyzer.GetAllReferencedAssemblies().ToArray();

            foreach (var assembly in scanAssemblies)
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (!type.IsInterface || type.IsGenericTypeDefinition)
                        continue;

                    var attr = type.GetCustomAttribute<ServiceAttribute>(true);
                    if (attr is null || !attr.IsService)
                        continue;

                    // 1. 注册名称映射
                    var name = !string.IsNullOrEmpty(attr.Name)
                        ? attr.Name
                        : TypeResolverHelper.GetName(type);
                    TypeResolverHelper.Register(name, type);

                    // 2. 已注册的服务不覆盖
                    if (services.Any(d => d.ServiceType == type))
                        continue;

                    // 3. 注册远程代理
                    var capturedType = type;
                    services.Add(new ServiceDescriptor(type,
                        sp => new ProxyGenerator().CreateInterfaceProxyWithoutTarget(
                            capturedType,
                            sp.GetRequiredService<RemoteServiceInvokeInterceptor>().ToInterceptor()),
                        ServiceLifetime.Scoped));
                }
            }
        }
    }
}
