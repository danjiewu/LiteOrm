using Castle.DynamicProxy;
using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// 1. 框架初始化 - 在宿主构建时初始化 LiteOrm 远程服务框架
    /// 2. 服务注册 - 注册所有 LiteOrm Remote 相关的服务
    /// 
    /// 使用示例：
    /// <code>
    /// var builder = Host.CreateDefaultBuilder(args)
    ///     .RegisterLiteOrm()
    ///     .RegisterLiteOrmRemote()
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
        /// 注册LiteOrm框架到主机构建器，并允许配置选项。
        /// <para>
        /// 通过 <paramref name="configureOptions"/> 回调构建默认选项；若要使用注入工厂方式配置，
        /// 可在 <c>ConfigureServices</c> 中调用 <see cref="AddRemoteOptions"/> 注册 <see cref="LiteOrmOptions"/> 工厂，
        /// 运行时以工厂构造的选项为准（覆盖 <paramref name="configureOptions"/>）。
        /// </para>
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

            return hostBuilder.ConfigureServices((hostContext, services) =>
            {
                // 1. 将默认选项注册进 DI：若通过 AddRemoteOptions 注册了工厂，用户工厂优先（运行时覆盖 configureOptions）。
                services.TryAddSingleton(options);

                // 2. 注册 ICredentialsResolver：工厂优先，其次实例；未提供时解析为 null 进行匿名连接。
                //    延迟从 DI 解析选项，以支持 AddRemoteOptions 工厂在运行时提供参数。
                //    自定义凭据解析器通常依赖 IHttpContextAccessor，
                //    调用方需自行调用 services.AddHttpContextAccessor() 注册（避免在客户端库中硬依赖 ASP.NET Core）。
                services.TryAddSingleton<ICredentialsResolver>(sp =>
                {
                    var opts = sp.GetRequiredService<LiteOrmOptions>();
                    if (opts.CredentialsResolverFactory is not null)
                        return opts.CredentialsResolverFactory(sp);
                    return opts.CredentialsResolver!;
                });

                // 3. 注册 IRemoteServiceTransport（HttpRemoteServiceTransport 或用户自定义实现），统一延迟解析选项。
                services.TryAddSingleton<IRemoteServiceTransport>(sp =>
                {
                    var opts = sp.GetRequiredService<LiteOrmOptions>();

                    // 用户自定义传输层实例：原样注册为 Singleton
                    if (opts.Transport is not null)
                        return opts.Transport;

                    // 默认 HttpRemoteServiceTransport，统一注册为 Singleton。
                    // 多用户会话隔离由 ICredentialsResolver 实现自行管理（如基于 HttpContext 的动态票据解析），
                    // 不依赖每个 Scope 一份 HttpClient，因此 Singleton 即可。
                    if (opts.RemoteServiceUri is not null)
                    {
#if NET8_0_OR_GREATER
                        var handler = new SocketsHttpHandler
                        {
                            PooledConnectionLifetime = TimeSpan.FromMinutes(2), // 2分钟后重建连接，重解析DNS
                            UseCookies = false, // 禁用自动 Cookie 处理，由 ICredentialsResolver 管理票据，避免多用户串号
                        };
#else
                        var handler = new HttpClientHandler
                        {
                            UseCookies = false, // 禁用自动 Cookie 处理，由 ICredentialsResolver 管理票据，避免多用户串号
                        };
#endif
                        var httpClient = new HttpClient(handler) { BaseAddress = opts.RemoteServiceUri };
                        opts.ConfigureHttpClient?.Invoke(httpClient);

                        // 解析 ICredentialsResolver：若 DI 中已注册则使用，否则匿名连接
                        var resolver = sp.GetService<ICredentialsResolver>();
                        var transport = new HttpRemoteServiceTransport(httpClient, resolver, opts.RemoteServicePath);

                        // 记录请求/响应 JSON 报文（排障用），从 DI 解析 ILoggerFactory 提供日志
                        if (opts.LogJsonPayloads)
                        {
                            var loggerFactory = sp.GetService<ILoggerFactory>();
                            transport.ConfigureJsonLogging(
                                loggerFactory?.CreateLogger<HttpRemoteServiceTransport>(),
                                true);
                        }
                        return transport;
                    }

                    throw new InvalidOperationException(
                        "LiteOrm.Remote requires either LiteOrmOptions.Transport or LiteOrmOptions.RemoteServiceUri to be set. " +
                        "Configure one of them in RegisterLiteOrmRemote(opts => { ... }) or register a factory via AddRemoteOptions(...).");
                });

                services.AddSingleton<RemoteServiceInvokeInterceptor>();
                services.AddScoped<ServiceGenerateInterceptor>();
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
        }

        /// <summary>
        /// 以工厂方式注册 <see cref="LiteOrmOptions"/> 到 DI 容器，便于在配置服务(<see cref="IConfiguration"/>)或其他 DI 服务基础上构造参数。
        /// <para>
        /// 工厂接收 <see cref="IServiceProvider"/>，可从其中解析 <see cref="IConfiguration"/> 等依赖后返回选项。
        /// 若同时调用了 <see cref="RegisterLiteOrmRemote"/>，则运行时以本工厂构造的选项为准（覆盖 configureOptions）。
        /// </para>
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="optionsFactory">选项工厂，接收 <see cref="IServiceProvider"/>，返回 <see cref="LiteOrmOptions"/>。</param>
        /// <returns>返回修改后的服务集合以支持链式调用。</returns>
        /// <example>
        /// <code>
        /// builder.ConfigureServices(services =>
        ///     services.AddRemoteOptions(sp =>
        ///     {
        ///         var config = sp.GetRequiredService&lt;IConfiguration&gt;();
        ///         return new LiteOrmOptions
        ///         {
        ///             RemoteServiceUri = new Uri(config["MyRemote:Uri"]),
        ///             RemoteServicePath = config["MyRemote:Path"],
        ///         };
        ///     }));
        /// </code>
        /// </example>
        public static IServiceCollection AddRemoteOptions(
            this IServiceCollection services,
            Func<IServiceProvider, LiteOrmOptions> optionsFactory)
        {
            if (optionsFactory is null)
                throw new ArgumentNullException(nameof(optionsFactory));
            services.AddSingleton(optionsFactory);
            return services;
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
            /// 是否在日志中记录远程调用的请求与响应 JSON 报文。默认为 false。
            /// <para>
            /// 设置为 true 时，默认的 <see cref="JsonRemoteServiceTransport"/> 在发送请求前与接收响应后，
            /// 会以 Debug 级别记录请求 JSON 与响应 JSON（需关闭相应日志提供程序的低级别过滤以观察到输出）。
            /// </para>
            /// </summary>
            public bool LogJsonPayloads { get; set; }

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
            /// 相对于 <see cref="RemoteServiceUri"/> 的登录（SignIn）路径，默认为 <c>api/remote/signin</c>。
            /// 仅在使用框架内置 <see cref="StaticCredentialsResolver"/> 时生效；自定义 <see cref="ICredentialsResolver"/> 自行管理路径。
            /// </summary>
            public string RemoteSignInPath { get; set; } = "api/remote/signin";

            /// <summary>
            /// 用于配置默认 <see cref="HttpRemoteServiceTransport"/> 内部 HttpClient 的回调（如超时、默认请求头等）。
            /// </summary>
            public Action<HttpClient>? ConfigureHttpClient { get; set; }

            /// <summary>
            /// 凭据解析器实例。设置后将在每次 <c>InvokeAsync</c> 时通过该解析器获取身份认证票据并写入 HTTP 请求头。
            /// <para>
            /// 若同时设置了 <see cref="CredentialsResolverFactory"/>，则工厂优先。
            /// </para>
            /// <para>
            /// 框架内置 <see cref="StaticCredentialsResolver"/>：启动时调用 <see cref="StaticCredentialsResolver.LoginAsync"/>
            /// 一次，登录成功后服务端返回的票据被保存到本地，后续所有 <c>InvokeAsync</c> 复用该票据。
            /// 多用户场景请实现自定义的 <see cref="ICredentialsResolver"/>（如从 <c>IHttpContextAccessor</c> 解析当前用户票据）。
            /// </para>
            /// <para>
            /// 不设置（<c>null</c>）表示匿名连接，<see cref="HttpRemoteServiceTransport"/> 不写入票据请求头。
            /// </para>
            /// </summary>
            public ICredentialsResolver? CredentialsResolver { get; set; }

            /// <summary>
            /// 凭据解析器工厂。接收 <see cref="IServiceProvider"/>，返回 <see cref="ICredentialsResolver"/> 实例。
            /// 优先级高于 <see cref="CredentialsResolver"/>，便于在解析器中注入其他 DI 服务。
            /// </summary>
            public Func<IServiceProvider, ICredentialsResolver>? CredentialsResolverFactory { get; set; }

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
        /// <param name="lifetime">服务生命周期，默认为 <see cref="ServiceLifetime.Scoped"/>。</param>
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
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TService : class
        {
            var serviceDescriptor = new ServiceDescriptor(typeof(TService),
                RemoteProxyGenerator.CreateRemoteServiceProxy<TService>,
                lifetime);
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
        /// <see cref="ServiceGenerateInterceptor"/> 访问属性时通过
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
        public static IServiceCollection AddRemoteServiceFactory<TService>(
            this IServiceCollection services,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TService : class
        {
            var serviceDescriptor = new ServiceDescriptor(typeof(TService),
                sp => RemoteProxyGenerator.CreateInterfaceProxy<TService>(sp.GetRequiredService<ServiceGenerateInterceptor>()),
                lifetime);
            services.Add(serviceDescriptor);

            // 自动扫描工厂接口的属性与方法返回类型，注册为远程代理
            AutoRegisterRemoteServices(services, typeof(TService), lifetime);

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
                    sp => RemoteProxyGenerator.CreateRemoteServiceProxy(capturedType, sp),
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
                        ? attr.Name!
                        : TypeResolverHelper.GetName(type);
                    TypeResolverHelper.Register(name, type);

                    // 2. 已注册的服务不覆盖
                    if (services.Any(d => d.ServiceType == type))
                        continue;

                    // 3. 注册远程代理
                    var capturedType = type;
                    services.Add(new ServiceDescriptor(type,
                        sp => RemoteProxyGenerator.CreateRemoteServiceProxy(capturedType, sp),
                        ServiceLifetime.Scoped));
                }
            }
        }
    }
}
