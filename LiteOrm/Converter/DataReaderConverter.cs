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

        private static readonly MethodInfo? _getConverterByTypeMethod =
            typeof(DataReaderConverter).GetMethod(nameof(DataReaderConverter.GetConverterByTable));

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

        /// <summary>
        /// 注册预编译的 DataReader 映射委托，用于 NativeAOT 场景替代运行时 <see cref="LambdaExpression.Compile()"/>。
        /// 注册后，<see cref="GetConverterByTable{T}(IDbConverter)"/> 和 <see cref="GetConverterByType(Type, IDbConverter)"/> 将直接返回该委托。
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
        /// 获取将 <see cref="AutoLockDataReader"/> 当前行转换为 <typeparamref name="TResult"/> 实例的编译委托（**基于列架构**，供 <c>SearchAs</c> 投影路径使用）。
        /// 基于读取器的列架构缓存编译委托，匿名类型按构造函数参数名匹配列名，通过 <see cref="CompileDataReaderConverter{TResult}"/> 生成编译委托。
        /// 预定义的实体类型请改用 <see cref="GetConverterByTable{TResult}(IDbConverter)"/>（基于表列位置映射，含 AOT 预注册 mapper）。
        /// </summary>
        /// <typeparam name="TResult">目标类型（可为任意投影类型 / 匿名类型）。</typeparam>
        /// <param name="reader">已打开的数据读取器，用于读取列架构信息。</param>
        /// <param name="dbConverter">数据库值转换器，用于推断 <see cref="DbValueType.Default"/> 列的 <see cref="DbType"/>。</param>
        /// <returns>编译后的映射委托。</returns>
        public static Func<AutoLockDataReader, TResult> GetConverterBySchema<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] TResult>(DbDataReader reader, IDbConverter dbConverter)
        {
            string columnKey = BuildColumnKey(reader);
            return (Func<AutoLockDataReader, TResult>)_cache.GetOrAdd((typeof(TResult), columnKey), _ => CompileDataReaderConverter<TResult>(reader, dbConverter));
        }

        /// <summary>
        /// 获取将 <see cref="AutoLockDataReader"/> 当前行转换为 <typeparamref name="TResult"/> 实例的编译委托（**基于表列位置映射**，供 <c>Search</c> / 预定义实体类型使用）。
        /// 通过 <see cref="TableInfoProvider.Instance"/> 读取 <typeparamref name="TResult"/> 对应的表视图，
        /// 并依据视图的 <see cref="SqlTable.SelectColumns"/> 进行位置映射，使用类型化读取方法避免装箱。
        /// 以 <typeparamref name="TResult"/> 类型为缓存键，首次调用时编译，后续调用直接复用（含 AOT 通过 <see cref="RegisterMapper{T}"/> 预注册的 mapper）。
        /// 任意投影 / 匿名类型请改用 <see cref="GetConverterBySchema{TResult}(DbDataReader, IDbConverter)"/>（基于列架构）。
        /// </summary>
        /// <typeparam name="TResult">目标类型。</typeparam>
        /// <param name="dbConverter">数据库值转换器，用于推断 <see cref="DbValueType.Default"/> 列的 <see cref="DbType"/>。</param>
        /// <returns>编译后的映射委托。</returns>
        public static Func<AutoLockDataReader, TResult> GetConverterByTable<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] TResult>(IDbConverter dbConverter)
        {
            return (Func<AutoLockDataReader, TResult>)_cacheByType.GetOrAdd(typeof(TResult), _ => CompileConverter<TResult>(dbConverter));
        }

        /// <summary>
        /// 获取将 <see cref="AutoLockDataReader"/> 当前行转换为 <paramref name="resultType"/> 实例的编译委托。
        /// 与 <see cref="GetConverterByTable{TResult}(IDbConverter)"/> 共用同一缓存，首次调用时通过反射调用泛型版本完成编译。
        /// </summary>
        /// <param name="resultType">目标类型。</param>
        /// <param name="dbConverter">数据库值转换器，用于推断 <see cref="DbValueType.Default"/> 列的 <see cref="DbType"/>。</param>
        /// <returns>编译后的映射委托，实际类型为 <see cref="Func{AutoLockDataReader, TResult}"/>。</returns>
        [RequiresDynamicCode("Converter dynamic creation relies on MakeGenericMethod; not supported under NativeAOT.")]
        public static Delegate GetConverterByType(Type resultType, IDbConverter? dbConverter = null)
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

        /// <summary>
        /// 动态读取委托编译仅在 JIT/Native（动态代码可用）下支持；AOT 下抛错并提示使用源生成器 mapper 或 <see cref="RegisterMapper{T}"/>。
        /// </summary>
        private static void EnsureDynamicCode<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] TResult>(bool suggestRegisterMapper)
        {
            if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
                throw new PlatformNotSupportedException(
                    $"DataReader mapping for type '{typeof(TResult).FullName}' requires a source-generated mapper. " +
                    $"Ensure the LiteOrm.Generators package is referenced and the type is marked with [Table]" +
                    (suggestRegisterMapper ? ", or call DataReaderConverter.RegisterMapper first." : "."));
        }

        private static Func<AutoLockDataReader, TResult> CompileConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] TResult>(IDbConverter dbConverter)
        {
            // AOT：无动态代码，经反射 + 转换委托构建映射
            if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
            {
                Type rt = typeof(TResult);
                if (DbConverterHelper.IsScalarType(rt))
                    return CompileAotScalarMapper<TResult>(dbConverter);
                var aotColumns = (TableInfoProvider.Instance?.GetTableView(rt)
                    ?? throw new InvalidOperationException($"TableInfoProvider.Instance is not configured, cannot resolve columns for type '{rt.FullName}'."))
                    .SelectColumns;
                return CompileAotColumnsMapper<TResult>(aotColumns, dbConverter);
            }

            EnsureDynamicCode<TResult>(suggestRegisterMapper: false);
            Type resultType = typeof(TResult);
            var readerParam = Expression.Parameter(typeof(AutoLockDataReader), "reader");

            if (DbConverterHelper.IsScalarType(resultType))
                return CompileScalarConverter<TResult>(readerParam, dbConverter);

            var selectColumns = (TableInfoProvider.Instance?.GetTableView(resultType)
                ?? throw new InvalidOperationException($"TableInfoProvider.Instance is not configured, cannot resolve columns for type '{resultType.FullName}'."))
                .SelectColumns;
            return CompileConverterByColumns<TResult>(selectColumns, dbConverter);
        }

        /// <summary>在 AOT 下标量结果的反射映射（读取第 0 列并转换）。</summary>
        private static Func<AutoLockDataReader, TResult> CompileAotScalarMapper<TResult>(IDbConverter dbConverter)
        {
            Type t = typeof(TResult);
            DbConvertHandler? handler = dbConverter.GetDbValueConverter(t, dbConverter.GetDbValueType(t))?.DbReadConverter;
            return reader =>
            {
                if (reader.IsDBNull(0)) return default(TResult)!;
                object raw = reader.GetValue(0);
                if (handler == null)
                {
                    // 仅首次调用：依 reader.DbConverter 按（核心类型 + 第 0 列实际 DbValueType）解析并按需缓存。
                    DbValueType dbValueType = reader.DbConverter.GetDbValueType(reader.GetFieldType(0));
                    handler = reader.DbConverter.GetDbValueConverter(t, dbValueType)?.DbReadConverter;
                }
                object? value = handler != null ? handler(raw) : Convert.ChangeType(raw, t);
                return (TResult)value!;
            };
        }

        /// <summary>按读取器列架构构建标量/列映射（SearchAs/投影，AOT 反射路径）。</summary>
        private static Func<AutoLockDataReader, TResult> CompileAotDataReaderMapper<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] TResult>(DbDataReader reader, IDbConverter dbConverter)
        {
            Type resultType = typeof(TResult);
            if (DbConverterHelper.IsScalarType(resultType))
                return CompileAotScalarMapper<TResult>(dbConverter);

            // 具名类型（公开无参构造函数）：按可写属性 setter 映射；匿名/构造类型（仅带参构造函数）：按构造参数名匹配列映射。
            List<ColumnReadSpec> specs = resultType.GetConstructor(Type.EmptyTypes) != null
                ? BuildPropertySpecs(reader, dbConverter, resultType)
                : BuildCtorSpecs(reader, dbConverter, resultType);
            return new AotReflectionMapper<TResult>(specs).Map;
        }

        /// <summary>具名类型：按列名匹配可写属性，构建属性 setter 映射规范（AOT 反射路径）。</summary>
        private static List<ColumnReadSpec> BuildPropertySpecs(DbDataReader reader, IDbConverter dbConverter, Type resultType)
        {
            var specs = new List<ColumnReadSpec>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var prop = resultType.Find(reader.GetName(i), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || !prop.CanWrite) continue;
                specs.Add(BuildColumnReadSpecFromProperty(i, prop, dbConverter, reader));
            }
            if (specs.Count == 0)
                throw new PlatformNotSupportedException(
                    $"AOT DataReader mapping for type '{resultType.FullName}' failed: no writable property matches any result column. " +
                    "Use a parameterless-constructor type with public settable properties, or register an explicit mapper via DataReaderConverter.RegisterMapper.");
            return specs;
        }

        /// <summary>匿名/构造类型：按公开构造函数参数名匹配列名，构建构造函数参数映射规范（AOT 反射路径）。</summary>
        private static List<ColumnReadSpec> BuildCtorSpecs(DbDataReader reader, IDbConverter dbConverter, Type resultType)
        {
            var ctor = resultType.GetConstructors()[0]; // 匿名类型通常仅一个公开构造函数
            ParameterInfo[] ctorParams = ctor.GetParameters();

            var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
                columnMap[reader.GetName(i)] = i;

            var specs = new List<ColumnReadSpec>();
            for (int p = 0; p < ctorParams.Length; p++)
            {
                ParameterInfo param = ctorParams[p];
                if (!columnMap.TryGetValue(param.Name!, out int ordinal)) continue;
                specs.Add(BuildColumnReadSpecFromParameter(ordinal, p, param.ParameterType, dbConverter, reader));
            }
            if (specs.Count == 0)
                throw new PlatformNotSupportedException(
                    $"AOT DataReader mapping for type '{resultType.FullName}' failed: no constructor parameter matches any result column. " +
                    "Register an explicit mapper via DataReaderConverter.RegisterMapper.");
            return specs;
        }

        /// <summary>按 <see cref="SqlTable.SelectColumns"/> 位置构建实体映射（Search，AOT 反射路径）。</summary>
        private static Func<AutoLockDataReader, TResult> CompileAotColumnsMapper<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] TResult>(IList<SqlColumn> selectColumns, IDbConverter dbConverter)
        {
            Type resultType = typeof(TResult);
            var specs = new List<ColumnReadSpec>();
            for (int i = 0; i < selectColumns.Count; i++)
            {
                SqlColumn column = selectColumns[i];
                var prop = resultType.Find(column.PropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || !prop.CanWrite) continue;
                specs.Add(BuildColumnReadSpecFromColumn(i, column, prop, dbConverter));
            }
            return new AotReflectionMapper<TResult>(specs).Map;
        }

        private static ColumnReadSpec BuildColumnReadSpecFromProperty(
            int ordinal, PropertyInfo prop, IDbConverter dbConverter, DbDataReader reader)
        {
            Type core = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            // 以实际数据库 CLR 类型推断 dbValueType，命中"按实际存储形式"注册的转换器
            // （如 SQLite 将 Guid 存为 String 文本 → (Guid, String) 的 Guid.Parse 转换器），
            // 避免按属性类型推断得到 (Guid, Guid) 恒等转换器、从而退回 ChangeType(string→Guid) 失败。
            DbValueType dbValueType = dbConverter.GetDbValueType(reader.GetFieldType(ordinal));
            return new ColumnReadSpec
            {
                Ordinal = ordinal,
                Property = prop,
                ParameterIndex = -1,
                CoreType = core,
                ReadHandler = ResolveHandler(dbConverter, reader, core, dbValueType)
            };
        }

        private static ColumnReadSpec BuildColumnReadSpecFromParameter(
            int ordinal, int parameterIndex, Type parameterType, IDbConverter dbConverter, DbDataReader reader)
        {
            Type core = Nullable.GetUnderlyingType(parameterType) ?? parameterType;
            // 与属性路径一致：以实际数据库 CLR 类型推断 dbValueType，命中按实际存储形式注册的转换器。
            DbValueType dbValueType = dbConverter.GetDbValueType(reader.GetFieldType(ordinal));
            return new ColumnReadSpec
            {
                Ordinal = ordinal,
                ParameterIndex = parameterIndex,
                CoreType = core,
                ReadHandler = ResolveHandler(dbConverter, reader, core, dbValueType)
            };
        }

        /// <summary>
        /// 解析列读取转换委托：优先用查询的 <paramref name="dbConverter"/>，未命中时回退到
        /// <see cref="AutoLockDataReader.DbConverter"/>（如全局 <see cref="SqlBuilder.Instance"/> 预注册的转换器）。
        /// 一次解析后固化为 <see cref="ColumnReadSpec.ReadHandler"/>，映射逐行时不再重复解析。
        /// </summary>
        private static DbConvertHandler? ResolveHandler(IDbConverter dbConverter, DbDataReader reader, Type core, DbValueType dbValueType)
        {
            DbConvertHandler? handler = dbConverter.GetDbValueConverter(core, dbValueType)?.DbReadConverter;
            if (handler == null && reader is AutoLockDataReader alr)
                handler = alr.DbConverter.GetDbValueConverter(core, dbValueType)?.DbReadConverter;
            return handler;
        }

        private static ColumnReadSpec BuildColumnReadSpecFromColumn(int ordinal, SqlColumn column, PropertyInfo prop, IDbConverter dbConverter)
        {
            DbConverterHelper.GetColumnReadDbType(column, prop.PropertyType, dbConverter, out DbValueType dbValueType);
            column.EnsureConverter(dbConverter);
            return new ColumnReadSpec
            {
                Ordinal = ordinal,
                Property = prop,
                ParameterIndex = -1,
                CoreType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType,
                ReadHandler = column.DbValueConverter?.DbReadConverter
            };
        }

