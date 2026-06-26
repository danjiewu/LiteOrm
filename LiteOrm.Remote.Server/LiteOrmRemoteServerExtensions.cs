using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Service
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
        /// JSON 序列化选项。默认使用 UnsafeRelaxedJsonEscaping + 大小写不敏感。
        /// </summary>
        public JsonSerializerOptions JsonSerializerOptions { get; set; } = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// 获取已注册的服务注册表，可通过它注册服务接口类型。
        /// </summary>
        public RemoteServiceRegistry Registry { get; } = new RemoteServiceRegistry();
    }

    /// <summary>
    /// LiteOrm.Remote.Server 服务端扩展方法。
    /// </summary>
    public static class LiteOrmRemoteServerExtensions
    {
        /// <summary>
        /// 注册远程服务服务端到 DI 容器。
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="configure">配置回调，用于设置端点路径和注册服务接口类型。</param>
        /// <returns>服务集合。</returns>
        public static IServiceCollection AddRemoteService(
            this IServiceCollection services,
            Action<RemoteServerOptions>? configure = null)
        {
            var options = new RemoteServerOptions();
            configure?.Invoke(options);

            services.AddSingleton(options.Registry);
            services.AddScoped<RemoteServiceDispatcher>();
            services.AddSingleton(options);
            return services;
        }

        /// <summary>
        /// 注册远程服务服务端并扫描指定程序集中的服务接口类型。
        /// 扫描规则：接口标记了 <see cref="ServiceAttribute"/> 且 <see cref="ServiceAttribute.IsService"/> 为 true。
        /// 支持开放泛型接口（如 <c>IEntityService&lt;&gt;</c>）的注册，查找时由 <see cref="RemoteServiceRegistry"/> 动态构造闭合类型。
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="assemblies">要扫描的程序集。</param>
        /// <param name="configure">额外的配置回调。</param>
        /// <returns>服务集合。</returns>
        public static IServiceCollection AddRemoteServiceServer(
            this IServiceCollection services,
            System.Reflection.Assembly[] assemblies,
            Action<RemoteServerOptions>? configure = null)
        {
            return services.AddRemoteService(opts =>
            {
                foreach (var assembly in assemblies)
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!type.IsInterface) continue;

                        // 标记了 [Service] 且 IsService = true（支持开放泛型定义）
                        var attr = type.GetCustomAttribute<ServiceAttribute>(true);
                        if (attr != null && attr.IsService)
                            opts.Registry.Register(type);
                    }
                }
                configure?.Invoke(opts);
            });
        }

        /// <summary>
        /// 映射远程调用 HTTP 端点。接收 POST 请求，反序列化 <see cref="RemoteInvocationRequest"/>，
        /// 调用 <see cref="RemoteServiceDispatcher"/>，返回 <see cref="RemoteInvocationResponse"/>。
        /// </summary>
        /// <param name="endpoints">端点路由构建器。</param>
        /// <param name="path">端点路径。为 null 时使用 <see cref="RemoteServerOptions.InvokePath"/>。</param>
        /// <returns>端点路由构建器。</returns>
        public static IEndpointRouteBuilder MapRemoteInvokeEndpoint(
            this IEndpointRouteBuilder endpoints,
            string? path = null)
        {
            var actualPath = path ?? "api/remote/invoke";
            if (!actualPath.StartsWith('/'))
                actualPath = "/" + actualPath;

            endpoints.MapPost(actualPath, async (HttpContext context) =>
            {
                var options = context.RequestServices.GetRequiredService<RemoteServerOptions>();
                var dispatcher = context.RequestServices.GetRequiredService<RemoteServiceDispatcher>();
                var serializerOptions = options.JsonSerializerOptions;

                RemoteInvocationRequest? request;
                try
                {
                    request = await JsonSerializer
                        .DeserializeAsync<RemoteInvocationRequest>(context.Request.Body, serializerOptions, context.RequestAborted)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    var errorResponse = new RemoteInvocationResponse
                    {
                        Success = false,
                        ErrorType = ex.GetType().FullName,
                        ErrorMessage = $"Failed to deserialize request: {ex.Message}",
                    };
                    await JsonSerializer.SerializeAsync(context.Response.Body, errorResponse, serializerOptions, context.RequestAborted)
                        .ConfigureAwait(false);
                    return;
                }

                if (request is null)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                var response = await dispatcher.InvokeAsync(request, context.RequestAborted).ConfigureAwait(false);
                context.Response.ContentType = "application/json; charset=utf-8";
                await JsonSerializer.SerializeAsync(context.Response.Body, response, serializerOptions, context.RequestAborted)
                    .ConfigureAwait(false);
            });

            return endpoints;
        }
    }
}
