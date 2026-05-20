using LiteOrm.Service;
using System;

namespace LiteOrm
{
    /// <summary>
    /// 为服务方法声明异常 hook。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface, Inherited = true, AllowMultiple = true)]
    public class ExceptionHookAttribute : Attribute
    {
        /// <summary>
        /// 使用指定 hook 类型初始化 <see cref="ExceptionHookAttribute"/>。
        /// </summary>
        public ExceptionHookAttribute(Type hookType)
        {
            if (hookType is null) throw new ArgumentNullException(nameof(hookType));
            if (!typeof(IServiceExceptionHook).IsAssignableFrom(hookType))
                throw new ArgumentException($"{hookType.FullName} must implement {typeof(IServiceExceptionHook).FullName}", nameof(hookType));

            HookType = hookType;
        }

        /// <summary>
        /// hook 类型。
        /// </summary>
        public Type HookType { get; }

        /// <summary>
        /// hook 模式。
        /// </summary>
        public ServiceExceptionHookMode Mode { get; set; } = ServiceExceptionHookMode.Notify;
    }

    /// <summary>
    /// Service 异常 hook 的处理模式。
    /// </summary>
    public enum ServiceExceptionHookMode
    {
        /// <summary>
        /// 仅通知，不允许将异常标记为已处理。
        /// </summary>
        Notify = 0,

        /// <summary>
        /// 允许将异常标记为已处理，并提供自定义返回结果。
        /// </summary>
        Handle = 1
    }
}
