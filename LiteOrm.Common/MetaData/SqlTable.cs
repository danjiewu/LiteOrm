using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LiteOrm.Common
{
    /// <summary>
    /// 数据库表定义
    /// </summary>
    public abstract class SqlTable : SqlObject
    {
        internal SqlTable(IEnumerable<SqlColumn> columns)
        {
            Columns = columns.ToList().AsReadOnly();
            foreach (SqlColumn column in Columns) column.Table = this;
        }

        #region 私有变量
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
            get;
        }

        /// <summary>
        /// 主键列，按属性名称的顺序排列
        /// </summary>
        public ReadOnlyCollection<ColumnDefinition> Keys
        {
            get
            {
                if (field == null)
                {
                    List<ColumnDefinition> keyList = new List<ColumnDefinition>();
                    foreach (SqlColumn column in Columns)
                    {
                        if (column is ColumnDefinition columnDef && columnDef.IsPrimaryKey) keyList.Add(columnDef);
                    }
                    keyList.Sort(delegate (ColumnDefinition column1, ColumnDefinition column2) { return String.Compare(column1.PropertyName, column2.PropertyName); });
                    field = keyList.AsReadOnly();
                }
                return field;
            }
        }

        /// <summary>
        /// 查询时需要读取的列，即 <see cref="ColumnMode"/> 允许读取的列。
        /// 结果按列在 <see cref="Columns"/> 中的顺序排列，与执行 SELECT 时的列顺序一致。
        /// </summary>
        public ReadOnlyCollection<SqlColumn> SelectColumns
        {
            get
            {
                if (field == null)
                {
                    field = new ReadOnlyCollection<SqlColumn>(Columns.Where(col =>
                    {
                        SqlColumn column = col;
                        while (column is ForeignColumn foreignColumn) column = foreignColumn.TargetColumn.Column;
                        if (column is ColumnDefinition columnDefinition)
                            return columnDefinition.Mode.CanRead();
                        return true;
                    }).ToList());
                }
                return field;
            }
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
        /// 确定指定的对象是否等于当前对象。
        /// </summary>
        /// <param name="obj">要与当前对象进行比较的对象。</param>
        /// <returns>如果指定的对象等于当前对象，则为 true；否则为 false。</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj == null || obj.GetType() != GetType()) return false;
            return DefinitionType == ((SqlTable)obj).DefinitionType;
        }

        /// <summary>
        /// 获取对象的哈希代码。
        /// </summary>
        /// <returns>当前对象的哈希代码。</returns>
        public override int GetHashCode()
        {
            return DefinitionType?.GetHashCode() ?? 0;
        }

        /// <summary>
        /// 重写ToString方法，返回表名。
        /// </summary>
        /// <returns>表名。</returns>
        public override string ToString()
        {
            return Name;
        }
    }
}
