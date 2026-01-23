using LiteOrm.Common;
using System.Collections.Generic;

namespace LiteOrm
{
    /// <summary>
    /// ObjectDAOBase 的扩展方法类
    /// </summary>
    /// <remarks>
    /// ObjectDAOExt 提供了 IObjectDAO&lt;T&gt; 和 IObjectViewDAO&lt;T&gt; 接口的扩展方法。
    /// 
    /// 主要功能：
    /// 1. WithArgs 扩展方法 - 为DAO对象设置动态表名参数
    /// 
    /// 这些扩展方法提供了一种流畅的API来处理参数化的表名，
    /// 允许在运行时动态指定表名或其他参数。
    /// 
    /// 使用示例：
    /// <code>
    /// var dao = serviceProvider.GetService&lt;IObjectDAO&lt;User&gt;&gt;();
    /// // 为表名参数创建新的DAO实例
    /// var specificTableDao = dao.WithArgs("User_2024");
    /// // 进行数据操作
    /// var users = await specificTableDao.SearchAsync(Expr.Property("Age") &gt; 18);
    /// </code>
    /// </remarks>
    public static class ObjectDAOExt
    {
        /// <summary>
        /// 使用指定的参数创建新的DAO实例
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dao"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static IObjectDAO<T> WithArgs<T>(this IObjectDAO<T> dao, params string[] args)
        {
            if (args is null || args.Length == 0) return dao;
            ObjectDAOBase dAOBase = dao as ObjectDAOBase;
            return dAOBase.WithArgs(args) as IObjectDAO<T>;
        }
        /// <summary>
        /// 使用指定的参数创建新的DAO实例
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dao"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static IObjectViewDAO<T> WithArgs<T>(this IObjectViewDAO<T> dao, params string[] args)
        {
            if (args is null || args.Length == 0) return dao;
            ObjectDAOBase dAOBase = dao as ObjectDAOBase;
            return dAOBase.WithArgs(args) as IObjectViewDAO<T>;
        }
    }
}
