namespace LiteOrm.Common
{
    /// <summary>
    /// ÁÐµÄÒýÓÃ
    /// </summary>
    public class ColumnRef : SqlObject
    {
        /// <summary>
        /// ´´½¨ÁÐµÄÒýÓÃ
        /// </summary>
        /// <param name="column">ÁÐÐÅÏ¢</param>
        public ColumnRef(SqlColumn column)
        {
            Name = column.Name;
            _column = column;
        }

        /// <summary>
        /// ´´½¨Ö¸¶¨±íµÄÁÐÒýÓÃ
        /// </summary>
        /// <param name="table">±í</param>
        /// <param name="column">ÁÐÒýÓÃ</param>
        public ColumnRef(TableRef table, SqlColumn column)
        {
            Name = column.Name;
            _column = column;
            _table = table;
        }

        private TableRef _table;
        /// <summary>
        /// ÁÐËùÔÚµÄ±í
        /// </summary>
        public TableRef Table
        {
            get { return _table; }
            internal set { _table = value; }
        }

        private SqlColumn _column;
        /// <summary>
        /// ÁÐÐÅÏ¢
        /// </summary>
        public SqlColumn Column
        {
            get { return _column; }
        }

        /// <summary>
        /// È·¶¨Ö¸¶¨µÄ¶ÔÏóÊÇ·ñµÈÓÚµ±Ç°¶ÔÏó¡£
        /// </summary>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj == null || obj.GetType() != GetType()) return false;
            ColumnRef other = (ColumnRef)obj;
            return Equals(Table, other.Table) && Equals(Column, other.Column);
        }

        /// <summary>
        /// »ñÈ¡¹þÏ£Âë¡£
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
