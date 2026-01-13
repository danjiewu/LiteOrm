using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

namespace LiteOrm.Common
{
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
        public string TableName { get; set; }

        /// <summary>
        /// 获取或设置数据源名称。该名称通常对应于配置文件中 ConnectionStrings 节点的名称。
        /// </summary>
        public string DataSource { get; set; }
    }   
}
