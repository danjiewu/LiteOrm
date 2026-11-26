using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace LiteOrm.Remote.Server
{
    /// <summary>
    /// 远程调用拦截上下文。在 <see cref="IRemoteInvocationInterceptor"/> 与最终分发之间传递请求与响应。
    /// </summary>
    public sealed class RemoteInvocationContext
    {
        /// <summary>
        /// 初始化 <see cref="RemoteInvocationContext"/> 类的新实例。
        /// </summary>
        /// <param name="httpContext">当前 HTTP 请求上下文。</param>
        /// <param name="request">已解析的远程调用请求。</param>
        public RemoteInvocationContext(HttpContext httpContext, RemoteInvocationRequest request)
        {
            HttpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }

        /// <summary>
        /// 当前 HTTP 请求上下文。可通过请求头、查询串等获取调用方信息。
        /// </summary>
        public HttpContext HttpContext { get; }

        /// <summary>
        /// 已解析的远程调用请求（服务名、方法、参数）。
        /// </summary>
        public RemoteInvocationRequest Request { get; }

        /// <summary>
        /// 远程调用响应。拦截器可在调用 <c>next</c> 后读取或替换；短路请求时应通过
        /// <see cref="Reject"/> 或直接赋值设置该属性。
        /// </summary>
        public RemoteInvocationResponse? Response { get; set; }

        /// <summary>
        /// 返回给客户端的 HTTP 状态码。默认为 200，拒绝请求时可设为 401、403 等。
        /// </summary>
        public int StatusCode { get; set; } = StatusCodes.Status200OK;

        /// <summary>
        /// 拒绝本次远程调用：设置 HTTP 状态码与失败响应，并终止后续管道（拦截器直接返回即可）。
        /// </summary>
        /// <param name="statusCode">返回给客户端的 HTTP 状态码，如 401、403。</param>
        /// <param name="message">错误消息。</param>
        /// <param name="errorType">错误类型名称，默认为 <see cref="ServiceException"/>。</param>
        public void Reject(int statusCode, string message, string? errorType = null)
        {
            StatusCode = statusCode;
            Response = new RemoteInvocationResponse
            {
                RequestID = Request.RequestID,
                Success = false,
                Error = new RemoteErrorInfo
                {
                    Type = errorType ?? nameof(ServiceException),
                    Message = message,
                },
            };
        }
    }

    /// <summary>
    /// 远程调用拦截器。注册到 DI 后，服务端会在分发远程调用前按注册顺序执行所有拦截器，
    /// 可用于静态 Key 认证、权限校验、审计日志、限流等。
    /// <para>
    /// 与 ASP.NET Core 中间件类似：调用 <paramref name="next"/> 继续执行后续拦截器与最终分发；
    /// 不调用 <paramref name="next"/> 即短路本次调用，此时应通过
    /// <see cref="RemoteInvocationContext.Reject"/> 或 <see cref="RemoteInvocationContext.Response"/> 设置响应。
    /// </para>
    /// <para>
    /// 注册方式：<c>services.AddSingleton&lt;IRemoteInvocationInterceptor, MyInterceptor&gt;()</c>。
    /// 支持 Singleton / Scoped / Transient 生命周期，每个请求按 DI 规则解析。
    /// </para>
    /// </summary>
    public interface IRemoteInvocationInterceptor
    {
        /// <summary>
        /// 拦截远程调用。
        /// </summary>
        /// <param name="context">远程调用上下文。</param>
        /// <param name="next">调用后续拦截器与最终分发；不调用即短路请求。</param>
        Task InvokeAsync(RemoteInvocationContext context, Func<Task> next);
    }
}
