using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using System.Linq;
using System.Data.Common;
using System.Collections.Concurrent;

namespace LiteOrm.Common
{
    /// <summary>
    /// 数据库表定义
    /// </summary>
    public abstract class SqlTable : SqlObject
    {
        internal SqlTable(ICollection<SqlColumn> columns)
        {
            _columns = new List<SqlColumn>(columns).AsReadOnly();
            foreach (SqlColumn column in columns) column.Table = this;
        }

        #region 私有变量
        private ReadOnlyCollection<SqlColumn> _columns;
        private ConcurrentDictionary<string, SqlColumn> _namedColumnCache = new ConcurrentDictionary<string, SqlColumn>(StringComparer.OrdinalIgnoreCase);
        #endregion

        /// <summary>
        /// 对应的数据库表的定义
        /// </summary>
        public abstract TableDefinition Definition
        {
            get;
        }

        /// <summary>
        /// 对象类型
        /// </summary>
        public Type DefinitionType
        {
            get { return Definition.ObjectType; }
        }

        /// <summary>
        /// 数据库表的列信息，包括关联的外部列
        /// </summary>
        public ReadOnlyCollection<SqlColumn> Columns
        {
            get { return _columns; }
        }

        /// <summary>
        /// 属性名对应列的缓存
        /// </summary>
        protected ConcurrentDictionary<string, SqlColumn> NamedColumnCache
        {
            get
            {
                if (_namedColumnCache.Count == 0)
                {
                    lock (_namedColumnCache)
                    {
                        if (_namedColumnCache.Count == 0)
                            foreach (SqlColumn column in Columns)
                                _namedColumnCache[column.PropertyName] = column;
                    }
                }
                return _namedColumnCache;
            }
        }

        /// <summary>
        /// 根据属性名获得列定义，忽略大小写
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <returns>列定义，列名不存在则返回null</returns>
        public virtual SqlColumn GetColumn(string propertyName)
        {
            if (String.IsNullOrEmpty(propertyName)) return null;
            SqlColumn column;
            NamedColumnCache.TryGetValue(propertyName, out column);
            return column;
        }

        /// <summary>
        /// 清空缓存
        /// </summary>
        public void ClearCache()
        {
            _namedColumnCache.Clear();
        }

        /// <summary>
        /// 重写ToString方法
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Name;
        }
    }



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

        private ReadOnlyCollection<ColumnDefinition> _keys = null;

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
        /// 主键列，按属性名称的顺序排列
        /// </summary>
        public ReadOnlyCollection<ColumnDefinition> Keys
        {
            get
            {
                if (_keys == null)
                {
                    List<ColumnDefinition> keyList = new List<ColumnDefinition>();
                    foreach (ColumnDefinition column in Columns)
                    {
                        if (column.IsPrimaryKey) keyList.Add(column);
                    }
                    keyList.Sort(delegate (ColumnDefinition column1, ColumnDefinition column2) { return String.Compare(column1.PropertyName, column2.PropertyName); });
                    _keys = keyList.AsReadOnly();
                }
                return _keys;
            }
        }

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

    /// <summary>
    /// 数据库表的引用
    /// </summary>
    public abstract class TableRef : SqlObject
    {
        /// <summary>
        /// 创建数据库表的引用
        /// </summary>
        /// <param name="table">引用的数据库表定义</param>
        public TableRef(TableDefinition table)
        {
            _tableDefinition = table;
            Name = table.Name;
            _columns = table.Columns.Select(column => new ColumnRef(this, column)).ToList().AsReadOnly();
        }

        private TableDefinition _tableDefinition;
        private ReadOnlyCollection<ColumnRef> _columns;
        private ConcurrentDictionary<string, ColumnRef> _namedColumnCache = new ConcurrentDictionary<string, ColumnRef>();

        /// <summary>
        /// 对应数据库表的定义
        /// </summary>
        public TableDefinition TableDefinition
        {
            get { return _tableDefinition; }
        }

        /// <summary>
        /// 数据库表的列信息
        /// </summary>
        public ReadOnlyCollection<ColumnRef> Columns
        {
            get { return _columns; }
        }

        /// <summary>
        /// 属性名对应列的缓存
        /// </summary>
        protected ConcurrentDictionary<string, ColumnRef> NamedColumnCache
        {
            get
            {
                if (_namedColumnCache.Count == 0)
                {
                    foreach (ColumnRef column in Columns)
                        _namedColumnCache[column.Column.PropertyName] = column;
                }
                return _namedColumnCache;
            }
        }

        /// <summary>
        /// 根据属性名获得列定义，忽略大小写
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <returns>列定义，列名不存在则返回null</returns>
        public virtual ColumnRef GetColumn(string propertyName)
        {
            if (String.IsNullOrEmpty(propertyName)) return null;
            ColumnRef column;
            NamedColumnCache.TryGetValue(propertyName, out column);
            return column;
        }
    }



    /// <summary>
    /// 外部表信息，用于描述关联的外部表
    /// </summary>
    public class ForeignTable
    {
        /// <summary>
        /// 外部表对应的实体类型
        /// </summary>
        public Type ForeignType { get; set; }

        /// <summary>
        /// 过滤表达式，用于定义关联条件
        /// </summary>
        public string FilterExpression { get; set; }
    }
}
