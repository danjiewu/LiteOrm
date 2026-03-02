using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Common
{


    #region IObjectViewDAO<T>
    /// <summary>
    /// 实体类的查询数据访问对象的泛型接口
    /// </summary>
    /// <typeparam name="T">实体类类型</typeparam>
    public interface IObjectViewDAO<T> : IObjectViewDAO
    {
        /// <summary>
        /// 根据主键获取对象
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <returns>可枚举结果对象，可通过FirstOrDefault()和FirstOrDefaultAsync()获取结果</returns>
        new EnumerableResult<T> GetObject(params object[] keys);

        /// <summary>
        /// 判断对象是否存在
        /// </summary>
        /// <param name="o">对象</param>
        /// <returns>值结果对象，可通过GetValue()和GetValueAsync()获取结果</returns>
        ValueResult<bool> Exists(T o);

        /// <summary>
        /// 根据条件查询
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <returns>符合条件的对象枚举，同时支持同步和异步操作</returns>
        new EnumerableResult<T> Search(Expr expr = null);




    }
    #endregion

    #region IObjectViewDAO
    /// <summary>
    /// 实体类的查询数据访问对象的非泛型接口
    /// </summary>
    [AutoRegister(false)]
    public interface IObjectViewDAO
    {
        /// <summary>
        /// 根据主键获取对象
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <returns>可枚举结果对象，可通过FirstOrDefault()和FirstOrDefaultAsync()获取结果</returns>
        object GetObject(params object[] keys);

        /// <summary>
        /// 判断主键对应的对象是否存在
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <returns>值结果对象，可通过GetValue()和GetValueAsync()获取结果</returns>
        ValueResult<bool> ExistsKey(params object[] keys);

        /// <summary>
        /// 判断对象是否存在
        /// </summary>
        /// <param name="o">对象</param>
        /// <returns>值结果对象，可通过GetValue()和GetValueAsync()获取结果</returns>
        ValueResult<bool> Exists(object o);

        /// <summary>
        /// 判断符合条件的对象是否存在
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <returns>值结果对象，可通过GetValue()和GetValueAsync()获取结果</returns>
        ValueResult<bool> Exists(Expr expr);

        /// <summary>
        /// 得到符合条件的对象个数
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <returns>值结果对象，可通过GetValue()和GetValueAsync()获取结果</returns>
        ValueResult<int> Count(Expr expr);

        /// <summary>
        /// 根据条件查询
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <returns>符合条件的对象枚举，同时支持同步和异步操作</returns>
        IEnumerableResult Search(Expr expr);
    }
    #endregion
}
