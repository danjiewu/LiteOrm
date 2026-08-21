using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace LiteOrm.Common
{
    /// <summary>
    /// 数据库值转换的统一分发辅助类。
    /// 读取方向见 <see cref="ConvertFromDbValue(IDbConverter, object?, Type)"/>，
    /// 写入方向见 <see cref="ToDbValue(IDbConverter, object?, DbValueType?)"/>；
    /// 两者均优先使用按 (值类型, 数据库取值类型) 注册的 <see cref="IDbValueConverter"/>，
    /// 未注册时提供枚举/bool/DateTimeOffset/TimeSpan 适配与
    /// <see cref="Convert.ChangeType(object, Type)"/> 兜底的通用转换链。
    /// 复杂类型（Collection/Json）不再作为兜底自动序列化，需用户按 (值类型, DbValueType) 预注册转换器，未预注册的复杂类型不处理。
    /// </summary>
    public static class DbConverterHelper
    {
        /// <summary>
        /// 将 .NET 值转换为数据库可接受的值（写入统一入口，与 <see cref="ConvertFromDbValue(IDbConverter, object?, Type)"/> 的读取方向对称）：
        /// null 返回 <see cref="DBNull.Value"/>；优先使用按 (值类型, 数据库取值类型) 注册的转换器
        /// （默认类型转换与方言特定转换均通过 LiteOrmConverterInitializer 预注册实现）；
        /// 复杂类型（Collection/Json）不再作为兜底自动序列化，需按 (值类型, DbValueType) 预注册转换器，未预注册的复杂类型不处理。
        /// 未注册时使用通用兜底：枚举转换、bool/DateTimeOffset/TimeSpan 适配，
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

            // 注册的转换器优先（如 bool/Guid/TimeSpan/DateTime/DateTimeOffset/string 及方言特定转换，
            // 以及用户为复杂/集合/Json 类型预注册的转换器）；未命中注册则落入通用兜底
            if (converter.GetDbValueConverter(type, dbType) is IDbValueConverter registered)
            {
                return registered.ConvertToDbValue(value);
            }

            return ToDbValueCore(value, dbType);
        }

        /// <summary>
        /// 通用兜底的「.NET 值 → 数据库值」转换：枚举转换、bool/DateTimeOffset/TimeSpan 适配，
        /// 最后以 <see cref="Convert.ChangeType(object, Type)"/> 兜底。
        /// 复杂类型（Collection/Json）不在此处自动序列化；未预注册时原样返回交由驱动绑定。
        /// </summary>
        private static object ToDbValueCore(object value, DbValueType dbType)
        {
            Type type = value.GetType();

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

            // 最后兜底：目标类型一致直返；否则 ChangeType，失败时原样返回交由驱动绑定
            // （未预注册的复杂类型不处理，原样返回）
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
        /// 读取列值的统一转换分发：空值短路（null / <see cref="DBNull"/> / 空字符串 → 目标类型默认值）后，
        /// 优先使用按 (<paramref name="targetType"/>, <see cref="DbValueType.Object"/>) 注册的转换器
        /// 未注册时使用通用兜底：同类型直返、按运行时类型命中注册转换器、枚举解析，
        /// 最后以 <see cref="Convert.ChangeType(object, Type)"/> 兜底。
        /// 复杂类型（Collection/Json）不再作为兜底自动反序列化/转换，需按 (值类型, DbValueType) 预注册转换器，未预注册的复杂类型不处理。
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

            // 最后兜底：ChangeType 转换到目标类型；无法转换时原样返回交由读取方处理
            // （未预注册的复杂类型不处理，不再自动 JSON 反序列化/集合转换）
            try { return Convert.ChangeType(value, underlyingType); }
            catch (InvalidCastException) { return value; }
        }

    }
}
