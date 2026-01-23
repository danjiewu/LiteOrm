using System;
using System.Collections.Generic;
using System.Collections;
using LiteOrm.Common;
using Microsoft.Extensions.Logging;
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
}
