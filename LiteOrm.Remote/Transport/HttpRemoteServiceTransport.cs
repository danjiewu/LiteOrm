using System.Text;

namespace LiteOrm.Remote
{
    /// <summary>
    /// 基于 <see cref="HttpClient"/> + System.Text.Json 的远程服务调用传输实现。
    /// </summary>
    public sealed class HttpRemoteServiceTransport : JsonRemoteServiceTransport
    {
        private readonly HttpClient _httpClient;
        private readonly string _requestUri;

        /// <summary>
        /// 初始化 <see cref="HttpRemoteServiceTransport"/> 类的新实例。
        /// </summary>
        /// <param name="httpClient">已配置好 BaseAddress 的 HttpClient 实例。</param>
        /// <param name="requestUri">相对于 BaseAddress 的请求路径，默认为 <c>/api/remote/invoke</c>。</param>
        public HttpRemoteServiceTransport(HttpClient httpClient, string requestUri = "api/remote/invoke")
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _requestUri = string.IsNullOrEmpty(requestUri) ? "api/remote/invoke" : requestUri;
        }

        public override async Task<string> GetResponseJsonAsync(string requestJson, CancellationToken cancellationToken = default)
        {
            using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(_requestUri, content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
#if NETSTANDARD2_0 || NETSTANDARD2_1
                // 针对 .NET Standard 2.0、2.1 的代码路径：不使用 CancellationToken
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#else
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#endif
                throw new RemoteTransportException(
                    $"Remote service returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
            }
#if NETSTANDARD2_0 || NETSTANDARD2_1
            // 针对 .NET Standard 2.0、2.1 的代码路径：不使用 CancellationToken
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#else
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#endif
        }
    }

    /// <summary>
    /// 远程调用传输层异常。用于表示传输过程中发生的非业务异常（如网络错误、HTTP 状态码异常）。
    /// </summary>
    public sealed class RemoteTransportException : Exception
    {
        /// <summary>
        /// 初始化 <see cref="RemoteTransportException"/> 类的新实例。
        /// </summary>
        public RemoteTransportException() : base() { }

        /// <summary>
        /// 使用指定错误消息初始化 <see cref="RemoteTransportException"/> 类的新实例。
        /// </summary>
        /// <param name="message">错误消息。</param>
        public RemoteTransportException(string message) : base(message) { }

        /// <summary>
        /// 使用指定错误消息和内部异常初始化 <see cref="RemoteTransportException"/> 类的新实例。
        /// </summary>
        /// <param name="message">错误消息。</param>
        /// <param name="inner">内部异常。</param>
        public RemoteTransportException(string message, Exception inner) : base(message, inner) { }
    }
}
