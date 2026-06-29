using LiteOrm.Service;
using System;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace LiteOrm.Remote
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
            var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#else
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#endif
            return JsonSerializer.Deserialize<RemoteInvocationResponse>(responseJson, _serializerOptions)
                ?? throw new RemoteTransportException("Remote service returned an empty response.");
        }


        public RemoteInvocationResponse ParseResponse(string json, MethodInfo method, JsonSerializerOptions options)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentNullException(nameof(json));

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new RemoteInvocationResponse
            {
                Success = root.GetProperty("Success").GetBoolean(),
                Error = root.TryGetProperty("Error", out var errorProp) && errorProp.ValueKind == JsonValueKind.Object
                    ? JsonSerializer.Deserialize<RemoteErrorInfo>(errorProp.GetRawText(), options)
                    : null,
                OutArguments = root.TryGetProperty("OutArguments", out var outArgsProp) && outArgsProp.ValueKind == JsonValueKind.Array
                    ? JsonSerializer.Deserialize<List<OutputArgument>>(outArgsProp.GetRawText(), options)
                    : Array.Empty<OutputArgument>(),
                Result = root.TryGetProperty("Result", out var resultProp) && resultProp.ValueKind != JsonValueKind.Null
                ? RemoteInvocationRequestConverter.DeserializeTypedValue(resultProp, method.ReturnType, options) : null,
            };
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
