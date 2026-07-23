using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;
using LiteOrm.Common;
using System.Text;

namespace LiteOrm.Remote.Server
{
    /// <summary>
    /// 服务端注册选项。
    /// </summary>
    public class RemoteServerOptions
    {
        /// <summary>
        /// 远程调用 HTTP 端点路径。默认为 <c>api/remote/invoke</c>，需与客户端 <see cref="HttpRemoteServiceTransport"/> 的 requestUri 一致。
        /// </summary>
        public string InvokePath { get; set; } = "api/remote/invoke";

        /// <summary>
        /// 建立会话的 HTTP 端点路径。默认为 <c>api/remote/connect</c>，
        /// 需与客户端 <see cref="HttpRemoteServiceTransport"/> 的 connectUri 一致。
        /// </summary>
        public string ConnectPath { get; set; } = "api/remote/connect";

        /// <summary>
        /// JSON 序列化选项。默认使用 UnsafeRelaxedJsonEscaping + 大小写不敏感。
        /// </summary>
        public JsonSerializerOptions JsonSerializerOptions { get; set; } = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// 获取或设置服务类型解析器实例。默认为 <see cref="DefaultServiceTypeResolver"/>（全程序集短名扫描）。
        /// 可替换为 <see cref="DelegateRemoteServiceTypeResolver"/>、指定命名空间的 <see cref="DefaultServiceTypeResolver"/> 或自定义实现。
        /// 若需要依赖其他 DI 服务构造解析器，可使用 <see cref="ServiceTypeResolverFactory"/>。
        /// </summary>
        public IRemoteServiceTypeResolver ServiceTypeResolver { get; set; } = new DefaultServiceTypeResolver();

        /// <summary>
        /// 获取或设置自定义服务类型解析器的工厂函数。
        /// 工厂接收 <see cref="IServiceProvider"/>，返回 <see cref="IRemoteServiceTypeResolver"/> 实例，
        /// 可用于在解析器中注入其他 DI 服务。
        /// 优先级高于 <see cref="ServiceTypeResolver"/>：若同时设置，工厂优先生效。
        /// </summary>
        public Func<IServiceProvider, IRemoteServiceTypeResolver>? ServiceTypeResolverFactory { get; set; }

        /// <summary>
        /// 是否自动扫描带 <see cref="ServiceAttribute"/> 特性的接口，通过 <see cref="TypeResolverHelper.Register"/>
        /// 注册到全局名称映射。默认为 true。
        /// <para>
        /// 设置为 true 时，框架会扫描 <see cref="Assemblies"/>（未设置则扫描所有引用程序集）中标记了
        /// <see cref="ServiceAttribute"/>（且 <c>IsService == true</c>）的接口，调用 <see cref="TypeResolverHelper.Register"/>
        /// 注册名称映射。注册后 <see cref="DefaultServiceTypeResolver"/> 可通过自定义注册名优先匹配服务类型。
        /// </para>
        /// <para>
        /// 若 <see cref="ServiceAttribute.Name"/> 非空，使用该名称注册；否则使用 <see cref="TypeResolverHelper.GetName"/> 生成的短名。
        /// </para>
        /// </summary>
        public bool AutoRegisterEntityServices { get; set; } = true;

        /// <summary>
        /// 要扫描的程序集列表（用于 <see cref="AutoRegisterEntityServices"/> 扫描 <see cref="ServiceAttribute"/> 接口）。
        /// 未设置时扫描所有引用的程序集。
        /// </summary>
        public Assembly[]? Assemblies { get; set; }

        /// <summary>
        /// 是否启用 Cookie 身份认证。启用后 Connect 端点通过 <c>HttpContext.SignInAsync</c>
        /// 创建身份票据，后续请求通过 <c>HttpContext.User</c> 恢复用户上下文。
        /// 默认为 true。
        /// <para>
        /// 设置为 false 时，Connect 端点不会尝试创建身份票据，Invoke 端点的
        /// <c>HttpContext.User</c> 由用户自行配置的身份认证中间件填充（如 JWT、自定义方案等）。
        /// </para>
        /// </summary>
        public bool EnableAuthentication { get; set; } = true;
    }

