using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace LiteOrm.Common
{
    /// <summary>
    /// 源生成器在编译期收集的列信息，用于动态注册到 <see cref="CommonTableInfoProvider"/>。
    /// </summary>
    public sealed class ColumnInfo
    {
        /// <summary>
        /// 实体属性名。
        /// </summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>
        /// 数据库列名。
        /// </summary>
        public string ColumnName { get; set; } = string.Empty;

        /// <summary>
        /// 数据库列取值类型，<see cref="DbValueType.Default"/> 表示未显式指定。
        /// </summary>
        public DbValueType DbType { get; set; } = DbValueType.Default;

        /// <summary>
        /// 列映射模式。
        /// </summary>
        public ColumnMode Mode { get; set; } = ColumnMode.Full;

        /// <summary>
        /// 是否为主键。
        /// </summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>
        /// 是否为自增标识列。
        /// </summary>
        public bool IsIdentity { get; set; }

        /// <summary>
        /// 是否为时间戳列。
        /// </summary>
        public bool IsTimestamp { get; set; }

        /// <summary>
        /// 是否创建索引。
        /// </summary>
        public bool IsIndex { get; set; }

        /// <summary>
        /// 是否具有唯一约束。
        /// </summary>
        public bool IsUnique { get; set; }

        /// <summary>
        /// 是否允许为空。
        /// </summary>
        public bool AllowNull { get; set; }

        /// <summary>
        /// 列长度。
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// 计算列表达式。
        /// </summary>
        public string? Expression { get; set; }

        /// <summary>
        /// 列默认值。
        /// </summary>
        public string? DefaultValue { get; set; }

        /// <summary>
        /// 标识列表达式（如序列名称）。
        /// </summary>
        public string? IdentityExpression { get; set; }

        /// <summary>
        /// 标识列起始值。
        /// </summary>
        public long IdentityStart { get; set; } = 1;

        /// <summary>
        /// 标识列增量。
        /// </summary>
        public int IdentityIncreasement { get; set; } = 1;

        /// <summary>
        /// 列值转换器实例（由源生成器生成的注册代码直接构造并赋值，AOT 友好）。
        /// </summary>
        public IDbValueConverter? ValueConverter { get; set; }
    }

    /// <summary>
    /// 源生成器在编译期收集的表信息，用于动态注册到 <see cref="CommonTableInfoProvider"/>。
    /// </summary>
    public sealed class TableInfo
    {
        /// <summary>
        /// 初始化 <see cref="TableInfo"/> 类的新实例。
        /// </summary>
        /// <param name="objectType">实体类型。</param>
        /// <param name="name">表名。</param>
        /// <param name="dataSource">数据源名称，可为空。</param>
        /// <param name="syncTable">表结构同步模式。</param>
        /// <param name="columns">列信息集合。</param>
        public TableInfo(
            [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)]
            Type objectType,
            string name,
            string? dataSource,
            SyncTableMode syncTable,
            IReadOnlyList<ColumnInfo> columns)
        {
            ObjectType = objectType ?? throw new ArgumentNullException(nameof(objectType));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DataSource = dataSource;
            SyncTable = syncTable;
            Columns = columns ?? throw new ArgumentNullException(nameof(columns));
        }

        /// <summary>
        /// 实体类型。
        /// </summary>
        [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)]
        public Type ObjectType { get; }

        /// <summary>
        /// 表名。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 数据源名称。
        /// </summary>
        public string? DataSource { get; }

        /// <summary>
        /// 表结构同步模式。
        /// </summary>
        public SyncTableMode SyncTable { get; }

        /// <summary>
        /// 列信息集合。
        /// </summary>
        public IReadOnlyList<ColumnInfo> Columns { get; }
    }
}
