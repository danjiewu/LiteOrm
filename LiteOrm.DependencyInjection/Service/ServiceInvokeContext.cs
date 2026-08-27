using System;
using System.Reflection;

namespace LiteOrm.Service
{
    /// <summary>
    /// 服务调用上下文，承载一次服务方法调用的元数据与结果，供服务调用事件订阅者使用。
    /// </summary>
    /// <remarks>
    /// 由 <c>ServiceInvokeInterceptor</c> 在服务方法调用生命周期内创建并填充：
    /// 调用前的上下文中 <see cref="Duration"/> 与 <see cref="Result"/> 尚未赋值；
    /// 调用成功返回时会对二者进行回填。
    /// </remarks>
    public class ServiceInvokeContext : EventArgs
    {
        /// <summary>
        /// 初始化 <see cref="ServiceInvokeContext"/> 类的新实例。
        /// </summary>
        /// <param name="serviceType">服务类型。</param>
        /// <param name="serviceName">服务名。</param>
        /// <param name="method">被调用的方法。</param>
        /// <param name="arguments">方法的原始参数。</param>
        /// <param name="sessionId">当前会话 ID；无会话时为 null。</param>
        public ServiceInvokeContext(Type serviceType, string serviceName, MethodInfo method, object?[] arguments, string? sessionId)
        {
            ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
            ServiceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
            Method = method ?? throw new ArgumentNullException(nameof(method));
            MethodName = method.Name;
            Arguments = arguments ?? Array.Empty<object?>();
            SessionId = sessionId;
        }

        /// <summary>
        /// 服务类型。
        /// </summary>
        public Type ServiceType { get; }

        /// <summary>
        /// 服务名。
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        /// 被调用的方法。
        /// </summary>
        public MethodInfo Method { get; }

        /// <summary>
        /// 方法名。
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// 方法的原始参数（不进行掩码处理）。
        /// </summary>
        public object?[] Arguments { get; }

        /// <summary>
        /// 当前会话 ID；无会话时为 null。
        /// </summary>
        public string? SessionId { get; }

        /// <summary>
        /// 方法执行耗时（仅调用成功返回后有效）。
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// 方法返回值（仅调用成功返回后有效）。
        /// </summary>
        public object? Result { get; set; }
    }
}