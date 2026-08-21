using System;
using System.Diagnostics.CodeAnalysis;

namespace LiteOrm.Common
{
    /// <summary>
    /// 数据库列特性，用于标识实体属性对应的数据库列。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class ColumnAttribute : Attribute
    {
        /// <summary>
        /// 初始化 <see cref="ColumnAttribute"/> 类的新实例。
        /// </summary>
        public ColumnAttribute()
        {
            ColumnMode = ColumnMode.Full;
            AllowNull = true;
        }

        /// <summary>
        /// 初始化 <see cref="ColumnAttribute"/> 类的新实例，指定是否为数据库列。
        /// </summary>
        /// <param name="isColumn">是否映射到数据库列。</param>
        public ColumnAttribute(bool isColumn)
            : this()
        {
            this.isColumn = isColumn;
        }

        /// <summary>
        /// 初始化 <see cref="ColumnAttribute"/> 类的新实例，并指定列名。
        /// </summary>
        /// <param name="columnName">数据库列名。</param>
        public ColumnAttribute(string columnName)
            : this(true)
        {
            ColumnName = columnName;
        }

        private readonly bool isColumn = true;

        /// <summary>
        /// 获取一个值，该值指示该属性是否映射到数据库列。
        /// </summary>
        public bool IsColumn
        {
            get { return isColumn; }
        }

        /// <summary>
        /// 获取或设置数据库列名。
        /// </summary>
        public string? ColumnName { get; set; }

        /// <summary>
        /// 获取或设置一个值，该值指示该列是否为主键。
        /// </summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>
        /// 获取或设置一个值，该值指示该列是否为标识列（自增）。
        /// </summary>
        public bool IsIdentity { get; set; }

        /// <summary>
        /// 获取或设置标识列（自增）的起始值。
        /// </summary>
        public long IdentityStart { get; set; } = 1;

        /// <summary>
        /// 获取或设置标识列（自增）的增量值。
        /// </summary>
        public int IdentityIncreasement { get; set; } = 1;

        /// <summary>
        /// 获取或设置一个值，该值指示该列是否为时间戳列。
        /// </summary>
        public bool IsTimestamp { get; set; }

        /// <summary>
        /// 获取或设置标识列（自增）的表达式（如序列名称）。
        /// </summary>
        public string? IdentityExpression { get; set; }

        /// <summary>
        /// 获取或设置一个值，该值指示该列是否应创建索引。
        /// </summary>
        public bool IsIndex { get; set; }

        /// <summary>
        /// 获取或设置一个值，该值指示该列是否具有唯一约束。
        /// </summary>
        public bool IsUnique { get; set; }

        /// <summary>
        /// 获取或设置数据库列的长度。
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// 获取或设置数据库列的数据类型。
        /// 为 <see cref="DbValueType.Default"/> 时表示使用默认值，由 <see cref="ISqlBuilder"/> 根据属性类型推断。
        /// </summary>
        public DbValueType DbType { get; set; } = DbValueType.Default;

        /// <summary>
        /// 计算列表达式（非实际列）。
        /// 设置后该列不生成物理列、不参与插入/更新；查询 SELECT 时以表达式返回结果，
        /// 查询条件中引用该属性时同样按表达式生成。表达式内用 <c>{属性名}</c> 引用同一实体的其他属性
        /// （如 <c>{FirstName} || ' ' || {LastName}</c>），占位符会按列名（含必要的引号与表限定）渲染；
        /// 也可直接书写数据库方言的原始 SQL 片段。建议同时设置 <see cref="ColumnMode"/> 为
        /// <see cref="ColumnMode.Computed"/>。
        /// </summary>
        public string? Expression { get; set; }

        /// <summary>
        /// 获取或设置一个值，该值指示该列是否允许为空。
        /// </summary>
        public bool AllowNull { get; set; }

        /// <summary>
        /// 获取或设置列的默认值，可以是一个常量值或一个数据库函数表达式。
        /// </summary>
        public string? DefaultValue { get; set; }

        /// <summary>
        /// 获取或设置列的固定筛选值。支持枚举和其他可转换到属性类型的常量值；对于枚举，支持使用枚举名、整型值或枚举成员声明。
        /// </summary>
        public object? Constant { get; set; }

        /// <summary>
        /// 获取或设置列映射模式。
        /// </summary>
        public ColumnMode ColumnMode { get; set; }

        /// <summary>
        /// 获取或设置列值转换器类型。该类型须实现 <see cref="IDbValueConverter"/> 并具有公共无参构造函数，
        /// 由表信息提供器实例化后赋给 <see cref="SqlColumn.DbValueConverter"/>。
        /// </summary>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        public Type? ValueConverterType { get; set; }
    }
}
