using LiteOrm.Common;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace LiteOrm
{
    /// <summary>
    /// 通过动态编译创建将 <see cref="AutoLockDataReader"/> 行映射到对象的委托。
    /// 编译结果按目标类型与列架构缓存，避免重复编译开销。
    /// </summary>
    public static class DataReaderConverter
    {
        private static readonly ConcurrentDictionary<(Type, string), Delegate> _cache =
            new ConcurrentDictionary<(Type, string), Delegate>();

        private static readonly MethodInfo? _getValueMethod =
            typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetValue), new[] { typeof(int) });

        // 读取列值的统一转换分发（注册转换器优先，SqlBuilder 兜底），表达式树经 MethodInfo 调用
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2111",
            Justification = "在运行时通过 MethodInfo 获取并调用该方法；AOT 场景下复杂类型列须由源生成器 mapper 处理，此处仅兜底已由 DAO 注册的目标类型。")]
#endif
        private static readonly MethodInfo _convertFromDbValueCoreMethod =
            typeof(DbConverterHelper).GetMethod(nameof(DbConverterHelper.ConvertFromDbValue),
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(IDbConverter), typeof(object), typeof(Type), typeof(DbValueType) },
                null)!;

        // IDbValueConverter.ConvertFromDbValue：数据库值 → .NET 值
        private static readonly MethodInfo _convertFromDbValueMethod =
            typeof(IDbValueConverter).GetMethod(nameof(IDbValueConverter.ConvertFromDbValue))!;

        private static readonly MethodInfo? _isDBNullMethod =
            typeof(DbDataReader).GetMethod(nameof(DbDataReader.IsDBNull), new[] { typeof(int) });

        private static readonly MethodInfo? _getFieldValueMethod =
            typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFieldValue), new[] { typeof(int) });

        private static readonly MethodInfo? _getStreamMethod =
            typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetStream), new[] { typeof(int) });

        // 用于在动态读取委托的 catch 块中构造包含成员名/列号的明确异常
        private static readonly MethodInfo? _createColumnReadExceptionMethod =
            typeof(DataReaderConverter).GetMethod(nameof(CreateColumnReadException),
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(Exception), typeof(int), typeof(string), typeof(Type) },
                null);

        /// <summary>
        /// 按 <see cref="DbType"/> 选择最自然的类型化读取方法。
        /// <see cref="DbType.Binary"/> 单独处理（<see cref="DbDataReader.GetFieldValue{T}"/> 或 <see cref="DbDataReader.GetStream"/>）。
        /// </summary>
        private static readonly Dictionary<DbType, MethodInfo?> _dbTypeReaderMethods = new Dictionary<DbType, MethodInfo?>
        {
            [DbType.Boolean] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetBoolean), new[] { typeof(int) }),
            [DbType.Byte] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetByte), new[] { typeof(int) }),
            [DbType.Int16] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetInt16), new[] { typeof(int) }),
            [DbType.Int32] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetInt32), new[] { typeof(int) }),
            [DbType.Int64] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetInt64), new[] { typeof(int) }),
            [DbType.Single] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFloat), new[] { typeof(int) }),
            [DbType.Double] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDouble), new[] { typeof(int) }),
            [DbType.Decimal] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDecimal), new[] { typeof(int) }),
            [DbType.Currency] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDecimal), new[] { typeof(int) }),
            [DbType.String] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetString), new[] { typeof(int) }),
            [DbType.AnsiString] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetString), new[] { typeof(int) }),
            [DbType.AnsiStringFixedLength] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetString), new[] { typeof(int) }),
            [DbType.StringFixedLength] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetString), new[] { typeof(int) }),
            [DbType.Xml] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetString), new[] { typeof(int) }),
            [DbType.DateTime] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDateTime), new[] { typeof(int) }),
            [DbType.Date] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDateTime), new[] { typeof(int) }),
            [DbType.DateTime2] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetDateTime), new[] { typeof(int) }),
            [DbType.Guid] = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetGuid), new[] { typeof(int) }),
            // DbType.Binary → GetFieldValue<byte[]> / GetStream (handled in BuildRawReadExpression)
        };

        /// <summary>
        /// 按目标类型缓存的映射委托（含源生成器预注册与运行时编译两种来源）。
        /// AOT 场景下通过 <see cref="RegisterMapper{T}"/> 注册后可完全绕开运行时表达式编译。
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Delegate> _cacheByType =
            new ConcurrentDictionary<Type, Delegate>();

        // 仅筛选 GetConverter<T>(IDbConverter) 泛型重载；GetMethods 枚举到的其他带 DAM 参数的 public 方法不会被调用
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2111",
            Justification = "GetConverter<T>(IDbConverter) 无 DynamicallyAccessedMembers 约束，反射枚举到的其他方法不会被调用。")]
