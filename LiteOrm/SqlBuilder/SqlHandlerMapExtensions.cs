using System;
using System.Collections.Generic;
using LiteOrm.Common;

namespace LiteOrm.SqlBuilder
{
    /// <summary>
    /// SqlBuilder 扩展方法
    /// </summary>
    public static class SqlHandlerMapExtensions
    {
        /// <summary>
        /// 注册函数的 SQL 语句处理器
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sqlBuilder"></param>
        /// <param name="functionName"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        public static void RegisterFunctionSqlHandler<T>(this T sqlBuilder, string functionName, Func<string, IList<KeyValuePair<string, Expr>>, string> handler) where T : BaseSqlBuilder
        {
            BaseSqlBuilder.GetSqlHandlerMap<T>().RegisterFunctionSqlHandler(functionName, handler);
        }

        /// <summary>
        /// 注册多个函数的 SQL 语句处理器
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sqlBuilder"></param>
        /// <param name="functionNames"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        public static void RegisterFunctionSqlHandler<T>(this T sqlBuilder, IEnumerable<string> functionNames, Func<string, IList<KeyValuePair<string, Expr>>, string> handler) where T : BaseSqlBuilder
        {
            foreach (string functionName in functionNames)
            {
                BaseSqlBuilder.GetSqlHandlerMap<T>().RegisterFunctionSqlHandler(functionName, handler);
            }
        }
        /// <summary>
        /// 获取函数的 SQL 语句处理器
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sqlBuilder"></param>
        /// <param name="functionName"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        public static bool TryGetFunctionSqlHandler<T>(this T sqlBuilder, string functionName, out Func<string, IList<KeyValuePair<string, Expr>>, string> handler) where T : BaseSqlBuilder
        {
            return BaseSqlBuilder.GetSqlHandlerMap<T>().TryGetFunctionSqlHandler(functionName, out handler);
        }
    }
}
