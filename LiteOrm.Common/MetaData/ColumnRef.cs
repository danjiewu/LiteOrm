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
        /// 创建指定关联表的列引用
        /// </summary>
        /// <param name="table">列所属的关联表，为 null 表示主表（使用默认别名）</param>
        /// <param name="column">列信息</param>
        public ColumnRef(JoinedTable? table, SqlColumn column)
        {
            Name = column.Name;
            _column = column;
            _table = table;
        }

        private JoinedTable? _table;
        /// <summary>
        /// 列所在的关联表，为 null 表示主表（使用默认别名）
        /// </summary>
        public JoinedTable? Table
        {
            get { return _table; }
            internal set { _table = value; }
        }

        private readonly SqlColumn _column;
        /// <summary>
        /// 列信息
        /// </summary>
        public SqlColumn Column
        {
            get { return _column; }
        }

        /// <summary>
        /// 确定指定的对象是否等于当前对象。
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj == null || obj.GetType() != GetType()) return false;
            ColumnRef other = (ColumnRef)obj;
            return Equals(Table, other.Table) && Equals(Column, other.Column);
        }

        /// <summary>
        /// 获取哈希码。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return ((Table?.GetHashCode() ?? 0) * 31) ^ (Column?.GetHashCode() ?? 0);
            }
        }
    }
}
