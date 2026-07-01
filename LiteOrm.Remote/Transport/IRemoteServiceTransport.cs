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
        /// 异步发送远程调用请求并返回响应。
        /// </summary>
        /// <param name="request">远程调用请求。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>远程调用响应。</returns>
        Task<RemoteInvocationResponse> InvokeAsync(RemoteInvocationRequest request, CancellationToken cancellationToken = default);
    }
}
