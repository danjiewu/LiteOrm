using System;
using System.Collections.Generic;
using System.Text;
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
                if (value.Count != _foreignPrimeKeys.Count) throw new ArgumentException("外键数量与目标主键数量不一致。");
                _foreignKeys = value;
            }
        }

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
        /// <summary>
        /// 获取格式化后的 SQL JOIN 表达片段。
        /// </summary>
        public override string FormattedExpression(ISqlBuilder sqlBuilder)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"\n{JoinType.ToString().ToLower()} join {base.FormattedExpression(sqlBuilder)} {FormattedName(sqlBuilder)} on ");
            bool isFirst = true;
            for (int i = 0; i < ForeignKeys.Count; i++)
            {
                if (isFirst)
                {
                    isFirst = false;
                }
                else
                {
                    sb.Append(" and ");
                }
                sb.Append($"{ForeignKeys[i].FormattedExpression(sqlBuilder)} = {ForeignPrimeKeys[i].FormattedExpression(sqlBuilder)}");
            }
            if (!String.IsNullOrEmpty(FilterExpression))
            {
                if (!isFirst)
                {
                    sb.Append(" and ");
                }
                sb.Append(FilterExpression);
            }
            return sb.ToString();

        }
    }

    /// <summary>
    /// 当前对象关联查询视图定义。
    /// </summary>
    public class TableView : SqlTable
    {
        /// <summary>
        /// 初始化该视图定义。
        /// </summary>
        /// <param name="table">基表（主表）定义。</param>
        /// <param name="joinedTables">相关联的外部表集合。</param>
        /// <param name="columns">视图中包含的所有列信息。</param>
        public TableView(TableDefinition table, ICollection<JoinedTable> joinedTables, ICollection<SqlColumn> columns)
            : base(columns)
        {
            _table = table;
            _tables = new List<JoinedTable>(joinedTables);
        }

        private readonly TableDefinition _table;
        private readonly List<JoinedTable> _tables;
        private ReadOnlyCollection<JoinedTable> _joinedTables;

        /// <summary>
        /// 获取格式化后的联合表集合（已处理依赖排序）。
        /// </summary>
        public ReadOnlyCollection<JoinedTable> JoinedTables
        {
            get
            {
                if (_joinedTables == null)
                {
                    _tables.Sort(delegate (JoinedTable t1, JoinedTable t2)
                    {
                        if (CheckDependOn(t1, t2)) return 1;
                        else if (CheckDependOn(t2, t1)) return -1;
                        else return 0;
                    });
                    _joinedTables = _tables.AsReadOnly();
                }
                return _joinedTables;
            }
        }

        /// <summary>
        /// 检查表加载依赖关系。
        /// </summary>
        private bool CheckDependOn(JoinedTable tableToCheck, JoinedTable baseTable)
        {
            foreach (ColumnRef column in tableToCheck.ForeignKeys)
            {
                ColumnRef columnRef = column;
                while (columnRef.Column is ForeignColumn foreignColumn)
                {
                    columnRef = foreignColumn.TargetColumn;
                }
                if (columnRef.Table != null && String.Equals(columnRef.Table.Name, baseTable.Name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 获取格式化后的 FROM 部分 SQL 片段。
        /// </summary>
        public override string FormattedExpression(ISqlBuilder sqlBuilder)
        {
            StringBuilder sb = new StringBuilder(_table.FormattedName(sqlBuilder) + " " + FormattedName(sqlBuilder));
            foreach (JoinedTable joinedTable in JoinedTables)
            {
                sb.Append(joinedTable.FormattedExpression(sqlBuilder));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 获取对应的表定义信息。
        /// </summary>
        public override TableDefinition Definition
        {
            get { return _table; }
        }
    }

    /// <summary>
    /// 联合查询的连接方式。
    /// </summary>
    public enum TableJoinType
    {
        /// <summary>
        /// 内连接
        /// </summary>
        Inner,
        /// <summary>
        /// 左连接
        /// </summary>
        Left,
        /// <summary>
        /// 右连接
        /// </summary>
        Right,
        /// <summary>
        /// 全外连接
        /// </summary>
        Outer,
        /// <summary>
        /// 交叉连接
        /// </summary>
        Cross
    }
}