#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "In AOT scenarios, the reflection mapper is used instead of invoking Expression.Compile")]
#endif
        private static Func<AutoLockDataReader, TResult> CompileScalarConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] TResult>(ParameterExpression readerParam, IDbConverter dbConverter)
        {
            if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
                return CompileAotScalarMapper<TResult>(dbConverter);
            EnsureDynamicCode<TResult>(suggestRegisterMapper: true);
            var body = BuildMemberReadExpression(readerParam, 0, null, typeof(TResult), dbConverter);
            return Expression.Lambda<Func<AutoLockDataReader, TResult>>(body, readerParam).Compile();
        }

#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "In AOT scenarios, the reflection mapper is used instead of invoking Expression.Compile")]
#endif
        private static Func<AutoLockDataReader, TResult> CompileDataReaderConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] TResult>(DbDataReader reader, IDbConverter dbConverter)
        {
            if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
                return CompileAotDataReaderMapper<TResult>(reader, dbConverter);
            EnsureDynamicCode<TResult>(suggestRegisterMapper: true);
            Type resultType = typeof(TResult);
            var readerParam = Expression.Parameter(typeof(AutoLockDataReader), "reader");

            if (DbConverterHelper.IsScalarType(resultType))
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
                    args[i] = BuildMemberReadExpression(readerParam, ordinal, param.Name, param.ParameterType, dbConverter);
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
                    bindings.Add(Expression.Bind(prop, BuildMemberReadExpression(readerParam, ordinal, prop.Name, prop.PropertyType, dbConverter)));
                }
                body = Expression.MemberInit(Expression.New(ctor), bindings);
            }

            return Expression.Lambda<Func<AutoLockDataReader, TResult>>(body, readerParam).Compile();
        }

        /// <summary>
        /// 构建读取指定列的完整表达式（含 IsDBNull 判定、Nullable 封装与列级异常包装）。
        /// 列级转换器（<paramref name="columnConverter"/>，编译期经 SqlBuilder 注册解析并回填列）优先：
        /// 直接转为泛型 <see cref="IDbValueConverter{TDbType,TValueType}"/> 并内联调用其 <see cref="IDbValueConverter{TDbType,TValueType}.DbReadConverter"/>
        /// 委托（注册的转换器均为泛型）；否则数据库读取方法返回类型与属性 CLR 类型一致时直接赋值。
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

            // 转换优先级：列转换器有可用读委托 → 转泛型并内联调用；否则读取类型与属性类型不一致时再处理
            if (columnConverter?.DbReadConverter != null)
            {
                readExpr = InvokeFromDbValueConverter(readExpr, coreType, columnConverter, null);
            }
            else if (readExpr.Type != coreType)
            {
                readExpr = ConvertRuntimeSafe(readExpr, coreType);
            }
            // 否则（无可用读委托且读取类型一致）：直接赋值，跳过转换
            // Wrap as Nullable<T>
            if (targetType != coreType)
                readExpr = Expression.Convert(readExpr, targetType);

            var isNull = Expression.Call(readerParam, _isDBNullMethod!, ordinalExpr);

            // 空值分支：可空类型（Nullable<T> 或引用类型）直接赋 null；非可空值类型赋默认零值
            Expression nullValue = targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null
                ? Expression.Default(targetType)
                : Expression.Constant(null, targetType);
            var body = Expression.Condition(isNull, nullValue, readExpr);

            // Wrap with try-catch to attach member name / ordinal information on failure
            return WrapWithColumnErrorHandling(ordinal, columnName, targetType, body);
        }

        /// <summary>
        /// 编译期解析并构建「读取单个成员（属性 / 构造参数）列」的表达式：
        /// <paramref name="dbConverter"/> 预解析转换器内联为常量。
        /// </summary>
        [RequiresDynamicCode("Used only by the JIT emit path; under AOT, CompileScalarConverter/CompileDataReaderConverter guard with EnsureDynamicCode and throw first.")]
        private static Expression BuildMemberReadExpression(
            ParameterExpression readerParam, int ordinal, string? memberName,
            Type memberType, IDbConverter dbConverter)
        {
            DbType? dbType = DbConverterHelper.InferReadDbType(memberType, dbConverter, out DbValueType dbValueType);
            Type coreType = Nullable.GetUnderlyingType(memberType) ?? memberType;
            IDbValueConverter? conv = dbConverter!.GetDbValueConverter(coreType, dbValueType);
            return BuildTypedReadExpression(readerParam, ordinal, memberType, memberName, dbType, dbValueType, conv);
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
        /// 编译期安全的「严格直接赋值」转换：
        /// 优先尝试直接 <c>Expression.Convert</c>（合法数值/引用转换，如 int→long 在树构建期即可转换）；
        /// 若该配对在树构建期即切线抛 <see cref="InvalidOperationException"/> No coercion（如 string→TimeSpan/DateTime），
        /// 则退而装箱为 object 后再转 <paramref name="coreType"/>（unbox.any/castclass，构建期不再抛错），
        /// 把不兼容推迟到运行期读取该行时以 <see cref="InvalidCastException"/> 报错（避免整棵 mapper 因单列类型不匹配而编译失败）。
        /// </summary>
        private static Expression ConvertRuntimeSafe(Expression valueExpr, Type coreType)
        {
            try
            {
                return Expression.Convert(valueExpr, coreType);
            }
            catch (InvalidOperationException)
            {
                return Expression.Convert(Expression.Convert(valueExpr, typeof(object)), coreType);
            }
        }

        /// <summary>
        /// 对内联已读取的 <paramref name="valueExpr"/> 应用转换器读取。
        /// 注册的转换器均为泛型 <see cref="IDbValueConverter{TDbType,TValueType}"/>，其 DB 侧类型即读取值的实际类型
        /// <c>TDb = <paramref name="valueExpr"/>.Type</c>、实体侧 <c>TValue = <paramref name="coreType"/></c>，
        /// 因此发射时直接把转换器转为 <c>IDbValueConverter{T, coreType}</c>（T 为属性实际读取类型，而非 object）
        /// 并内联调用其 <see cref="IDbValueConverter{TDbType,TValueType}.DbReadConverter"/> 委托：
        /// <list type="bullet">
        /// <item>转换器为编译期常量（<paramref name="columnConverter"/>）时，其闭合泛型读取委托在编译期一次取得、烘焙为常量后内联调用；</item>
        /// <item>否则若提供 <paramref name="runtimeConverterExpr"/>（编译期缺少 dbConverter），发射带 null/空缺守卫的运行时解析 + 泛型调用。</item>
        /// </list>
        /// 转换器缺失、非对应泛型或委托为 null 时严格直接赋值。
        /// </summary>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "In AOT scenarios, an exception is thrown before this expression tree path is invoked (CompileConverter checks RuntimeFeature.IsDynamicCodeSupported).")]
#endif
        private static Expression InvokeFromDbValueConverter(
            Expression valueExpr, Type coreType, IDbValueConverter? columnConverter, Expression? runtimeConverterExpr)
        {
            Type closedInterface = typeof(IDbValueConverter<,>).MakeGenericType(valueExpr.Type, coreType);
            Expression boxed = Expression.Convert(valueExpr, typeof(object));

            if (columnConverter != null)
            {
                // 编译期常量转换器：查找其闭合泛型 IDbValueConverter<TDb, coreType>，取后端 DbReadConverter 委托并内联调用
                (Delegate? readDelegate, Type inputType) = TryGetTypedReadDelegate(columnConverter, coreType);
                if (readDelegate != null)
                {
                    // 若输入类型不匹配，添加转换（支持隐式转换）
                    Expression inputExpr = (inputType == valueExpr.Type)
                        ? valueExpr
                        : Expression.Convert(valueExpr, inputType); // 此处允许隐式转换
                    return Expression.Invoke(Expression.Constant(readDelegate), inputExpr);
                }
                return ConvertRuntimeSafe(valueExpr, coreType);
            }

            if (runtimeConverterExpr != null)
            {
                // 运行时解析 + 泛型调用（带 null / 空缺守卫；未命中泛型或读取委托为 null 时严格直接赋值）
                var readProp = closedInterface.GetProperty(nameof(IDbValueConverter<object, object>.DbReadConverter))!;
                var convVar = Expression.Variable(typeof(IDbValueConverter), "converter");
                var castVar = Expression.Variable(closedInterface, "genericConverter");
                var delegateVar = Expression.Variable(readProp.PropertyType, "readDelegate");
                var nullDelegate = Expression.Constant(null, readProp.PropertyType);
                var nullCast = Expression.Constant(null, closedInterface);

                var result = Expression.Block(new[] { convVar, castVar, delegateVar },
                    Expression.Assign(convVar, runtimeConverterExpr),
                    Expression.Assign(castVar, Expression.TypeAs(convVar, closedInterface)),
                    Expression.Assign(delegateVar,
                        Expression.Condition(Expression.NotEqual(castVar, nullCast),
                            Expression.Property(castVar, readProp),
                            nullDelegate)),
                    Expression.Condition(Expression.NotEqual(delegateVar, nullDelegate),
                        Expression.Invoke(delegateVar, valueExpr),
                        Expression.Convert(boxed, coreType)));
                return result;
            }

            return ConvertRuntimeSafe(valueExpr, coreType);
        }

        /// <summary>
        /// 从 <paramref name="converter"/> 上查找闭合泛型 <c>IDbValueConverter&lt;TDb, coreType&gt;</c> 接口，
        /// 返回其后端 <see cref="IDbValueConverter{TDbType,TValueType}.DbReadConverter"/> 委托及其输入类型 <c>TDb</c>；
        /// 未命中或读取委托为 null 时返回 (null, <paramref name="coreType"/>)。供编译期常量转换器内联调用（一次查找，非每行）。
        /// </summary>
        private static (Delegate?, Type) TryGetTypedReadDelegate(IDbValueConverter converter, Type coreType)
        {
            foreach (Type iface in converter.GetType().GetInterfaces())
            {
                if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != typeof(IDbValueConverter<,>)) continue;
                Type[] args = iface.GetGenericArguments();
                if (args[1] != coreType) continue;
                var readDelegate = iface.GetProperty(nameof(IDbValueConverter<object, object>.DbReadConverter))?.GetValue(converter) as Delegate;
                return readDelegate == null ? (null, coreType) : (readDelegate, args[0]);
            }
            return (null, coreType);
        }

        /// <summary>
        /// 编译基于 <see cref="SqlColumn"/> 定义的位置映射委托。
        /// <paramref name="selectColumns"/>[i] 对应读取器第 i 列，使用列的属性名定位目标属性。
        /// </summary>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "In AOT scenarios, an exception is thrown instead of invoking Expression.Compile")]
