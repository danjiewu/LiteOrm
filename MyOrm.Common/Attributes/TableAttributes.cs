using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

namespace MyOrm.Common
{
    /// <summary>
    /// 数据库表属性，用来标识对象对应的数据库表
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
    public class TableAttribute : System.Attribute
    {
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public TableAttribute() { }
        /// <summary>
        /// 指定表名的构造函数
        /// </summary>
        /// <param name="tableName">表名</param>
        public TableAttribute(string tableName) { TableName = tableName; }

        /// <summary>
        /// 数据库表名
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// 数据源名称，对应配置文件中ConnectionStrings中名称，为空则取默认数据源
        /// </summary>
        public string DataSource { get; set; }
    }   
}
