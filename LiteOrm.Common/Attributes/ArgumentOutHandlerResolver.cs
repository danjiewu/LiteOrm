using System;
using System.Reflection;

namespace LiteOrm.Common
{
    /// <summary>
    /// 参数输出回写处理器解析器。统一处理处理器的实例化逻辑，供客户端拦截器和服务端分发器共用。
    /// </summary>
    public static class ArgumentOutHandlerResolver
    {
        /// <summary>
        /// 解析并创建回写处理器实例。
        /// 优先从 DI 容器解析；无法解析时通过带 <c>Type</c> 参数的构造函数创建，
        /// 将 <see cref="ArgumentOutAttribute.ReturnType"/> 作为构造参数传入。
        /// </summary>
        /// <param name="attribute">参数输出特性。</param>
        /// <param name="serviceProvider">DI 服务提供者，可为 null。</param>
        /// <returns>处理器实例；若无法创建则返回 null。</returns>
        public static IArgumentOutHandler Resolve(ArgumentOutAttribute attribute, IServiceProvider serviceProvider)
        {
            if (attribute == null || attribute.HandlerType == null)
                return null;

            // 优先 DI
            if (serviceProvider != null)
            {
                var fromDi = serviceProvider.GetService(attribute.HandlerType) as IArgumentOutHandler;
                if (fromDi != null) return fromDi;
            }

            // 通过带 Type 参数的构造函数实例化
            var ctorWithType = attribute.HandlerType.GetConstructor(new[] { typeof(Type) });
            if (ctorWithType != null)
                return Activator.CreateInstance(attribute.HandlerType, attribute.ReturnType) as IArgumentOutHandler;

            return null;
        }
    }
}
