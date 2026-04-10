using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示一个联合查询的外部表。
    /// </summary>
    public class JoinedTable : TableRef
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
        public JoinedTable(TableDefinition foreignTable, IEnumerable<ColumnDefinition> foreignPrimeKeys)
            : base(foreignTable)
        {
            if (foreignTable == null) throw new ArgumentNullException(nameof(foreignTable));
            if (foreignPrimeKeys == null) throw new ArgumentNullException(nameof(foreignPrimeKeys));

            _foreignTable = foreignTable;
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
        private readonly TableDefinition _foreignTable;
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
        public LogicExpr ConstFilter { get; internal set; }
    }

}
