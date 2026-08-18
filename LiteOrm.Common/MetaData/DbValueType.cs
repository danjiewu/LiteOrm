namespace LiteOrm.Common
{
    /// <summary>
    /// 列的自定义取值类型。枚举值与 <see cref="System.Data.DbType"/> 对齐以便映射，
    /// 额外提供 <see cref="Default"/>、<see cref="Json"/>、<see cref="Jsonb"/> 与 <see cref="Array"/> 类型。
    /// <see cref="Default"/> 表示未显式指定，运行时按属性类型自动推断；
    /// 集合类型属性在未显式指定类型时按 <see cref="Array"/> 推断。
    /// <para>
    /// <see cref="Array"/> 作为掩码（值 128），可与其他标量类型按位或组合
    ///（如 <c>DbValueType.Int32 | DbValueType.Array</c> 表示 Int32 数组）；
    /// 使用 <see cref="DbValueTypeMap.HasArray"/> 检测是否含数组掩码。
    /// </para>
    /// </summary>
    public enum DbValueType
    {
        /// <summary>未显式指定类型，运行时按属性类型自动推断。</summary>
        Default = -1,
        /// <summary>可变长度的非 Unicode 字符串。</summary>
        AnsiString = 0,
        /// <summary>二进制数据的可变长度流。</summary>
        Binary = 1,
        /// <summary>8 位无符号整数。</summary>
        Byte = 2,
        /// <summary>布尔值。</summary>
        Boolean = 3,
        /// <summary>货币值。</summary>
        Currency = 4,
        /// <summary>日期值。</summary>
        Date = 5,
        /// <summary>日期时间值。</summary>
        DateTime = 6,
        /// <summary>日期和时间值。</summary>
        Decimal = 7,
        /// <summary>浮点型。</summary>
        Double = 8,
        /// <summary>全局唯一标识符。</summary>
        Guid = 9,
        /// <summary>16 位有符号整数。</summary>
        Int16 = 10,
        /// <summary>32 位有符号整数。</summary>
        Int32 = 11,
        /// <summary>64 位有符号整数。</summary>
        Int64 = 12,
        /// <summary>通用类型。</summary>
        Object = 13,
        /// <summary>8 位有符号整数。</summary>
        SByte = 14,
        /// <summary>IEEE 32 位浮点数。</summary>
        Single = 15,
        /// <summary>可变长度 Unicode 字符串。</summary>
        String = 16,
        /// <summary>时间值。</summary>
        Time = 17,
        /// <summary>16 位无符号整数。</summary>
        UInt16 = 18,
        /// <summary>32 位无符号整数。</summary>
        UInt32 = 19,
        /// <summary>64 位无符号整数。</summary>
        UInt64 = 20,
        /// <summary>可变长度的数值。</summary>
        VarNumeric = 21,
        /// <summary>固定长度的非 Unicode 字符串。</summary>
        AnsiStringFixedLength = 22,
        /// <summary>固定长度的 Unicode 字符串。</summary>
        StringFixedLength = 23,
        /// <summary>XML 文档。</summary>
        Xml = 25,
        /// <summary>日期时间 2 类型。</summary>
        DateTime2 = 26,
        /// <summary>日期时间偏移类型。</summary>
        DateTimeOffset = 27,
        /// <summary>
        /// JSON 类型。映射到数据库时为 <see cref="System.Data.DbType.String"/>，
        /// 存储时对象会被序列化为 JSON 字符串，读取时反序列化回属性类型。
        /// </summary>
        Json = 28,
        /// <summary>
        /// PostgreSQL 二进制 JSON 类型（<c>jsonb</c>）。仅在 PostgreSQL / KingbaseES / GaussDB 等
        /// 兼容方言下生成 <c>JSONB</c> 列类型，其他方言回退为文本 JSON。
        /// </summary>
        Jsonb = 29,
        /// <summary>
        /// 数组掩码（值 128）。可与其他标量类型按位或组合
        ///（如 <c>DbValueType.Int32 | DbValueType.Array</c> 表示 Int32 数组）；
        /// 集合类型属性未显式指定类型时自动附加本掩码。
        /// PostgreSQL 等支持原生数组的方言据此生成数组列（如 <c>integer[]</c>），
        /// 其他方言回退为文本 JSON 存储。
        /// </summary>
        Array = 128
    }
}
