using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示一个联合查询的外部表。
    /// </summary>
    public class JoinedTable : SqlObject
    {
        /// <summary>
        /// 使用指定的外部表定义初始化联合表。
        /// </summary>
        /// <param name="foreignTable">外部表的定义信息。</param>
        public JoinedTable(TableDefinition foreignTable)
            : this(foreignTable, foreignTable?.Keys)
        {
        }

        /// <summary>
        /// 使用指定的外部表定义和目标关联键初始化联合表。
        /// </summary>
        /// <param name="foreignTable">外部表的定义信息。</param>
        /// <param name="foreignPrimeKeys">目标表用于参与关联的键列集合。</param>
        public JoinedTable(TableDefinition foreignTable, IEnumerable<ColumnDefinition>? foreignPrimeKeys)
        {
            if (foreignTable == null) throw new ArgumentNullException(nameof(foreignTable));
            if (foreignPrimeKeys == null) throw new ArgumentNullException(nameof(foreignPrimeKeys));

            _tableDefinition = foreignTable;
            Name = foreignTable.Name;

            JoinType = TableJoinType.Left;
            List<ColumnRef> keys = new List<ColumnRef>();
            foreach (ColumnDefinition key in foreignPrimeKeys)
            {
                if (key == null) throw new ArgumentException("Target key column cannot be null.", nameof(foreignPrimeKeys));
                keys.Add(new ColumnRef(this, key));
            }
            if (keys.Count == 0) throw new ArgumentException("At least one target key column is required.", nameof(foreignPrimeKeys));
            _foreignPrimeKeys = keys.AsReadOnly();
        }

        private readonly ReadOnlyCollection<ColumnRef> _foreignPrimeKeys;
        private ReadOnlyCollection<ColumnRef> _foreignKeys = new List<ColumnRef>().AsReadOnly();
        private readonly TableDefinition _tableDefinition;
        private readonly ConcurrentDictionary<string, ColumnRef> _namedColumnCache = new ConcurrentDictionary<string, ColumnRef>();

        /// <summary>
        /// 对应数据库表的定义
        /// </summary>
        public TableDefinition TableDefinition
        {
            get { return _tableDefinition; }
        }

        /// <summary>
        /// 属性名对应列引用的缓存，按属性名精确匹配（区分大小写）
        /// </summary>
        protected ConcurrentDictionary<string, ColumnRef> NamedColumnCache
        {
            get
            {
                if (_namedColumnCache.Count == 0)
                {
                    lock (_namedColumnCache)
                    {
                        if (_namedColumnCache.Count == 0)
                        {
                            foreach (ColumnDefinition column in _tableDefinition.Columns)
                                _namedColumnCache[column.PropertyName] = new ColumnRef(this, column);
                        }
                    }
                }
                return _namedColumnCache;
            }
        }

        /// <summary>
        /// 根据属性名获得列引用，区分大小写
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <returns>列引用，属性名不存在则返回null</returns>
        public virtual ColumnRef? GetColumn(string propertyName)
        {
            if (String.IsNullOrEmpty(propertyName)) return null;
            ColumnRef? column;
            NamedColumnCache.TryGetValue(propertyName, out column);
            return column;
        }

        /// <summary>
        /// 获取或内部设置关联外部表的列集合。
        /// </summary>
        public ReadOnlyCollection<ColumnRef> ForeignKeys
        {
            get { return _foreignKeys; }
            internal set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (value.Count != _foreignPrimeKeys.Count) throw new ArgumentException("The number of foreign keys does not match the target primary keys.");
                _foreignKeys = value;
            }
        }

        /// <summary>
        /// 指示该联合表是否已被使用。
        /// </summary>
        public bool Used { get; internal set; } = false;

        /// <summary>
        /// 联合查询连接类型（如 Left Join）。
        /// </summary>
        public TableJoinType JoinType { get; set; }

        /// <summary>
        /// 是否自动扩展连接的外表。当AutoExpand为true并且作为外表被引用时，自动将本表关联的外表引入连接。默认为false，即不自动扩展连接的外表。
        /// </summary>
        public bool AutoExpand { get; set; } = false;

        /// <summary>
        /// 获取关联外部表的目标主键列集合。
        /// </summary>
        public ReadOnlyCollection<ColumnRef> ForeignPrimeKeys
        {
            get { return _foreignPrimeKeys; }
        }

        /// <summary>
        /// 获取或设置该关联表的固定筛选条件。
        /// </summary>
        public LogicExpr? ConstFilter { get; internal set; }

        /// <summary>
        /// 确定指定的对象是否等于当前对象。相等性以表定义与表别名为准，
        /// <see cref="TableView.SortTables"/> 依赖该语义对关联表排序。
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj == null || obj.GetType() != GetType()) return false;
            JoinedTable other = (JoinedTable)obj;
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
