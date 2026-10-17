using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace LiteOrm.Common
{
    /// <summary>
    /// 源生成器在编译期收集的关联外表列定义（对应 <see cref="ForeignColumnAttribute"/>）。
    /// 由 <see cref="CommonTableInfoProvider"/> 在构建 <see cref="TableView"/> 时还原为 <see cref="ForeignColumn"/>。
    /// </summary>
    public sealed class ForeignColumnInfo
    {
        /// <summary>
        /// 关联的外部实体类型，对应 <c>[ForeignColumn(typeof(X))]</c>；为 null 时按 <see cref="ForeignAlias"/> 匹配。
        /// </summary>
        [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)]
        public Type? ForeignType { get; set; }

        /// <summary>
        /// 关联的外部表别名，对应 <c>[ForeignColumn("Alias")]</c>；仅在 <see cref="ForeignType"/> 为 null 时使用。
        /// </summary>
        public string? ForeignAlias { get; set; }

        /// <summary>
        /// 目标表的属性名，对应 <see cref="ForeignColumnAttribute.Property"/>；为空时取本列的同名属性。
        /// </summary>
        public string? Property { get; set; }
    }

    /// <summary>
    /// 源生成器在编译期收集的关联表定义（对应 <see cref="TableJoinAttribute"/>）。
    /// 由 <see cref="CommonTableInfoProvider"/> 在构建 <see cref="TableView"/> 时还原为 <see cref="JoinedTable"/>。
    /// </summary>
    public sealed class JoinedTableInfo
    {
        /// <summary>
        /// 关联的目标实体类型。
        /// </summary>
        [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)]
        public Type TargetType { get; set; } = null!;

        /// <summary>
        /// 关联表别名，为空时取目标类型的类型名。
        /// </summary>
        public string? Alias { get; set; }

        /// <summary>
        /// 外键来自的源表别名，对应 <c>[TableJoin("SourceAlias", ...)]</c>。
        /// 为 null 且 <see cref="SourceType"/> 也为 null 时表示外键来自主表。
        /// </summary>
        public string? SourceAlias { get; set; }

        /// <summary>
        /// 外键来自的源表类型，对应 <c>[TableJoin(typeof(Source), ...)]</c>。
        /// </summary>
        [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)]
        public Type? SourceType { get; set; }

        /// <summary>
        /// 连接类型。
        /// </summary>
        public TableJoinType JoinType { get; set; } = TableJoinType.Left;

        /// <summary>
        /// 是否自动展开该关联表的后续关联路径。
        /// </summary>
        public bool AutoExpand { get; set; }

        /// <summary>
        /// 外键属性名，多个外键以逗号分隔，顺序与目标表参与关联的键一致。
        /// </summary>
        public string ForeignKeys { get; set; } = string.Empty;

        /// <summary>
        /// 目标表参与关联的键属性名，多个以逗号分隔；为空时使用目标表主键。
        /// </summary>
        public string? PrimeKeys { get; set; }
    }

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
        /// 列的固定筛选值（对应 <see cref="ColumnAttribute.Constant"/>）。
        /// 为 null 表示该列不参与固定筛选；非 null 时由 <see cref="CommonTableInfoProvider"/>
        /// 组装进 <see cref="TableDefinition.ConstFilter"/>。
        /// </summary>
        public object? Constant { get; set; }

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

        /// <summary>
        /// 该属性关联的外表集合（对应 <see cref="ForeignTypeAttribute"/>，可多个）。
        /// 用于在构建视图时把外键列扩展到关联表。
        /// </summary>
        public IReadOnlyList<ForeignTable>? ForeignTables { get; set; }

        /// <summary>
        /// 非空表示这是一个 <see cref="ForeignColumnAttribute"/> 投影列（不是真实表列），
        /// 构建视图时据此生成 <see cref="ForeignColumn"/>。
        /// </summary>
        public ForeignColumnInfo? ForeignColumn { get; set; }
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
        /// <param name="columns">列信息集合，按实体属性声明顺序排列，可包含 <see cref="ForeignColumnInfo"/> 投影列。</param>
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
        /// 列信息集合。按实体属性声明顺序排列，包含真实表列与 <see cref="ForeignColumnInfo"/> 投影列，
        /// 以保证构建出的 <c>TableView.Columns</c> 顺序与源生成的读取映射一一对应。
        /// </summary>
        public IReadOnlyList<ColumnInfo> Columns { get; }

        /// <summary>
        /// 类级关联表定义（对应 <see cref="TableJoinAttribute"/>）。
        /// 为 null 或空表示该类型没有预声明的关联表。
        /// </summary>
        public IReadOnlyList<JoinedTableInfo>? JoinedTables { get; set; }
    }
}
