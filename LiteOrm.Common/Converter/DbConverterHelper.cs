using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace LiteOrm.Common
{
    /// <summary>
    /// 数据库值与 .NET 值转换的解析 + 委托应用助手。
    /// 一律通过「列级 Converter 优先，否则解析自 SqlBuilder 注册表的 <see cref="IDbValueConverter"/>」，
    /// 取其 <see cref="IDbValueConverter.DbReadConverter"/> / <see cref="IDbValueConverter.DbWriteConverter"/> 委托执行；
    /// 委托为 null 表示无需转换，直接赋值 / 直返。
    /// </summary>
    public static class DbConverterHelper
    {
        /// <summary>
        /// 以严格无兜底语义应用「数据库值 → .NET 值」转换：
        /// 优先使用 <paramref name="colConv"/>（列级转换器），为空时按 (<paramref name="targetType"/>, <paramref name="dbType"/>) 从 <paramref name="c"/> 解析。
        /// 解析到的转换器 <see cref="IDbValueConverter.DbReadConverter"/> 为 null 时原样返回 <paramref name="raw"/>（直接赋值）。
        /// 不做 null / <see cref="DBNull"/> / 空串短路，调用方需自行判空。
        /// </summary>
        /// <param name="c">数据库值转换器（SqlBuilder）；为 null 时仅使用 <paramref name="colConv"/>。</param>
        /// <param name="colConv">列级转换器，优先于注册表解析。</param>
        /// <param name="raw">数据库取得的原始值。</param>
        /// <param name="targetType">目标属性 / 值类型。</param>
        /// <param name="dbType">数据库取值类型（用于注册查找）。</param>
        /// <returns>转换后的值；无转换器或委托为 null 时原样返回。</returns>
        public static object? ApplyRead(IDbConverter? c, IDbValueConverter? colConv, object raw,
            Type targetType, DbValueType dbType)
        {
            IDbValueConverter? conv = colConv
                ?? c?.GetDbValueConverter(Nullable.GetUnderlyingType(targetType) ?? targetType, dbType);
            return conv?.DbReadConverter != null ? conv.DbReadConverter(raw) : raw;
        }

        /// <summary>
        /// 以严格无兜底语义应用「.NET 值 → 数据库值」转换：
        /// 优先使用 <paramref name="colConv"/>（列级转换器），为空时按 (<paramref name="value"/> 的运行时类型, <paramref name="dbType"/>) 从 <paramref name="c"/> 解析。
        /// 解析到的转换器 <see cref="IDbValueConverter.DbWriteConverter"/> 为 null 时原样返回（直接赋值）。
        /// </summary>
        /// <param name="c">数据库值转换器（SqlBuilder）；为 null 时仅使用 <paramref name="colConv"/>。</param>
        /// <param name="colConv">列级转换器，优先于注册表解析。</param>
        /// <param name="value">.NET 值（非 null，调用方需处理 null）。</param>
        /// <param name="dbType">数据库取值类型（用于注册查找）。</param>
        /// <returns>转换后的数据库值；无转换器或委托为 null 时原样返回。</returns>
        public static object ApplyWrite(IDbConverter? c, IDbValueConverter? colConv, object value,
            DbValueType dbType)
        {
            IDbValueConverter? conv = colConv ?? c?.GetDbValueConverter(value.GetType(), dbType);
            return conv?.DbWriteConverter != null ? conv.DbWriteConverter(value) : value;
        }

        /// <summary>
        /// 强类型读取转换（供 DataReaderConverter 在编译期按已知的 DB 值与目标类型闭包调用）：
        /// 当 <paramref name="converter"/> 是匹配 <typeparamref name="TDb"/> → <typeparamref name="TValue"/> 的泛型转换器时，
        /// 调用其泛型 <see cref="IDbValueConverter{TDbType,TValueType}.DbReadConverter"/> 委托；否则直接赋值 / 直返。
        /// </summary>
        public static TValue ApplyReadGeneric<TDb, TValue>(IDbValueConverter? converter, TDb value)
        {
            // 优先使用与 TDb → TValue 匹配的泛型转换器委托
            if (converter is IDbValueConverter<TDb, TValue> typed && typed.DbReadConverter is { } handler)
                return handler(value);
            // 兜底：非泛型转换器（object 传输）经非泛型 DbReadConverter；委托为 null 时直接赋值
            if (converter?.DbReadConverter is { } objHandler)
                return (TValue)objHandler(value!)!;
            return (TValue)(object)value!;
        }

        /// <summary>
        /// 为非可空值类型创建默认值（零值），替代 <see cref="Activator.CreateInstance(Type)"/>。
        /// <para>
        /// NativeAOT 下 <see cref="Activator.CreateInstance(Type)"/> 会触发 IL2072 裁剪告警；
        /// 在 .NET 5+ 上使用 AOT 友好的 <c>RuntimeHelpers.GetUninitializedObject</c>
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
        /// 判断指定类型是否为集合类型（数组、<see cref="System.Collections.Generic.IEnumerable{T}"/> 等），
        /// 排除 <see cref="string"/> 与 <see cref="byte"/>[]。
        /// </summary>
        /// <param name="type">要判断的类型。</param>
        /// <returns>如果类型是集合类型则返回 true。</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "GetUnderlyingType only checks Nullable<T> and is safe for known property types.")]
        [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "This collection-check helper performs no member enumeration; the Type argument is used for IEnumerable assignability checks only (runtime path).")]
        public static bool IsCollectionType(Type type)
        {
            if (type is null) return false;
            type = type.GetUnderlyingType();
            if (type == typeof(string) || type == typeof(byte[])) return false;
            return typeof(IEnumerable).IsAssignableFrom(type);
        }

        /// <summary>
        /// 解析集合类型的元素类型。
        /// 数组返回 <see cref="Type.GetElementType"/>；<c>IEnumerable&lt;T&gt;/ICollection&lt;T&gt;/IList&lt;T&gt;</c>
        /// 返回泛型参数 <c>T</c>；非泛型集合返回 <see cref="object"/>；无法解析返回 <see langword="null"/>。
        /// </summary>
        /// <param name="type">集合类型。</param>
        /// <returns>元素类型；无法解析时为 null。</returns>
        public static Type? GetCollectionElementType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
        {
            if (type is null) return null;
            type = type.GetUnderlyingType();
            if (type == typeof(string) || type == typeof(byte[])) return null;

            if (type.IsArray) return type.GetElementType();

            if (type.IsGenericType)
            {
                Type genericDef = type.GetGenericTypeDefinition();
                if (genericDef == typeof(IEnumerable<>) || genericDef == typeof(ICollection<>)
                    || genericDef == typeof(IList<>) || genericDef == typeof(List<>)
                    || genericDef == typeof(IReadOnlyCollection<>) || genericDef == typeof(IReadOnlyList<>)
                    || genericDef == typeof(ISet<>) || genericDef == typeof(HashSet<>))
                {
                    return type.GetGenericArguments()[0];
                }

                // 其他泛型集合接口实现：查找其实现的 IEnumerable<T>
                Type? enumerable = type.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
                if (enumerable is not null) return enumerable.GetGenericArguments()[0];
            }

            if (typeof(IEnumerable).IsAssignableFrom(type)) return typeof(object);

            return null;
        }

        /// <summary>
        /// 判断类型是否为可直接映射的标量数据库读取目标（原始类型 / string / 数值 / 日期 / Guid / byte[] / Stream / 时间等）。
        /// </summary>
        public static bool IsScalarType(Type type)
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
        public static DbType? GetColumnReadDbType(SqlColumn column, Type propertyType, IDbConverter? dbConverter, out DbValueType dbValueType)
        {
            DbValueType declared = column.Definition?.DbType ?? DbValueType.Default;
            if (declared == DbValueType.Default)
                return InferReadDbType(propertyType, dbConverter, out dbValueType);

            dbValueType = declared;
            if (declared.HasArray() || IsCollectionType(propertyType)) return null;
            return dbConverter?.ToDbType(declared) ?? DbValueTypeMap.ToDbType(declared);
        }

        /// <summary>
        /// 按属性 CLR 类型推断读取时应使用的 <see cref="DbType"/> 与 <see cref="DbValueType"/>（无列定义信息时的兜底）。
        /// 数组/集合属性返回 null（GetValue 兜底）。
        /// </summary>
        public static DbType? InferReadDbType(Type propertyType, IDbConverter? dbConverter, out DbValueType dbValueType)
        {
            dbValueType = dbConverter != null
                ? dbConverter.GetDbValueType(propertyType)
                : DbValueTypeMap.InferFromPropertyType(propertyType);
            if (dbValueType.HasArray() || IsCollectionType(propertyType)) return null;
            return dbConverter != null ? dbConverter.ToDbType(dbValueType) : DbValueTypeMap.ToDbType(dbValueType);
        }
    }
}