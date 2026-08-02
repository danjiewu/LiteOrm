using Castle.DynamicProxy;
using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace LiteOrm.Framework
{
    /// <summary>
    /// MS DI + Castle DynamicProxy 拦截器扩展方法。
    /// </summary>
    /// <remarks>
    /// 为不使用 Autofac 容器的用户提供 Castle DynamicProxy 拦截能力。
    /// 通过 <see cref="AddCastleInterception(IServiceCollection)"/> 注册拦截器，
    /// 通过 <see cref="AddServiceGenerator{TService}"/> 注册服务工厂代理。
    /// </remarks>
    public static class CastleServiceExtensions
    {
        private static readonly ProxyGenerator _proxyGenerator = new ProxyGenerator();

        /// <summary>
        /// 注册 Castle 拦截器到 MS DI 容器。
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <returns>服务集合。</returns>
        public static IServiceCollection AddCastleInterception(this IServiceCollection services)
        {
            services.AddScoped<ServiceInvokeInterceptor>();
            services.AddScoped<ServiceGenerateInterceptor>();
            services.AddSingleton(_proxyGenerator);
            return services;
        }

        /// <summary>
        /// 注册服务生成器接口代理。
        /// </summary>
        /// <typeparam name="TService">服务工厂接口类型。</typeparam>
        /// <param name="services">服务集合。</param>
        /// <param name="lifetime">服务生命周期。</param>
        /// <returns>服务集合。</returns>
        /// <remarks>
        /// 使用 Castle DynamicProxy 创建接口代理（无目标对象），
        /// 拦截器从服务提供者解析返回类型对应的服务实例。
        /// </remarks>
        public static IServiceCollection AddServiceGenerator<TService>(
            this IServiceCollection services,
            ServiceLifetime lifetime = ServiceLifetime.Scoped) where TService : class
        {
            services.Add(new ServiceDescriptor(typeof(TService), sp =>
            {
                var interceptor = new ServiceFactoryInterceptor(sp);
                return _proxyGenerator.CreateInterfaceProxyWithoutTarget<TService>(interceptor);
            }, lifetime));

            return services;
        }

        /// <summary>
        /// 创建指定接口类型的拦截代理（带目标对象）。
        /// </summary>
        /// <typeparam name="T">接口类型。</typeparam>
        /// <param name="serviceProvider">服务提供者。</param>
        /// <param name="target">目标实现实例。</param>
        /// <returns>拦截代理实例。</returns>
        public static T CreateInterceptedProxy<T>(this IServiceProvider serviceProvider, T target) where T : class
        {
            var interceptor = serviceProvider.GetService(typeof(ServiceInvokeInterceptor)) as IInterceptor;
            if (interceptor == null)
                return target;
            return _proxyGenerator.CreateInterfaceProxyWithTarget(target, interceptor);
        }
    }
}