    /// <summary>
    /// LiteOrm.Remote.Server 服务端扩展方法。
    /// </summary>
    public static class LiteOrmRemoteServerExtensions
    {
        /// <summary>
        /// 注册远程服务服务端到 DI 容器。
        /// 默认使用 <see cref="DefaultServiceTypeResolver"/>（全程序集短名扫描）解析服务类型，
        /// 可通过 <see cref="RemoteServerOptions.ServiceTypeResolver"/> 或 <see cref="RemoteServerOptions.ServiceTypeResolverFactory"/> 替换。
        /// 服务类型解析优先级：<see cref="RemoteServerOptions.ServiceTypeResolverFactory"/> &gt; <see cref="RemoteServerOptions.ServiceTypeResolver"/>。
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="configure">配置回调，用于设置端点路径和解析器。</param>
        /// <returns>服务集合。</returns>
        public static IServiceCollection AddRemoteServer(
            this IServiceCollection services,
            Action<RemoteServerOptions>? configure = null)
        {
            var options = new RemoteServerOptions();
            configure?.Invoke(options);

            // 自动扫描带 [Service] 特性的接口，通过 TypeResolverHelper.Register 注册名称映射
            if (options.AutoRegisterEntityServices)
            {
                AutoRegisterServiceTypes(options.Assemblies);
            }

            // 注册 IRemoteServiceTypeResolver：工厂优先，否则使用实例（默认 DefaultServiceTypeResolver）
            if (options.ServiceTypeResolverFactory is not null)
            {
                services.AddSingleton<IRemoteServiceTypeResolver>(options.ServiceTypeResolverFactory);
            }
            else
            {
                services.AddSingleton<IRemoteServiceTypeResolver>(options.ServiceTypeResolver);
            }

            if (options.EnableAuthentication)
            {
                services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddCookie();
            }

            services.AddScoped<RemoteServiceDispatcher>();
            services.AddSingleton(options);
            return services;
        }

