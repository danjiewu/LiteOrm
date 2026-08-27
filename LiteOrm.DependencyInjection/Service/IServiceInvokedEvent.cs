namespace LiteOrm.Service
{
    /// <summary>
    /// 服务方法成功返回后事件接口。
    /// </summary>
    /// <remarks>
    /// 由订阅者实现并通过依赖注入注册，<c>ServiceInvokeInterceptor</c> 在服务方法成功返回后调用
    /// <see cref="OnInvoked"/>，此时 <see cref="ServiceInvokeContext.Duration"/> 与
    /// <see cref="ServiceInvokeContext.Result"/> 已回填。可用于耗时/结果统计、审计等。
    /// 该回调为通知性质（不影响调用流程）。
    /// </remarks>
    public interface IServiceInvokedEvent
    {
        /// <summary>
        /// 服务方法成功返回后回调。
        /// </summary>
        /// <param name="context">服务调用上下文，已回填耗时与返回值。</param>
        void OnInvoked(ServiceInvokeContext context);
    }
}