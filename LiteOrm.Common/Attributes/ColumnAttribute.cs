using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

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
            DbType = DbType.Object;
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
        public string ColumnName { get; set; }

        /// <summary>
        /// 获取或设置一个值，该值指示该列是否为主键。
        /// </summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>
        /// 获取或设置一个值，该值指示该列是否为标识列（自增）。
        /// </summary>
        public bool IsIdentity { get; set; }

        /// <summary>
        /// 获取或设置一个值，该值指示该列是否为时间戳列。
        /// </summary>
        public bool IsTimestamp { get; set; }

        /// <summary>
        /// 获取或设置标识列（自增）的表达式（如序列名称）。
        /// </summary>
        public string IdentityExpression { get; set; }

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
        /// </summary>
        public DbType DbType { get; set; }

        /// <summary>
        /// 获取或设置一个值，该值指示该列是否允许为空。
        /// </summary>
        public bool AllowNull { get; set; }

        /// <summary>
        /// 获取或设置列映射模式。
        /// </summary>
        public ColumnMode ColumnMode { get; set; }
    }    
}
