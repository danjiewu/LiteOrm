using LiteOrm.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LiteOrm
{
    /// <summary>
    /// 函数 SQL 生成委托，将函数表达式直接写入 <see cref="ValueStringBuilder"/>。
    /// </summary>
    public delegate void FunctionSqlHandler(ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams);

    internal class SqlHandlerMap
    {
        private readonly ConcurrentDictionary<string, FunctionSqlHandler> FunctionSqlHandlers = new ConcurrentDictionary<string, FunctionSqlHandler>(StringComparer.OrdinalIgnoreCase);

        public void RegisterFunctionSqlHandler(string functionName, FunctionSqlHandler handler)
        {
            if (string.IsNullOrWhiteSpace(functionName)) throw new ArgumentNullException(nameof(functionName));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            FunctionSqlHandlers[functionName] = handler;
        }

        public bool TryGetFunctionSqlHandler(string functionName, out FunctionSqlHandler handler)
        {
            return FunctionSqlHandlers.TryGetValue(functionName, out handler);
        }
    }

    /// <summary>
    /// SqlBuilder 扩展方法
    /// </summary>
    public static class SqlHandlerMapExtensions
    {
        /// <summary>
        /// 注册函数的 SQL 语句处理器
        /// </summary>
        /// <typeparam name="T">SQL 构建器的具体类型。</typeparam>
        /// <param name="sqlBuilder">要注册处理器的 SQL 构建器实例。</param>
        /// <param name="functionName">要处理的函数名称。</param>
        /// <param name="handler">将函数表达式直接写入输出缓冲区的处理委托。</param>
        public static void RegisterFunctionSqlHandler<T>(this T sqlBuilder, string functionName, FunctionSqlHandler handler) where T : SqlBuilder
        {
            SqlBuilder.GetSqlHandlerMap<T>().RegisterFunctionSqlHandler(functionName, handler);
        }

        /// <summary>
        /// 注册多个函数的 SQL 语句处理器
        /// </summary>
        /// <typeparam name="T">SQL 构建器的具体类型。</typeparam>
        /// <param name="sqlBuilder">要注册处理器的 SQL 构建器实例。</param>
        /// <param name="functionNames">要处理的函数名称集合。</param>
        /// <param name="handler">将函数表达式直接写入输出缓冲区的处理委托。</param>
        public static void RegisterFunctionSqlHandler<T>(this T sqlBuilder, IEnumerable<string> functionNames, FunctionSqlHandler handler) where T : SqlBuilder
        {
            foreach (string functionName in functionNames)
            {
                SqlBuilder.GetSqlHandlerMap<T>().RegisterFunctionSqlHandler(functionName, handler);
            }
        }

        /// <summary>
        /// 获取函数的 SQL 语句处理器
        /// </summary>
        /// <typeparam name="T">SQL 构建器的具体类型。</typeparam>
        /// <param name="sqlBuilder">要查找处理器的 SQL 构建器实例。</param>
        /// <param name="functionName">要查找的函数名称。</param>
        /// <param name="handler">如果找到，则为对应的处理委托；否则为 null。</param>
        /// <returns>如果找到对应的处理器则返回 true，否则返回 false。</returns>
        public static bool TryGetFunctionSqlHandler<T>(this T sqlBuilder, string functionName, out FunctionSqlHandler handler) where T : SqlBuilder
        {
            return SqlBuilder.GetSqlHandlerMap<T>().TryGetFunctionSqlHandler(functionName, out handler);
        }
    }
}
