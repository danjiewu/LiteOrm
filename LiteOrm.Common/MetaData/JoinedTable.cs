using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

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
            : base(foreignTable)
        {
            _foreignTable = foreignTable;
            JoinType = TableJoinType.Left;
            List<ColumnRef> keys = new List<ColumnRef>();
            foreach (ColumnDefinition key in foreignTable.Keys)
            {
                keys.Add(new ColumnRef(this, key));
            }
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
        /// 获取关联外部表的目标主键列集合。
        /// </summary>
        public ReadOnlyCollection<ColumnRef> ForeignPrimeKeys
        {
            get { return _foreignPrimeKeys; }
        }

        /// <summary>
        /// 关联查询时的筛选表达式（由 Filter 属性定义）。
        /// </summary>
        public string FilterExpression { get; set; }

    }

}
