using System;
using System.Diagnostics.CodeAnalysis;

namespace LiteOrm.Common
{
    /// <summary>
    /// 数据库值与 .NET 值转换的解析 + 委托应用助手。
    /// 转换不再做通用兜底（无 ChangeType / 枚举 / bool / TimeSpan / Json / 集合回退），
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
        public static object? ApplyRead(IDbConverter? c, IDbValueConverter? colConv, object? raw,
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
    }
}