using Autofac;
using Autofac.Builder;
using Autofac.Core;
using Castle.DynamicProxy;
using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LiteOrm
{
    /// <summary>
    /// 远程服务代理注册源。作为 Autofac <see cref="IRegistrationSource"/> 实现，
    /// 按需为指定的开放泛型接口（如 <c>IEntityServiceAsync&lt;&gt;</c>、<c>IEntityViewServiceAsync&lt;&gt;</c>）
    /// 及其派生接口创建远程调用动态代理。
    /// <para>
    /// 工作原理：
    /// <list type="number">
    /// <item>当容器尝试解析未注册的服务接口时，Autofac 会询问所有 <see cref="IRegistrationSource"/>；</item>
    /// <item>本源检查请求类型是否为指定开放泛型接口的闭合构造（如 <c>IEntityServiceAsync&lt;User&gt;</c>），
    ///       或是否实现了这些开放泛型接口的自定义接口（如 <c>IDemoUserService</c>）；</item>
    /// <item>匹配则通过 <see cref="ProxyGenerator.CreateInterfaceProxyWithoutTarget"/> 创建无目标代理，
    ///       由 <see cref="RemoteServiceInvokeInterceptor"/> 拦截并转发到远程服务端；</item>
    /// <item>已注册的服务不会被覆盖。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 此方式无需扫描 <c>[Table]</c> 特性逐个注册实体类型，
    /// 任何 <c>IEntityServiceAsync&lt;T&gt;</c> / <c>IEntityViewServiceAsync&lt;T&gt;</c> 的闭合构造
    /// 均可按需从容器解析。
    /// </para>
    /// </summary>
    public class RemoteServiceProxyRegistrationSource : IRegistrationSource
    {
        private readonly HashSet<Type> _openGenericInterfaces;
        private readonly ServiceLifetime _lifetime;

        /// <summary>
        /// 初始化 <see cref="RemoteServiceProxyRegistrationSource"/> 类的新实例。
        /// </summary>
        /// <param name="openGenericInterfaces">需要代理的开放泛型接口定义集合（如 <c>IEntityServiceAsync&lt;&gt;</c>）。</param>
        /// <param name="lifetime">代理服务生命周期，默认为 <see cref="ServiceLifetime.Scoped"/>。</param>
        public RemoteServiceProxyRegistrationSource(
            IEnumerable<Type> openGenericInterfaces,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            _openGenericInterfaces = new HashSet<Type>(openGenericInterfaces);
            _lifetime = lifetime;
        }

        /// <inheritdoc />
        public bool IsAdapterForIndividualComponents => false;

        /// <inheritdoc />
        public IEnumerable<IComponentRegistration> RegistrationsFor(
            Autofac.Core.Service service,
            Func<Autofac.Core.Service, IEnumerable<Autofac.Core.ServiceRegistration>> registrationAccessor)
        {
            if (service is not IServiceWithType swt)
                yield break;

            var serviceType = swt.ServiceType;
            if (!serviceType.IsInterface)
                yield break;

            if (!ShouldHandle(serviceType))
                yield break;

            // 已注册的服务不覆盖
            if (registrationAccessor(service).Any())
                yield break;

            var capturedType = serviceType;

            var rb = RegistrationBuilder.ForDelegate(serviceType, (c, p) =>
            {
                var interceptor = c.Resolve<RemoteServiceInvokeInterceptor>();
                return new ProxyGenerator().CreateInterfaceProxyWithoutTarget(
                    capturedType, interceptor.ToInterceptor());
            })
            .As(serviceType);

            rb = _lifetime switch
            {
                ServiceLifetime.Singleton => rb.SingleInstance(),
                ServiceLifetime.Scoped => rb.InstancePerLifetimeScope(),
                ServiceLifetime.Transient => rb.InstancePerDependency(),
                _ => rb.InstancePerLifetimeScope(),
            };

            yield return rb.CreateRegistration();
        }

        /// <summary>
        /// 判断服务类型是否应由本源处理：
        /// 1. 直接是指定开放泛型接口的闭合构造（如 <c>IEntityServiceAsync&lt;User&gt;</c>）；
        /// 2. 实现了任一指定开放泛型接口的自定义接口（如 <c>IDemoUserService</c> 继承自 <c>IEntityServiceAsync&lt;DemoUser&gt;</c>）。
        /// </summary>
        private bool ShouldHandle(Type serviceType)
        {
            // 情况1：直接是开放泛型接口的闭合构造
            if (serviceType.IsGenericType)
            {
                var genericDef = serviceType.GetGenericTypeDefinition();
                if (_openGenericInterfaces.Contains(genericDef))
                    return true;
            }

            // 情况2：自定义接口实现了任一开放泛型接口
            // 排除开放泛型接口定义自身（IEntityServiceAsync<> 不应处理）
            if (serviceType.IsGenericTypeDefinition)
                return false;

            foreach (var iface in serviceType.GetInterfaces())
            {
                if (iface.IsGenericType && _openGenericInterfaces.Contains(iface.GetGenericTypeDefinition()))
                    return true;
            }

            return false;
        }
    }
}
