using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// 自动注册特性，用于标记需要自动注册到依赖注入容器的类或接口。
    /// <para>LiteOrm.Generators 源生成器在编译期扫描带 <c>[AutoRegister]</c> 特性的类型并生成注册代码；
    /// LiteOrm.DependencyInjection 的 Autofac 扫描亦读取本特性进行运行时注册。</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
    public class AutoRegisterAttribute : Attribute
    {
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
        public AutoRegisterAttribute(Lifetime lifetime) => Lifetime = lifetime;

        /// <summary>
        /// 构造函数，指定注册的服务类型范围
        /// </summary>
        /// <param name="policy">注册的服务类型范围</param>
        public AutoRegisterAttribute(RegisterPolicy policy) => Policy = policy;

        /// <summary>
        /// 构造函数，指定服务生命周期和注册的服务类型范围
        /// </summary>
        /// <param name="lifetime">服务生命周期</param>
        /// <param name="serviceTypes">注册的服务类型范围</param>
        public AutoRegisterAttribute(Lifetime lifetime, RegisterPolicy serviceTypes)
        {
            Lifetime = lifetime;
            Policy = serviceTypes;
        }
        /// <summary>
        /// 服务生命周期，默认为 Singleton
        /// </summary>
        public Lifetime Lifetime { get; set; } = Lifetime.Singleton;

        /// <summary>
        /// 注册的服务类型范围，默认为 All（实现类型自身及其接口）。
        /// </summary>
        public RegisterPolicy Policy { get; set; } = RegisterPolicy.All;

        /// <summary>
        /// 是否启用自动注册
        /// </summary>
        public bool Enabled { get; } = true;

        /// <summary>
        /// 服务唯一标识
        /// </summary>
        public object? Key { get; set; }

        /// <summary>
        /// 是否自动激活服务（即在容器构建完成后立即解析实例），默认为 false
        /// </summary>
        public bool AutoActivate { get; set; }
    }
}
