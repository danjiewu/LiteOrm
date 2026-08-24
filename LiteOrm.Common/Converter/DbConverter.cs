using System;
using System.Data;

namespace LiteOrm.Common
{

    /// <summary>
    /// 双向值转换委托：数据库值与 .NET 值之间的转换，读取（数据库值 → .NET 值）与写入（.NET 值 → 数据库值）共用同一类型。
    /// </summary>
    /// <param name="value">待转换的值（读取方向为数据库原始值，写入方向为 .NET 值）。</param>
    /// <returns>转换后的值。</returns>
    public delegate object DbConvertHandler(object value);

    /// <summary>强类型双向往返值转换委托。</summary>
    public delegate TResult DbConvertHandler<T, TResult>(T value);

    /// <summary>
    /// 强类型数据库值转换器接口，提供数据库值与 .NET 值之间的双向转换。
    /// 委托为 null 表示该方向无需转换（直接赋值 / 直返）。
    /// </summary>
    /// <typeparam name="TDbType">数据库驱动返回的数据库值 CLR 类型。</typeparam>
    /// <typeparam name="TValueType">实体属性 / .NET 值类型。</typeparam>
    public interface IDbValueConverter<TDbType, TValueType> : IDbValueConverter
    {
        /// <summary>数据库值 → .NET 值的转换委托；为 null 时直接赋值。</summary>
        new DbConvertHandler<TDbType, TValueType>? DbReadConverter { get; }

        /// <summary>.NET 值 → 数据库值的转换委托；为 null 时直接赋值。</summary>
        new DbConvertHandler<TValueType, object>? DbWriteConverter { get; }
    }

    /// <summary>
    /// 数据库值转换器接口，提供数据库值与 .NET 值之间的双向转换。
    /// 转换器注册表统一使用 (值类型, 数据库取值类型) 作为主键，读取与写入共用同一注册表。
    /// </summary>
    public interface IDbValueConverter
    {
        /// <summary>实体属性 / .NET 值类型。</summary>
        Type ValueType { get; }

        /// <summary>数据库值 → .NET 值的转换委托；为 null 时表示无需转换，直接赋值。</summary>
        DbConvertHandler? DbReadConverter { get; }

        /// <summary>.NET 值 → 数据库值的转换委托；为 null 时表示无需转换，直接赋值。</summary>
        DbConvertHandler? DbWriteConverter { get; }
    }

    /// <summary>
    /// 基于委托的 <see cref="IDbValueConverter"/> 适配器。
    /// 两个方向均可只提供单向委托；未提供的方向（委托为 null）表示无需转换，直接赋值。
    /// 注册到 DbValueConverterMap 时建议双向提供委托，避免读写单向命中后无回方向转换。
    /// </summary>
    /// <typeparam name="TDbType">数据库驱动返回的数据库值 CLR 类型。</typeparam>
    /// <typeparam name="TValueType">实体属性 / .NET 值类型。</typeparam>
    public sealed class FuncDbValueConverter<TDbType, TValueType> : IDbValueConverter<TDbType, TValueType>
    {
        private readonly DbConvertHandler<TDbType, TValueType>? _fromDb;
        private readonly DbConvertHandler<TValueType, object>? _toDb;

        /// <summary>
        /// 创建基于委托的双向转换器。任一委托可为 null（该方向无需转换，直接赋值）。
        /// </summary>
        /// <param name="fromDb">数据库值 → .NET 值 的转换委托。</param>
        /// <param name="toDb">.NET 值 → 数据库值 的转换委托。</param>
        public FuncDbValueConverter(DbConvertHandler<TDbType, TValueType>? fromDb, DbConvertHandler<TValueType, object>? toDb)
        {
            _fromDb = fromDb;
            _toDb = toDb;
        }

        Type IDbValueConverter.ValueType => typeof(TValueType);

        /// <summary>数据库值 → <typeparamref name="TValueType"/> 的转换委托；为 null 时直接赋值。</summary>
        public DbConvertHandler<TDbType, TValueType>? DbReadConverter => _fromDb;

        /// <summary><typeparamref name="TValueType"/> → 数据库值的转换委托；为 null 时直接赋值。</summary>
        public DbConvertHandler<TValueType, object>? DbWriteConverter => _toDb;

        DbConvertHandler? IDbValueConverter.DbReadConverter =>
            _fromDb == null ? null : obj =>     
                _fromDb(typeof(TDbType) == typeof(object) || obj is TDbType
                    ? (TDbType)obj!
                    : (TDbType)Convert.ChangeType(obj, typeof(TDbType)))!;

        DbConvertHandler? IDbValueConverter.DbWriteConverter =>
            _toDb == null ? null : obj => _toDb((TValueType)obj);
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

        /// <summary>
        /// 获取按 <paramref name="dbValueType"/> 注册的、值类型为 <typeparamref name="TValueType"/>、数据库 CLR 类型为
        /// <typeparamref name="TDbType"/> 的强类型转换器；不存在或类型不匹配时返回 null。
        /// </summary>
        /// <typeparam name="TValueType">实体属性 / .NET 值类型。</typeparam>
        /// <typeparam name="TDbType">数据库驱动返回的数据库值 CLR 类型。</typeparam>
        /// <param name="dbValueType">数据库取值类型。</param>
        /// <returns>匹配的强类型转换器；未注册或类型外键不匹配时返回 null。</returns>
        IDbValueConverter<TValueType, TDbType>? GetDbValueConverter<TValueType, TDbType>(DbValueType dbValueType);

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
