using LiteOrm.Common;
using System.Text;
using System.Text.Json;

namespace LiteOrm.Remote
{
    /// <summary>
    /// 基于 <see cref="HttpClient"/> + System.Text.Json 的远程服务调用传输实现。
    /// </summary>
    public sealed class HttpRemoteServiceTransport : JsonRemoteServiceTransport
    {
        private readonly HttpClient _httpClient;
        private readonly string _requestUri;
        private readonly string _connectUri;

        /// <summary>
        /// 初始化 <see cref="HttpRemoteServiceTransport"/> 类的新实例。
        /// </summary>
        /// <param name="httpClient">已配置好 BaseAddress 的 HttpClient 实例。</param>
        /// <param name="requestUri">相对于 BaseAddress 的请求路径，默认为 <c>/api/remote/invoke</c>。</param>
        /// <param name="connectUri">相对于 BaseAddress 的连接路径，默认为 <c>/api/remote/connect</c>。</param>
        public HttpRemoteServiceTransport(HttpClient httpClient, string requestUri = "api/remote/invoke", string connectUri = "api/remote/connect")
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _requestUri = string.IsNullOrEmpty(requestUri) ? "api/remote/invoke" : requestUri;
            _connectUri = string.IsNullOrEmpty(connectUri) ? "api/remote/connect" : connectUri;
        }

        /// <inheritdoc />
        protected override async Task<string> GetConnectResponseJsonAsync(RemoteCredentials? credentials, CancellationToken cancellationToken = default)
        {
            HttpContent? content = null;
            if (credentials is not null)
            {
                var body = JsonSerializer.Serialize(credentials, new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    PropertyNameCaseInsensitive = true,
                });
                content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            using var response = await _httpClient.PostAsync(_connectUri, content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
                throw new RemoteTransportException(
                    $"Remote connect returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
            }
            return await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
#if NETSTANDARD2_0 || NETSTANDARD2_1
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#else
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#endif
        }

        public override async Task<string> GetResponseJsonAsync(string requestJson, CancellationToken cancellationToken = default)
        {
            using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(_requestUri, content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
                throw new RemoteTransportException(
                    $"Remote service returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
            }
            return await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
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
