using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace LiteOrm.Common
{
    /// <summary>
    /// 数据库值转换的统一分发辅助类。
    /// 读取方向见 <see cref="ConvertFromDbValue(IDbConverter, object?, Type)"/>，
    /// 写入方向见 <see cref="ToDbValue(IDbConverter, object?, DbValueType?)"/>；
    /// 两者均优先使用按 (值类型, 数据库取值类型) 注册的 <see cref="IDbValueConverter"/>，
    /// 未注册时提供数组/Json 序列化、枚举/bool/DateTimeOffset/TimeSpan 适配与
    /// <see cref="Convert.ChangeType(object, Type)"/> 兜底的通用转换链。
    /// </summary>
    public static class DbConverterHelper
    {
        /// <summary>
        /// 将 .NET 值转换为数据库可接受的值（写入统一入口，与 <see cref="ConvertFromDbValue(IDbConverter, object?, Type)"/> 的读取方向对称）：
        /// null 返回 <see cref="DBNull.Value"/>；优先使用按 (值类型, 数据库取值类型) 注册的转换器
        /// （默认类型转换与方言特定转换均通过 LiteOrmConverterInitializer 预注册实现）；
        /// 未注册时使用通用兜底：数组/Json 序列化、按运行时类型命中注册转换器、枚举转换、bool/DateTimeOffset/TimeSpan 适配，
        /// 最后以 <see cref="Convert.ChangeType(object, Type)"/> 兜底（失败时原样返回交由驱动绑定）。
        /// </summary>
        /// <param name="converter">数据库值转换器。</param>
        /// <param name="value">.NET 值。</param>
        /// <param name="dbValueType">数据字段取值类型（可含 <see cref="DbValueType.Array"/> 掩码，为 null 时按值的运行时类型推断）。</param>
        /// <returns>数据库可接受的值。</returns>
        public static object ToDbValue(this IDbConverter converter, object? value, DbValueType? dbValueType = null)
        {
            if (value is null) return DBNull.Value;

            Type type = value.GetType();
            DbValueType dbType = (dbValueType is null || dbValueType == DbValueType.Object || dbValueType == DbValueType.Default)
                ? converter.GetDbValueType(type)
                : dbValueType.Value;

            // 注册的转换器优先（如 bool/Guid/TimeSpan/DateTime/DateTimeOffset/string 及方言特定转换）；
            // 数组/Json 掩码的组合通常未注册，直接落入通用兜底
            if (!dbType.HasArray()
                && converter.GetDbValueConverter(type.GetUnderlyingType(), dbType) is IDbValueConverter registered)
            {
                return registered.ConvertToDbValue(value);
            }

            return ToDbValueCore(converter, value, dbType);
        }

        /// <summary>
        /// 通用兜底的「.NET 值 → 数据库值」转换：数组/Json 序列化、按运行时类型命中注册转换器、
        /// 枚举转换、bool/DateTimeOffset/TimeSpan 适配，最后以 <see cref="Convert.ChangeType(object, Type)"/> 兜底。
        /// </summary>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL3050",
            Justification = "JSON serialization path is only triggered when the value is a complex object/collection; under AOT, users must provide a System.Text.Json source-gen context for complex property types, otherwise a NotSupportedException is thrown at runtime.")]
