namespace LiteOrm.Common
{
    /// <summary>
    /// 列的引用
    /// </summary>
    public class ColumnRef : SqlObject
    {
        /// <summary>
        /// 创建列的引用
        /// </summary>
        /// <param name="column">列信息</param>
        public ColumnRef(SqlColumn column)
        {
            Name = column.Name;
            _column = column;
        }

        /// <summary>
        /// 创建指定表的列引用
        /// </summary>
        /// <param name="table">表</param>
        /// <param name="column">列引用</param>
        public ColumnRef(TableRef table, SqlColumn column)
        {
            Name = column.Name;
            _column = column;
            _table = table;
        }

        private TableRef _table;
        /// <summary>
        /// 列所在的表
        /// </summary>
        public TableRef Table
        {
            get { return _table; }
            internal set { _table = value; }
        }

        private SqlColumn _column;
        /// <summary>
        /// 列信息
        /// </summary>
        public SqlColumn Column
        {
            get { return _column; }
        }
    }
}
