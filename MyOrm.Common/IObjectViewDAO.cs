using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Collections;
using System.ComponentModel;
using System.Linq.Expressions;

namespace MyOrm.Common
{
    #region IObjectViewDAO<T>
    /// <summary>
    /// 实体类的查询操作的泛型接口
    /// </summary>
    /// <typeparam name="T">实体类类型</typeparam>
    public interface IObjectViewDAO<T> : IObjectViewDAO
    {
        /// <summary>
        /// 根据主键获取对象
        /// </summary>
        /// <param name="keys">主键，多个主键按照名称顺序排列</param>
        /// <returns></returns>
        new T GetObject(params object[] keys);

        /// <summary>
        /// 根据条件获取单个对象
        /// </summary>
        /// <param name="condition">查询条件，若为null则表示没有条件</param>
        /// <returns>第一个符合条件的对象，若不存在则返回null</returns>
        new T SearchOne(Condition condition);
        /// <summary>
        /// 根据条件遍历对象
        /// </summary>
        /// <param name="condition">查询条件，若为null则表示没有条件</param>
        /// <param name="func">调用的函数委托</param>
        void ForEach(Condition condition, Action<T> func);
        /// <summary>
        /// 根据条件表达式遍历对象
        /// </summary>
        /// <param name="expression">查询表达式</param>
        /// <param name="func">调用的函数委托</param>
        void ForEach(Expression<Func<T, bool>> expression, Action<T> func);
        /// <summary>
        /// 根据条件获取单个对象
        /// </summary>
        /// <param name="expression">查询表达式</param>
        /// <returns>第一个符合条件的对象，若不存在则返回null</returns>
        T SearchOne(Expression<Func<T, bool>> expression);
        /// <summary>
        /// 根据条件检查对象是否存在
        /// </summary>
        /// <param name="expression">查询表达式</param>
        /// <returns>是否存在</returns>
        bool Exists(Expression<Func<T, bool>> expression);

        /// <summary>
        /// 得到满足条件的对象个数
        /// </summary>
        /// <param name="expression">查询表达式</param>
        /// <returns>满足条件的对象个数</returns>
        int Count(Expression<Func<T, bool>> expression);
        /// <summary>
        /// 根据条件查询
        /// </summary>
        /// <param name="condition">查询条件，若为null则表示没有条件</param>
        /// <returns>符合条件的对象列表</returns>
        new List<T> Search(Condition condition = null);
        /// <summary>
        /// 根据条件表达式查询
        /// </summary>
        /// <param name="expression">查询表达式</param>
        /// <returns>符合条件的对象列表</returns>
        List<T> Search(Expression<Func<T, bool>> expression);

        /// <summary>
        /// 根据条件查询
        /// </summary>
        /// <param name="condition">查询条件，若为null则表示没有条件</param>
        /// <param name="orderBy">排列顺序，若为null则表示不指定顺序</param>
        /// <returns>符合条件的对象列表</returns>
        new List<T> Search(Condition condition, params Sorting[] orderBy);
        /// <summary>
        /// 根据条件表达式查询
        /// </summary>
        /// <param name="expression">查询表达式</param>
        /// <param name="orderby">排列顺序，若为null则表示不指定顺序</param>
        /// <returns>符合条件的对象列表</returns>
        List<T> Search(Expression<Func<T, bool>> expression, params Sorting[] orderby);

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="condition">查询条件</param>
        /// <param name="section">分页设定</param>
        /// <returns></returns>
        new List<T> SearchSection(Condition condition, SectionSet section);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="section"></param>
        /// <returns></returns>
        List<T> SearchSection(Expression<Func<T, bool>> expression, SectionSet section);
    }
    #endregion

    #region IObjectViewDAO
    /// <summary>
    /// 实体类的查询操作的非范型接口
    /// </summary>
    public interface IObjectViewDAO
    {
        /// <summary>
        /// 根据主键获取对象
        /// </summary>
        /// <param name="keys">主键，多个主键按照名称顺序排列</param>
        /// <returns></returns>
        Object GetObject(params object[] keys);

        /// <summary>
        /// 根据主键检查对象是否存在
        /// </summary>
        /// <param name="keys">主键，多个主键按照名称顺序排列</param>
        /// <returns>是否存在</returns>
        bool ExistsKey(params object[] keys);

        /// <summary>
        /// 检查对象是否存在
        /// </summary>
        /// <param name="o">对象</param>
        /// <returns>是否存在</returns>
        bool Exists(object o);

        /// <summary>
        /// 根据条件检查对象是否存在
        /// </summary>
        /// <param name="condition">查询条件，若为null则表示没有条件</param>
        /// <returns>是否存在</returns>
        bool Exists(Condition condition);

        /// <summary>
        /// 得到满足条件的对象个数
        /// </summary>
        /// <param name="condition">查询条件，若为null则表示没有条件</param>
        /// <returns>满足条件的对象个数</returns>
        int Count(Condition condition);

        /// <summary>
        /// 根据条件获取单个对象
        /// </summary>
        /// <param name="condition">查询条件，若为null则表示没有条件</param>
        /// <returns>第一个符合条件的对象，若不存在则返回null</returns>
        Object SearchOne(Condition condition);

        /// <summary>
        /// 根据条件查询
        /// </summary>
        /// <param name="condition">查询条件，若为null则表示没有条件</param>
        /// <returns>符合条件的对象列表</returns>
        IList Search(Condition condition);

        /// <summary>
        /// 根据条件查询
        /// </summary>
        /// <param name="condition">查询条件，若为null则表示没有条件</param>
        /// <param name="orderBy">排列顺序，若为null则表示不指定顺序</param>
        /// <returns>符合条件的对象列表</returns>
        IList Search(Condition condition, params Sorting[] orderBy);

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="condition">查询条件</param>
        /// <param name="section">分页设定</param>
        /// <returns></returns>
        IList SearchSection(Condition condition, SectionSet section);
    }
    #endregion
}
