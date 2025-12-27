using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

namespace MyOrm.Common
{
    /// <summary>
    /// 数据库列定义
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class ColumnAttribute : Attribute
    {
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public ColumnAttribute()
        {
            ColumnMode = ColumnMode.Full;
            DbType = DbType.Object;
            AllowNull = true;
        }

        /// <summary>
        /// 构造函数
        /// <param name="isColumn">是否是数据库列</param>
        /// </summary>
        public ColumnAttribute(bool isColumn)
            : this()
        {
            this.isColumn = isColumn;
        }

        /// <summary>
        /// 指定列名的构造函数
        /// </summary>
        /// <param name="columnName">列名</param>
        public ColumnAttribute(string columnName)
            : this(true)
        {
            ColumnName = columnName;
        }

        private readonly bool isColumn = true;

        /// <summary>
        /// 是否是数据库列
        /// </summary>
        public bool IsColumn
        {
            get { return isColumn; }
        }

        /// <summary>
        /// 数据库列名
        /// </summary>
        public string ColumnName { get; set; }

        /// <summary>
        /// 是否是主键
        /// </summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>
        /// 是否是自增长标识
        /// </summary>
        public bool IsIdentity { get; set; }

        /// <summary>
        /// 是否是时间戳
        /// </summary>
        public bool IsTimestamp { get; set; }
        /// <summary>
        /// 自增长列表达式
        /// </summary>
        public string IdentityExpression { get; set; }

        /// <summary>
        /// 是否是索引
        /// </summary>
        public bool IsIndex { get; set; }

        /// <summary>
        /// 是否唯一
        /// </summary>
        public bool IsUnique { get; set; }

        /// <summary>
        /// 数据库列长度
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// 数据库列数据类型
        /// </summary>
        public DbType DbType { get; set; }

        /// <summary>
        /// 列类型
        /// </summary>
        public ColumnMode ColumnMode { get; set; }

        /// <summary>
        /// 是否允许为空
        /// </summary>
        public bool AllowNull { get; set; }
    }

    /// <summary>
    /// 关联列信息
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class ForeignColumnAttribute : Attribute
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="foreignType">关联的外部对象类型</param>
        public ForeignColumnAttribute(Type foreignType)
        {
            this.Foreign = foreignType;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="foreignName">关联的外部表名称</param>
        public ForeignColumnAttribute(string foreignName)
        {
            this.Foreign = foreignName;
        }

        /// <summary>
        /// 关联的外部表，可以为外部表对应的Type，也可以为TableJoin中的AliasName
        /// </summary>
        public object Foreign { get; private set; }

        ///<summary>
        /// 关联的外部对象属性
        /// </summary>
        public string Property { get; set; }
    }
}
