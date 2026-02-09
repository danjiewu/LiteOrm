using LiteOrm.Common;
using System;
using System.Collections.Generic;

namespace LiteOrm
{
    /// <summary>
    /// SqlBuilder À©Õ¹·½·¨
    /// </summary>
    public static class SqlHandlerMapExtensions
    {
        /// <summary>
        /// ×¢²áº¯ÊýµÄ SQL Óï¾ä´¦ÀíÆ÷
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sqlBuilder"></param>
        /// <param name="functionName"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        public static void RegisterFunctionSqlHandler<T>(this T sqlBuilder, string functionName, Func<string, IList<KeyValuePair<string, Expr>>, string> handler) where T : SqlBuilder
        {
            SqlBuilder.GetSqlHandlerMap<T>().RegisterFunctionSqlHandler(functionName, handler);
        }

        /// <summary>
        /// ×¢²á¶à¸öº¯ÊýµÄ SQL Óï¾ä´¦ÀíÆ÷
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sqlBuilder"></param>
        /// <param name="functionNames"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        public static void RegisterFunctionSqlHandler<T>(this T sqlBuilder, IEnumerable<string> functionNames, Func<string, IList<KeyValuePair<string, Expr>>, string> handler) where T : SqlBuilder
        {
            foreach (string functionName in functionNames)
            {
                SqlBuilder.GetSqlHandlerMap<T>().RegisterFunctionSqlHandler(functionName, handler);
            }
        }
        /// <summary>
        /// »ñÈ¡º¯ÊýµÄ SQL Óï¾ä´¦ÀíÆ÷
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sqlBuilder"></param>
        /// <param name="functionName"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        public static bool TryGetFunctionSqlHandler<T>(this T sqlBuilder, string functionName, out Func<string, IList<KeyValuePair<string, Expr>>, string> handler) where T : SqlBuilder
        {
            return SqlBuilder.GetSqlHandlerMap<T>().TryGetFunctionSqlHandler(functionName, out handler);
        }
    }
}
