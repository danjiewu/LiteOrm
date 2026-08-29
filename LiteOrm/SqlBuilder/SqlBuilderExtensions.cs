using LiteOrm.Common;
using System;
using System.Collections.Generic;

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
        /// 读取委托接收 <typeparamref name="TDbType"/> 类型的数据库原始值，写入委托返回数据库可接受的值
        /// （<typeparamref name="TValueType"/> → object，null 应转换为 <see cref="DBNull.Value"/>）。
        /// </summary>
        /// <remarks>
        /// <para><typeparamref name="TDbType"/> 是数据库驱动实际返回的原始值 CLR 类型（读取委托的输入类型），
        /// 必须与 <paramref name="targetType"/> 经 <see cref="DbValueTypeMap.ToDbType(DbValueType)"/> 得到 <see cref="System.Data.DbType"/>
        /// 后所选读取方法（<c>GetInt32/GetString/GetDateTime/GetGuid…</c>）的返回类型匹配，可参考
        /// <see cref="DbValueTypeMap.GetReaderReturnType(System.Data.DbType)"/>。示例：
        /// <c>Int32→int、Int64→long、String→string、DateTime→DateTime、Guid→Guid、
        /// Json/Jsonb→string、Binary→byte[]、Object 及 <see cref="DbValueType.SByte"/>/<see cref="DbValueType.UInt16"/>/
        /// <see cref="DbValueType.UInt32"/>/<see cref="DbValueType.UInt64"/>/<see cref="DbValueType.Time"/>/
        /// <see cref="DbValueType.DateTimeOffset"/> 等无类型化读取方法的类型→object</c>。</para>
        /// <para>当声明的 <typeparamref name="TDbType"/> 与实际读取返回类型不一致时，框架会经非泛型委托的装箱 / <c>Convert.ChangeType</c>
        /// 或编译期映射的隐式转换桥接——功能可用但不推荐；声明正确的类型可保证读取类型严格并命中强类型
        /// <see cref="IDbValueConverter{TDbType,TValueType}"/> 泛型匹配。</para>
        /// </remarks>
        /// <typeparam name="T">SQL 构建器的具体类型。</typeparam>
        /// <typeparam name="TDbType">数据库驱动返回的数据库值 CLR 类型（读取委托的输入类型）；须与 <paramref name="targetType"/> 的读取方法返回类型一致。</typeparam>
        /// <typeparam name="TValueType">实体属性 / .NET 值类型。</typeparam>
        /// <param name="sqlBuilder">要注册转换器的 SQL 构建器实例。</param>
        /// <param name="targetType">目标数据库取值类型。</param>
        /// <param name="fromDb">数据库值 → .NET 值 的转换委托。</param>
        /// <param name="toDb">.NET 值 → 数据库值 的转换委托。</param>
        public static void RegisterDbValueConverter<T, TDbType, TValueType>(this T sqlBuilder, DbValueType targetType, DbConvertHandler<TDbType, TValueType>? fromDb, DbConvertHandler<TValueType, object>? toDb) where T : SqlBuilder
        {
            SqlBuilder.GetDbValueConverterMap<T>().RegisterConverter(new FuncDbValueConverter<TDbType, TValueType>(fromDb, toDb), targetType);
        }

        /// <summary>
        /// 注册双向数据库值转换器。注册主键为 (值类型, 目标数据库取值类型)，读取与写入共用同一注册表：        /// 读取委托接收 <typeparamref name="TDbType"/> 类型的数据库原始值，写入委托返回数据库可接受的值
        /// （<typeparamref name="TValueType"/> → object，null 应转换为 <see cref="DBNull.Value"/>）。
        /// </summary>
        /// <remarks>
        /// <para><typeparamref name="TDbType"/> 是数据库驱动实际返回的原始值 CLR 类型（读取委托的输入类型），
        /// 必须与 <paramref name="targetType"/> 经 <see cref="DbValueTypeMap.ToDbType(DbValueType)"/> 得到 <see cref="System.Data.DbType"/>
        /// 后所选读取方法（<c>GetInt32/GetString/GetDateTime/GetGuid…</c>）的返回类型匹配，可参考
        /// <see cref="DbValueTypeMap.GetReaderReturnType(System.Data.DbType)"/>。示例：
        /// <c>Int32→int、Int64→long、String→string、DateTime→DateTime、Guid→Guid、
        /// Json/Jsonb→string、Binary→byte[]、Object 及 <see cref="DbValueType.SByte"/>/<see cref="DbValueType.UInt16"/>/
        /// <see cref="DbValueType.UInt32"/>/<see cref="DbValueType.UInt64"/>/<see cref="DbValueType.Time"/>/
        /// <see cref="DbValueType.DateTimeOffset"/> 等无类型化读取方法的类型→object</c>。</para>
        /// <para>当声明的 <typeparamref name="TDbType"/> 与实际读取返回类型不一致时，框架会经非泛型委托的装箱 / <c>Convert.ChangeType</c>
        /// 或编译期映射的隐式转换桥接——功能可用但不推荐；声明正确的类型可保证读取类型严格并命中强类型
        /// <see cref="IDbValueConverter{TDbType,TValueType}"/> 泛型匹配。</para>
        /// <typeparam name="T">SQL 构建器的具体类型。</typeparam>
        /// <typeparam name="TDbType">数据库驱动返回的数据库值 CLR 类型（读取委托的输入类型）；须与 <paramref name="targetType"/> 的读取方法返回类型一致。</typeparam>
        /// <typeparam name="TValueType">实体属性 / .NET 值类型。</typeparam>
        /// <param name="sqlBuilder">要注册转换器的 SQL 构建器实例。</param>
        /// <param name="targetType">目标数据库取值类型。</param>
        /// <param name="converter">要注册的双向数据库值转换器。</param>
        public static void RegisterDbValueConverter<T, TDbType, TValueType>(this T sqlBuilder, DbValueType targetType, IDbValueConverter<TDbType, TValueType> converter) where T : SqlBuilder
        {
            SqlBuilder.GetDbValueConverterMap<T>().RegisterConverter(converter, targetType);
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
        /// 注册按默认方式（函数名加参数列表）生成 SQL 的函数处理器，等价于注册 <see cref="SqlBuilder.DefaultFunctionSqlHandler"/>。
        /// 适用于函数名与目标 SQL 函数名一致、参数直接拼接即可的场景。
        /// </summary>
        /// <typeparam name="T">SQL 构建器的具体类型。</typeparam>
        /// <param name="sqlBuilder">要注册处理器的 SQL 构建器实例。</param>
        /// <param name="functionName">要处理的函数名称。</param>
        public static void RegisterFunctionSqlHandler<T>(this T sqlBuilder, string functionName) where T : SqlBuilder
        {
            SqlBuilder.GetSqlHandlerMap<T>().RegisterFunctionSqlHandler(functionName, SqlBuilder.DefaultFunctionSqlHandler);
        }

        /// <summary>
        /// 注册按默认方式（函数名加参数列表）生成 SQL 的多个函数处理器，等价于为每个函数名注册 <see cref="SqlBuilder.DefaultFunctionSqlHandler"/>。
        /// </summary>
        /// <typeparam name="T">SQL 构建器的具体类型。</typeparam>
        /// <param name="sqlBuilder">要注册处理器的 SQL 构建器实例。</param>
        /// <param name="functionNames">要处理的函数名称集合。</param>
        public static void RegisterFunctionSqlHandler<T>(this T sqlBuilder, IEnumerable<string> functionNames) where T : SqlBuilder
        {
            foreach (string functionName in functionNames)
            {
                SqlBuilder.GetSqlHandlerMap<T>().RegisterFunctionSqlHandler(functionName, SqlBuilder.DefaultFunctionSqlHandler);
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
