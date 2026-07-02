using Castle.DynamicProxy;
using LiteOrm.Common;
using LiteOrm.Service;
using System.Collections;

namespace LiteOrm.Remote
{
    /// <summary>
    /// 远程实体服务异步代理。通过 <see cref="ProxyGenerator"/> 为 <see cref="IEntityServiceAsync{T}"/>
    /// 创建动态代理，所有方法调用由 <see cref="RemoteServiceInvokeInterceptor"/> 拦截并转发到远程服务端。
    /// <para>
    /// 本类通过 <c>RegisterGeneric(typeof(RemoteServiceAsyncProxy&lt;&gt;))</c> 注册为 <c>IEntityServiceAsync&lt;&gt;</c> 的实现。
    /// </para>
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public class RemoteServiceAsyncProxy<T> : IEntityServiceAsync<T> where T : class
    {
        private readonly IEntityServiceAsync<T> _proxy;

        /// <summary>
        /// 初始化 <see cref="RemoteServiceAsyncProxy{T}"/> 类的新实例。
        /// </summary>
        /// <param name="interceptor">远程调用拦截器，用于拦截代理方法调用并转发到远程服务端。</param>
        public RemoteServiceAsyncProxy(RemoteServiceInvokeInterceptor interceptor)
        {
            var generator = new ProxyGenerator();
            _proxy = generator.CreateInterfaceProxyWithoutTarget<IEntityServiceAsync<T>>(interceptor.ToInterceptor());
        }

        /// <inheritdoc />
        public Task<bool> InsertAsync([IdentityOut] T entity, CancellationToken cancellationToken = default)
            => _proxy.InsertAsync(entity, cancellationToken);

        /// <inheritdoc />
        public Task<bool> InsertAsync(object entity, CancellationToken cancellationToken = default)
            => _proxy.InsertAsync(entity, cancellationToken);

        /// <inheritdoc />
        public Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default)
            => _proxy.UpdateAsync(entity, cancellationToken);

        /// <inheritdoc />
        public Task<bool> UpdateAsync(object entity, CancellationToken cancellationToken = default)
            => _proxy.UpdateAsync(entity, cancellationToken);

        /// <inheritdoc />
        public Task<bool> UpdateOrInsertAsync([IdentityOut] T entity, CancellationToken cancellationToken = default)
            => _proxy.UpdateOrInsertAsync(entity, cancellationToken);

        /// <inheritdoc />
        public Task<bool> UpdateOrInsertAsync(object entity, CancellationToken cancellationToken = default)
            => _proxy.UpdateOrInsertAsync(entity, cancellationToken);

        /// <inheritdoc />
        public Task<bool> DeleteAsync(T entity, CancellationToken cancellationToken = default)
            => _proxy.DeleteAsync(entity, cancellationToken);

        /// <inheritdoc />
        public Task<bool> DeleteIDAsync(object id, string[] tableArgs = null, CancellationToken cancellationToken = default)
            => _proxy.DeleteIDAsync(id, tableArgs, cancellationToken);

        /// <inheritdoc />
        public Task<int> DeleteAllAsync(LogicExpr expr, string[] tableArgs = null, CancellationToken cancellationToken = default)
            => _proxy.DeleteAllAsync(expr, tableArgs, cancellationToken);

        /// <inheritdoc />
        public Task<int> UpdateAllAsync(UpdateExpr expr, string[] tableArgs = null, CancellationToken cancellationToken = default)
            => _proxy.UpdateAllAsync(expr, tableArgs, cancellationToken);

        /// <inheritdoc />
        public Task BatchInsertAsync([IdentityOut(Mode = ArgumentMode.Collection)] IEnumerable<T> entities, CancellationToken cancellationToken = default)
            => _proxy.BatchInsertAsync(entities, cancellationToken);

        /// <inheritdoc />
        public Task BatchInsertAsync(IEnumerable entities, CancellationToken cancellationToken = default)
            => _proxy.BatchInsertAsync(entities, cancellationToken);

        /// <inheritdoc />
        public Task BatchUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
            => _proxy.BatchUpdateAsync(entities, cancellationToken);

        /// <inheritdoc />
        public Task BatchUpdateAsync(IEnumerable entities, CancellationToken cancellationToken = default)
            => _proxy.BatchUpdateAsync(entities, cancellationToken);

        /// <inheritdoc />
        public Task BatchUpdateOrInsertAsync([IdentityOut(Mode = ArgumentMode.Collection)] IEnumerable<T> entities, CancellationToken cancellationToken = default)
            => _proxy.BatchUpdateOrInsertAsync(entities, cancellationToken);

        /// <inheritdoc />
        public Task BatchUpdateOrInsertAsync(IEnumerable entities, CancellationToken cancellationToken = default)
            => _proxy.BatchUpdateOrInsertAsync(entities, cancellationToken);

        /// <inheritdoc />
        public Task BatchDeleteAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
            => _proxy.BatchDeleteAsync(entities, cancellationToken);

        /// <inheritdoc />
        public Task BatchDeleteAsync(IEnumerable entities, CancellationToken cancellationToken = default)
            => _proxy.BatchDeleteAsync(entities, cancellationToken);

        /// <inheritdoc />
        public Task BatchDeleteIDAsync(IEnumerable ids, CancellationToken cancellationToken = default, params string[] tableArgs)
            => _proxy.BatchDeleteIDAsync(ids, cancellationToken, tableArgs);

        /// <inheritdoc />
        public Task BatchAsync([IdentityOut(Mode = ArgumentMode.Collection)] IEnumerable<EntityOperation<T>> entities, CancellationToken cancellationToken = default)
            => _proxy.BatchAsync(entities, cancellationToken);
    }
}
