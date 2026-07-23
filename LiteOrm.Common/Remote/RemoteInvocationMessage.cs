using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiteOrm.Remote
{
    /// <summary>
    /// 远程调用请求。
    /// </summary>
    /// <remarks>
    /// <see cref="Method"/> 直接使用 <see cref="MethodInfo"/> 类型，仅在客户端构建时赋值，
    /// 不参与序列化。序列化时由 <see cref="RemoteInvocationRequestConverter"/> 将方法名写入 JSON；
    /// 反序列化由 RemoteServiceDispatcher.ParseRequest 完成——
    /// 先根据 <see cref="ServiceName"/> 解析服务类型，再按方法名匹配 <see cref="MethodInfo"/>，
    /// 最后按方法参数类型反序列化 <see cref="Arguments"/>。
    /// <para>
    /// 序列化规则（<see cref="Arguments"/>）：
    /// 1. 当实参运行时类型与参数声明类型相同，或参数声明类型为 <see cref="Common.Expr"/> 派生类时，直接使用参数类型序列化，无需额外类型信息；
    /// 2. 类型不一致时，以 <c>{"$type":"实际类型名","$value":&lt;值&gt;}</c> 结构包装。
    /// </para>
    /// </remarks>
    [JsonConverter(typeof(RemoteInvocationRequestConverter))]
    public sealed class RemoteInvocationRequest
    {
        /// <summary>
        /// 服务名称。客户端与服务端使用相同的 ServiceName 进行匹配。
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// 请求唯一标识。客户端生成，使用 12 位 <see cref="ShortId"/>，
        /// 服务端处理后在 <see cref="RemoteInvocationResponse"/> 中原样返回，用于日志关联与请求追踪。
        /// </summary>
        public string RequestID { get; set; } = ShortId.NewId(12);

        /// <summary>
        /// 方法信息。客户端构建请求时直接赋值 <c>invocation.Method</c>；
        /// JSON 序列化只生成方法名（<see cref="JsonIgnoreAttribute"/>）。
        /// 服务端根据方法名查找到 <see cref="MethodInfo"/> 。
        /// </summary>
        [JsonIgnore]
        public MethodInfo Method { get; set; }

        /// <summary>
        /// 调用参数列表（不含 <see cref="System.Threading.CancellationToken"/>）。
        /// </summary>
        public object[] Arguments { get; set; } = Array.Empty<object>();
    }

    /// <summary>
    /// 远程调用响应。
    /// </summary>
    /// <remarks>
    /// <see cref="Result"/> 与 <see cref="OutArguments"/> 中的值为 <c>object</c> 类型，
    /// 反序列化后为 <see cref="JsonElement"/>，由调用方根据已知预期类型进行二次反序列化。
    /// 当服务端发现实际类型与预期类型不一致时，以 <see cref="TypeWrappedValue"/> 包装。
    /// <para>
    /// <see cref="OutArguments"/> 以参数在请求 <see cref="RemoteInvocationRequest.Arguments"/> 列表中的索引为键，
    /// 回写值为值，按索引升序排列。
    /// </para>
    /// </remarks>
    public sealed class RemoteInvocationResponse
    {
        /// <summary>
        /// 对应的请求唯一标识。服务端从 <see cref="RemoteInvocationRequest.RequestID"/> 复制，
        /// 用于日志关联与请求追踪。
        /// </summary>
        public string RequestID { get; set; }

        /// <summary>
        /// 调用是否成功。
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 返回值。反序列化后为 <see cref="JsonElement"/> 或 <see cref="TypeWrappedValue"/> 的 JSON 表示，
        /// 调用方根据方法返回类型进行反序列化。
        /// </summary>
        public object Result { get; set; }

        /// <summary>
        /// 需要回写到客户端的参数。键为参数在请求 <see cref="RemoteInvocationRequest.Arguments"/> 列表中的索引，
        /// 值为回写值（反序列化后为 <see cref="JsonElement"/>，调用方根据 <c>IArgumentOutHandler.ReturnType</c> 进行二次反序列化）。
        /// </summary>
        public SortedList<int, object> OutArguments { get; set; } = new();

        /// <summary>
        /// 远程调用异常信息。仅在 <see cref="Success"/> 为 false 时返回。
        /// </summary>
        public RemoteErrorInfo Error { get; set; }
    }

    /// <summary>
    /// 远程调用异常信息。
    /// </summary>
    public sealed class RemoteErrorInfo
    {
        /// <summary>
        /// 远程抛出异常的类型全名。
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 远程异常消息。
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 远程异常堆栈。
        /// </summary>
        public string StackTrace { get; set; }
    }

    /// <summary>
    /// 类型包装值。当实际值类型与预期类型不一致时使用，携带实际类型名与值。
    /// 序列化为 &lt;c&gt;{"$type":"类型名","$value":&lt;值&gt;}&lt;/c&gt; 结构。
    /// </summary>
    public sealed class TypeWrappedValue
    {
        /// <summary>
        /// 初始化 <see cref="TypeWrappedValue"/> 类的新实例。
        /// </summary>
        public TypeWrappedValue() { }
        /// <summary>
        /// 初始化 <see cref="TypeWrappedValue"/> 类的新实例。
        /// </summary>
        /// <param name="value">需包装的值</param>
        public TypeWrappedValue(object value)
        {
            Type = TypeResolverHelper.GetName(value?.GetType());
            Value = value;
        }
        /// <summary>
        /// 实际值类型的程序集限定名。
        /// </summary>
        [JsonPropertyName("$type")]
        public string Type { get; set; }

        /// <summary>
        /// 实际值。
        /// </summary>
        [JsonPropertyName("$value")]
        public object Value { get; set; }
    }
}
