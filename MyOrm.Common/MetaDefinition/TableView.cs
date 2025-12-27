using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;

namespace MyOrm.Common
{
    /// <summary>
    /// 关联的外部表
    /// </summary>
    public class JoinedTable : TableRef
    {
        /// <summary>
        /// 创建关联表
        /// </summary>
        /// <param name="foreignTable">外部表的表定义</param>
        public JoinedTable(TableDefinition foreignTable)
            : base(foreignTable)
        {
            this.foreignTable = foreignTable;
            JoinType = TableJoinType.Left;
            List<ColumnRef> keys = new List<ColumnRef>();
            foreach (ColumnDefinition key in foreignTable.Keys)
            {
                keys.Add(new ColumnRef(this, key));
            }
            foreignPrimeKeys = keys.AsReadOnly();
        }

        private ReadOnlyCollection<ColumnRef> foreignPrimeKeys;
        private ReadOnlyCollection<ColumnRef> foreignKeys = new List<ColumnRef>().AsReadOnly();
        private TableDefinition foreignTable;
        /// <summary>
        /// 用来连接的外键
        /// </summary>
        public ReadOnlyCollection<ColumnRef> ForeignKeys
        {
            get { return foreignKeys; }
            internal set
            {
                if (value == null) throw new ArgumentNullException("value");
                if (value.Count != foreignPrimeKeys.Count) throw new ArgumentException("Quantity of foreignKeys not same as foreignPrimeKeys.");
                foreignKeys = value;
            }
        }

        /// <summary>
        /// 表连接类型
        /// </summary>
        public TableJoinType JoinType { get; set; }

        /// <summary>
        /// 关联表的主键
        /// </summary>
        public ReadOnlyCollection<ColumnRef> ForeignPrimeKeys
        {
            get { return foreignPrimeKeys; }
        }

        /// <summary>
        /// 筛选属性
        /// </summary>
        public string FilterExpression { get; set; }
        /// <summary>
        /// 格式化的表达式
        /// </summary>
        public override string FormattedExpression(ISqlBuilder sqlBuilder)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("\n{0} join {1} {2} on ", JoinType, base.FormattedExpression(sqlBuilder), FormattedName(sqlBuilder));
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
                sb.AppendFormat("{0} = {1}", ForeignKeys[i].FormattedExpression(sqlBuilder), ForeignPrimeKeys[i].FormattedExpression(sqlBuilder));
            }
            if (!String.IsNullOrEmpty(FilterExpression))
            {
                if (!isFirst)
                {
                    sb.Append(" and ");
                }
                sb.Append(sqlBuilder.ReplaceSqlName(FilterExpression));
            }
            return sb.ToString();

        }
    }

    /// <summary>
    /// 用于查询的关联表
    /// </summary>
    public class TableView : Table
    {
        /// <summary>
        /// 创建用于查询的关联表
        /// </summary>
        /// <param name="table">主表</param>
        /// <param name="joinedTables">关联的外表</param>
        /// <param name="columns">查询的列集合</param>
        public TableView(TableDefinition table, ICollection<JoinedTable> joinedTables, ICollection<Column> columns)
            : base(columns)
        {
            this.table = table;
            tables = new List<JoinedTable>(joinedTables);
        }

        private TableDefinition table;
        private List<JoinedTable> tables;
        private ReadOnlyCollection<JoinedTable> joinedTables;

        /// <summary>
        /// 关联的外表集合
        /// </summary>
        public ReadOnlyCollection<JoinedTable> JoinedTables
        {
            get
            {
                if (joinedTables == null)
                {
                    tables.Sort(delegate (JoinedTable t1, JoinedTable t2)
                    {
                        if (CheckDependOn(t1, t2)) return 1;
                        else if (CheckDependOn(t2, t1)) return -1;
                        else return 0;
                    });
                    joinedTables = tables.AsReadOnly();
                }
                return joinedTables;
            }
        }

        /// <summary>
        /// 检查表依赖关系，表tableToCheck是否依赖于baseTable
        /// </summary>
        /// <param name="tableToCheck">待检查依赖关系的表</param>
        /// <param name="baseTable">基本表</param>
        /// <returns></returns>
        private bool CheckDependOn(JoinedTable tableToCheck, JoinedTable baseTable)
        {
            foreach (ColumnRef column in tableToCheck.ForeignKeys)
            {
                ColumnRef columnRef = column;
                while (columnRef.Column is ForeignColumn)
                {
                    columnRef = ((ForeignColumn)columnRef.Column).TargetColumn;
                }
                if (columnRef.Table != null && String.Equals(columnRef.Table.Name, baseTable.Name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 格式化的表达式
        /// </summary>
        public override string FormattedExpression(ISqlBuilder sqlBuilder)
        {
            StringBuilder sb = new StringBuilder(table.FormattedName(sqlBuilder) + " " + FormattedName(sqlBuilder));
            foreach (JoinedTable joinedTable in JoinedTables)
            {
                sb.Append(joinedTable.FormattedExpression(sqlBuilder));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 主表的定义
        /// </summary>
        public override TableDefinition Definition
        {
            get { return table; }
        }
    }

    /// <summary>
    /// 表关联类型
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
