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
        /// 注册从 <typeparamref name="TSource"/> 到 <typeparamref name="TResult"/> 的数据库读取转换器（泛型版本）。
        /// </summary>
        /// <typeparam name="T">SQL 构建器的具体类型。</typeparam>
        /// <typeparam name="TSource">从数据库读取的值的类型。</typeparam>
        /// <typeparam name="TResult">要转换的目标实体属性类型。</typeparam>
        /// <param name="sqlBuilder">要注册转换器的 SQL 构建器实例。</param>
        /// <param name="handler">转换器函数。</param>
        public static void RegisterDbReadConverter<T, TSource, TResult>(this T sqlBuilder, Func<TSource, TResult> handler) where T : SqlBuilder
        {
            SqlBuilder.GetDbValueConverterMap<T>().RegisterReadConverter(handler);
        }

        /// <summary>
        /// 尝试获取从 <typeparamref name="TSource"/> 到 <typeparamref name="TResult"/> 的数据库读取转换器（泛型版本）。
        /// </summary>
        /// <typeparam name="T">SQL 构建器的具体类型。</typeparam>
        /// <typeparam name="TSource">从数据库读取的值的类型。</typeparam>
        /// <typeparam name="TResult">要转换的目标实体属性类型。</typeparam>
        /// <param name="sqlBuilder">要获取转换器的 SQL 构建器实例。</param>
        /// <param name="handler">输出转换器函数。</param>
        /// <returns>如果成功获取转换器函数，则返回 true；否则返回 false。</returns>
        public static bool TryGetDbReadConverter<T, TSource, TResult>(this T sqlBuilder, out Func<TSource, TResult>? handler) where T : SqlBuilder
        {
            return SqlBuilder.GetDbValueConverterMap<T>().TryGetReadConverter<TSource, TResult>(out handler);
        }

        /// <summary>
        /// 按 (源类型, 目标类型) 查找数据库读取转换器的非泛型版本。
        /// </summary>
        /// <typeparam name="T">SQL 构建器的具体类型。</typeparam>
        /// <param name="sqlBuilder">要获取转换器的 SQL 构建器实例。</param>
        /// <param name="key">转换器的源类型与目标类型。</param>
        /// <param name="handler">输出转换器函数。</param>
        /// <returns>如果成功获取转换器函数，则返回 true；否则返回 false。</returns>
        public static bool TryGetDbReadConverter<T>(this T sqlBuilder, (Type Source, Type Target) key, out Func<object, object>? handler) where T : SqlBuilder
        {
            return SqlBuilder.GetDbValueConverterMap<T>().TryGetReadConverter(key, out handler);
        }

        /// <summary>
        /// 注册从 <typeparamref name="TSource"/> 到目标 <see cref="DbValueType"/> 的数据库写入转换器。
        /// </summary>
        /// <typeparam name="T">SQL 构建器的具体类型。</typeparam>
        /// <typeparam name="TSource">源值类型。</typeparam>
        /// <param name="sqlBuilder">要注册转换器的 SQL 构建器实例。</param>
        /// <param name="targetType">目标数据库取值类型。</param>
        /// <param name="handler">转换器函数。</param>
        public static void RegisterDbWriteConverter<T, TSource>(this T sqlBuilder, DbValueType targetType, Func<TSource, object> handler) where T : SqlBuilder
        {
            SqlBuilder.GetDbValueConverterMap<T>().RegisterWriteConverter(targetType, handler);
        }

        /// <summary>
        /// 尝试获取从 <typeparamref name="TSource"/> 到目标 <see cref="DbValueType"/> 的数据库写入转换器（泛型版本）。
        /// </summary>
        /// <typeparam name="T">SQL 构建器的具体类型。</typeparam>
        /// <typeparam name="TSource">源值类型。</typeparam>
        /// <param name="sqlBuilder">要获取转换器的 SQL 构建器实例。</param>
        /// <param name="targetType">目标数据库取值类型。</param>
        /// <param name="handler">输出转换器函数。</param>
        /// <returns>如果成功获取转换器函数，则返回 true；否则返回 false。</returns>
        public static bool TryGetDbWriteConverter<T, TSource>(this T sqlBuilder, DbValueType targetType, out Func<TSource, object>? handler) where T : SqlBuilder
        {
            return SqlBuilder.GetDbValueConverterMap<T>().TryGetWriteConverter<TSource>((typeof(TSource), targetType), out handler);
        }

        /// <summary>
        /// 按 (源类型, DbValueType) 查找数据库写入转换器的非泛型版本。
        /// </summary>
        /// <typeparam name="T">SQL 构建器的具体类型。</typeparam>
        /// <param name="sqlBuilder">要获取转换器的 SQL 构建器实例。</param>
        /// <param name="key">转换器的源类型与目标数据库取值类型。</param>
        /// <param name="handler">输出转换器函数。</param>
        /// <returns>如果成功获取转换器函数，则返回 true；否则返回 false。</returns>
        public static bool TryGetDbWriteConverter<T>(this T sqlBuilder, (Type Source, DbValueType Target) key, out Func<object, object>? handler) where T : SqlBuilder
        {
            return SqlBuilder.GetDbValueConverterMap<T>().TryGetWriteConverter(key, out handler);
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
