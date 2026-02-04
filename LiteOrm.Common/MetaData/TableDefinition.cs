using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LiteOrm.Common
{
    /// <summary>
    /// 数据库表定义信息。
    /// 包含表的结构信息，如对应的实体类型、列定义、数据源等。
    /// </summary>
    public class TableDefinition : SqlTable
    {
        /// <summary>
        /// 初始化 <see cref="TableDefinition"/> 类的新实例。
        /// </summary>
        /// <param name="objectType">对应的实体类型。</param>
        /// <param name="columns">列定义集合。</param>
        internal TableDefinition(Type objectType, ICollection<ColumnDefinition> columns) :
            base(new List<ColumnDefinition>(columns).ConvertAll<SqlColumn>(column => column))
        {
            this.ObjectType = objectType;
            Columns = new List<ColumnDefinition>(columns).AsReadOnly();
        }

        /// <summary>
        /// 获取当前表的定义信息。
        /// </summary>
        public override TableDefinition Definition
        {
            get { return this; }
        }

        /// <summary>
        /// 获取对应的实体类型。
        /// </summary>
        public Type ObjectType { get; }

        /// <summary>
        /// 获取或设置数据源名称。
        /// 该名称通常对应于配置文件中 ConnectionStrings 节点的名称。
        /// 若为空，则使用默认数据源。
        /// </summary>
        public string DataSource { get; protected internal set; }

        /// <summary>
        /// 获取或设置数据提供程序类型。
        /// 例如：Microsoft.Data.SqlClient.SqlConnection
        /// </summary>
        public Type DataProviderType { get; protected internal set; }

        /// <summary>
        /// 获取数据库表的列定义集合。
        /// </summary>
        public new ReadOnlyCollection<ColumnDefinition> Columns { get; }

        /// <summary>
        /// 根据属性名获取对应的列定义，忽略大小写。
        /// </summary>
        /// <param name="propertyName">属性名称。</param>
        /// <returns>列定义，若不存在则返回null。</returns>
        public new ColumnDefinition GetColumn(string propertyName)
        {
            return base.GetColumn(propertyName) as ColumnDefinition;
        }
    }
}
