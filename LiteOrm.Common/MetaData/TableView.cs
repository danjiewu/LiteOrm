using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;

namespace LiteOrm.Common
{
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
