using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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
        /// <param name="columns">视图中包含的所有列信息。</param>
        /// <param name="joinedTables">相关联的外部表集合。</param>
        public TableView(TableDefinition table, ICollection<SqlColumn> columns, ICollection<JoinedTable> joinedTables)
            : base(columns)
        {
            _table = table;
            _tables = new List<JoinedTable>(joinedTables);
        }

        private readonly TableDefinition _table;
        private readonly List<JoinedTable> _tables;

        /// <summary>
        /// 获取格式化后的联合表集合（已处理依赖排序）。
        /// </summary>
        public ReadOnlyCollection<JoinedTable> JoinedTables
        {
            get
            {
                if (field == null)
                {
                    field = SortTables().AsReadOnly();
                }
                return field;
            }
        }

        private List<JoinedTable> SortTables()
        {
            Dictionary<JoinedTable, HashSet<JoinedTable>> dependencies = new Dictionary<JoinedTable, HashSet<JoinedTable>>();
            Dictionary<JoinedTable, int> indegrees = new Dictionary<JoinedTable, int>();
            Queue<JoinedTable> queue = new Queue<JoinedTable>();
            List<JoinedTable> sortedTables = new List<JoinedTable>(_tables.Count);

            foreach (JoinedTable table in _tables)
            {
                HashSet<JoinedTable> dependencySet = new HashSet<JoinedTable>();
                foreach (JoinedTable otherTable in _tables)
                {
                    if (!ReferenceEquals(table, otherTable) && CheckDependOn(table, otherTable))
                        dependencySet.Add(otherTable);
                }
                dependencies[table] = dependencySet;
                indegrees[table] = dependencySet.Count;
            }

            foreach (JoinedTable table in _tables)
            {
                if (indegrees[table] == 0)
                    queue.Enqueue(table);
            }

            while (queue.Count > 0)
            {
                JoinedTable table = queue.Dequeue();
                sortedTables.Add(table);
                foreach (JoinedTable otherTable in _tables)
                {
                    if (dependencies[otherTable].Remove(table))
                    {
                        indegrees[otherTable]--;
                        if (indegrees[otherTable] == 0)
                            queue.Enqueue(otherTable);
                    }
                }
            }

            if (sortedTables.Count != _tables.Count)
            {
                string circularTables = string.Join(", ", indegrees.Where(item => item.Value > 0).Select(item => item.Key.Name));
                throw new InvalidOperationException($"Detected circular joined table dependency: {circularTables}");
            }

            return sortedTables;
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
                if (ReferenceEquals(columnRef.Table, baseTable))
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
        Full,
        /// <summary>
        /// 交叉连接
        /// </summary>
        Cross
    }
}
