using Castle.DynamicProxy;
using LiteOrm.Common;
using LiteOrm.Service;
using System.Collections;

namespace LiteOrm.Remote
{
    /// <summary>
    /// 远程实体视图服务同步代理。通过 <see cref="ProxyGenerator"/> 为 <see cref="IEntityViewService{T}"/>
    /// 创建动态代理，所有方法调用由 <see cref="RemoteServiceInvokeInterceptor"/> 拦截并转发到远程服务端。
    /// <para>
    /// 本类通过 <c>RegisterGeneric(typeof(RemoteViewServiceProxy&lt;&gt;))</c> 注册为 <c>IEntityViewService&lt;&gt;</c> 的实现。
    /// </para>
    /// </summary>
    /// <typeparam name="T">实体视图类型</typeparam>
    public class RemoteViewServiceProxy<T> : IEntityViewService<T> where T : class
    {
        private readonly IEntityViewService<T> _proxy;

        /// <summary>
        /// 初始化 <see cref="RemoteViewServiceProxy{T}"/> 类的新实例。
        /// </summary>
        /// <param name="sp">远程调用拦截器的服务提供者，用于获取<seealso cref="RemoteServiceInvokeInterceptor"/>远程代理拦截器。</param>
        public RemoteViewServiceProxy(IServiceProvider sp)
        {
            _proxy = RemoteProxyGenerator.CreateRemoteServiceProxy<IEntityViewService<T>>(sp);
        }
        /// <inheritdoc />
        public T GetObject(object id, params string[] tableArgs) => _proxy.GetObject(id, tableArgs);

        /// <inheritdoc />
        public T SearchOne(Expr? expr, params string[]? tableArgs) => _proxy.SearchOne(expr, tableArgs);

        /// <inheritdoc />
        public void ForEach(Expr expr, Action<T> func, params string[] tableArgs) => _proxy.ForEach(expr, func, tableArgs);

        /// <inheritdoc />
        public List<T> Search(Expr? expr = null, params string[]? tableArgs) => _proxy.Search(expr, tableArgs);

        /// <inheritdoc />
        public List<TResult> SearchAs<TResult>(SelectExpr selectExpr, params string[] tableArgs) => _proxy.SearchAs<TResult>(selectExpr, tableArgs);

        /// <inheritdoc />
        public TResult SearchOneAs<TResult>(SelectExpr selectExpr, params string[] tableArgs) => _proxy.SearchOneAs<TResult>(selectExpr, tableArgs);

        /// <inheritdoc />
        public bool ExistsID(object id, params string[] tableArgs) => _proxy.ExistsID(id, tableArgs);

        /// <inheritdoc />
        public bool Exists(Expr? expr, params string[]? tableArgs) => _proxy.Exists(expr, tableArgs);

        /// <inheritdoc />
        public int Count(Expr? expr = null, params string[]? tableArgs) => _proxy.Count(expr, tableArgs);

        /// <inheritdoc />
        object IEntityViewService.GetObject(object id, params string[] tableArgs) => ((IEntityViewService)_proxy).GetObject(id, tableArgs);

        /// <inheritdoc />
        object IEntityViewService.SearchOne(Expr? expr, params string[]? tableArgs) => ((IEntityViewService)_proxy).SearchOne(expr, tableArgs);

        /// <inheritdoc />
        IList IEntityViewService.Search(Expr? expr, params string[] tableArgs) => ((IEntityViewService)_proxy).Search(expr, tableArgs);
    }
}
