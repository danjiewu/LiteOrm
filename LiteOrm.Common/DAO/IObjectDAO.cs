using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Linq.Expressions;
using System.Collections;

namespace LiteOrm.Common
{
    #region IObjectDAO<T>
    /// <summary>
    /// 实体类的增删改等基本操作的泛型接口
    /// </summary>
    /// <typeparam name="T">实体类类型</typeparam>
    public interface IObjectDAO<T> : IObjectDAOAsync<T>, IObjectDAO
    {
        /// <summary>
        /// 添加对象
        /// </summary>
        /// <param name="o">待添加的对象</param>
        /// <returns>是否成功添加</returns>
        bool Insert(T o);
        /// <summary>
        /// 批量添加对象
        /// </summary>
        /// <param name="values">待添加的对象集合</param>
        void BatchInsert(IEnumerable<T> values);
        /// <summary>
        /// 更新对象
        /// </summary>
        /// <param name="o">待更新的对象</param>
        /// <param name="timestamp">时间戳</param>
        /// <returns>是否成功更新</returns>
        bool Update(T o, object timestamp = null);

        /// <summary>
        /// 更新或添加对象，若存在则更新，若不存在则添加
        /// </summary>
        /// <param name="o">待更新或添加的对象</param>
        /// <returns>是否更新或添加</returns>
        UpdateOrInsertResult UpdateOrInsert(T o);

        /// <summary>
        /// 删除对象
        /// </summary>
        /// <param name="o">待删除的对象</param>
        /// <returns>是否成功删除</returns>
        bool Delete(T o);
    }
    #endregion

    #region IObjectDAO
    /// <summary>
    /// 实体类的增删改等基本操作的非泛型接口
    /// </summary>
    [AutoRegister(false)]
    public interface IObjectDAO: IObjectDAOAsync
    {
        /// <summary>
        /// 添加对象
        /// </summary>
        /// <param name="o">待添加的对象</param>
        /// <returns>是否成功添加</returns>
        bool Insert(Object o);

        /// <summary>
        /// 批量添加对象
        /// </summary>
        /// <param name="values">待添加的对象集合</param>
        void BatchInsert(IEnumerable values);

        /// <summary>
        /// 更新对象
        /// </summary>
        /// <param name="o">待更新的对象</param>
        /// <returns>是否成功更新</returns>
        bool Update(Object o);

        /// <summary>
        /// 更新或添加对象，若存在则更新，若不存在则添加
        /// </summary>
        /// <param name="o">待更新或添加的对象</param>
        /// <returns>是否更新或添加</returns>
        UpdateOrInsertResult UpdateOrInsert(Object o);

        /// <summary>
        /// 根据条件更新数据
        /// </summary>
        /// <param name="values">需要更新的属性及数值，key为属性名，value为数值</param>
        /// <param name="expr">更新的条件</param>
        /// <returns>更新的记录数</returns>
        int UpdateAllValues(IEnumerable<KeyValuePair<string, object>> values, Expr expr);
        /// <summary>
        /// 根据主键更新数据
        /// </summary>
        /// <param name="values">需要更新的属性及数值，key为属性名，value为数值</param>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <returns>更新是否成功</returns>
        bool UpdateValues(IEnumerable<KeyValuePair<string, object>> values, params object[] keys);

        /// <summary>
        /// 删除对象
        /// </summary>
        /// <param name="o">待删除的对象</param>
        /// <returns>是否成功删除</returns>
        bool Delete(Object o);

        /// <summary>
        /// 根据主键删除对象
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <returns>是否成功删除</returns>
        bool DeleteByKeys(params object[] keys);

        /// <summary>
        /// 根据条件删除对象
        /// </summary>
        /// <param name="expr">条件</param>
        /// <returns>删除对象数量</returns>
        int Delete(Expr expr);
    }
    #endregion

    /// <summary>
    /// 插入或更新操作的返回结果
    /// </summary>
    public enum UpdateOrInsertResult
    {
        /// <summary>
        /// 插入成功
        /// </summary>
        Inserted,
        /// <summary>
        /// 更新成功
        /// </summary>
        Updated,
        /// <summary>
        /// 失败
        /// </summary>
        Failed
    }
}
