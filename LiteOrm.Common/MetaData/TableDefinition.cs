using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LiteOrm.Common
{
    /// <summary>
    /// 数据库表的定义
    /// </summary>
    public class TableDefinition : SqlTable
    {
        internal TableDefinition(Type objectType, ICollection<ColumnDefinition> columns) :
            base(new List<ColumnDefinition>(columns).ConvertAll<SqlColumn>(column => column))
        {
            this.ObjectType = objectType;
            Columns = new List<ColumnDefinition>(columns).AsReadOnly();
        }

        /// <summary>
        /// 对应数据库表的定义
        /// </summary>
        public override TableDefinition Definition
        {
            get { return this; }
        }

        /// <summary>
        /// 对象类型
        /// </summary>
        public Type ObjectType { get; }

        /// <summary>
        /// 数据源名称，对应配置文件中ConnectionStrings中名称，为空则取默认数据源
        /// </summary>
        public string DataSource { get; protected internal set; }

        /// <summary>
        /// 数据提供程序类型，如Microsoft.Data.SqlClient.SqlConnection
        /// </summary>
        public Type DataProviderType { get; protected internal set; }

        /// <summary>
        /// 数据库表的列定义
        /// </summary>
        public new ReadOnlyCollection<ColumnDefinition> Columns { get; }


        /// <summary>
        /// 根据属性名获得列定义，忽略大小写
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <returns>列定义，列名不存在则返回null</returns>
        public new ColumnDefinition GetColumn(string propertyName)
        {
            return base.GetColumn(propertyName) as ColumnDefinition;
        }
    }
}