#endif
        private static readonly MethodInfo? _getConverterByTypeMethod =
            typeof(DataReaderConverter).GetMethods(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(m => m.IsGenericMethod && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(IDbConverter));

        /// <summary>
        /// 注册预编译的 DataReader 映射委托，用于 NativeAOT 场景替代运行时 <see cref="LambdaExpression.Compile()"/>。
        /// 注册后，<see cref="GetConverter{T}(IDbConverter)"/> 和 <see cref="GetConverter(Type, IDbConverter)"/> 将直接返回该委托。
        /// </summary>
        /// <typeparam name="T">目标实体类型。</typeparam>
        /// <param name="mapper">将 <see cref="AutoLockDataReader"/> 当前行映射为 <typeparamref name="T"/> 实例的委托。</param>
        public static void RegisterMapper<T>(Func<AutoLockDataReader, T> mapper)
        {
            _cacheByType[typeof(T)] = mapper;
        }

        /// <summary>
        /// 注册预编译的 DataReader 映射委托（非泛型版本）。
        /// </summary>
        /// <param name="type">目标实体类型。</param>
        /// <param name="mapper">将 <see cref="AutoLockDataReader"/> 当前行映射为目标类型实例的委托。</param>
        public static void RegisterMapper(Type type, Func<AutoLockDataReader, object> mapper)
        {
            _cacheByType[type] = mapper;
        }

        /// <summary>
        /// 获取将 <see cref="AutoLockDataReader"/> 当前行转换为 <typeparamref name="TResult"/> 实例的编译委托。
        /// 对于匿名类型，基于读取器的列架构缓存编译委托，通过构造函数参数名与列名匹配；
        /// 对于普通类型，委托给 <see cref="GetConverter{TResult}(IDbConverter)"/> 使用 <see cref="TableInfoProvider.Instance"/> 进行位置映射。
        /// </summary>
        /// <typeparam name="TResult">目标类型。</typeparam>
        /// <param name="reader">已打开的数据读取器，用于读取列架构信息（匿名类型时使用）。</param>
        /// <param name="dbConverter">数据库值转换器，用于推断 <see cref="DbValueType.Default"/> 列的 <see cref="DbType"/>；为 null 时退回到按属性 CLR 类型匹配。</param>
        /// <returns>编译后的映射委托。</returns>
        public static Func<AutoLockDataReader, TResult> GetConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] TResult>(DbDataReader reader, IDbConverter? dbConverter = null)
        {
            Type type = typeof(TResult);
            if (TableInfoProvider.Instance.GetTableView(type) != null)
                return GetConverter<TResult>(dbConverter);
            string columnKey = BuildColumnKey(reader);
            return (Func<AutoLockDataReader, TResult>)_cache.GetOrAdd((type, columnKey), _ => CompileDataReaderConverter<TResult>(reader, dbConverter));
        }

        /// <summary>
        /// 获取将 <see cref="AutoLockDataReader"/> 当前行转换为 <typeparamref name="TResult"/> 实例的编译委托。
        /// 通过 <see cref="TableInfoProvider.Instance"/> 读取 <typeparamref name="TResult"/> 对应的表视图，
        /// 并依据视图的 <see cref="SqlTable.SelectColumns"/> 进行位置映射，使用类型化读取方法避免装箱。
        /// 以 <typeparamref name="TResult"/> 类型为缓存键，首次调用时编译，后续调用直接复用。
        /// </summary>
        /// <typeparam name="TResult">目标类型。</typeparam>
        /// <param name="dbConverter">数据库值转换器，用于推断 <see cref="DbValueType.Default"/> 列的 <see cref="DbType"/>；为 null 时退回到按属性 CLR 类型匹配。</param>
        /// <returns>编译后的映射委托。</returns>
        public static Func<AutoLockDataReader, TResult> GetConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] TResult>(IDbConverter? dbConverter = null)
        {
            return (Func<AutoLockDataReader, TResult>)_cacheByType.GetOrAdd(typeof(TResult), _ => CompileConverter<TResult>(dbConverter));
        }

        /// <summary>
        /// 获取将 <see cref="AutoLockDataReader"/> 当前行转换为 <paramref name="resultType"/> 实例的编译委托。
        /// 与 <see cref="GetConverter{TResult}(IDbConverter?)"/> 共用同一缓存，首次调用时通过反射调用泛型版本完成编译。
        /// </summary>
        /// <param name="resultType">目标类型。</param>
        /// <param name="dbConverter">数据库值转换器，用于推断 <see cref="DbValueType.Default"/> 列的 <see cref="DbType"/>。</param>
        /// <returns>编译后的映射委托，实际类型为 <see cref="Func{AutoLockDataReader, TResult}"/>。</returns>
        [RequiresDynamicCode("Converter dynamic creation relies on MakeGenericMethod; not supported under NativeAOT.")]
        public static Delegate GetConverter(Type resultType, IDbConverter? dbConverter = null)
        {
            return _cacheByType.GetOrAdd(resultType,
                t => (Delegate)_getConverterByTypeMethod!.MakeGenericMethod(t).Invoke(null, new object?[] { dbConverter })!);
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

        private static Func<AutoLockDataReader, TResult> CompileConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] TResult>(IDbConverter? dbConverter)
        {
            if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
                throw new PlatformNotSupportedException(
                    $"DataReader mapping for type '{typeof(TResult).FullName}' requires a source-generated mapper. " +
                    $"Ensure the LiteOrm.Generators package is referenced and the type is marked with [Table].");
            Type resultType = typeof(TResult);
            var readerParam = Expression.Parameter(typeof(AutoLockDataReader), "reader");

            if (IsScalarType(resultType))
                return CompileScalarConverter<TResult>(readerParam, dbConverter);

            var selectColumns = (TableInfoProvider.Instance?.GetTableView(resultType)
                ?? throw new InvalidOperationException($"TableInfoProvider.Default is not configured, cannot resolve columns for type '{resultType.FullName}'."))
                .SelectColumns;
            return CompileConverterByColumns<TResult>(selectColumns, dbConverter);
        }

