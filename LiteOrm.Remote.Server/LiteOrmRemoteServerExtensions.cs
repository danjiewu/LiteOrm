using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LiteOrm.Service;

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

            // 注册 IRemoteServiceTypeResolver：工厂优先，否则使用实例（默认 DefaultServiceTypeResolver）
            if (options.ServiceTypeResolverFactory is not null)
            {
                services.AddSingleton<IRemoteServiceTypeResolver>(options.ServiceTypeResolverFactory);
            }
            else
            {
                services.AddSingleton<IRemoteServiceTypeResolver>(options.ServiceTypeResolver);
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
            var actualPath = path ?? "api/remote/invoke";
            if (!actualPath.StartsWith('/'))
                actualPath = "/" + actualPath;

            endpoints.MapPost(actualPath, async (HttpContext context) =>
            {
                var options = context.RequestServices.GetRequiredService<RemoteServerOptions>();
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

            return endpoints;
        }
    }
}
