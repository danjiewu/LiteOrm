using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

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

        /// <summary>
        /// 将 .NET 值转换为数据库可接受的值（写入统一入口，与 <see cref="DbConverterHelper.ConvertFromDbValue(IDbConverter, object?, Type)"/> 的读取方向对称）：
        /// null 返回 <see cref="DBNull.Value"/>；优先使用按 (值类型, 数据库取值类型) 注册的转换器
        /// （默认类型转换与方言特定转换均通过 <see cref="LiteOrmConverterInitializer"/> 预注册实现）；
        /// 未注册时使用通用兜底：数组/Json 序列化、按运行时类型命中注册转换器、枚举转换、bool/DateTimeOffset/TimeSpan 适配，
        /// 最后以 <see cref="Convert.ChangeType(object, Type)"/> 兜底（失败时原样返回交由驱动绑定）。
        /// </summary>
        /// <param name="dbConverter">数据库值转换器。</param>
        /// <param name="value">.NET 值。</param>
        /// <param name="dbValueType">数据字段取值类型（可含 <see cref="DbValueType.Array"/> 掩码，为 null/Object/Default 时按值的运行时类型推断）。</param>
        /// <returns>数据库可接受的值。</returns>
        public static object ConvertToDbValue(this IDbConverter dbConverter, object? value, DbValueType? dbValueType)
        {
            if (value is null) return DBNull.Value;

            Type type = value.GetType();
            DbValueType dbType = (dbValueType is null || dbValueType == DbValueType.Object || dbValueType == DbValueType.Default)
                ? dbConverter.GetDbValueType(type)
                : dbValueType.Value;

            // 注册的转换器优先（如 bool/Guid/TimeSpan/DateTime/DateTimeOffset/string 及方言特定转换）；
            // 数组/Json 掩码的组合通常未注册，直接落入通用兜底
            if (!dbType.HasArray()
                && dbConverter.GetDbValueConverter(type.GetUnderlyingType(), dbType) is IDbValueConverter converter)
            {
                return converter.ConvertToDbValue(value);
            }

            return ConvertToDbValueCore(dbConverter, value, dbType);
        }

        /// <summary>
        /// 通用兜底的「.NET 值 → 数据库值」转换：数组/Json 序列化、按运行时类型命中注册转换器、
        /// 枚举转换、bool/DateTimeOffset/TimeSpan 适配，最后以 <see cref="Convert.ChangeType(object, Type)"/> 兜底。
        /// </summary>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL3050",
            Justification = "JSON serialization path is only triggered when the value is a complex object/collection; under AOT, users must provide a System.Text.Json source-gen context for complex property types, otherwise a NotSupportedException is thrown at runtime.")]
#endif
        private static object ConvertToDbValueCore(IDbConverter dbConverter, object value, DbValueType dbType)
        {
            // 数组列：原生数组方言（如 PostgreSQL）原样返回交由驱动绑定；其余方言回退为 JSON 字符串存储
            if (dbType.HasArray() && ColumnDefinitionExtensions.IsCollectionType(value.GetType()))
                return dbConverter is SqlBuilder { SupportsNativeArrays: true } ? value : ToJsonString(dbConverter, value);

            // Json/Jsonb 列：复杂值序列化为 JSON 字符串，标量直返字符串形式
            if (dbType == DbValueType.Json || dbType == DbValueType.Jsonb)
                return IsComplexJsonValue(value) ? ToJsonString(dbConverter, value) : value.ToString()!;

            Type type = value.GetType();

            // 优先命中按实际值类型注册的转换器（支持 object/多态属性按运行时类型转换）
            if (!dbType.HasArray()
                && dbConverter.GetDbValueConverter(type, dbType) is IDbValueConverter runtimeConverter)
                return runtimeConverter.ConvertToDbValue(value);

            // 处理枚举：字符串类列存名称，其余按基础类型转换
            if (type.IsEnum)
            {
                if (dbType == DbValueType.String || dbType == DbValueType.AnsiString ||
                    dbType == DbValueType.StringFixedLength || dbType == DbValueType.AnsiStringFixedLength)
                {
                    return value.ToString()!;
                }
                return Convert.ChangeType(value, Enum.GetUnderlyingType(type));
            }

            // 通用兜底：bool / DateTimeOffset / TimeSpan 的通用适配
            if (value is bool b) return b ? 1 : 0;
            if (value is DateTimeOffset dto) return dto.DateTime;
            if (value is TimeSpan ts) return ts.Ticks;

            // 字符串类列的非标量值（集合、复杂对象）序列化为 JSON 字符串（SQLite 等以字符串存储复杂类型的方言）
            if ((dbType == DbValueType.String || dbType == DbValueType.AnsiString ||
                 dbType == DbValueType.StringFixedLength || dbType == DbValueType.AnsiStringFixedLength)
                && IsComplexJsonValue(value))
            {
                return ToJsonString(dbConverter, value);
            }

            // 最后兜底：目标类型一致直返；否则 ChangeType，失败时原样返回交由驱动绑定
            Type targetType = dbType.ToType();
            if (targetType.IsInstanceOfType(value)) return value;
            try { return Convert.ChangeType(value, targetType); }
            catch (InvalidCastException) { return value; }
        }

        /// <summary>
        /// 判断指定值是否需要以 JSON 序列化（非标量类型）。
        /// </summary>
        private static bool IsComplexJsonValue(object value)
        {
            Type type = value.GetType();
            if (type.IsPrimitive) return false;
            if (type.IsEnum) return false;
            if (type == typeof(string)
                || type == typeof(char)
                || type == typeof(decimal)
                || type == typeof(DateTime)
                || type == typeof(DateTimeOffset)
                || type == typeof(Guid)
                || type == typeof(TimeSpan)
                || type == typeof(byte[])) return false;
            return true;
        }

        /// <summary>
        /// 经 <see cref="SqlBuilder.ToJsonString(object)"/> 序列化（保留方言覆写点）；
        /// 非 SqlBuilder 的 <see cref="IDbConverter"/> 实现直接使用 System.Text.Json 序列化。
        /// </summary>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL3050",
            Justification = "JSON serialization is only triggered when the value is a complex object/collection; under AOT, users must provide a System.Text.Json source-gen context for complex property types, otherwise a NotSupportedException is thrown at runtime.")]
#endif
        private static string ToJsonString(IDbConverter dbConverter, object value)
        {
            return dbConverter is SqlBuilder builder
                ? builder.ToJsonString(value)
                : JsonSerializer.Serialize(value, value.GetType());
        }
    }
}
