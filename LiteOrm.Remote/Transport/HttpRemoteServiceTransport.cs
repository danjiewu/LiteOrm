using LiteOrm.Common;
using System.Text;

namespace LiteOrm.Remote
{
    /// <summary>
    /// 基于 <see cref="HttpClient"/> + System.Text.Json 的远程服务调用传输实现。
    /// <para>
    /// 通过 <see cref="ICredentialsResolver"/> 获取身份认证票据，
    /// 在每次 <see cref="InvokeAsync"/> 时将票据写入 HTTP 请求头（默认为 <c>Cookie</c> 头），
    /// 服务端通过认证中间件或自定义逻辑恢复用户上下文。
    /// </para>
    /// <para>
    /// 该实现不使用 <see cref="HttpClientHandler"/>/<see cref="SocketsHttpHandler"/> 的
    /// <c>CookieContainer</c>，而是由 <see cref="ICredentialsResolver"/> 决定每次请求携带的票据，
    /// 便于在共享 Singleton HttpClient 的前提下实现会话隔离。
    /// </para>
    /// </summary>
    public sealed class HttpRemoteServiceTransport : JsonRemoteServiceTransport
    {
        private readonly HttpClient _httpClient;
        private readonly string _requestUri;
        private readonly ICredentialsResolver? _credentialsResolver;

        /// <summary>
        /// 票据写入的 HTTP 请求头名称。默认为 <c>Cookie</c>。
        /// <para>
        /// 服务端默认使用 ASP.NET Core Cookie 认证，从 <c>Cookie</c> 头读取身份票据。
        /// 若使用 JWT 等方案，可改为 <c>Authorization</c> 并在 <see cref="TicketFormat"/>
        /// 中指定前缀（如 <c>Bearer </c>）。
        /// </para>
        /// </summary>
        public string TicketHeaderName { get; set; } = "Cookie";

        /// <summary>
        /// 票据写入请求头时的格式化模板。默认为 <c>{0}</c>（直接写入票据原值）。
        /// <para>
        /// 例如 JWT Bearer 模式可设置为 <c>Bearer {0}</c>，票据会被拼接到 <c>Bearer </c> 之后写入 <c>Authorization</c> 头。
        /// </para>
        /// </summary>
        public string TicketFormat { get; set; } = "{0}";

        /// <summary>
        /// 初始化 <see cref="HttpRemoteServiceTransport"/> 类的新实例。
        /// </summary>
        /// <param name="httpClient">已配置好 BaseAddress 的 HttpClient 实例（建议禁用 <c>UseCookies</c>，由 <see cref="ICredentialsResolver"/> 管理票据）。</param>
        /// <param name="credentialsResolver">凭据解析器，用于获取身份认证票据。为 <c>null</c> 表示匿名连接（不带票据发起请求）。</param>
        /// <param name="requestUri">相对于 BaseAddress 的请求路径，默认为 <c>api/remote/invoke</c>。</param>
        public HttpRemoteServiceTransport(HttpClient httpClient, ICredentialsResolver? credentialsResolver = null, string requestUri = "api/remote/invoke")
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _credentialsResolver = credentialsResolver;
            _requestUri = string.IsNullOrEmpty(requestUri) ? "api/remote/invoke" : requestUri;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 调用 <see cref="ICredentialsResolver.GetTicketAsync"/> 获取票据，按 <see cref="TicketHeaderName"/>
        /// 与 <see cref="TicketFormat"/> 写入 HTTP 请求头；解析器返回 <c>null</c> 时不写入请求头（匿名调用）。
        /// </remarks>
        public override async Task<string> GetResponseJsonAsync(string requestJson, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, _requestUri);
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            if (_credentialsResolver is not null)
            {
                var ticket = await _credentialsResolver.GetTicketAsync(cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(ticket))
                {
                    var headerValue = string.Format(TicketFormat, ticket);
                    request.Headers.Add(TicketHeaderName, headerValue);
                }
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
                throw new RemoteTransportException(
                    $"Remote service returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
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
