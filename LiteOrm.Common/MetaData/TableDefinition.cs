using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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
        private ColumnDefinition _dentityColumn;
        private ColumnDefinition[] _insertableColumns;
        private ColumnDefinition[] _updatableColumns;
        private ColumnDefinition _timestampColumn;

        /// <summary>
        /// 获取可插入的列定义数组，排除自增列和不可插入的列。
        /// </summary>
        public ColumnDefinition[] InsertableColumns
        {
            get
            {
                if (_insertableColumns is null)
                {
                    _insertableColumns = Columns.Where(column => !column.IsIdentity && column.Mode.CanInsert()).ToArray();
                }
                return _insertableColumns;
            }
        }

        /// <summary>
        /// 获取可更新的列定义数组，排除主键列和不可更新的列。
        /// </summary>
        public ColumnDefinition[] UpdatableColumns
        {
            get
            {
                if (_updatableColumns is null)
                {
                    _updatableColumns = Columns.Where(column => !column.IsPrimaryKey && column.Mode.CanUpdate()).ToArray();
                }
                return _updatableColumns;
            }
        }

        /// <summary>
        /// 识别列
        /// </summary>
        public ColumnDefinition IdentityColumn
        {
            get
            {
                if (_dentityColumn is null) _dentityColumn = Columns.FirstOrDefault(col => col.IsIdentity);
                return _dentityColumn;
            }
        }

        /// <summary>
        /// 时间戳列（用于乐观并发控制）
        /// </summary>
        public ColumnDefinition TimestampColumn
        {
            get
            {
                if (_timestampColumn is null) _timestampColumn = Columns.FirstOrDefault(col => col.IsTimestamp);
                return _timestampColumn;
            }
        }

        /// <summary>
        /// 获取或设置该表的固定筛选条件。
        /// </summary>
        public LogicExpr ConstFilter { get; set; }

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
