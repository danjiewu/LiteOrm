using System;
using System.Collections.Generic;
using System.Linq;

namespace LiteOrm.Service
{
    /// <summary>
    /// 远程调用请求。
    /// </summary>
    public sealed class RemoteInvocationRequest
    {
        /// <summary>
        /// 服务名称。客户端与服务端使用相同的 ServiceName 进行匹配。
        /// 由 <see cref="RemoteServiceNameUtil.GetServiceName"/> 从服务接口类型生成。
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// 被调用的方法名。
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// 调用参数列表。已剔除 <see cref="System.Threading.CancellationToken"/> 等不可序列化参数。
        /// </summary>
        public IList<RemoteArgument> Arguments { get; set; } = Array.Empty<RemoteArgument>();

        /// <summary>
        /// 需要服务端回写的参数索引列表（对应 <see cref="Arguments"/> 列表中的索引）。
        /// 由客户端根据 <see cref="LiteOrm.Common.ArgumentOutAttribute"/> 标记生成。
        /// 服务端在调用完成后将这些索引对应的参数对象重新序列化放入 <see cref="RemoteInvocationResponse.WriteBackArguments"/>。
        /// </summary>
        public IList<int> WriteBackArgumentIndices { get; set; } = Array.Empty<int>();
    }

    /// <summary>
    /// 远程调用单个参数的序列化表示。
    /// </summary>
    public sealed class RemoteArgument
    {
        /// <summary>
        /// 参数类型的程序集限定名。
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// 参数值的 JSON 序列化字符串。
        /// </summary>
        public string ValueJson { get; set; }
    }

    /// <summary>
    /// 远程调用响应。
    /// </summary>
    public sealed class RemoteInvocationResponse
    {
        /// <summary>
        /// 调用是否成功。
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 返回值类型的程序集限定名（用于支持多态返回类型）。无返回值时为 null。
        /// </summary>
        public string ResultTypeName { get; set; }

        /// <summary>
        /// 返回值的 JSON 序列化字符串。无返回值时为 null。
        /// </summary>
        public string ResultJson { get; set; }

        /// <summary>
        /// 远程抛出异常的类型全名。仅当 <see cref="Success"/> 为 false 时有值。
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

        /// <summary>
        /// 需要回写到客户端的参数列表。每个元素对应请求 <see cref="RemoteInvocationRequest.Arguments"/> 中的一个参数（按索引）。
        /// 仅当请求中标记了 <see cref="LiteOrm.Common.ArgumentOutAttribute"/> 的参数被服务端修改后才有值。
        /// </summary>
        public IList<OutputArgument> WriteBackArguments { get; set; } = Array.Empty<OutputArgument>();
    }

    /// <summary>
    /// 远程调用回写参数项。表示服务端修改后需要同步回客户端的参数值。
    /// </summary>
    public sealed class OutputArgument
    {
        /// <summary>
        /// 对应请求 <see cref="RemoteInvocationRequest.Arguments"/> 列表中的参数索引。
        /// </summary>
        public int ArgumentIndex { get; set; }

        /// <summary>
        /// 参数类型的程序集限定名。
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// 回写值的 JSON 序列化字符串（整个参数对象的序列化结果）。
        /// </summary>
        public string ValueJson { get; set; }
    }

    /// <summary>
    /// 服务名称工具类。客户端与服务端共用，确保 ServiceName 生成逻辑一致。
    /// </summary>
    public static class RemoteServiceNameUtil
    {
        /// <summary>
        /// 从服务接口类型生成服务名称。
        /// 对于非泛型类型返回类型名（如 "IRemoteCalculator"）；
        /// 对于泛型类型返回可读格式（如 "IRepository&lt;User&gt;"）。
        /// </summary>
        /// <param name="serviceType">服务接口类型。</param>
        /// <returns>服务名称。</returns>
        public static string GetServiceName(Type serviceType)
        {
            if (serviceType is null) return string.Empty;
            if (serviceType.IsGenericType)
            {
                int backtickIndex = serviceType.Name.IndexOf('`');
                var baseName = backtickIndex > 0
                    ? serviceType.Name.Substring(0, backtickIndex)
                    : serviceType.Name;
                return baseName + "<" + string.Join(",", serviceType.GetGenericArguments().Select(t => t.Name)) + ">";
            }
            return serviceType.Name;
        }
    }
}
