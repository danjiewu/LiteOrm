using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiteOrm.Service
{
    /// <summary>
    /// 远程调用请求。
    /// </summary>
    /// <remarks>
    /// <see cref="Method"/> 直接使用 <see cref="MethodInfo"/> 类型，仅在客户端构建时赋值，
    /// 不参与序列化。序列化时由 <see cref="RemoteInvocationRequestConverter"/> 将方法名写入 JSON；
    /// 反序列化由 <see cref="RemoteServiceDispatcher.ParseRequest"/> 完成——
    /// 先根据 <see cref="ServiceName"/> 解析服务类型，再按方法名匹配 <see cref="MethodInfo"/>，
    /// 最后按方法参数类型反序列化 <see cref="Arguments"/>。
    /// <para>
    /// 序列化规则（<see cref="Arguments"/>）：
    /// 1. 当实参运行时类型与参数声明类型相同，或参数声明类型为 <see cref="Common.Expr"/> 派生类时，直接使用参数类型序列化，无需额外类型信息；
    /// 2. 类型不一致时，以 <c>{"$type":"实际类型名","$value":<值>}</c> 结构包装。
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
        /// 方法信息。客户端构建请求时直接赋值 <c>invocation.Method</c>；
        /// 不参与 JSON 序列化（<see cref="JsonIgnoreAttribute"/>）。
        /// 服务端由 <see cref="RemoteServiceDispatcher.ParseRequest"/> 根据方法名查找到 <see cref="MethodInfo"/> 后赋值。
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
    /// <see cref="Result"/> 与 <see cref="OutputArgument.Value"/> 为 <c>object</c> 类型，
    /// 反序列化后为 <see cref="JsonElement"/>，由调用方根据已知预期类型进行二次反序列化。
    /// 当服务端发现实际类型与预期类型不一致时，以 <see cref="TypeWrappedValue"/> 包装。
    /// </remarks>
    public sealed class RemoteInvocationResponse
    {
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
        /// 需要回写到客户端的参数列表。
        /// </summary>
        public IList<OutputArgument> WriteBackArguments { get; set; } = Array.Empty<OutputArgument>();

        /// <summary>
        /// 远程抛出异常的类型全名。
        /// </summary>
        public string ErrorType { get; set; }

        /// <summary>
        /// 远程异常消息。
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 远程异常堆栈。
        /// </summary>
        public string ErrorStackTrace { get; set; }
    }

    /// <summary>
    /// 远程调用回写参数项。
    /// </summary>
    public sealed class OutputArgument
    {
        /// <summary>
        /// 对应请求 <see cref="RemoteInvocationRequest.Arguments"/> 列表中的参数索引。
        /// </summary>
        public int ArgumentIndex { get; set; }

        /// <summary>
        /// 回写值。反序列化后为 <see cref="JsonElement"/>，调用方根据 <see cref="IArgumentOutHandler.ReturnType"/> 进行反序列化。
        /// </summary>
        public object Value { get; set; }
    }

    /// <summary>
    /// 类型包装值。当实际值类型与预期类型不一致时使用，携带实际类型名与值。
    /// 序列化为 <c>{"$type":"类型名","$value":<值>}</c> 结构。
    /// </summary>
    public sealed class TypeWrappedValue
    {
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

    /// <summary>
    /// 服务名称工具类。客户端与服务端共用，确保 ServiceName 生成逻辑一致。
    /// </summary>
    public static class RemoteServiceNameUtil
    {
        /// <summary>
        /// 获取或设置是否使用短类型名作为服务名。默认为 true。
        /// 为 false 时使用类型全名（含命名空间），避免跨程序集同名接口冲突。
        /// </summary>
        public static bool UseShortTypeName { get; set; } = true;

        /// <summary>
        /// 从服务接口类型生成服务名称。
        /// </summary>
        public static string GetServiceName(Type serviceType)
        {
            if (serviceType is null) return string.Empty;
            if (serviceType.IsGenericType)
            {
                int backtickIndex = serviceType.Name.IndexOf('`');
                var baseName = backtickIndex > 0
                    ? serviceType.Name.Substring(0, backtickIndex)
                    : serviceType.Name;
                var argNames = serviceType.GetGenericArguments().Select(t => UseShortTypeName ? t.Name : t.FullName);
                return baseName + "<" + string.Join(",", argNames) + ">";
            }
            return UseShortTypeName ? serviceType.Name : serviceType.FullName;
        }
    }
}
