using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using LiteOrm.Common;
using System.ComponentModel;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Service
{
    /// <summary>
    /// 实体操作类型定义
    /// </summary>
    [Serializable]
    public enum OpDef
    {
        /// <summary>
        /// 无操作
        /// </summary>
        Nothing,

        /// <summary>
        /// 新增操作
        /// </summary>
        Insert,

        /// <summary>
        /// 更新操作
        /// </summary>
        Update,

        /// <summary>
        /// 删除操作
        /// </summary>
        Delete
    }

    /// <summary>
    /// 实体操作类，封装实体和对应的操作类型
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    [Serializable]
    public class EntityOperation<T>
    {
        /// <summary>
        /// 操作类型
        /// </summary>
        public OpDef Operation { get; set; }

        /// <summary>
        /// 实体对象
        /// </summary>
        public T Entity { get; set; }
    }
    /// <summary>
    /// 实体类更改接口
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
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
    /// <typeparam name="T">实体类型</typeparam>
    public interface IEntityServiceAsync<T> : IEntityServiceAsync
    {
        /// <summary>
        /// 异步新增实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果，true表示成功，false表示失败</returns>
        Task<bool> InsertAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步更新实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果，true表示成功，false表示失败</returns>
        Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步更新或新增实体（实体存在则更新，否则新增）
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果，true表示成功，false表示失败</returns>
        Task<bool> UpdateOrInsertAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量新增实体
        /// </summary>
        /// <param name="entities">实体列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        Task BatchInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量更新实体
        /// </summary>
        /// <param name="entities">实体列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        Task BatchUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量更新或新增实体（实体存在则更新，否则新增）
        /// </summary>
        /// <param name="entities">实体列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        Task BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量删除实体
        /// </summary>
        /// <param name="entities">实体列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        Task BatchDeleteAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量操作实体（操作类型可以为新增、更新或删除）
        /// </summary>
        /// <param name="entities">实体操作列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
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
        /// <param name="expr">更新条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>更改记录数</returns>
        int UpdateValues(IEnumerable<KeyValuePair<string, object>> updateValues, Expr expr, params string[] tableArgs);
        /// <summary>
        /// 根据主键和字段内容更新值
        /// </summary>
        /// <param name="updateValues">字段内容，Key为字段名，Value为更新的值</param>
        /// <param name="keys">主键</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>
        /// true:成功
        /// false:失败</returns>
        bool UpdateValues(IEnumerable<KeyValuePair<string, object>> updateValues, object[] keys, params string[] tableArgs);

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
        /// <param name="tableArgs">表名参数</param>
        /// <returns>
        /// true:成功
        /// false:失败</returns>
        [Service]
        bool DeleteID(object id, params string[] tableArgs);

        /// <summary>
        /// 根据条件删除实体
        /// </summary>
        /// <param name="expr">删除条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>删除的记录数</returns>
        [Service]
        int Delete(Expr expr, params string[] tableArgs);
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
        /// <summary>
        /// 异步新增实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果，true表示成功，false表示失败</returns>
        Task<bool> InsertAsync(object entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步更新实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果，true表示成功，false表示失败</returns>
        Task<bool> UpdateAsync(object entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件和字段内容更新值
        /// </summary>
        /// <param name="updateValues">字段内容，Key为字段名，Value为更新的值</param>
        /// <param name="expr">更新条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>更改记录数</returns>
        Task<int> UpdateValuesAsync(IEnumerable<KeyValuePair<string, object>> updateValues, Expr expr, string[] tableArgs, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据主键和字段内容更新值
        /// </summary>
        /// <param name="updateValues">字段内容，Key为字段名，Value为更新的值</param>
        /// <param name="keys">主键</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果，true表示成功，false表示失败</returns>
        Task<bool> UpdateValuesAsync(IEnumerable<KeyValuePair<string, object>> updateValues, object[] keys, string[] tableArgs, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步更新或新增实体（实体存在则更新，否则新增）
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果，true表示成功，false表示失败</returns>
        Task<bool> UpdateOrInsertAsync(object entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据ID删除实体
        /// </summary>
        /// <param name="id">待删除id</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果，true表示成功，false表示失败</returns>
        Task<bool> DeleteIDAsync(object id, string[] tableArgs, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件删除实体
        /// </summary>
        /// <param name="expr">删除条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>受影响的行数</returns>
        Task<int> DeleteAsync(Expr expr, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量新增实体
        /// </summary>
        /// <param name="entities">实体列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        Task BatchInsertAsync(IEnumerable entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量更新实体
        /// </summary>
        /// <param name="entities">实体列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        Task BatchUpdateAsync(IEnumerable entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量更新或新增实体（实体存在则更新，否则新增）
        /// </summary>
        /// <param name="entities">实体列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        Task BatchUpdateOrInsertAsync(IEnumerable entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量删除实体
        /// </summary>
        /// <param name="entities">实体列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        Task BatchDeleteAsync(IEnumerable entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量根据ID删除实体
        /// </summary>
        /// <param name="ids">待删除id</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        Task BatchDeleteIDAsync(IEnumerable ids, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 实体类查询接口
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
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
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>第一个符合条件的实体，若不存在则返回null</returns>
        new T SearchOne(Expr expr, params string[] tableArgs);
        /// <summary>
        /// 根据条件遍历对象
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="func">调用的函数委托</param>
        /// <param name="tableArgs">表名参数</param>
        void ForEach(Expr expr, Action<T> func, params string[] tableArgs);
        /// <summary>
        /// 根据条件获取实体列表
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>符合条件的实体列表</returns>
        new List<T> Search(Expr expr = null, params string[] tableArgs);
        /// <summary>
        /// 根据条件分页查询
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="section">分页设置</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>符合条件的分页对象列表</returns>
        new List<T> SearchSection(Expr expr, PageSection section, params string[] tableArgs);
    }

    /// <summary>
    /// 异步版本 - 泛型实体查询接口
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public interface IEntityViewServiceAsync<T> : IEntityViewServiceAsync
    {
        /// <summary>
        /// 异步获取实体
        /// </summary>
        /// <param name="id">实体主键</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>id对应实体，若不存在则返回null</returns>
        new Task<T> GetObjectAsync(object id, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件获取单个实体
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>第一个符合条件的实体，若不存在则返回null</returns>
        new Task<T> SearchOneAsync(Expr expr, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件遍历对象
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="func">调用的异步函数委托</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        Task ForEachAsync(Expr expr, Func<T, Task> func, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件获取实体列表
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>符合条件的实体列表</returns>
        new Task<List<T>> SearchAsync(Expr expr = null, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件分页查询
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="section">分页设置</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>符合条件的分页对象列表</returns>
        new Task<List<T>> SearchSectionAsync(Expr expr, PageSection section, string[] tableArgs = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 提供对实体视图（只读或关联视图）进行查询操作的非泛型接口。
    /// </summary>
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
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>是否存在记录</returns>
        [Service]
        bool Exists(Expr expr, params string[] tableArgs);
        /// <summary>
        /// 根据条件获取记录总数
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>符合条件的记录总数</returns>
        [Service]
        int Count(Expr expr = null, params string[] tableArgs);
        /// <summary>
        /// 根据条件获取单个实体
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>第一个符合条件的实体，若不存在则返回null</returns>
        object SearchOne(Expr expr, params string[] tableArgs);
        /// <summary>
        /// 根据条件获取实体列表
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>符合条件的实体列表</returns>
        IList Search(Expr expr = null, params string[] tableArgs);
        /// <summary>
        /// 根据条件分页查询
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="section">分页设置</param>
        /// <param name="tableArgs">表名参数</param>
        /// <returns>符合条件的分页对象列表</returns>
        IList SearchSection(Expr expr, PageSection section, params string[] tableArgs);
    }

    /// <summary>
    /// 异步版本 - 非泛型实体查询接口
    /// </summary>
    [AutoRegister(false)]
    public interface IEntityViewServiceAsync
    {
        /// <summary>
        /// 异步获取实体
        /// </summary>
        /// <param name="id">实体主键</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>id对应实体，若不存在则返回null</returns>
        Task<object> GetObjectAsync(object id, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步检测ID是否存在
        /// </summary>
        /// <param name="id">实体主键</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否存在记录</returns>
        Task<bool> ExistsIDAsync(object id, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件检查是否存在记录
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否存在记录</returns>
        Task<bool> ExistsAsync(Expr expr, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件获取记录总数
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>符合条件的记录总数</returns>
        Task<int> CountAsync(Expr expr = null, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件获取单个实体
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>第一个符合条件的实体，若不存在则返回null</returns>
        Task<object> SearchOneAsync(Expr expr, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件获取实体列表
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>符合条件的实体列表</returns>
        Task<IList> SearchAsync(Expr expr = null, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件分页查询
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="section">分页设置</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="tableArgs">表名参数</param>
        /// 
        /// <returns>符合条件的分页对象列表</returns>
        Task<IList> SearchSectionAsync(Expr expr, PageSection section, string[] tableArgs = null, CancellationToken cancellationToken = default);
    }
}
