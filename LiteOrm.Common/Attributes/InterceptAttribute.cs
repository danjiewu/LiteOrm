using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// 标记需要被 AOP 拦截的服务类，并指定拦截器类型
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public class InterceptAttribute : Attribute
    {
        /// <summary>
        /// 拦截器类型
        /// </summary>
        public Type InterceptorType { get; }

        /// <summary>
        /// 初始化 <see cref="InterceptAttribute"/> 类的新实例
        /// </summary>
        /// <param name="interceptorType">拦截器类型，必须实现 Castle.Core IInterceptor 或 IAsyncInterceptor</param>
        public InterceptAttribute(Type interceptorType)
        {
            InterceptorType = interceptorType ?? throw new ArgumentNullException(nameof(interceptorType));
        }
    }
}