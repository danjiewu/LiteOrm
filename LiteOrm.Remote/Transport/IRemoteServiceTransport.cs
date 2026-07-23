using LiteOrm.Common;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Remote
{
    /// <summary>
    /// 远程服务调用传输层抽象。
    /// </summary>
    /// <remarks>
    /// 实现该接口以提供具体的远程调用传输机制（如 HTTP、gRPC、消息队列等）。
    /// 默认实现 <see cref="HttpRemoteServiceTransport"/> 基于 HttpClient + System.Text.Json。
    /// </remarks>
    public interface IRemoteServiceTransport
    {
        /// <summary>
        /// 携带凭据与服务端建立已认证连接。
        /// 服务端验证通过后通过 <c>HttpContext.SignInAsync</c> 创建身份票据，
        /// 后续请求自动携带票据，通过 <c>HttpContext.User</c> 恢复用户上下文。
        /// 多次调用仅首次生效，后续调用直接返回。
        /// </summary>
        /// <param name="credentials">远程调用凭据（包含用户名、密码及自定义扩展字段）。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        Task ConnectAsync(RemoteCredentials credentials, CancellationToken cancellationToken = default);

        /// <summary>
        /// 使用匿名身份与服务端连接。
        /// 不发送凭据，服务端以匿名用户处理后续请求。
        /// 多次调用仅首次生效，后续调用直接返回。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步发送远程调用请求并返回响应。
        /// </summary>
        /// <param name="request">远程调用请求。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>远程调用响应。</returns>
        Task<RemoteInvocationResponse> InvokeAsync(RemoteInvocationRequest request, CancellationToken cancellationToken = default);
    }
}