#endif
        private static object ToDbValueCore(IDbConverter converter, object value, DbValueType dbType)
        {
            // 数组列：原生数组方言（如 PostgreSQL）原样返回交由驱动绑定；其余方言回退为 JSON 字符串存储
            if (dbType.HasArray() && ColumnDefinitionExtensions.IsCollectionType(value.GetType()))
                return converter is ISqlBuilder { SupportsNativeArrays: true } ? value : JsonSerializer.Serialize(value, value.GetType());

            // Json/Jsonb 列：复杂值序列化为 JSON 字符串，标量直返字符串形式
            if (dbType == DbValueType.Json || dbType == DbValueType.Jsonb)
                return IsComplexType(value.GetType()) ? JsonSerializer.Serialize(value, value.GetType()) : value.ToString()!;

            Type type = value.GetType();

            // 优先命中按实际值类型注册的转换器（支持 object/多态属性按运行时类型转换）
            if (!dbType.HasArray()
                && converter.GetDbValueConverter(type, dbType) is IDbValueConverter runtimeConverter)
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
                && IsComplexType(type))
            {
                return JsonSerializer.Serialize(value, value.GetType());
            }

            // 最后兜底：目标类型一致直返；否则 ChangeType，失败时原样返回交由驱动绑定
            Type targetType = dbType.ToType();
            if (targetType.IsInstanceOfType(value)) return value;
            try { return Convert.ChangeType(value, targetType); }
            catch (InvalidCastException) { return value; }
        }

        /// <summary>
        /// 为非可空值类型创建默认值（零值），替代 <see cref="Activator.CreateInstance(Type)"/>。
        /// <para>
        /// NativeAOT 下 <see cref="Activator.CreateInstance(Type)"/> 会触发 IL2072 裁剪告警；
        /// 这里在 .NET 5+ 上使用 AOT 友好的 <c>RuntimeHelpers.GetUninitializedObject</c>
        /// 为值类型生成零值装箱实例，语义与 <c>default(T)</c> 一致。
        /// </para>
        /// </summary>
        /// <param name="objectType">非可空值类型。</param>
        /// <returns>该值类型的零值（装箱后）。</returns>
        public static object CreateDefaultValue([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] Type objectType)
        {
#if NET5_0_OR_GREATER
            return System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(objectType);
#else
            return Activator.CreateInstance(objectType)!;
#endif
        }

        /// <summary>
        /// 判断类型是否为复杂类型（非标量、非枚举）。
        /// 复杂类型（集合、数组、自定义对象）在遇到字符串值时按 JSON 反序列化。
        /// </summary>
        public static bool IsComplexType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsPrimitive || type.IsEnum) return false;
            return !(type == typeof(string)
                || type == typeof(char)
                || type == typeof(decimal)
                || type == typeof(DateTime)
                || type == typeof(DateTimeOffset)
                || type == typeof(Guid)
                || type == typeof(TimeSpan)
                || type == typeof(byte[]));
        }

        /// <summary>
        /// 读取列值的统一转换分发：空值短路（null / <see cref="DBNull"/> / 空字符串 → 目标类型默认值）后，
        /// 优先使用按 (<paramref name="targetType"/>, <see cref="DbValueType.Object"/>) 注册的转换器
        /// 未注册时使用通用兜底：同类型直返、按运行时类型命中注册转换器、枚举解析、JSON 反序列化、集合转换，
        /// 最后以 <see cref="Convert.ChangeType(object, Type)"/> 兜底。
        /// 供运行时编译的读取委托与源生成器生成的 mapper 代码共用。
        /// </summary>
        /// <param name="dbConverter">数据库值转换器（AutoLockDataReader.DbConverter）。</param>
        /// <param name="value">数据库取得的原始值。</param>
        /// <param name="targetType">目标属性 / 构造参数类型（已剥离 Nullable）。</param>
        /// <returns>转换后的目标类型值。</returns>
        public static object? ConvertFromDbValue(IDbConverter? dbConverter, object? value,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] Type targetType)
        {
            return ConvertFromDbValue(dbConverter, value, targetType, DbValueType.Object);
        }

        /// <summary>同 <see cref="ConvertFromDbValue(IDbConverter, object?, Type)"/>，显式指定用于注册查找的 <paramref name="dbValueType"/>。</summary>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL3050",
            Justification = "JSON deserialization path is only triggered when dbValue is a string and the target type is a complex object/collection; under AOT, users must provide a System.Text.Json source-gen context for complex property types, otherwise a NotSupportedException is thrown at runtime.")]