#endif
        private static Func<AutoLockDataReader, TResult> CompileConverterByColumns<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] TResult>(IList<SqlColumn> selectColumns, IDbConverter dbConverter)
        {
            if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
                return CompileAotColumnsMapper<TResult>(selectColumns, dbConverter);
            EnsureDynamicCode<TResult>(suggestRegisterMapper: false);
            Type resultType = typeof(TResult);
            var readerParam = Expression.Parameter(typeof(AutoLockDataReader), "reader");
            var ctor = resultType.GetConstructor(Type.EmptyTypes)
                ?? throw new InvalidOperationException($"Type '{resultType.FullName}' does not have a public parameterless constructor.");

            var bindings = new List<MemberBinding>();
            int count = selectColumns.Count;
            for (int i = 0; i < count; i++)
            {
                SqlColumn column = selectColumns[i];
                var prop = resultType.Find(column.PropertyName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || !prop.CanWrite) continue;

                DbType? dbType = DbConverterHelper.GetColumnReadDbType(column, prop.PropertyType, dbConverter, out DbValueType dbValueType);

                column.EnsureConverter(dbConverter);

                bindings.Add(Expression.Bind(prop, BuildTypedReadExpression(readerParam, i, prop.PropertyType, column.PropertyName, dbType, dbValueType, column.DbValueConverter)));
            }

            var body = Expression.MemberInit(Expression.New(ctor), bindings);
            return Expression.Lambda<Func<AutoLockDataReader, TResult>>(body, readerParam).Compile();
        }
    }
}
