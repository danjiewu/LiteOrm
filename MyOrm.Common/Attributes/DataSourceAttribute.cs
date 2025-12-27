using System;
using System.Collections.Generic;
using System.Text;

namespace MyOrm.Common
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
    public class DataSourceAttribute : System.Attribute
    {
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public DataSourceAttribute() { }
        /// <summary>
        /// 指定数据库连接名的构造函数
        /// </summary>
        /// <param name="connectionName">数据库连接名</param>
        public DataSourceAttribute(string connectionName) { ConnectionName = connectionName; }

        /// <summary>
        /// 数据库连接名
        /// </summary>
        public string ConnectionName { get; set; }
    }
}
