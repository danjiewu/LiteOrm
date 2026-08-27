namespace LiteOrm.Service
{
    /// <summary>
    /// 服务方法异常事件接口。
    /// </summary>
    /// <remarks>
    /// 由订阅者实现并通过依赖注入注册，<c>ServiceInvokeInterceptor</c> 在服务方法抛出异常后调用
    /// <see cref="OnException"/>。可用于统一告警、补登与指标统计。
    /// 该回调为通知性质（不影响调用流程）；异常仍会原样抛出。
    /// </remarks>
    public interface IServiceExceptionEvent
    {
        /// <summary>
        /// 服务方法抛出异常后的回调。
        /// </summary>
        /// <param name="context">服务异常上下文。</param>
        void OnException(ServiceExceptionContext context);
    }
}