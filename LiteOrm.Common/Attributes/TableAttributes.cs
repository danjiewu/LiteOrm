using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表结构同步模式，用于在实体类级别覆盖数据源（连接池）的 <see cref="DataSourceConfig.SyncTable"/> 配置。
    /// </summary>
    public enum SyncTableMode
    {
        /// <summary>
        /// 使用数据源（连接池）级别的 <see cref="DataSourceConfig.SyncTable"/> 配置，不进行覆盖。
        /// </summary>
        Default = 0,
        /// <summary>
        /// 永不对该实体类型执行表结构同步，覆盖数据源级别配置。
        /// </summary>
        Never = 1,
        /// <summary>
        /// 始终对该实体类型执行表结构同步，覆盖数据源级别配置。
        /// </summary>
        Always = 2
    }

    /// <summary>
    /// 数据库表特性，用于标识实体类对应的数据库表。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
    public class TableAttribute : System.Attribute
    {
        /// <summary>
        /// 初始化 <see cref="TableAttribute"/> 类的新实例。
        /// </summary>
        public TableAttribute() { }
        /// <summary>
        /// 初始化 <see cref="TableAttribute"/> 类的新实例，并指定表名。
        /// </summary>
        /// <param name="tableName">数据库表名。</param>
        public TableAttribute(string tableName) { TableName = tableName; }

        /// <summary>
        /// 获取或设置数据库表名。
        /// </summary>
        public string? TableName { get; set; }

        /// <summary>
        /// 获取或设置数据源名称。该名称通常对应于配置文件中 ConnectionStrings 节点的名称。
        /// </summary>
        public string? DataSource { get; set; }

        /// <summary>
        /// 获取或设置该实体类型的表结构同步模式。
        /// 默认为 <see cref="SyncTableMode.Default"/>，即沿用数据源（连接池）级别的 <see cref="DataSourceConfig.SyncTable"/> 配置；
        /// 设为 <see cref="SyncTableMode.Never"/> 或 <see cref="SyncTableMode.Always"/> 时将覆盖数据源配置，优先级高于 <see cref="DataSourceConfig.SyncTable"/>。
        /// </summary>
        public SyncTableMode SyncTable { get; set; }

        /// <summary>
        /// 获取或设置一个值，该值指示是否自动为未标注 <see cref="ColumnAttribute"/> 的属性生成列定义。
        /// 默认为 <c>true</c>：未标注 <see cref="ColumnAttribute"/> 的属性中，已知标量类型（数值、<see cref="string"/>、
        /// <see cref="byte"/> 数组、<see cref="Guid"/>、日期时间、枚举等）以及 <see cref="DbValueType.Json"/> /
        /// <see cref="DbValueType.Jsonb"/> 映射类型自动映射为列，数组、集合与自定义类等复杂类型不生成列。
        /// 设为 <c>false</c> 时只有显式标注 <see cref="ColumnAttribute"/>（且未通过 <see cref="ColumnAttribute.IsColumn"/>
        /// 排除）的属性才映射为列，未标注的属性一律忽略；标注 <see cref="ForeignColumnAttribute"/> 的属性仍作为外键列参与关联查询。
        /// 该标记同时作用于运行时表信息提供器与源生成器。
        /// </summary>
        public bool AutoGenerateColumns { get; set; } = true;
    }
}
