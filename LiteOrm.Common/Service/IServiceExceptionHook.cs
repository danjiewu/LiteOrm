namespace LiteOrm.Service
{
    /// <summary>
    /// 服务异常 hook。
    /// </summary>
    public interface IServiceExceptionHook
    {
        /// <summary>
        /// 当服务方法抛出异常时执行。
        /// </summary>
        void OnException(ServiceExceptionContext context);
    }
}
