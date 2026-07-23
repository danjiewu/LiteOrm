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
