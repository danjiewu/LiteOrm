using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm 自动注册中心。
    /// <para>
    /// LiteOrm.Generators 源生成器在编译期扫描带 <c>[AutoRegister]</c> 特性的自定义服务与 DAO，
    /// 生成注册代码，并通过模块初始化器登记到本类；<see cref="LiteOrmServiceExtensions.AddLiteOrm(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>
    /// 在启用自动注册时统一应用到 <see cref="IServiceCollection"/>。
    /// </para>
    /// </summary>
    public static class LiteOrmAutoRegistration
    {
        private static readonly object _syncRoot = new object();
        private static readonly List<Action<IServiceCollection>> _registrations = new List<Action<IServiceCollection>>();

        /// <summary>
        /// 登记一条自动注册回调（由 LiteOrm.Generators 生成的模块初始化器调用）。
        /// </summary>
        /// <param name="registration">接收 <see cref="IServiceCollection"/> 的注册动作。</param>
        public static void Add(Action<IServiceCollection> registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            lock (_syncRoot)
            {
                _registrations.Add(registration);
            }
        }

        /// <summary>
        /// 将已登记的所有注册回调应用到 <paramref name="services"/>。
        /// </summary>
        /// <param name="services">服务集合。</param>
        public static void Apply(IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            Action<IServiceCollection>[] snapshot;
            lock (_syncRoot)
            {
                snapshot = _registrations.ToArray();
            }
            foreach (var registration in snapshot)
            {
                registration(services);
            }
        }
    }
}
