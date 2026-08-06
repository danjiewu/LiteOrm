using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm.Remote
{
    /// <summary>
    /// 提供基于共享 <see cref="ProxyGenerator"/> 单例的远程代理创建方法。
    /// </summary>
    /// <remarks>
    /// Castle DynamicProxy 会在 <see cref="ProxyGenerator"/> 实例内部缓存生成的代理类型，
    /// 复用同一实例可避免每次创建代理时重复生成类型，显著提升性能。
    /// 所有远程代理的创建均应通过本类的静态方法进行，确保类型缓存被充分复用。
    /// </remarks>
    public static class RemoteProxyGenerator
    {
        /// <summary>
        /// 全局共享的 <see cref="ProxyGenerator"/> 实例。
        /// </summary>
        private static readonly ProxyGenerator _proxyGenerator = new ProxyGenerator();

        /// <summary>
        /// 为指定接口类型创建无目标对象的动态代理，所有方法调用由 <paramref name="sp"/> 提供的拦截器拦截并转发至服务端。
        /// </summary>
        /// <typeparam name="T">要代理的接口类型。</typeparam>
        /// <param name="sp">提供<seealso cref="RemoteServiceInvokeInterceptor"/>拦截器的服务提供者。</param>
        /// <returns>实现了 <typeparamref name="T"/> 接口的代理实例。</returns>
        public static T CreateRemoteServiceProxy<T>(IServiceProvider sp)
            where T : class
        {
            return _proxyGenerator.CreateInterfaceProxyWithoutTarget<T>(sp.GetRequiredService<RemoteServiceInvokeInterceptor>());
        }

        /// <summary>
        /// 为指定接口类型创建无目标对象的动态代理，所有方法调用由 <paramref name="remoteServiceInvokeInterceptor"/> 拦截器拦截并转发至服务端。
        /// </summary>
        /// <typeparam name="T">要代理的接口类型。</typeparam>
        /// <param name="remoteServiceInvokeInterceptor"> 转发请求到服务端的拦截器。
        /// <seealso cref="RemoteServiceInvokeInterceptor"/></param>
        /// <returns>实现了 <typeparamref name="T"/> 接口的代理实例。</returns>
        public static T CreateRemoteServiceProxy<T>(RemoteServiceInvokeInterceptor remoteServiceInvokeInterceptor)
            where T : class
        {
            return _proxyGenerator.CreateInterfaceProxyWithoutTarget<T>(remoteServiceInvokeInterceptor);
        }

        /// <summary>
        /// 为指定接口类型创建无目标对象的动态代理，所有方法调用由 <paramref name="sp"/> 提供的拦截器拦截。
        /// </summary>
        /// <param name="interfaceType">要代理的接口类型。</param>
        /// <param name="sp">提供<seealso cref="RemoteServiceInvokeInterceptor"/>拦截器的服务注册类。</param>
        /// <returns>实现了 <paramref name="interfaceType"/> 接口的代理实例。</returns>
        public static object CreateRemoteServiceProxy(Type interfaceType, IServiceProvider sp)
        {
            return _proxyGenerator.CreateInterfaceProxyWithoutTarget(interfaceType, sp.GetRequiredService<RemoteServiceInvokeInterceptor>());
        }

        /// <summary>
        /// 创建接口代理类
        /// </summary>
        /// <typeparam name="T">要代理的接口类型。</typeparam>
        /// <param name="interceptor">拦截器。</param>
        /// <returns>实现了 <typeparamref name="T"/> 接口的代理实例。</returns>
        public static T CreateInterfaceProxy<T>(IInterceptor interceptor) where T : class
        {
            return _proxyGenerator.CreateInterfaceProxyWithoutTarget<T>(interceptor);
        }
    }
}
