using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace LiteOrm.Remote
{
    /// <summary>
    /// 凭据模式。决定 <see cref="HttpRemoteServiceTransport"/> 如何获取远程调用凭据。
    /// </summary>
    public enum RemoteCredentialsMode
    {
        /// <summary>
        /// 单凭据模式（默认，向后兼容）。整个进程共享一份 <see cref="RemoteCredentials"/>，
        /// <see cref="IRemoteServiceTransport"/> 注册为 Singleton。
        /// 调用方在启动时手动 <see cref="HttpRemoteServiceTransport.ConnectAsync"/> 一次，
        /// 之后所有 <c>InvokeAsync</c> 自动复用 Connect 返回的 Cookie，不再重复验证身份。
        /// </summary>
        SingleCredential,

        /// <summary>
        /// 动态凭据模式。<see cref="IRemoteServiceTransport"/> 注册为 Singleton，
        /// <see cref="HttpRemoteServiceTransport"/> 内部按凭证 key 缓存每个会话的 Cookie。
        /// 每次调用 <c>InvokeAsync</c> 时通过 <see cref="CredentialsResolver"/> 解析当前会话的凭据，
        /// 从缓存中取出对应 Cookie 写入请求头；缓存未命中时返回匿名（无 Cookie）请求。
        /// <para>
        /// 调用方需在会话开始时（如 BFF 登录流程）调用一次 <see cref="HttpRemoteServiceTransport.ConnectAsync"/>
        /// 建立 Cookie，之后该会话的所有 <c>InvokeAsync</c> 均复用此 Cookie，避免每次请求重复验证身份。
        /// </para>
        /// </summary>
        Dynamic,
    }

    /// <summary>
    /// 基于 <see cref="HttpClient"/> + System.Text.Json 的远程服务调用传输实现。
    /// <para>
    /// 该实现不使用 <see cref="HttpClientHandler"/>/<see cref="SocketsHttpHandler"/> 的
    /// <c>CookieContainer</c>，而是手动解析 Connect 响应中的 <c>Set-Cookie</c> 并按凭证 key 缓存，
    /// 每次 <c>InvokeAsync</c> 时手动写入 <c>Cookie</c> 请求头。这样可在共享 Singleton HttpClient 的前提下
    /// 实现多用户会话隔离，且避免每次 Invoke 触发 Connect 验证。
    /// </para>
    /// </summary>
    public sealed class HttpRemoteServiceTransport : JsonRemoteServiceTransport
    {
        private readonly HttpClient _httpClient;
        private readonly string _requestUri;
        private readonly string _connectUri;
        private readonly SemaphoreSlim _connectLock = new SemaphoreSlim(1, 1);
        private RemoteCredentials? _credentials;

        /// <summary>
        /// 单凭据模式下缓存的 Cookie 字符串（格式 <c>name1=value1; name2=value2</c>）。
        /// </summary>
        private string? _singleCookie;

        /// <summary>
        /// 动态模式下按凭证 key 缓存的 Cookie。key 由 <see cref="GetCredentialsKey"/> 生成。
        /// </summary>
        private readonly ConcurrentDictionary<string, string> _dynamicCookies = new();

        /// <summary>
        /// 凭据模式。默认为 <see cref="RemoteCredentialsMode.SingleCredential"/>。
        /// </summary>
        public RemoteCredentialsMode CredentialsMode { get; set; } = RemoteCredentialsMode.SingleCredential;

        /// <summary>
        /// 动态凭据解析器。仅在 <see cref="CredentialsMode"/> 为
        /// <see cref="RemoteCredentialsMode.Dynamic"/> 时生效。
        /// <para>
        /// 接收当前会话的 <see cref="IServiceProvider"/>，返回该会话使用的 <see cref="RemoteCredentials"/>；
        /// 返回 null 表示匿名连接（不带 Cookie）。典型实现：从 <c>IHttpContextAccessor.HttpContext.Request.Cookies</c>
        /// 提取用户名/密码或 ClientId/ClientSecret。
        /// </para>
        /// </summary>
        public Func<IServiceProvider, RemoteCredentials?>? CredentialsResolver { get; set; }

        /// <summary>
        /// 当前会话关联的 <see cref="IServiceProvider"/>。由 DI 注入，
        /// 供 <see cref="CredentialsResolver"/> 解析动态凭据使用。SingleCredential 模式下可为 null。
        /// </summary>
        public IServiceProvider? ServiceProvider { get; set; }

        /// <summary>
        /// 初始化 <see cref="HttpRemoteServiceTransport"/> 类的新实例。
        /// </summary>
        /// <param name="httpClient">已配置好 BaseAddress 的 HttpClient 实例（建议禁用 <c>UseCookies</c>，由本类手动管理 Cookie）。</param>
        /// <param name="credentials">单凭据模式下使用的固定凭据（向后兼容）。</param>
        /// <param name="requestUri">相对于 BaseAddress 的请求路径，默认为 <c>/api/remote/invoke</c>。</param>
        /// <param name="connectUri">相对于 BaseAddress 的连接路径，默认为 <c>/api/remote/connect</c>。</param>
        public HttpRemoteServiceTransport(HttpClient httpClient, RemoteCredentials? credentials = null, string requestUri = "api/remote/invoke", string connectUri = "api/remote/connect")
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _requestUri = string.IsNullOrEmpty(requestUri) ? "api/remote/invoke" : requestUri;
            _connectUri = string.IsNullOrEmpty(connectUri) ? "api/remote/connect" : connectUri;
            _credentials = credentials;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 调用后从响应头提取 <c>Set-Cookie</c>，按凭证 key 缓存：
        /// <list type="bullet">
        /// <item><see cref="RemoteCredentialsMode.SingleCredential"/>：写入 <c>_singleCookie</c> 字段</item>
        /// <item><see cref="RemoteCredentialsMode.Dynamic"/>：写入 <c>_dynamicCookies[key]</c>，支持多用户并发会话</item>
        /// </list>
        /// 后续 <see cref="InvokeAsync"/> / <see cref="GetResponseJsonAsync"/> 会自动将该 Cookie 写入请求头，
        /// 不再重复调用 Connect 验证身份。
        /// </remarks>
        public override async Task ConnectAsync(RemoteCredentials credentials, CancellationToken cancellationToken = default)
        {
            if (credentials is null) throw new ArgumentNullException(nameof(credentials));

            await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _credentials = credentials;
                var json = JsonSerializer.Serialize(credentials, _serializerOptions);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync(_connectUri, content, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
                    throw new RemoteTransportException(
                        $"Remote connect returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
                }

                // 提取 Set-Cookie 头中的 name=value 部分，组装为可发送的 Cookie 串
                var cookie = ExtractCookieHeader(response);
                if (CredentialsMode == RemoteCredentialsMode.Dynamic)
                {
                    var key = GetCredentialsKey(credentials);
                    if (!string.IsNullOrEmpty(cookie))
                        _dynamicCookies[key] = cookie;
                    else
                        _dynamicCookies.TryRemove(key, out _);
                }
                else
                {
                    _singleCookie = cookie;
                }
            }
            finally
            {
                _connectLock.Release();
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 不再自动触发 Connect。调用方需在会话开始时显式调用 <see cref="ConnectAsync"/> 建立 Cookie，
        /// 之后所有 Invoke 会从缓存取出对应 Cookie 写入请求头；缓存未命中时以匿名身份（无 Cookie）发起请求。
        /// </remarks>
        public override async Task<RemoteInvocationResponse> InvokeAsync(RemoteInvocationRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            var json = JsonSerializer.Serialize(request, _serializerOptions);
            var responseJson = await GetResponseJsonAsync(json, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<RemoteInvocationResponse>(responseJson, _serializerOptions)
                ?? throw new RemoteTransportException("Remote service returned an empty response.");
        }

        /// <inheritdoc/>
        public override async Task<string> GetResponseJsonAsync(string requestJson, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, _requestUri);
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            // 从按凭证 key 缓存的 Cookie 中取出当前会话对应的 Cookie 写入请求头
            var cookie = ResolveCookie();
            if (!string.IsNullOrEmpty(cookie))
            {
                request.Headers.Add("Cookie", cookie);
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

        /// <summary>
        /// 解析当前会话应使用的 Cookie 字符串。
        /// <list type="bullet">
        /// <item><see cref="RemoteCredentialsMode.SingleCredential"/>：返回 <c>_singleCookie</c></item>
        /// <item><see cref="RemoteCredentialsMode.Dynamic"/>：调用 <see cref="CredentialsResolver"/> 获取当前凭据，
        /// 从 <c>_dynamicCookies</c> 中取出该凭据对应的 Cookie；解析失败或缓存未命中时返回 null（匿名）</item>
        /// </list>
        /// </summary>
        private string? ResolveCookie()
        {
            if (CredentialsMode == RemoteCredentialsMode.Dynamic)
            {
                if (CredentialsResolver is null)
                {
                    throw new InvalidOperationException(
                        $"HttpRemoteServiceTransport.CredentialsMode is {nameof(RemoteCredentialsMode.Dynamic)} " +
                        $"but CredentialsResolver is not set. Provide a resolver or switch to SingleCredential mode.");
                }
                if (ServiceProvider is null)
                {
                    throw new InvalidOperationException(
                        $"HttpRemoteServiceTransport.ServiceProvider is null in {nameof(RemoteCredentialsMode.Dynamic)} mode. " +
                        $"Ensure IRemoteServiceTransport is registered with the DI scope available.");
                }
                var credentials = CredentialsResolver(ServiceProvider);
                if (credentials is null)
                    return null; // 匿名连接，不带 Cookie
                var key = GetCredentialsKey(credentials);
                return _dynamicCookies.TryGetValue(key, out var cookie) ? cookie : null;
            }
            return _singleCookie;
        }

        /// <summary>
        /// 生成用于缓存 Cookie 的凭证 key。相同凭证返回相同 key，便于多用户会话区分。
        /// <para>规则：Password 模式使用 <c>Username</c>；ClientCredentials 模式使用 <c>ClientId</c>；
        /// 都为空时回退到完整序列化串。</para>
        /// </summary>
        private static string GetCredentialsKey(RemoteCredentials credentials)
        {
            if (credentials.GrantType == AuthGrantType.Password)
            {
                if (!string.IsNullOrEmpty(credentials.Username))
                    return "u:" + credentials.Username;
            }
            else if (credentials.GrantType == AuthGrantType.ClientCredentials)
            {
                if (!string.IsNullOrEmpty(credentials.ClientId))
                    return "c:" + credentials.ClientId;
            }
            return "s:" + JsonSerializer.Serialize(credentials);
        }

        /// <summary>
        /// 从 Connect 响应头中提取 <c>Set-Cookie</c>，组装为可写入 <c>Cookie</c> 请求头的字符串。
        /// <para>仅取每个 <c>Set-Cookie</c> 的 <c>name=value</c> 部分（第一个分号前），多个用 <c>; </c> 连接。</para>
        /// </summary>
        private static string? ExtractCookieHeader(HttpResponseMessage response)
        {
            // HttpResponseMessage.Headers 默认不暴露 Set-Cookie（它属于 HttpContent.Headers 或 Headers），
            // 需同时尝试 response.Headers 和 response.Content.Headers
            var setCookies = Enumerable.Empty<string>();
            if (response.Headers.TryGetValues("Set-Cookie", out var h1))
                setCookies = setCookies.Concat(h1);
            if (response.Content.Headers.TryGetValues("Set-Cookie", out var h2))
                setCookies = setCookies.Concat(h2);

            var pairs = setCookies
                .Select(c => c.Split(';')[0].Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            return pairs.Count == 0 ? null : string.Join("; ", pairs);
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
