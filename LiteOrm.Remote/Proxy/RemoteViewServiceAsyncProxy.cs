using Castle.DynamicProxy;
using LiteOrm.Common;
using LiteOrm.Service;
using System.Collections;

namespace LiteOrm.Remote
{
    /// <summary>
    /// 远程实体视图服务异步代理。通过 <see cref="ProxyGenerator"/> 为 <see cref="IEntityViewServiceAsync{T}"/>
    /// 创建动态代理，所有方法调用由 <see cref="RemoteServiceInvokeInterceptor"/> 拦截并转发到远程服务端。
    /// <para>
    /// 本类通过 <c>RegisterGeneric(typeof(RemoteViewServiceAsyncProxy&lt;&gt;))</c> 注册为 <c>IEntityViewServiceAsync&lt;&gt;</c> 的实现。
    /// </para>
    /// </summary>
    /// <typeparam name="T">实体视图类型</typeparam>
    public class RemoteViewServiceAsyncProxy<T> : IEntityViewServiceAsync<T> where T : class
    {
        private readonly IEntityViewServiceAsync<T> _proxy;

        /// <summary>
        /// 初始化 <see cref="RemoteViewServiceAsyncProxy{T}"/> 类的新实例。
        /// </summary>
        /// <param name="interceptor">远程调用拦截器，用于拦截代理方法调用并转发到远程服务端。</param>
        public RemoteViewServiceAsyncProxy(RemoteServiceInvokeInterceptor interceptor)
        {
            var generator = new ProxyGenerator();
            _proxy = generator.CreateInterfaceProxyWithoutTarget<IEntityViewServiceAsync<T>>(interceptor.ToInterceptor());
        }

        /// <inheritdoc />
        public Task<T> GetObjectAsync(object id, string[] tableArgs = null, CancellationToken cancellationToken = default)
            => _proxy.GetObjectAsync(id, tableArgs, cancellationToken);

        /// <inheritdoc />
        public Task<T> SearchOneAsync(Expr expr, string[] tableArgs = null, CancellationToken cancellationToken = default)
            => _proxy.SearchOneAsync(expr, tableArgs, cancellationToken);

        /// <inheritdoc />
        public Task ForEachAsync(Expr expr, Func<T, Task> func, string[] tableArgs = null, CancellationToken cancellationToken = default)
            => _proxy.ForEachAsync(expr, func, tableArgs, cancellationToken);

        /// <inheritdoc />
        public Task<List<T>> SearchAsync(Expr expr = null, string[] tableArgs = null, CancellationToken cancellationToken = default)
            => _proxy.SearchAsync(expr, tableArgs, cancellationToken);

        /// <inheritdoc />
        public Task<List<TResult>> SearchAsAsync<TResult>(SelectExpr selectExpr = null, params string[] tableArgs)
            => _proxy.SearchAsAsync<TResult>(selectExpr, tableArgs);

        /// <inheritdoc />
        public Task<TResult> SearchOneAsAsync<TResult>(SelectExpr selectExpr = null, params string[] tableArgs)
            => _proxy.SearchOneAsAsync<TResult>(selectExpr, tableArgs);

        /// <inheritdoc />
        public Task<bool> ExistsIDAsync(object id, string[] tableArgs = null, CancellationToken cancellationToken = default)
            => _proxy.ExistsIDAsync(id, tableArgs, cancellationToken);

        /// <inheritdoc />
        public Task<bool> ExistsAsync(Expr expr, string[] tableArgs = null, CancellationToken cancellationToken = default)
            => _proxy.ExistsAsync(expr, tableArgs, cancellationToken);

        /// <inheritdoc />
        public Task<int> CountAsync(Expr expr = null, string[] tableArgs = null, CancellationToken cancellationToken = default)
            => _proxy.CountAsync(expr, tableArgs, cancellationToken);

        /// <inheritdoc />
        Task<object> IEntityViewServiceAsync.GetObjectAsync(object id, string[] tableArgs, CancellationToken cancellationToken)
            => ((IEntityViewServiceAsync)_proxy).GetObjectAsync(id, tableArgs, cancellationToken);

        /// <inheritdoc />
        Task<object> IEntityViewServiceAsync.SearchOneAsync(Expr expr, string[] tableArgs, CancellationToken cancellationToken)
            => ((IEntityViewServiceAsync)_proxy).SearchOneAsync(expr, tableArgs, cancellationToken);

        /// <inheritdoc />
        Task<IList> IEntityViewServiceAsync.SearchAsync(Expr expr, string[] tableArgs, CancellationToken cancellationToken)
            => ((IEntityViewServiceAsync)_proxy).SearchAsync(expr, tableArgs, cancellationToken);
    }
}
