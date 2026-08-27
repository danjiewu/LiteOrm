namespace LiteOrm.Service
{
    /// <summary>
    /// 服务方法执行前事件接口。
    /// </summary>
    /// <remarks>
    /// 由订阅者实现并通过依赖注入注册，<c>ServiceInvokeInterceptor</c> 在服务方法执行前调用
    /// <see cref="OnInvoking"/>。可用于指标埋点、前置校验等。
    /// 该回调为通知性质（不返回结果，不影响调用流程）。
    /// </remarks>
    public interface IServiceInvokingEvent
    {
        /// <summary>
        /// 服务方法执行前回调。
        /// </summary>
        /// <param name="context">服务调用上下文。</param>
        void OnInvoking(ServiceInvokeContext context);
    }
}