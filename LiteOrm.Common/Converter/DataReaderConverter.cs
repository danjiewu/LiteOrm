using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace LiteOrm.Common
{
    /// <summary>
    /// 通过动态编译创建将 <see cref="DbDataReader"/> 行映射到对象的委托。
    /// 编译结果按目标类型与列架构缓存，避免重复编译开销。
    /// </summary>
    public static class DataReaderConverter
    {
        private static readonly ConcurrentDictionary<(Type, string), Delegate> _cache =
            new ConcurrentDictionary<(Type, string), Delegate>();

        private static readonly MethodInfo _getValueMethod =
            typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetValue), new[] { typeof(int) });

        private static readonly MethodInfo _convertValueMethod =
            typeof(DataReaderConverter).GetMethod(nameof(ConvertValue), BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly MethodInfo _isDBNullMethod =
            typeof(DbDataReader).GetMethod(nameof(DbDataReader.IsDBNull), new[] { typeof(int) });

        private static readonly MethodInfo _getFieldValueMethod =
            typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFieldValue), new[] { typeof(int) });

        private static readonly MethodInfo _changeTypeMethod =
            typeof(Convert).GetMethod(nameof(Convert.ChangeType), new[] { typeof(object), typeof(Type) });

        private static readonly Dictionary<Type, MethodInfo> _typedReaderMethods = new Dictionary<Type, MethodInfo>
        {
            [typeof(bool)]     = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetBoolean),  new[] { typeof(int) }),
            [typeof(byte)]     = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetByte),     new[] { typeof(int) }),
            [typeof(char)]     = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetChar),     new[] { typeof(int) }),
            [typeof(short)]    = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetInt16),    new[] { typeof(int) }),
            [typeof(int)]      = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetInt32),    new[] { typeof(int) }),
            [typeof(long)]     = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetInt64),    new[] { typeof(int) }),
            [typeof(float)]    = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFloat),    new[] { typeof(int) }),
            [typeof(double)]   = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDouble),   new[] { typeof(int) }),
            [typeof(decimal)]  = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDecimal),  new[] { typeof(int) }),
            [typeof(string)]   = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetString),   new[] { typeof(int) }),
            [typeof(DateTime)] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDateTime), new[] { typeof(int) }),
            [typeof(Guid)]     = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetGuid),     new[] { typeof(int) }),
            [typeof(Stream)]   = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetStream),   new[] { typeof(int) }),
        };

        /// <summary>
        /// 按 <see cref="DbType"/> 选择最自然的类型化读取方法。
        /// <see cref="DbType.Binary"/> 单独处理（<see cref="DbDataReader.GetFieldValue{T}"/> 或 <see cref="DbDataReader.GetStream"/>）。
        /// </summary>
        private static readonly Dictionary<DbType, MethodInfo> _dbTypeReaderMethods = new Dictionary<DbType, MethodInfo>
        {
            [DbType.Boolean]               = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetBoolean),  new[] { typeof(int) }),
            [DbType.Byte]                  = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetByte),     new[] { typeof(int) }),
            [DbType.Int16]                 = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetInt16),    new[] { typeof(int) }),
            [DbType.Int32]                 = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetInt32),    new[] { typeof(int) }),
            [DbType.Int64]                 = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetInt64),    new[] { typeof(int) }),
            [DbType.Single]                = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFloat),    new[] { typeof(int) }),
            [DbType.Double]                = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDouble),   new[] { typeof(int) }),
            [DbType.Decimal]               = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDecimal),  new[] { typeof(int) }),
            [DbType.Currency]              = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDecimal),  new[] { typeof(int) }),
            [DbType.String]                = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetString),   new[] { typeof(int) }),
            [DbType.AnsiString]            = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetString),   new[] { typeof(int) }),
            [DbType.AnsiStringFixedLength] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetString),   new[] { typeof(int) }),
            [DbType.StringFixedLength]     = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetString),   new[] { typeof(int) }),
            [DbType.Xml]                   = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetString),   new[] { typeof(int) }),
            [DbType.DateTime]              = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDateTime), new[] { typeof(int) }),
            [DbType.Date]                  = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDateTime), new[] { typeof(int) }),
            [DbType.DateTime2]             = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDateTime), new[] { typeof(int) }),
            [DbType.Guid]                  = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetGuid),     new[] { typeof(int) }),
            // DbType.Binary → GetFieldValue<byte[]> / GetStream (handled in BuildRawReadExpression)
        };

        private static readonly ConcurrentDictionary<Type, Delegate> _cacheByType =
            new ConcurrentDictionary<Type, Delegate>();

        /// <summary>
        /// 获取将 <see cref="DbDataReader"/> 当前行转换为 <typeparamref name="TResult"/> 实例的编译委托。
        /// 对于匿名类型，基于读取器的列架构缓存编译委托，通过构造函数参数名与列名匹配；
        /// 对于普通类型，委托给 <see cref="GetConverter{TResult}()"/> 使用 <see cref="TableInfoProvider.Default"/> 进行位置映射。
        /// </summary>
        /// <typeparam name="TResult">目标类型。</typeparam>
        /// <param name="reader">已打开的数据读取器，用于读取列架构信息（匿名类型时使用）。</param>
        /// <returns>编译后的映射委托。</returns>
        public static Func<DbDataReader, TResult> GetConverter<TResult>(DbDataReader reader)
        {
            Type type = typeof(TResult);
            if (IsAnonymousType(type))
            {
                string columnKey = BuildColumnKey(reader);
                return (Func<DbDataReader, TResult>)_cache.GetOrAdd((type, columnKey), _ => CompileAnonymousConverter<TResult>(reader));
            }
            return GetConverter<TResult>();
        }

        /// <summary>
        /// 获取将 <see cref="DbDataReader"/> 当前行转换为 <typeparamref name="TResult"/> 实例的编译委托。
        /// 通过 <see cref="TableInfoProvider.Default"/> 读取 <typeparamref name="TResult"/> 对应的表视图，
        /// 并依据视图的 <see cref="SqlTable.SelectColumns"/> 进行位置映射，使用类型化读取方法避免装箱。
        /// 以 <typeparamref name="TResult"/> 类型为缓存键，首次调用时编译，后续调用直接复用。
        /// </summary>
        /// <typeparam name="TResult">目标类型。</typeparam>
        /// <returns>编译后的映射委托。</returns>
        public static Func<DbDataReader, TResult> GetConverter<TResult>()
        {
            return (Func<DbDataReader, TResult>)_cacheByType.GetOrAdd(typeof(TResult), _ => CompileConverter<TResult>());
        }

        private static string BuildColumnKey(DbDataReader reader)
        {
            int fieldCount = reader.FieldCount;
            var sb = new StringBuilder(fieldCount * 16);
            for (int i = 0; i < fieldCount; i++)
            {
                if (i > 0) sb.Append('|');
                sb.Append(reader.GetName(i));
            }
            return sb.ToString();
        }

        private static Func<DbDataReader, TResult> CompileConverter<TResult>()
        {
            Type resultType = typeof(TResult);
            var readerParam = Expression.Parameter(typeof(DbDataReader), "reader");

            if (IsScalarType(resultType))
                return CompileScalarConverter<TResult>(readerParam);

            var selectColumns = (TableInfoProvider.Default?.GetTableView(resultType)
                ?? throw new InvalidOperationException($"TableInfoProvider.Default is not configured, cannot resolve columns for type '{resultType.FullName}'."))
                .SelectColumns;
            return CompileConverterByColumns<TResult>(selectColumns);
        }

        private static Func<DbDataReader, TResult> CompileScalarConverter<TResult>(ParameterExpression readerParam)
        {
            var body = BuildTypedReadExpression(readerParam, 0, typeof(TResult));
            return Expression.Lambda<Func<DbDataReader, TResult>>(body, readerParam).Compile();
        }

        private static Func<DbDataReader, TResult> CompileAnonymousConverter<TResult>(DbDataReader reader)
        {
            Type resultType = typeof(TResult);
            var readerParam = Expression.Parameter(typeof(DbDataReader), "reader");
            var ctor = resultType.GetConstructors()[0];
            var ctorParams = ctor.GetParameters();

            var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
                columnMap[reader.GetName(i)] = i;

            var args = new Expression[ctorParams.Length];
            for (int i = 0; i < ctorParams.Length; i++)
            {
                ParameterInfo param = ctorParams[i];
                args[i] = columnMap.TryGetValue(param.Name, out int ordinal)
                    ? BuildTypedReadExpression(readerParam, ordinal, param.ParameterType)
                    : Expression.Default(param.ParameterType);
            }

            var body = Expression.New(ctor, args);
            return Expression.Lambda<Func<DbDataReader, TResult>>(body, readerParam).Compile();
        }

        /// <summary>
        /// 构建读取指定列的完整表达式（含 IsDBNull 守卫与 Nullable 封装）。
        /// 优先按 <paramref name="dbType"/> 选择与数据库列类型精确对应的读取方法；
        /// 若读取方法的返回类型与属性 CLR 类型不符（如枚举、数值扩宽），则自动插入 Convert 表达式。
        /// 未提供 <paramref name="dbType"/> 时退回到按属性 CLR 类型查找，仍无匹配则使用 GetValue + ConvertValue 兜底。
        /// </summary>
        private static Expression BuildTypedReadExpression(
            ParameterExpression readerParam, int ordinal, Type targetType, DbType? dbType = null)
        {
            Type coreType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            var ordinalExpr = Expression.Constant(ordinal);

            Expression readExpr = BuildRawReadExpression(readerParam, ordinalExpr, coreType, dbType);

            // Convert reader return type → core property type (enum cast, numeric widening, etc.)
            // If no direct conversion operator exists, fall back to Convert.ChangeType.
            if (readExpr.Type != coreType)
            {
                try
                {
                    readExpr = Expression.Convert(readExpr, coreType);
                }
                catch (InvalidOperationException)
                {
                    readExpr = Expression.Convert(
                        Expression.Call(_changeTypeMethod,
                            Expression.Convert(readExpr, typeof(object)),
                            Expression.Constant(coreType)),
                        coreType);
                }
            }

            // Wrap as Nullable<T>
            if (targetType != coreType)
                readExpr = Expression.Convert(readExpr, targetType);

            var isNull = Expression.Call(readerParam, _isDBNullMethod, ordinalExpr);
            return Expression.Condition(isNull, Expression.Default(targetType), readExpr);
        }

        /// <summary>
        /// 返回读取列值的原始表达式（不含 IsDBNull 检查与 Nullable 封装）。
        /// 选取顺序：DbType 映射 → CLR 类型映射 → byte[] 特殊路径 → GetValue + ConvertValue 兜底。
        /// </summary>
        private static Expression BuildRawReadExpression(
            ParameterExpression readerParam, Expression ordinalExpr, Type coreType, DbType? dbType)
        {
            // 1. DbType-driven: pick the reader method that matches the database column type
            if (dbType.HasValue)
            {
                // Binary column: GetFieldValue<byte[]>, or GetStream if the target is a Stream
                if (dbType.Value == DbType.Binary)
                {
                    if (typeof(Stream).IsAssignableFrom(coreType))
                        return Expression.Call(readerParam, _typedReaderMethods[typeof(Stream)], ordinalExpr);
                    return Expression.Call(readerParam,
                        _getFieldValueMethod.MakeGenericMethod(typeof(byte[])), ordinalExpr);
                }

                if (_dbTypeReaderMethods.TryGetValue(dbType.Value, out MethodInfo dbMethod))
                    return Expression.Call(readerParam, dbMethod, ordinalExpr);
            }

            // 2. CLR type-driven: anonymous projections, scalar columns, and unmapped DbType values
            if (_typedReaderMethods.TryGetValue(coreType, out MethodInfo typedMethod))
                return Expression.Call(readerParam, typedMethod, ordinalExpr);

            // 3. byte[] without DbType context
            if (coreType == typeof(byte[]))
                return Expression.Call(readerParam,
                    _getFieldValueMethod.MakeGenericMethod(typeof(byte[])), ordinalExpr);

            // 4. Fallback: GetValue + ConvertValue<T> (enums, TimeSpan, DateTimeOffset, etc.)
            return Expression.Call(
                _convertValueMethod.MakeGenericMethod(coreType),
                Expression.Call(readerParam, _getValueMethod, ordinalExpr));
        }

        /// <summary>
        /// 编译基于 <see cref="SqlColumn"/> 定义的位置映射委托。
        /// <paramref name="selectColumns"/>[i] 对应读取器第 i 列，使用列的属性名定位目标属性。
        /// </summary>
        private static Func<DbDataReader, TResult> CompileConverterByColumns<TResult>(SqlColumn[] selectColumns)
        {
            Type resultType = typeof(TResult);
            var readerParam = Expression.Parameter(typeof(DbDataReader), "reader");
            var ctor = resultType.GetConstructor(Type.EmptyTypes)
                ?? throw new InvalidOperationException($"Type '{resultType.FullName}' does not have a public parameterless constructor.");

            var bindings = new List<MemberBinding>();
            int count = selectColumns.Length;
            for (int i = 0; i < count; i++)
            {
                SqlColumn column = selectColumns[i];
                var prop = resultType.GetProperty(column.PropertyName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || !prop.CanWrite) continue;

                bindings.Add(Expression.Bind(prop, BuildTypedReadExpression(readerParam, i, prop.PropertyType, column.Definition?.DbType)));
            }

            var body = Expression.MemberInit(Expression.New(ctor), bindings);
            return Expression.Lambda<Func<DbDataReader, TResult>>(body, readerParam).Compile();
        }

        private static bool IsScalarType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type.IsPrimitive
                || type == typeof(string)
                || type == typeof(decimal)
                || type == typeof(DateTime)
                || type == typeof(Guid)
                || type == typeof(byte[])
                || type == typeof(Stream)
                || type == typeof(DateTimeOffset)
                || type == typeof(TimeSpan);
        }

        private static bool IsAnonymousType(Type type) =>
            !type.IsPublic
            && type.IsGenericType
            && type.Name.StartsWith("<>")
            && Attribute.IsDefined(type, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute));

        private static T ConvertValue<T>(object value)
        {
            if (value == null || value is DBNull) return default;
            if (value is T t) return t;
            Type targetType = typeof(T);
            Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (underlyingType.IsEnum)
                return (T)Enum.ToObject(underlyingType, Convert.ChangeType(value, Enum.GetUnderlyingType(underlyingType)));
            return (T)Convert.ChangeType(value, underlyingType);
        }
    }
}
