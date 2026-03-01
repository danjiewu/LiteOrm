using System;
using System.Collections;
using System.Collections.Generic;

namespace LiteOrm.Common
{
    #region IObjectViewDAO<T>
    /// <summary>
    /// 实体类的查询数据访问对象的泛型接口
    /// </summary>
    /// <typeparam name="T">实体类类型</typeparam>
    public interface IObjectViewDAO<T> : IObjectViewDAOAsync<T>, IObjectViewDAO
    {
        /// <summary>
        /// 根据主键获取对象
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <returns></returns>
        new T GetObject(params object[] keys);

        /// <summary>
        /// 获取单个符合条件的对象
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <returns>第一个符合条件的对象，若不存在则返回null</returns>
        new T SearchOne(Expr expr);
        /// <summary>
        /// 遍历每个符合条件的对象
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="func">要调用的函数委托</param>
        void ForEach(Expr expr, Action<T> func);

        /// <summary>
        /// 根据条件查询
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <returns>符合条件的对象列表</returns>
        new List<T> Search(Expr expr = null);

    }
    #endregion

    #region IObjectViewDAO
    /// <summary>
    /// 实体类的查询数据访问对象的非泛型接口
    /// </summary>
    [AutoRegister(false)]
    public interface IObjectViewDAO : IObjectViewDAOAsync
    {
        /// <summary>
        /// 根据主键获取对象
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <returns></returns>
        Object GetObject(params object[] keys);

        /// <summary>
        /// 判断主键对应的对象是否存在
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <returns>是否存在</returns>
        bool ExistsKey(params object[] keys);

        /// <summary>
        /// 判断对象是否存在
        /// </summary>
        /// <param name="o">对象</param>
        /// <returns>是否存在</returns>
        bool Exists(object o);

        /// <summary>
        /// 判断符合条件的对象是否存在
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <returns>是否存在</returns>
        bool Exists(Expr expr);

        /// <summary>
        /// 得到符合条件的对象个数
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <returns>符合条件的对象数量</returns>
        int Count(Expr expr);

        /// <summary>
        /// 获取单个符合条件的对象
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <returns>第一个符合条件的对象，若不存在则返回null</returns>
        Object SearchOne(Expr expr);

        /// <summary>
        /// 根据条件查询
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <returns>符合条件的对象列表</returns>
        IList Search(Expr expr);
    }
    #endregion
}