        /// <summary>
        /// 映射远程调用 HTTP 端点。接收 POST 请求，读取 JSON 并通过
        /// <see cref="RemoteServiceDispatcher.ParseRequest"/> 解析为 <see cref="RemoteInvocationRequest"/>
        /// （先匹配服务类型，再按方法名查找 <see cref="MethodInfo"/>，最后按参数类型反序列化参数），
        /// 然后调用 <see cref="RemoteServiceDispatcher.InvokeAsync"/>，返回 <see cref="RemoteInvocationResponse"/>。
        /// </summary>
        /// <param name="endpoints">端点路由构建器。</param>
        /// <param name="path">端点路径。为 null 时使用 <see cref="RemoteServerOptions.InvokePath"/>。</param>
        /// <returns>端点路由构建器。</returns>
        public static IEndpointRouteBuilder MapRemoteInvokeEndpoint(
            this IEndpointRouteBuilder endpoints,
            string? path = null)
        {
            var sp = endpoints.ServiceProvider;
            var options = sp.GetRequiredService<RemoteServerOptions>();

            var actualPath = path ?? options.InvokePath;
            if (!actualPath.StartsWith('/'))
                actualPath = "/" + actualPath;

            endpoints.MapPost(actualPath, async (HttpContext context) =>
            {
                var dispatcher = context.RequestServices.GetRequiredService<RemoteServiceDispatcher>();
                var serializerOptions = options.JsonSerializerOptions;

                // 读取请求体 JSON 字符串
                string json;
                try
                {
                    json = await new System.IO.StreamReader(context.Request.Body).ReadToEndAsync(context.RequestAborted)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    var errorResponse = new RemoteInvocationResponse
                    {
                        Success = false,
                        Error = new RemoteErrorInfo
                        {
                            Type = ex.GetType().FullName,
                            Message = $"Failed to read request body: {ex.Message}",
                        }
                    };
                    await JsonSerializer.SerializeAsync(context.Response.Body, errorResponse, serializerOptions, context.RequestAborted)
                        .ConfigureAwait(false);
                    return;
                }

                // 通过 dispatcher.ParseRequest 解析请求：匹配服务 → 查找方法 → 反序列化参数
                RemoteInvocationRequest request;
                try
                {
                    request = dispatcher.ParseRequest(json, serializerOptions);
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    var errorResponse = new RemoteInvocationResponse
                    {
                        Success = false,
                        Error = new RemoteErrorInfo
                        {
                            Type = ex.GetType().FullName,
                            Message = $"Failed to parse request: {ex.Message}",
                        }                        
                    };
                    await JsonSerializer.SerializeAsync(context.Response.Body, errorResponse, serializerOptions, context.RequestAborted)
                        .ConfigureAwait(false);
                    return;
                }

                var response = await dispatcher.InvokeAsync(request, context.RequestAborted).ConfigureAwait(false);
                context.Response.ContentType = "application/json; charset=utf-8";
                await JsonSerializer.SerializeAsync(context.Response.Body, response, serializerOptions, context.RequestAborted)
                    .ConfigureAwait(false);
            });

            // 建立会话端点：接收 POST 携带 RemoteCredentials JSON，
            // 若注册了 IRemoteAuthenticationHandler 则验证凭据，通过后调用 HttpContext.SignInAsync 创建身份票据；
            // 未注册 IRemoteAuthenticationHandler 时直接返回成功（匿名连接）。
            var connectPath = options.ConnectPath;
            if (!connectPath.StartsWith('/'))
                connectPath = "/" + connectPath;

            endpoints.MapPost(connectPath, async (HttpContext context) =>
            {
                var serverOptions = context.RequestServices.GetRequiredService<RemoteServerOptions>();
                var authHandler = context.RequestServices.GetService<IRemoteAuthenticationHandler>();

                // 读取请求体中的凭据（若存在）
                RemoteCredentials? credentials = null;
                using (var reader = new System.IO.StreamReader(context.Request.Body, Encoding.UTF8, false, 4096, true))
                {
                    var body = await reader.ReadToEndAsync().ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        try
                        {
                            credentials = JsonSerializer.Deserialize<RemoteCredentials>(body, serverOptions.JsonSerializerOptions);
                        }
                        catch { }
                    }
                }

                if (authHandler is not null)
                {
                    if (credentials is null || !IsCredentialsValid(credentials))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json; charset=utf-8";
                        await JsonSerializer.SerializeAsync(context.Response.Body,
                            new { Error = GetMissingCredentialsMessage(credentials) },
                            serverOptions.JsonSerializerOptions, context.RequestAborted)
                            .ConfigureAwait(false);
                        return;
                    }

                    var principal = await authHandler.ValidateCredentialsAsync(credentials, context.RequestAborted)
                        .ConfigureAwait(false);
                    if (principal is null)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json; charset=utf-8";
                        await JsonSerializer.SerializeAsync(context.Response.Body,
                            new { Error = "Authentication failed: invalid credentials." },
                            serverOptions.JsonSerializerOptions, context.RequestAborted)
                            .ConfigureAwait(false);
                        return;
                    }

                    // 验证通过，使用返回的 ClaimsPrincipal 创建身份票据
                    await context.SignInAsync(principal)
                        .ConfigureAwait(false);

                    context.Response.StatusCode = StatusCodes.Status200OK;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    await JsonSerializer.SerializeAsync(context.Response.Body,
                        new { Success = true },
                        serverOptions.JsonSerializerOptions, context.RequestAborted)
                        .ConfigureAwait(false);
                }
                else
                {
                    // 未注册 IRemoteAuthenticationHandler —— 匿名连接，直接返回成功
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    await JsonSerializer.SerializeAsync(context.Response.Body,
                        new { Success = true },
                        serverOptions.JsonSerializerOptions, context.RequestAborted)
                        .ConfigureAwait(false);
                }
            });

            return endpoints;
        }

        /// <summary>
        /// 根据 <see cref="AuthGrantType"/> 校验凭据必填字段是否完整。
        /// <para>
        /// <see cref="AuthGrantType.Password"/> 模式要求 <see cref="RemoteCredentials.Username"/> 非空；
        /// <see cref="AuthGrantType.ClientCredentials"/> 模式要求 <see cref="RemoteCredentials.ClientId"/> 非空。
        /// </para>
        /// </summary>
        /// <param name="credentials">待校验的凭据。</param>
        /// <returns>必填字段均已提供返回 true；否则返回 false。</returns>
        private static bool IsCredentialsValid(RemoteCredentials credentials)
        {
            return credentials.GrantType switch
            {
                AuthGrantType.Password => !string.IsNullOrEmpty(credentials.Username),
                AuthGrantType.ClientCredentials => !string.IsNullOrEmpty(credentials.ClientId),
                _ => false,
            };
        }

        /// <summary>
        /// 根据 <see cref="AuthGrantType"/> 生成缺失凭据字段的错误提示。
        /// </summary>
        /// <param name="credentials">凭据实例（可能为 null）。</param>
        /// <returns>错误提示字符串。</returns>
        private static string GetMissingCredentialsMessage(RemoteCredentials? credentials)
        {
            if (credentials is null)
                return "Credentials are required for authenticated connection.";

            return credentials.GrantType switch
            {
                AuthGrantType.Password => "Username is required for Password grant type.",
                AuthGrantType.ClientCredentials => "ClientId is required for ClientCredentials grant type.",
                _ => $"Unsupported grant type: {credentials.GrantType}",
            };
        }

        /// <summary>
        /// 扫描程序集，将标记了 <see cref="ServiceAttribute"/>（且 <c>IsService == true</c>）的接口
        /// 通过 <see cref="TypeResolverHelper.Register"/> 注册到全局名称映射。
        /// <para>
        /// 若 <see cref="ServiceAttribute.Name"/> 非空，使用该名称注册；否则使用 <see cref="TypeResolverHelper.GetName"/> 生成的短名。
        /// 注册后 <see cref="DefaultServiceTypeResolver"/> 的 FindType 优先返回自定义注册的类型，确保客户端与服务端 ServiceName 一致。
        /// </para>
        /// </summary>
        /// <param name="assemblies">要扫描的程序集列表。为 null 时扫描所有引用的程序集。</param>
        private static void AutoRegisterServiceTypes(Assembly[]? assemblies)
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

                    var name = !string.IsNullOrEmpty(attr.Name)
                        ? attr.Name
                        : TypeResolverHelper.GetName(type);
                    TypeResolverHelper.Register(name, type);
                }
            }
        }
    }
}