#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "In AOT scenarios, an exception is thrown instead of invoking Expression.Compile")]
#endif
        private static Func<AutoLockDataReader, TResult> CompileScalarConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] TResult>(ParameterExpression readerParam, IDbConverter? dbConverter)
        {
            if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
                throw new PlatformNotSupportedException(
                    $"DataReader mapping for type '{typeof(TResult).FullName}' requires a source-generated mapper. " +
                    $"Ensure the LiteOrm.Generators package is referenced and the type is marked with [Table], or call DataReaderConverter.RegisterMapper first.");
            DbType? dbType = InferReadDbType(typeof(TResult), dbConverter, out DbValueType dbValueType);
            var body = BuildTypedReadExpression(readerParam, 0, typeof(TResult), null, dbType, dbValueType, null);
            return Expression.Lambda<Func<AutoLockDataReader, TResult>>(body, readerParam).Compile();
        }

#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "In AOT scenarios, an exception is thrown instead of invoking Expression.Compile")]
#endif
        private static Func<AutoLockDataReader, TResult> CompileDataReaderConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] TResult>(DbDataReader reader, IDbConverter? dbConverter)
        {
            if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
                throw new PlatformNotSupportedException(
                    $"DataReader mapping for type '{typeof(TResult).FullName}' requires a source-generated mapper. " +
                    $"Ensure the LiteOrm.Generators package is referenced and the type is marked with [Table], or call DataReaderConverter.RegisterMapper first.");
            Type resultType = typeof(TResult);
            var readerParam = Expression.Parameter(typeof(AutoLockDataReader), "reader");

            if (IsScalarType(resultType))
                return CompileScalarConverter<TResult>(readerParam, dbConverter);

            var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
                columnMap[reader.GetName(i)] = i;

            var ctor = resultType.GetConstructors()[0];
            var ctorParams = ctor.GetParameters();

            Expression body;
            if (ctorParams.Length > 0)
            {
                // 匿名类型：按构造函数参数名匹配列名，按参数 CLR 类型推断 DbType 选择类型化读取方法
                var args = new Expression[ctorParams.Length];
                for (int i = 0; i < ctorParams.Length; i++)
                {
                    ParameterInfo param = ctorParams[i];
                    if (!columnMap.TryGetValue(param.Name!, out int ordinal))
                    {
                        args[i] = Expression.Default(param.ParameterType);
                        continue;
                    }
                    DbType? dbType = InferReadDbType(param.ParameterType, dbConverter, out DbValueType dbValueType);
                    args[i] = BuildTypedReadExpression(readerParam, ordinal, param.ParameterType, param.Name, dbType, dbValueType, null);
                }
                body = Expression.New(ctor, args);
            }
            else
            {
                // 具名类型（有无参构造函数）：按属性名匹配列名，使用 MemberInit 赋值
                var bindings = new List<MemberBinding>();
                foreach (var prop in resultType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!prop.CanWrite) continue;
                    if (!columnMap.TryGetValue(prop.Name, out int ordinal)) continue;
                    DbType? dbType = InferReadDbType(prop.PropertyType, dbConverter, out DbValueType dbValueType);
                    bindings.Add(Expression.Bind(prop, BuildTypedReadExpression(readerParam, ordinal, prop.PropertyType, prop.Name, dbType, dbValueType, null)));
                }
                body = Expression.MemberInit(Expression.New(ctor), bindings);
            }

            return Expression.Lambda<Func<AutoLockDataReader, TResult>>(body, readerParam).Compile();
        }

        /// <summary>
        /// 构建读取指定列的完整表达式（含 IsDBNull 判定、Nullable 封装与列级异常包装）。
        /// 转换优先级：<paramref name="columnConverter"/>（列级转换器）→ 数据库读取方法返回类型与属性 CLR 类型一致时直接赋值
        /// → ConvertFromDbValue(reader.DbConverter, ..., 属性类型, <paramref name="dbValueType"/>) 统一分发（注册转换器优先，SqlBuilder 默认兜底）。
        /// <paramref name="columnName"/> 用于在读取失败时抛出包含成员名（属性名或构造函数参数名）的明确异常；为 null 时仅依据 <paramref name="ordinal"/> 描述。
        /// </summary>
        [RequiresDynamicCode("The code for building the typed read expression used MakeGenericMethod and might not be available.")]
        private static Expression BuildTypedReadExpression(
            ParameterExpression readerParam, int ordinal, Type targetType, string? columnName,
            DbType? dbType, DbValueType dbValueType, IDbValueConverter? columnConverter)
        {
            Type coreType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            var ordinalExpr = Expression.Constant(ordinal);

            Expression readExpr = BuildRawReadExpression(readerParam, ordinalExpr, coreType, dbType);

            // 列级转换器优先；否则读取类型与属性类型一致直接赋值；否则经 IDbValueConverter 转换
            if (columnConverter != null || readExpr.Type != coreType)
            {
                Expression converted = InvokeFromDbValueConverter(readerParam, readExpr, coreType, dbValueType, columnConverter);
                readExpr = Expression.Convert(converted, coreType);
            }

            // Wrap as Nullable<T>
            if (targetType != coreType)
                readExpr = Expression.Convert(readExpr, targetType);

            var isNull = Expression.Call(readerParam, _isDBNullMethod!, ordinalExpr);
            var body = Expression.Condition(isNull, Expression.Default(targetType), readExpr);

            // Wrap with try-catch to attach member name / ordinal information on failure
            return WrapWithColumnErrorHandling(ordinal, columnName, targetType, body);
        }

        /// <summary>
        /// 用 TryCatch 包裹列读取表达式，捕获任何异常后重新抛出包含成员名、列号与目标类型的明确异常。
        /// </summary>
        private static Expression WrapWithColumnErrorHandling(
            int ordinal,
            string? memberName,
            Type targetType,
            Expression body)
        {
            var exVar = Expression.Parameter(typeof(Exception), "ex");

            var rethrowExpr = Expression.Throw(
                Expression.Call(
                    _createColumnReadExceptionMethod!,
                    exVar,
                    Expression.Constant(ordinal),
                    Expression.Constant(memberName, typeof(string)),
                    Expression.Constant(targetType, typeof(Type))),
                body.Type);

            return Expression.TryCatch(body, Expression.Catch(exVar, rethrowExpr));
        }

        /// <summary>
        /// 构造包含成员名、列号与目标类型的明确异常，原始异常作为 InnerException 保留。
        /// </summary>
        private static Exception CreateColumnReadException(
            Exception inner, int ordinal, string? memberName, Type targetType)
        {
            string memberDesc = !string.IsNullOrEmpty(memberName)
                ? $"member '{memberName}' (ordinal {ordinal})"
                : $"ordinal {ordinal}";

            return new InvalidOperationException(
                $"Failed to read {memberDesc} and convert to target type '{targetType.FullName}'. " +
                $"Check that the database column type is compatible with the target type. See inner exception for details.",
                inner);
        }

        /// <summary>
        /// 返回读取列值的原始表达式（不含 IsDBNull 检查与 Nullable 封装）。
        /// 选取顺序：Stream 目标（GetStream）→ DbType 映射的类型化读取方法 → GetValue 兜底
        /// （Time/DateTimeOffset/Object/数组等无类型化读取方法的列）。
        /// 后续是否需要 <see cref="IDbValueConverter"/> 转换由 <see cref="BuildTypedReadExpression"/> 决定。
        /// </summary>
        [RequiresDynamicCode("The code for building the raw read expression used MakeGenericMethod and might not be available.")]
        private static Expression BuildRawReadExpression(
            ParameterExpression readerParam, Expression ordinalExpr, Type coreType, DbType? dbType)
        {
            // Stream 目标：无论 DbType 如何均使用 GetStream（与历史行为一致）
            if (typeof(Stream).IsAssignableFrom(coreType))
                return Expression.Call(readerParam, _getStreamMethod!, ordinalExpr);

            // 二进制列：GetFieldValue<byte[]>
            if (dbType == DbType.Binary)
                return Expression.Call(readerParam,
                    _getFieldValueMethod!.MakeGenericMethod(typeof(byte[])), ordinalExpr);

            // 按 DbType 选择类型化读取方法（DbType 与目标 CLR 类型不一致时仍读取为数据库类型，转换交给 IDbValueConverter）
            if (dbType.HasValue && _dbTypeReaderMethods.TryGetValue(dbType.Value, out MethodInfo? dbMethod) && dbMethod != null)
                return Expression.Call(readerParam, dbMethod, ordinalExpr);

            // 无 DbType 或无对应读取方法：GetValue 兜底
            return Expression.Call(readerParam, _getValueMethod!, ordinalExpr);
        }

        /// <summary>
        /// 构建「按转换优先级对 <paramref name="valueExpr"/> 求值转换」的表达式。
        /// 列级转换器直接以常量内联调用；否则调用 <see cref="DbConverterHelper.ConvertFromDbValue(IDbConverter, object?, Type, DbValueType)"/>
        /// 统一分发：注册转换器优先，未注册时由 SqlBuilder 通用兜底。
        /// </summary>
        private static Expression InvokeFromDbValueConverter(
            ParameterExpression readerParam, Expression valueExpr, Type targetType, DbValueType dbValueType, IDbValueConverter? columnConverter)
        {
            Expression boxedValue = Expression.Convert(valueExpr, typeof(object));

            // 列级转换器：直接以常量内联调用
            if (columnConverter != null)
                return Expression.Call(Expression.Constant(columnConverter), _convertFromDbValueMethod!, boxedValue);

            // 注册转换器 → SqlBuilder 兜底
            return Expression.Call(
                _convertFromDbValueCoreMethod!,
                Expression.Property(readerParam, nameof(AutoLockDataReader.DbConverter)),
                boxedValue,
                Expression.Constant(targetType),
                Expression.Constant(dbValueType));
        }

        /// <summary>
        /// 编译基于 <see cref="SqlColumn"/> 定义的位置映射委托。
        /// <paramref name="selectColumns"/>[i] 对应读取器第 i 列，使用列的属性名定位目标属性。
        /// </summary>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "In AOT scenarios, an exception is thrown instead of invoking Expression.Compile")]
