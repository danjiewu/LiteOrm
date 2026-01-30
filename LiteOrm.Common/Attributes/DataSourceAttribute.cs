using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// 数据源特性，用于指定类、结构体或接口对应的数据库连接
    /// </summary>
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
