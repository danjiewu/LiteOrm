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
        /// <para>
        /// 解析优先级：
        /// 1. 若特性本身实现 <see cref="IArgumentOutHandler"/>（如 <see cref="IdentityOutAttribute"/>、<see cref="CopyableOutAttribute"/>），
        ///    直接返回特性实例，无需额外实例化；
        /// 2. 通过带 <c>Type</c> 参数的构造函数创建，
        ///    将 <see cref="ArgumentOutAttribute.ReturnType"/> 作为构造参数传入。
        /// </para>
        /// </summary>
        /// <param name="attribute">参数输出特性。</param>
        /// <returns>处理器实例；若无法创建则返回 null。</returns>
        public static IArgumentOutHandler Resolve(ArgumentOutAttribute attribute)
        {
            if (attribute == null)
                return null;

            // 1. 特性本身实现 IArgumentOutHandler（如 IdentityOutAttribute/CopyableOutAttribute），直接返回
            if (attribute is IArgumentOutHandler selfHandler)
                return selfHandler;

            if (attribute.HandlerType == null)
                return null;

            // 2. 通过带 Type 参数的构造函数实例化
            var ctorWithType = attribute.HandlerType.GetConstructor(new[] { typeof(Type) });
            if (ctorWithType != null)
                return Activator.CreateInstance(attribute.HandlerType, attribute.ReturnType) as IArgumentOutHandler;

            return null;
        }

        /// <summary>
        /// 解析并创建回写处理器实例（DI 优先）。
        /// <para>
        /// 解析优先级：
        /// 1. 若特性本身实现 <see cref="IArgumentOutHandler"/>（如 <see cref="IdentityOutAttribute"/>、<see cref="CopyableOutAttribute"/>），
        ///    直接返回特性实例，无需额外实例化；
        /// 2. 通过 <paramref name="serviceProvider"/> 从 DI 容器解析（若 <see cref="ArgumentOutAttribute.HandlerType"/> 已注册）；
        /// 3. 通过带 <c>Type</c> 参数的构造函数创建，
        ///    将 <see cref="ArgumentOutAttribute.ReturnType"/> 作为构造参数传入。
        /// </para>
        /// </summary>
        /// <param name="attribute">参数输出特性。</param>
        /// <param name="serviceProvider">服务提供者，用于 DI 解析（可空）。</param>
        /// <returns>处理器实例；若无法创建则返回 null。</returns>
        public static IArgumentOutHandler Resolve(ArgumentOutAttribute attribute, IServiceProvider serviceProvider)
        {
            if (attribute == null)
                return null;

            // 1. 特性本身实现 IArgumentOutHandler（如 IdentityOutAttribute/CopyableOutAttribute），直接返回
            if (attribute is IArgumentOutHandler selfHandler)
                return selfHandler;

            if (attribute.HandlerType == null)
                return null;

            // 2. DI 优先：尝试从 DI 容器解析
            if (serviceProvider != null)
            {
                var diInstance = serviceProvider.GetService(attribute.HandlerType);
                if (diInstance is IArgumentOutHandler diHandler)
                    return diHandler;
            }

            // 3. 通过带 Type 参数的构造函数实例化
            var ctorWithType = attribute.HandlerType.GetConstructor(new[] { typeof(Type) });
            if (ctorWithType != null)
                return Activator.CreateInstance(attribute.HandlerType, attribute.ReturnType) as IArgumentOutHandler;

            return null;
        }
    }
}
