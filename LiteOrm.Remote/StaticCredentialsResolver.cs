using LiteOrm.Common;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Remote
{
    /// <summary>
    /// 静态凭据解析器。通过 <see cref="LoginAsync"/> 向服务端 SignIn 端点提交凭据，
    /// 登录成功后将服务端返回的身份票据保存到本地，后续 <see cref="GetTicketAsync"/> 直接返回该票据。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 适用于单用户场景（如后台服务、桌面客户端），整个进程共享一份身份票据。
    /// 多用户场景请实现自定义的 <see cref="ICredentialsResolver"/>。
    /// </para>
    /// <para>
    /// 典型用法：
    /// <code>
    /// var resolver = new StaticCredentialsResolver(httpClient);
    /// bool ok = await resolver.LoginAsync(new RemoteCredentials { Username = "alice", Password = "pwd" });
    /// // resolver 注册到 DI 后，HttpRemoteServiceTransport 在每次 InvokeAsync 时自动取出票据写入请求头
    /// </code>
    /// </para>
    /// <para>
    /// 该类是线程安全的：登录通过信号量串行化，<see cref="GetTicketAsync"/> 读取缓存的票据引用。
    /// </para>
    /// </remarks>
    public sealed class StaticCredentialsResolver : ICredentialsResolver
    {
        private readonly HttpClient _httpClient;
        private readonly string _signInUri;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private string? _ticket;

        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// 当前缓存的身份票据。未登录时为 <c>null</c>。
        /// </summary>
        public string? Ticket => _ticket;

        /// <summary>
        /// 是否已登录（持有非空票据）。
        /// </summary>
        public bool IsLoggedIn => !string.IsNullOrEmpty(_ticket);

        /// <summary>
        /// 初始化 <see cref="StaticCredentialsResolver"/> 类的新实例。
        /// </summary>
        /// <param name="httpClient">已配置好 BaseAddress 的 HttpClient 实例。</param>
        /// <param name="signInUri">相对于 BaseAddress 的登录路径，默认为 <c>api/remote/signin</c>。</param>
        public StaticCredentialsResolver(HttpClient httpClient, string signInUri = "api/remote/signin")
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _signInUri = string.IsNullOrEmpty(signInUri) ? "api/remote/signin" : signInUri;
        }

        /// <summary>
        /// 使用指定凭据登录服务端，登录成功后保存返回的身份票据到本地。
        /// </summary>
        /// <param name="credentials">远程调用凭据。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>登录成功返回 <c>true</c> 并保存票据；失败（凭据无效、HTTP 非 2xx、响应不含 Ticket 字段）返回 <c>false</c>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="credentials"/> 为 <c>null</c>。</exception>
        public async Task<bool> LoginAsync(RemoteCredentials credentials, CancellationToken cancellationToken = default)
        {
            if (credentials is null) throw new ArgumentNullException(nameof(credentials));

            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var json = JsonSerializer.Serialize(credentials, _serializerOptions);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync(_signInUri, content, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _ticket = null;
                    return false;
                }

                var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
                _ticket = ParseTicket(body);
                return IsLoggedIn;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc/>
        /// <remarks>返回 <see cref="Ticket"/> 缓存的票据；未登录时返回 <c>null</c>（匿名调用）。</remarks>
        public Task<string?> GetTicketAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_ticket);
        }

        /// <summary>
        /// 从 SignIn 端点返回的 JSON 响应中提取 <c>Ticket</c> 字段。
        /// </summary>
        /// <param name="body">HTTP 响应体字符串。</param>
        /// <returns>票据字符串；响应为空、非 JSON 或不含 Ticket 字段时返回 <c>null</c>。</returns>
        private static string? ParseTicket(string? body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("Ticket", out var ticketProp)
                    && ticketProp.ValueKind == JsonValueKind.String)
                    return ticketProp.GetString();
                return null;
            }
            catch
            {
                return null;
            }
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
}
