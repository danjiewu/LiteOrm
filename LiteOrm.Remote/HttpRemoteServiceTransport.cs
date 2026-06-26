using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Service
{
    /// <summary>
    /// 基于 <see cref="HttpClient"/> + System.Text.Json 的远程服务调用传输实现。
    /// </summary>
    public sealed class HttpRemoteServiceTransport : IRemoteServiceTransport
    {
        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true,
        };

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

        /// <inheritdoc />
        public async Task<RemoteInvocationResponse> InvokeAsync(RemoteInvocationRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            var json = JsonSerializer.Serialize(request, _serializerOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(_requestUri, content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new RemoteTransportException(
                    $"Remote service returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<RemoteInvocationResponse>(responseJson, _serializerOptions)
                ?? throw new RemoteTransportException("Remote service returned an empty response.");
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
