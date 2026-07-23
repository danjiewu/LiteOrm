using LiteOrm.Common;
using System.Reflection;
using System.Text.Json;

namespace LiteOrm.Remote
{
    /// <summary>
    /// 基于 System.Text.Json 序列化的远程服务调用基类。
    /// </summary>
    public abstract class JsonRemoteServiceTransport : IRemoteServiceTransport
    {
        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true,
        };

        /// <inheritdoc />
        public async Task ConnectAsync(RemoteCredentials credentials, CancellationToken cancellationToken = default)
        {
            if (credentials is null) throw new ArgumentNullException(nameof(credentials));
            await GetConnectResponseJsonAsync(credentials, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            await GetConnectResponseJsonAsync(null, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 异步获取建立会话的响应 JSON 字符串。
        /// <paramref name="credentials"/> 为 null 时使用匿名连接。
        /// </summary>
        /// <param name="credentials">远程调用凭据，匿名连接时为 null。</param>
        /// <param name="cancellationToken">用于取消操作的 <see cref="CancellationToken"/>。</param>
        /// <returns>建立会话返回的 JSON 字符串。</returns>
        protected abstract Task<string> GetConnectResponseJsonAsync(RemoteCredentials? credentials, CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public async Task<RemoteInvocationResponse> InvokeAsync(RemoteInvocationRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            var json = JsonSerializer.Serialize(request, _serializerOptions);
            var responseJson = await GetResponseJsonAsync(json, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<RemoteInvocationResponse>(responseJson, _serializerOptions)
                ?? throw new RemoteTransportException("Remote service returned an empty response.");
        }

        /// <summary>
        /// 异步获取远程调用响应 JSON 字符串。
        /// </summary>
        /// <param name="requestJson">包含参数内容的 JSON 字符串。</param>
        /// <param name="cancellationToken">用于取消操作的 <see cref="CancellationToken"/>。</param>
        /// <returns>远程调用返回的 JSON 字符串。</returns>
        public abstract Task<string> GetResponseJsonAsync(string requestJson, CancellationToken cancellationToken = default);


        protected virtual RemoteInvocationResponse ParseResponse(string json, MethodInfo method, JsonSerializerOptions options)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentNullException(nameof(json));

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new RemoteInvocationResponse
            {
                RequestID = root.TryGetProperty("RequestID", out var requestIdProp) && requestIdProp.ValueKind == JsonValueKind.String
                    ? requestIdProp.GetString()
                    : null,
                Success = root.GetProperty("Success").GetBoolean(),
                Error = root.TryGetProperty("Error", out var errorProp) && errorProp.ValueKind == JsonValueKind.Object
                    ? JsonSerializer.Deserialize<RemoteErrorInfo>(errorProp.GetRawText(), options)
                    : null,
                OutArguments = root.TryGetProperty("OutArguments", out var outArgsProp) && outArgsProp.ValueKind == JsonValueKind.Object
                    ? JsonSerializer.Deserialize<SortedList<int, object>>(outArgsProp.GetRawText(), options)
                    : new(),
                Result = root.TryGetProperty("Result", out var resultProp) && resultProp.ValueKind != JsonValueKind.Null
                ? RemoteInvocationRequestConverter.DeserializeTypedValue(resultProp, method.ReturnType, options) : null,
            };
        }
    }
}
