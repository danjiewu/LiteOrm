using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using MyOrm.Common;
using System.ComponentModel;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace MyOrm.Service
{
    /// <summary>
    /// 实体类更改接口
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Service]
    [ServicePermission(false)]
    [ServiceLog(LogLevel = LogLevel.Information)]
    public interface IEntityService<T> : IEntityService
    {
        /// <summary>
        /// 新增实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns>
        /// true:成功
        /// false:失败</returns>
        bool Insert(T entity);
        /// <summary>
        /// 更新实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns>
        /// true:成功
        /// false:失败</returns>
        bool Update(T entity);
        /// <summary>
        /// 实体存在则更新，否则新增
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns></returns>
        bool UpdateOrInsert(T entity);
        /// <summary>
        /// 批量新增实体
        /// </summary>
        /// <param name="entities">实体列表</param>
        [Transaction]
        void BatchInsert(IEnumerable<T> entities);
        /// <summary>
        /// 批量更新实体
        /// </summary>
        /// <param name="entities">实体列表</param>
        [Transaction]
        void BatchUpdate(IEnumerable<T> entities);
        /// <summary>
        /// 批量更新或新增实体，实体存在则更新，否则新增
        /// </summary>
        /// <param name="entities">实体列表</param>
        [Transaction]
        void BatchUpdateOrInsert(IEnumerable<T> entities);
        /// <summary>
        /// 批量删除实体
        /// </summary>
        /// <param name="entities">实体列表</param>
        [Transaction]
        void BatchDelete(IEnumerable<T> entities);
        /// <summary>
        /// 批量操作实体，操作类型可以为新增、更新或删除
        /// </summary>
        /// <param name="entities">实体操作列表</param>
        [Transaction]
        void Batch(IEnumerable<EntityOperation<T>> entities);
    }

    /// <summary>
    /// 异步版本 - 泛型实体更改接口
    /// </summary>
    public interface IEntityServiceAsync<T> : IEntityServiceAsync
    {
        Task<bool> InsertAsync(T entity, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default);
        Task<bool> UpdateOrInsertAsync(T entity, CancellationToken cancellationToken = default);
        Task BatchInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
        Task BatchUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
        Task BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
        Task BatchDeleteAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
        Task BatchAsync(IEnumerable<EntityOperation<T>> entities, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 实体类更改接口
    /// </summary>
    [ServicePermission(false)]
    [AutoRegister(false)]
    public interface IEntityService
    {
        /// <summary>
        /// 新增实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns>
        /// true:成功
        /// false:失败</returns>
        bool Insert(object entity);
        /// <summary>
        /// 更新实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns>
        /// true:成功
        /// false:失败</returns>
        bool Update(object entity);
        /// <summary>
        /// 根据条件和字段内容更新值
        /// </summary>
        /// <param name="updateValues">字段内容，Key为字段名，Value为更新的值</param>
        /// <param name="condition">更新条件</param>
        /// <returns>更改记录数</returns>
        int UpdateValues(IEnumerable<KeyValuePair<string, object>> updateValues, Statement condition);
        /// <summary>
        /// 根据主键和字段内容更新值
        /// </summary>
        /// <param name="updateValues">字段内容，Key为字段名，Value为更新的值</param>
        /// <param name="keys">主键</param>
        /// <returns>
        /// true:成功
        /// false:失败</returns>
        bool UpdateValues(IEnumerable<KeyValuePair<string, object>> updateValues, params object[] keys);

        /// <summary>
        /// 实体存在则更新，否则新增
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns>
        /// true:成功
        /// false:失败</returns>
        bool UpdateOrInsert(object entity);
        /// <summary>
        /// 根据ID删除实体
        /// </summary>
        /// <param name="id">待删除id</param>
        /// <returns>
        /// true:成功
        /// false:失败</returns>
        [Service]
        bool DeleteID(object id);
        /// <summary>
        /// 批量新增实体
        /// </summary>
        /// <param name="entities">实体列表</param>
        [Transaction]
        void BatchInsert(IEnumerable entities);
        /// <summary>
        /// 批量更新实体
        /// </summary>
        /// <param name="entities">实体列表</param>
        [Transaction]
        void BatchUpdate(IEnumerable entities);
        /// <summary>
        /// 批量更新或新增实体，实体存在则更新，否则新增
        /// </summary>
        /// <param name="entities">实体列表</param>
        [Transaction]
        void BatchUpdateOrInsert(IEnumerable entities);
        /// <summary>
        /// 批量删除实体
        /// </summary>
        /// <param name="entities">实体列表</param>
        [Transaction]
        void BatchDelete(IEnumerable entities);
        /// <summary>
        /// 批量根据ID删除实体
        /// </summary>
        /// <param name="ids">待删除id</param>
        [Transaction]
        void BatchDeleteID(IEnumerable ids);
    }

    /// <summary>
    /// 异步版本 - 非泛型实体更改接口
    /// </summary>
    [AutoRegister(false)]
    public interface IEntityServiceAsync
    {
        Task<bool> InsertAsync(object entity, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(object entity, CancellationToken cancellationToken = default);
        Task<int> UpdateValuesAsync(IEnumerable<KeyValuePair<string, object>> updateValues, Statement condition, CancellationToken cancellationToken = default);
        Task<bool> UpdateValuesAsync(IEnumerable<KeyValuePair<string, object>> updateValues, object[] keys, CancellationToken cancellationToken = default);
        Task<bool> UpdateOrInsertAsync(object entity, CancellationToken cancellationToken = default);
        Task<bool> DeleteIDAsync(object id, CancellationToken cancellationToken = default);
        Task BatchInsertAsync(IEnumerable entities, CancellationToken cancellationToken = default);
        Task BatchUpdateAsync(IEnumerable entities, CancellationToken cancellationToken = default);
        Task BatchUpdateOrInsertAsync(IEnumerable entities, CancellationToken cancellationToken = default);
        Task BatchDeleteAsync(IEnumerable entities, CancellationToken cancellationToken = default);
        Task BatchDeleteIDAsync(IEnumerable ids, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 实体类查询接口
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Service]
    [ServicePermission(true)]
    [ServiceLog(LogLevel = LogLevel.Debug)]
    public interface IEntityViewService<T> : IEntityViewService
    {
        /// <summary>
        /// 获取实体
        /// </summary>
        /// <param name="id">实体主键</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>id对应实体，若不存在则返回null</returns>
        new T GetObject(object id, params string[] tableArgs);
        /// <summary>
        /// 根据条件获取单个实体
        /// </summary>
        /// <param name="condition">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>第一个符合条件的实体，若不存在则返回null</returns>
        new T SearchOne(Statement condition, params string[] tableArgs);
        /// <summary>
        /// 根据条件遍历对象
        /// </summary>
        /// <param name="condition">查询条件，若为null则表示没有条件</param>
        /// <param name="func">调用的函数委托</param>
        /// <param name="tableArgs">表名参数</param>
        void ForEach(Statement condition, Action<T> func, params string[] tableArgs);
        /// <summary>
        /// 根据条件获取实体列表
        /// </summary>
        /// <param name="condition">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>符合条件的实体列表</returns>
        new List<T> Search(Statement condition = null, params string[] tableArgs);
        /// <summary>
        /// 根据条件分页查询
        /// </summary>
        /// <param name="condition">查询条件，若为null则表示没有条件</param>
        /// <param name="section">分页设置</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>符合条件的分页对象列表</returns>
        new List<T> SearchSection(Statement condition, SectionSet section, params string[] tableArgs);
    }

    /// <summary>
    /// 异步版本 - 泛型实体查询接口
    /// </summary>
    public interface IEntityViewServiceAsync<T> : IEntityViewServiceAsync
    {
        Task<T> GetObjectAsync(object id, string[] tableArgs = null, CancellationToken cancellationToken = default);
        Task<T> SearchOneAsync(Statement condition, string[] tableArgs = null, CancellationToken cancellationToken = default);
        Task ForEachAsync(Statement condition, Func<T, Task> func, string[] tableArgs = null, CancellationToken cancellationToken = default);
        Task<List<T>> SearchAsync(Statement condition = null, string[] tableArgs = null, CancellationToken cancellationToken = default);
        Task<List<T>> SearchSectionAsync(Statement condition, SectionSet section, string[] tableArgs = null, CancellationToken cancellationToken = default);
    }

    [ServicePermission(true)]
    [ServiceLog(LogLevel = LogLevel.Debug)]
    [AutoRegister(false)]
    public interface IEntityViewService
    {
        /// <summary>
        /// 获取实体
        /// </summary>
        /// <param name="id">实体主键</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>id对应实体，若不存在则返回null</returns>
        object GetObject(object id, params string[] tableArgs);
        /// <summary>
        /// 检测ID是否存在
        /// </summary>
        /// <param name="id">实体主键</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>是否存在记录</returns>
        [Service]
        bool ExistsID(object id, params string[] tableArgs);
        /// <summary>
        /// 根据条件检查是否存在记录
        /// </summary>
        /// <param name="condition">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>是否存在记录</returns>
        [Service]
        bool Exists(Statement condition, params string[] tableArgs);
        /// <summary>
        /// 根据条件获取记录总数
        /// </summary>
        /// <param name="condition">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>符合条件的记录总数</returns>
        [Service]
        int Count(Statement condition = null, params string[] tableArgs);
        /// <summary>
        /// 根据条件获取单个实体
        /// </summary>
        /// <param name="condition">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>第一个符合条件的实体，若不存在则返回null</returns>
        object SearchOne(Statement condition, params string[] tableArgs);
        /// <summary>
        /// 根据条件获取实体列表
        /// </summary>
        /// <param name="condition">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>符合条件的实体列表</returns>
        IList Search(Statement condition = null, params string[] tableArgs);
        /// <summary>
        /// 根据条件分页查询
        /// </summary>
        /// <param name="condition">查询条件，若为null则表示没有条件</param>
        /// <param name="section">分页设置</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>符合条件的分页对象列表</returns>
        IList SearchSection(Statement condition, SectionSet section, params string[] tableArgs);
    }

    /// <summary>
    /// 异步版本 - 非泛型实体查询接口
    /// </summary>
    [AutoRegister(false)]
    public interface IEntityViewServiceAsync
    {
        Task<object> GetObjectAsync(object id, string[] tableArgs = null, CancellationToken cancellationToken = default);
        Task<bool> ExistsIDAsync(object id, string[] tableArgs = null, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(Statement condition, string[] tableArgs = null, CancellationToken cancellationToken = default);
        Task<int> CountAsync(Statement condition = null, string[] tableArgs = null, CancellationToken cancellationToken = default);
        Task<object> SearchOneAsync(Statement condition, string[] tableArgs = null, CancellationToken cancellationToken = default);
        Task<IList> SearchAsync(Statement condition = null, string[] tableArgs = null, CancellationToken cancellationToken = default);
        Task<IList> SearchSectionAsync(Statement condition, SectionSet section, string[] tableArgs = null, CancellationToken cancellationToken = default);
    }

    [Serializable]
    public class EntityOperation<T>
    {
        public OpDef Operation { get; set; }
        public T Entity { get; set; }
    }

    [Serializable]
    public enum OpDef
    {
        Nothing,
        Insert,
        Update,
        Delete
    }
}
