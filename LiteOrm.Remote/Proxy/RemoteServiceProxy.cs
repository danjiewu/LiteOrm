using Castle.DynamicProxy;
using LiteOrm.Common;
using LiteOrm.Service;
using System.Collections;

namespace LiteOrm.Remote
{
    /// <summary>
    /// 远程实体服务同步代理。通过 <see cref="ProxyGenerator"/> 为 <see cref="IEntityService{T}"/>
    /// 创建动态代理，所有方法调用由 <see cref="RemoteServiceInvokeInterceptor"/> 拦截并转发到远程服务端。
    /// <para>
    /// 本类作为 Autofac <see cref="Autofac.Builder.IRegistrationBuilder{TLimit,TActivatorData,TRegistrationStyle}.As"/> 
    /// 的开放泛型实现类型，通过 <c>RegisterGeneric(typeof(RemoteServiceProxy&lt;&gt;))</c> 注册为 <c>IEntityService&lt;&gt;</c> 的实现。
    /// </para>
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public class RemoteServiceProxy<T> : IEntityService<T> where T : class
    {
        private readonly IEntityService<T> _proxy;

        /// <summary>
        /// 初始化 <see cref="RemoteServiceProxy{T}"/> 类的新实例。
        /// </summary>
        /// <param name="interceptor">远程调用拦截器，用于拦截代理方法调用并转发到远程服务端。</param>
        public RemoteServiceProxy(RemoteServiceInvokeInterceptor interceptor)
        {
            var generator = new ProxyGenerator();
            // 为 IEntityService<T> 接口创建无目标代理，所有调用由拦截器转发
            _proxy = generator.CreateInterfaceProxyWithoutTarget<IEntityService<T>>(interceptor.ToInterceptor());
        }

        /// <inheritdoc />
        public bool Insert([IdentityOut] T entity) => _proxy.Insert(entity);

        /// <inheritdoc />
        public bool Insert(object entity) => _proxy.Insert(entity);

        /// <inheritdoc />
        public bool Update(T entity) => _proxy.Update(entity);

        /// <inheritdoc />
        public bool Update(object entity) => _proxy.Update(entity);

        /// <inheritdoc />
        public bool UpdateOrInsert([IdentityOut] T entity) => _proxy.UpdateOrInsert(entity);

        /// <inheritdoc />
        public bool UpdateOrInsert(object entity) => _proxy.UpdateOrInsert(entity);

        /// <inheritdoc />
        public bool Delete(T entity) => _proxy.Delete(entity);

        /// <inheritdoc />
        public bool DeleteID(object id, params string[] tableArgs) => _proxy.DeleteID(id, tableArgs);

        /// <inheritdoc />
        public int Delete(LogicExpr expr, params string[] tableArgs) => _proxy.Delete(expr, tableArgs);

        /// <inheritdoc />
        public int Update(UpdateExpr expr, params string[] tableArgs) => _proxy.Update(expr, tableArgs);

        /// <inheritdoc />
        public void BatchInsert([IdentityOut(Mode = ArgumentMode.Collection)] IEnumerable<T> entities) => _proxy.BatchInsert(entities);

        /// <inheritdoc />
        public void BatchInsert(IEnumerable entities) => _proxy.BatchInsert(entities);

        /// <inheritdoc />
        public void BatchUpdate(IEnumerable<T> entities) => _proxy.BatchUpdate(entities);

        /// <inheritdoc />
        public void BatchUpdate(IEnumerable entities) => _proxy.BatchUpdate(entities);

        /// <inheritdoc />
        public void BatchUpdateOrInsert([IdentityOut(Mode = ArgumentMode.Collection)] IEnumerable<T> entities) => _proxy.BatchUpdateOrInsert(entities);

        /// <inheritdoc />
        public void BatchUpdateOrInsert(IEnumerable entities) => _proxy.BatchUpdateOrInsert(entities);

        /// <inheritdoc />
        public void BatchDelete(IEnumerable<T> entities) => _proxy.BatchDelete(entities);

        /// <inheritdoc />
        public void BatchDelete(IEnumerable entities) => _proxy.BatchDelete(entities);

        /// <inheritdoc />
        public void BatchDeleteID(IEnumerable ids, params string[] tableArgs) => _proxy.BatchDeleteID(ids, tableArgs);

        /// <inheritdoc />
        public void Batch([IdentityOut(Mode = ArgumentMode.Collection)] IEnumerable<EntityOperation<T>> entities) => _proxy.Batch(entities);
    }
}