#endif
        public static object? ConvertFromDbValue(IDbConverter? dbConverter, object? value,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] Type targetType,
            DbValueType dbValueType)
        {
            Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            // 空值短路：null / DBNull / 空字符串 → 目标类型默认值（引用类型与可空类型为 null，非可空值类型为零值）。
            // 注册转换器不应收到空值（如 SQLite ALTER TABLE 加列产生的 DEFAULT '' 旧数据）。
            if (value is null || value == DBNull.Value || (value is string empty && empty.Length == 0))
            {
                return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null
                    ? DbConverterHelper.CreateDefaultValue(underlyingType)
                    : null;
            }

            // 注册的转换器优先（默认类型转换与方言特定转换均通过预注册实现）
            if (dbConverter?.GetDbValueConverter(underlyingType, dbValueType) is IDbValueConverter converter)
                return converter.ConvertFromDbValue(value);

            // 通用兜底：同类型直返
            if (underlyingType.IsInstanceOfType(value))
                return value;

            // 按值的实际运行时类型推断 DbValueType 后命中注册的转换器（支持 ExecuteScalar 等无 DbType 上下文的场景）
            if (dbConverter?.GetDbValueConverter(underlyingType, DbValueTypeMap.GetDbValueType(value.GetType())) is IDbValueConverter runtimeConverter)
                return runtimeConverter.ConvertFromDbValue(value);

            if (underlyingType.IsEnum)
            {
                if (value is string strEnum) return Enum.Parse(underlyingType, strEnum, true);
                return Enum.ToObject(underlyingType, Convert.ChangeType(value, Enum.GetUnderlyingType(underlyingType)));
            }

            // Json/Jsonb 列（或数组列在非原生数组方言下的文本回退）：
            // 字符串值按 JSON 反序列化到集合或复杂对象类型
            if (value is string jsonSource && IsComplexType(underlyingType))
            {
                return JsonSerializer.Deserialize(jsonSource, underlyingType);
            }

            // 数组列（原生数组方言，如 Npgsql 返回 T[]）：转换为目标集合
            if (value is IEnumerable enumerable && ColumnDefinitionExtensions.IsCollectionType(underlyingType))
            {
                return ConvertToCollection(enumerable, underlyingType);
            }

            // 最后兜底
            return Convert.ChangeType(value, underlyingType);
        }

        /// <summary>
        /// 将数据库返回的数组/可枚举值转换为目标集合类型（数组或 <see cref="List{T}"/>）。
        /// 目标类型无法构造时回退为原始值。
        /// </summary>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "JIT path is guarded by RuntimeFeature.IsDynamicCodeSupported; AOT path throws PlatformNotSupportedException.")]
        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "JIT path is guarded by RuntimeFeature.IsDynamicCodeSupported; AOT path throws PlatformNotSupportedException.")]
#endif
        private static object ConvertToCollection(IEnumerable source, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type targetType)
        {
            // 目标为数组
            if (targetType.IsArray)
            {
                Type elementType = targetType.GetElementType()!;
                List<object?> items = new List<object?>();
                foreach (object? item in source) items.Add(item);
                Array array = Array.CreateInstance(elementType, items.Count);
                for (int i = 0; i < items.Count; i++)
                    array.SetValue(ChangeCollectionItem(items[i], elementType), i);
                return array;
            }

            // 目标为 List<T>
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type elementType = targetType.GetGenericArguments()[0];
                // AOT 模式下 Activator.CreateInstance(Type) 需要 List<T> 的无参构造函数被保留，
                // 但 targetType 为运行时变量，trimmer 无法静态追踪。
                // 通过 RuntimeFeature.IsDynamicCodeSupported 守卫仅在 JIT 路径调用反射。
                if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
                    throw new PlatformNotSupportedException(
                        $"Creating List<T> for type '{targetType.FullName}' in AOT mode requires the parameterless constructor to be preserved. " +
                        $"Use T[] properties, or ensure the property type is rooted via source generator, to enable AOT support.");
                System.Collections.IList list = (System.Collections.IList)Activator.CreateInstance(targetType)!;
                foreach (object? item in source)
                    list.Add(ChangeCollectionItem(item, elementType));
                return list;
            }

            // 其他集合类型：尽力而为，返回原始值
            return source;
        }

        /// <summary>
        /// 将集合元素转换为目标元素类型；无法转换时返回原始值。
        /// </summary>
        private static object? ChangeCollectionItem(object? item, Type elementType)
        {
            if (item is null || elementType == typeof(object) || elementType.IsInstanceOfType(item)) return item;
            try { return Convert.ChangeType(item, elementType); }
            catch { return item; }
        }

    }
}
