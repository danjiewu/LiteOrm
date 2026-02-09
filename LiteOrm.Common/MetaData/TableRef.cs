using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;

namespace LiteOrm.Common
{
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

        /// <summary>
        /// 确定指定的对象是否等于当前对象。
        /// </summary>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj == null || obj.GetType() != GetType()) return false;
            TableRef other = (TableRef)obj;
            return Equals(TableDefinition, other.TableDefinition) && Name == other.Name;
        }

        /// <summary>
        /// 获取哈希码。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return ((TableDefinition?.GetHashCode() ?? 0) * 31) ^ (Name?.GetHashCode() ?? 0);
            }
        }
    }
}
