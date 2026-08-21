using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace LiteOrm
{
    /// <summary>
    /// SqlBuilder 扩展方法
    /// </summary>
    public static class SqlBuilderExtensions
    {
        /// <summary>
        /// 注册双向数据库值转换器。注册主键为 (值类型, 目标数据库取值类型)，读取与写入共用同一注册表：
        /// 读取按 (目标属性类型, 列取值类型) 查找，写入按 (源值类型, 目标取值类型) 查找。
        /// </summary>
        /// <typeparam name="T">SQL 构建器的具体类型。</typeparam>
        /// <param name="sqlBuilder">要注册转换器的 SQL 构建器实例。</param>
        /// <param name="targetType">目标数据库取值类型。</param>
        /// <param name="converter">双向转换器实例。</param>
        public static void RegisterDbValueConverter<T>(this T sqlBuilder, DbValueType targetType, IDbValueConverter converter) where T : SqlBuilder
        {
            SqlBuilder.GetDbValueConverterMap<T>().RegisterConverter(converter, targetType);
        }

        /// <summary>
        /// 注册基于委托的双向数据库值转换器（快捷方式）。
        /// 读取委托接收数据库驱动返回的原始值（object），写入委托返回数据库可接受的值（object，null 应转换为 <see cref="DBNull.Value"/>）。
        /// </summary>
        /// <typeparam name="T">SQL 构建器的具体类型。</typeparam>
        /// <typeparam name="TValueType">实体属性 / .NET 值类型。</typeparam>
        /// <param name="sqlBuilder">要注册转换器的 SQL 构建器实例。</param>
        /// <param name="targetType">目标数据库取值类型。</param>
        /// <param name="fromDb">数据库值 → .NET 值 的转换委托。</param>
        /// <param name="toDb">.NET 值 → 数据库值 的转换委托。</param>
        public static void RegisterDbValueConverter<T, TValueType>(this T sqlBuilder, DbValueType targetType, Func<object, TValueType>? fromDb, Func<TValueType, object>? toDb) where T : SqlBuilder
        {
            SqlBuilder.GetDbValueConverterMap<T>().RegisterConverter(new FuncDbValueConverter<object, TValueType>(fromDb, toDb), targetType);
        }

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
            SqlBuilder.GetSqlHandlerMap<T>().RegisterFunctionSqlHandler(functionName, (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, SqlBuilder sqlBuilder, ICollection<Param> outputParams) =>
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
        public static bool TryGetFunctionSqlHandler<T>(this T sqlBuilder, string? functionName, out FunctionSqlHandler? handler) where T : SqlBuilder
        {
            return SqlBuilder.GetSqlHandlerMap<T>().TryGetFunctionSqlHandler(functionName ?? throw new ArgumentNullException(nameof(functionName)), out handler);
        }
    }
}
