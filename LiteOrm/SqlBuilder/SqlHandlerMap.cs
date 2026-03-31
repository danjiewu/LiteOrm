using LiteOrm.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LiteOrm
{
    /// <summary>
    /// 函数 SQL 生成委托，将函数表达式直接写入 <see cref="ValueStringBuilder"/>。
    /// </summary>
    /// <param name="outSql"></param>
    /// <param name="expr"></param>
    /// <param name="context"></param>
    /// <param name="sqlBuilder"></param>
    /// <param name="outputParams"></param>
    public delegate void FunctionSqlHandler(ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams);

    /// <summary>
    /// 简单函数 SQL 生成委托，直接提供函数名称和参数列表，适用于仅需调整函数格式，不需要自定义解析参数的场景。
    /// </summary>
    /// <param name="outSql"></param>
    /// <param name="functionName"></param>
    /// <param name="arguments"></param>
    public delegate void SimpleFunctionSqlHandler(ref ValueStringBuilder outSql, string functionName, ICollection<string> arguments);

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
        /// 注册函数的 SQL 语句处理器，根据函数名称和解析好的参数语句生成 SQL ，适用于仅需调整函数格式。
        /// </summary>
        /// <typeparam name="T">SQL 构建器的具体类型。</typeparam>
        /// <param name="sqlBuilder">要注册处理器的 SQL 构建器实例。</param>
        /// <param name="functionName">要处理的函数名称。</param>
        /// <param name="handler">将函数名成和参数表达式直接写入输出缓冲区的处理委托。</param>
        public static void RegisterFunctionSqlHandler<T>(this T sqlBuilder, string functionName, SimpleFunctionSqlHandler handler) where T : SqlBuilder
        {
            SqlBuilder.GetSqlHandlerMap<T>().RegisterFunctionSqlHandler(functionName, (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
            {
                List<string> arguments = new List<string>();
                foreach (var arg in expr.Args)
                {
                    arguments.Add(arg.ToSql(context, sqlBuilder, outputParams));
                }
                handler(ref outSql, functionName, arguments);
            });
        }

        /// <summary>
        /// 注册函数的 SQL 语句处理器，根据函数名称和解析好的参数语句生成 SQL ，适用于仅需调整函数格式。
        /// </summary>
        /// <typeparam name="T">SQL 构建器的具体类型。</typeparam>
        /// <param name="sqlBuilder">要注册处理器的 SQL 构建器实例。</param>
        /// <param name="functionNames">要处理的函数名称集合。</param>
        /// <param name="handler">将函数名成和参数表达式直接写入输出缓冲区的处理委托。</param>
        public static void RegisterFunctionSqlHandler<T>(this T sqlBuilder, IEnumerable<string> functionNames, SimpleFunctionSqlHandler handler) where T : SqlBuilder
        {
            foreach (string functionName in functionNames)
            {
                RegisterFunctionSqlHandler(sqlBuilder, functionName, handler);
            }
        }

        /// <summary>
        /// 获取函数的 SQL 语句处理器
        /// </summary>
        /// <typeparam name="T">SQL 构建器的具体类型。</typeparam>
        /// <param name="sqlBuilder">要获取处理器的 SQL 构建器实例。</param>
        /// <param name="functionName">要获取的函数名称。</param>
        /// <param name="handler">输出参数，返回对应的函数 SQL 语句处理器。</param>
        /// <returns></returns>
        public static bool TryGetFunctionSqlHandler<T>(this T sqlBuilder, string functionName, out FunctionSqlHandler handler) where T : SqlBuilder
        {
            return SqlBuilder.GetSqlHandlerMap<T>().TryGetFunctionSqlHandler(functionName, out handler);
        }
    }
}
