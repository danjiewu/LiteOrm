using LiteOrm.Common;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

namespace LiteOrm.Remote
{
    /// <summary>
    /// 基于 System.Text.Json 序列化的远程服务调用基类。
    /// </summary>
    public abstract class JsonRemoteServiceTransport : IRemoteServiceTransport
    {
        protected static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// 用于记录请求/响应 JSON 报文的日志记录器。仅在 <see cref="LogJsonPayloads"/> 启用时使用。
        /// </summary>
        protected ILogger? Logger { get; set; }

        /// <summary>
        /// 是否启用 JSON 报文日志。可通过 <see cref="ConfigureJsonLogging"/> 设置。
        /// </summary>
        protected bool LogJsonPayloads { get; set; }

        /// <summary>
        /// 配置 JSON 报文日志。启用后，每次 <see cref="InvokeAsync"/> 会以 Debug 级别记录请求与响应 JSON。
        /// </summary>
        /// <param name="logger">日志记录器。</param>
        /// <param name="logJsonPayloads">是否启用 JSON 报文日志。</param>
        public void ConfigureJsonLogging(ILogger? logger, bool logJsonPayloads)
        {
            Logger = logger;
            LogJsonPayloads = logJsonPayloads;
        }

        /// <inheritdoc />
        public virtual async Task<RemoteInvocationResponse> InvokeAsync(RemoteInvocationRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            var json = JsonSerializer.Serialize(request, _serializerOptions);
            LogPayload(">>> RemoteInvoke Request JSON: {Json}", json);
            var responseJson = await GetResponseJsonAsync(json, cancellationToken).ConfigureAwait(false);
            LogPayload("<<< RemoteInvoke Response JSON: {Json}", responseJson);
            return JsonSerializer.Deserialize<RemoteInvocationResponse>(responseJson, _serializerOptions)
                ?? throw new RemoteTransportException("Remote service returned an empty response.");
        }

        /// <summary>
        /// 以 Debug 级别记录 JSON 报文（若已启用 <see cref="LogJsonPayloads"/> 且提供了 <see cref="Logger"/>）。
        /// </summary>
        /// <param name="message">日志消息模板。</param>
        /// <param name="json">JSON 报文。</param>
        private void LogPayload(string message, string json)
        {
            if (!LogJsonPayloads || Logger is null)
                return;
            Logger.LogDebug(message, json);
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
