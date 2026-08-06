using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// 标记需要拦截的服务类型。由 DI 适配器（LiteOrm.DependencyInjection）读取并应用拦截。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true, AllowMultiple = true)]
    public class InterceptAttribute : Attribute
    {
        /// <summary>
        /// 拦截器类型
        /// </summary>
        public Type InterceptorType { get; }

        /// <summary>
        /// 初始化 <see cref="InterceptAttribute"/> 类的新实例。
        /// </summary>
        /// <param name="interceptorType">拦截器类型</param>
        public InterceptAttribute(Type interceptorType)
        {
            InterceptorType = interceptorType ?? throw new ArgumentNullException(nameof(interceptorType));
        }
    }
}
