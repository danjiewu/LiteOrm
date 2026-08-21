using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace LiteOrm.Common
{
    /// <summary>
    /// 强类型数据库值转换器接口，提供数据库值与 .NET 值之间的双向转换。
    /// </summary>
    /// <typeparam name="TDbType">数据库驱动返回的数据库值 CLR 类型。</typeparam>
    /// <typeparam name="TValueType">实体属性 / .NET 值类型。</typeparam>
    public interface IDbValueConverter<TDbType, TValueType> : IDbValueConverter
    {
        /// <summary>将数据库值转换为 .NET 值。</summary>
        /// <param name="value">数据库驱动返回的原始值。</param>
        /// <returns>转换后的 .NET 值。</returns>
        TValueType ConvertFromDbValue(TDbType value);

        /// <summary>将 .NET 值转换为数据库可接受的值。</summary>
        /// <param name="value">.NET 值。</param>
        /// <returns>数据库可接受的值。</returns>
        TDbType ConvertToDbValue(TValueType value);
    }

    /// <summary>
    /// 数据库值转换器接口，提供数据库值与 .NET 值之间的双向转换。
    /// 转换器注册表统一使用 (值类型, 数据库取值类型) 作为主键，读取与写入共用同一注册表。
    /// </summary>
    public interface IDbValueConverter
    {
        /// <summary>数据库值类型。</summary>
        DbValueType DbValueType { get; }

        /// <summary>实体属性 / .NET 值类型。</summary>
        Type ValueType { get; }

        /// <summary>将数据库值转换为 .NET 值。</summary>
        /// <param name="value">数据库驱动返回的原始值。</param>
        /// <returns>转换后的 .NET 值。</returns>
        object ConvertFromDbValue(object value);

        /// <summary>将 .NET 值转换为数据库可接受的值。</summary>
        /// <param name="value">.NET 值。</param>
        /// <returns>数据库可接受的值。</returns>
        object ConvertToDbValue(object value);
    }

    public class DefaultDbValueConverter : IDbValueConverter
    {
        public DbValueType DbValueType => DbValueType.Object;
        public Type ValueType { get; } 

        public DefaultDbValueConverter(Type targetType)
        {
            ValueType = targetType ?? throw new ArgumentNullException(nameof(targetType));
        }
        public object ConvertFromDbValue(object value)
        {
            Type underlyingType = Nullable.GetUnderlyingType(ValueType) ?? ValueType;

            // 通用兜底：同类型直返
            if (underlyingType.IsInstanceOfType(value))
                return value;

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

        public object ConvertToDbValue(object value)
        {
            if (value is null) return DBNull.Value;
            return value;
        }
    }

    /// <summary>
    /// 基于委托的 <see cref="IDbValueConverter"/> 适配器。
    /// 两个方向均可只提供单向委托，未提供委托的方向被调用时抛出 <see cref="NotSupportedException"/>。
    /// 注册到 DbValueConverterMap 时建议双向提供委托，避免读取/写入链路单向命中后无回退。
    /// </summary>
    /// <typeparam name="TDbType">数据库驱动返回的数据库值 CLR 类型。</typeparam>
    /// <typeparam name="TValueType">实体属性 / .NET 值类型。</typeparam>
    public sealed class FuncDbValueConverter<TDbType, TValueType> : IDbValueConverter<TDbType, TValueType>
    {
        private readonly Func<TDbType, TValueType>? _fromDb;
        private readonly Func<TValueType, TDbType>? _toDb;

        /// <summary>
        /// 创建基于委托的双向转换器。任一委托可为 null（该方向不支持）。
        /// </summary>
        /// <param name="fromDb">数据库值 → .NET 值 的转换委托。</param>
        /// <param name="toDb">.NET 值 → 数据库值 的转换委托。</param>
        public FuncDbValueConverter(Func<TDbType, TValueType>? fromDb, Func<TValueType, TDbType>? toDb)
        {
            _fromDb = fromDb;
            _toDb = toDb;
        }

        DbValueType IDbValueConverter.DbValueType => DbValueType.Object;
        Type IDbValueConverter.ValueType => typeof(TValueType);

        /// <summary>将数据库值转换为 <typeparamref name="TValueType"/> 值。</summary>
        /// <param name="value">数据库驱动返回的原始值。</param>
        /// <returns>转换后的 .NET 值。</returns>
        public TValueType ConvertFromDbValue(TDbType value)
        {
            if (_fromDb == null)
                throw new NotSupportedException($"转换器 {typeof(FuncDbValueConverter<TDbType, TValueType>)} 未提供数据库值到 {typeof(TValueType)} 的转换委托。");
            return _fromDb(value);
        }

        /// <summary>将 <typeparamref name="TValueType"/> 值转换为数据库可接受的值。</summary>
        /// <param name="value">.NET 值。</param>
        /// <returns>数据库可接受的值。</returns>
        public TDbType ConvertToDbValue(TValueType value)
        {
            if (_toDb == null)
                throw new NotSupportedException($"转换器 {typeof(FuncDbValueConverter<TDbType, TValueType>)} 未提供 {typeof(TValueType)} 到数据库值的转换委托。");
            return _toDb(value);
        }

        object IDbValueConverter.ConvertFromDbValue(object value)
        {
            return ConvertFromDbValue((TDbType)value)!;
        }

        object IDbValueConverter.ConvertToDbValue(object value)
        {
            return ConvertToDbValue((TValueType)value)!;
        }
    }

    /// <summary>
    /// 表示用于数据库值与 .NET 对象值之间转换的接口。
    /// 转换器注册表统一使用 (值类型, DbValueType) 作为主键，读取与写入共用同一注册表：
    /// 读取按 (目标属性类型, 列取值类型) 查找，写入按 (源值类型, 目标取值类型) 查找。
    /// </summary>
    public interface IDbConverter
    {
        /// <summary>
        /// 获取按 (值类型, 数据库取值类型) 注册的转换器（读取与写入共用注册表，沿 SqlBuilder 继承链查找，方言注册优先于基类）。
        /// 未注册时返回 null，由调用方决定兜底转换策略。
        /// </summary>
        /// <param name="valueType">实体属性 / .NET 值类型。</param>
        /// <param name="dbValueType">数据库取值类型。</param>
        /// <returns>注册的转换器；未注册时返回 null。</returns>
        IDbValueConverter? GetDbValueConverter(Type valueType, DbValueType dbValueType);

        IDbValueConverter<TValueType, TDbType>? GetDbValueConverter<TValueType, TDbType>(DbValueType dbValueType);

        /// <summary>
        /// 将 .NET 值转换为数据库可接受的值（写入方向的统一入口，委托 <see cref="DbConverterHelper.ToDbValue(IDbConverter, object?, DbValueType?)"/> 分发）：
        /// null 返回 <see cref="DBNull.Value"/>；优先使用按 (值类型, 数据库取值类型) 注册的转换器，
        /// 未注册时使用通用兜底：枚举转换、bool/DateTimeOffset/TimeSpan 适配，
        /// 最后以 <see cref="Convert.ChangeType(object, Type)"/> 兜底（失败时原样返回交由驱动绑定）。
        /// 复杂类型（Collection/Json）不再自动序列化，需按 (值类型, DbValueType) 预注册转换器，未预注册的复杂类型不处理。
        /// </summary>
        /// <param name="value">要转换的对象值。</param>
        /// <param name="dbValueType">数据库取值类型（可含 <see cref="DbValueType.Array"/> 掩码，为 null/Object/Default 时按值的运行时类型推断）。</param>
        /// <returns>数据库可接受的值。</returns>
        object ToDbValue(object? value, DbValueType? dbValueType = null);
        /// <summary>
        /// 将 .NET 类型映射为数据库对应的 <see cref="DbValueType"/>。
        /// </summary>
        /// <param name="type">要映射的 .NET 类型。</param>
        /// <returns>返回对应的 <see cref="DbValueType"/> 值。</returns>
        DbValueType GetDbValueType(Type type);
        /// <summary>
        /// 获取指定数据库取值类型的默认列长度。
        /// </summary>
        /// <param name="dbValueType">数据库取值类型。</param>
        /// <returns>默认存储长度。</returns>
        int GetDefaultLength(DbValueType dbValueType);
        /// <summary>
        /// 将 <see cref="DbValueType"/> 转换为 <see cref="DbType"/>（数据库操作边界转换）。
        /// 子类可覆盖以提供方言特定的映射（如 Oracle 将 Boolean 映射为 Byte、DateTime 映射为 Date）。
        /// </summary>
        /// <param name="dbValueType">数据库取值类型（可含 <see cref="DbValueType.Array"/> 掩码）。</param>
        /// <returns>对应的 <see cref="DbType"/> 值。</returns>
        DbType ToDbType(DbValueType dbValueType);
    }
}
