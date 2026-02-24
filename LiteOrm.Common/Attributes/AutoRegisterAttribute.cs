using Microsoft.Extensions.DependencyInjection;
using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// 自动注册特性，用于标记需要自动注册到依赖注入容器的类或接口
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
    public class AutoRegisterAttribute : Attribute
    {
        /// <summary>
        /// 服务生命周期，默认为Transient
        /// </summary>
        public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Singleton;

        /// <summary>
        /// 支持多个服务类型
        /// </summary>
        public Type[] ServiceTypes { get; set; }

        /// <summary>
        /// 是否启用自动注册
        /// </summary>
        public bool Enabled { get; } = true;

        /// <summary>
        /// 服务唯一标识
        /// </summary>
        public object Key { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public AutoRegisterAttribute() { }

        /// <summary>
        /// 构造函数，指定是否启用自动注册
        /// </summary>
        /// <param name="enabled">是否启用自动注册</param>
        public AutoRegisterAttribute(bool enabled) { Enabled = enabled; }

        /// <summary>
        /// 构造函数，指定服务生命周期
        /// </summary>
        /// <param name="lifetime">服务生命周期</param>
        public AutoRegisterAttribute(ServiceLifetime lifetime) => Lifetime = lifetime;

        /// <summary>
        /// 构造函数，指定服务类型
        /// </summary>
        /// <param name="serviceTypes">服务类型数组</param>
        public AutoRegisterAttribute(params Type[] serviceTypes) => ServiceTypes = serviceTypes;

        /// <summary>
        /// 构造函数，指定服务生命周期和服务类型
        /// </summary>
        /// <param name="lifetime">服务生命周期</param>
        /// <param name="serviceTypes">服务类型数组</param>
        public AutoRegisterAttribute(ServiceLifetime lifetime, params Type[] serviceTypes)
        {
            Lifetime = lifetime;
            ServiceTypes = serviceTypes;
        }
    }
}
