using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// 标记服务在通过依赖注入解析接口时需要应用拦截器。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
    public sealed class InterceptAttribute : Attribute
    {
        /// <summary>
        /// 要应用的拦截器类型列表。
        /// </summary>
        public Type[] InterceptorTypes { get; }

        /// <summary>
        /// 使用一个或多个拦截器初始化属性。
        /// </summary>
        /// <param name="interceptorTypes">拦截器类型列表。</param>
        public InterceptAttribute(params Type[] interceptorTypes)
        {
            InterceptorTypes = interceptorTypes ?? Array.Empty<Type>();
        }
    }
}