#endif
        private static Func<AutoLockDataReader, TResult> CompileConverterByColumns<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] TResult>(IList<SqlColumn> selectColumns, IDbConverter? dbConverter)
        {
            if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
                throw new PlatformNotSupportedException(
                    $"DataReader mapping for type '{typeof(TResult).FullName}' requires a source-generated mapper. " +
                    $"Ensure the LiteOrm.Generators package is referenced and the type is marked with [Table].");
            Type resultType = typeof(TResult);
            var readerParam = Expression.Parameter(typeof(AutoLockDataReader), "reader");
            var ctor = resultType.GetConstructor(Type.EmptyTypes)
                ?? throw new InvalidOperationException($"Type '{resultType.FullName}' does not have a public parameterless constructor.");

            var bindings = new List<MemberBinding>();
            int count = selectColumns.Count;
            for (int i = 0; i < count; i++)
            {
                SqlColumn column = selectColumns[i];
                var prop = resultType.GetProperty(column.PropertyName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || !prop.CanWrite) continue;

                DbType? dbType = GetColumnReadDbType(column, prop.PropertyType, dbConverter, out DbValueType dbValueType);
                bindings.Add(Expression.Bind(prop, BuildTypedReadExpression(readerParam, i, prop.PropertyType, column.PropertyName, dbType, dbValueType, column.DbValueConverter)));
            }

            var body = Expression.MemberInit(Expression.New(ctor), bindings);
            return Expression.Lambda<Func<AutoLockDataReader, TResult>>(body, readerParam).Compile();
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

        /// <summary>
        /// 计算读取列时应使用的 <see cref="DbType"/> 与用于转换器查找的 <see cref="DbValueType"/>。
        /// <para>
        /// 当列的 <see cref="DbValueType"/> 为 <see cref="DbValueType.Default"/>（未显式指定）时，
        /// 通过 <paramref name="dbConverter"/>（当前 SqlBuilder）按属性 CLR 类型推断，
        /// 使不同数据库方言能选择正确的类型化读取方法（如 Oracle 的 bool 映射为 Int32、SQLite 的日期映射为 String）。
        /// </para>
        /// 数组/集合列的 DbType 返回 null（GetValue 兜底），但 <paramref name="dbValueType"/> 仍输出用于转换器查找。
        /// </summary>
        private static DbType? GetColumnReadDbType(SqlColumn column, Type propertyType, IDbConverter? dbConverter, out DbValueType dbValueType)
        {
            DbValueType declared = column.Definition?.DbType ?? DbValueType.Default;
            if (declared == DbValueType.Default)
                return InferReadDbType(propertyType, dbConverter, out dbValueType);

            dbValueType = declared;
            if (declared.HasArray() || ColumnDefinitionExtensions.IsCollectionType(propertyType)) return null;
            return dbConverter?.ToDbType(declared) ?? DbValueTypeMap.ToDbType(declared);
        }

        /// <summary>
        /// 按属性 CLR 类型推断读取时应使用的 <see cref="DbType"/> 与 <see cref="DbValueType"/>（无列定义信息时的兜底）。
        /// 数组/集合属性返回 null（GetValue 兜底）。
        /// </summary>
        private static DbType? InferReadDbType(Type propertyType, IDbConverter? dbConverter, out DbValueType dbValueType)
        {
            dbValueType = dbConverter != null
                ? dbConverter.GetDbValueType(propertyType)
                : DbValueTypeMap.InferFromPropertyType(propertyType);
            if (dbValueType.HasArray() || ColumnDefinitionExtensions.IsCollectionType(propertyType)) return null;
            return dbConverter != null ? dbConverter.ToDbType(dbValueType) : DbValueTypeMap.ToDbType(dbValueType);
        }
    }
}
