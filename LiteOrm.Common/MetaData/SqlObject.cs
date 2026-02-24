namespace LiteOrm.Common
{
    /// <summary>
    /// SQL 对象基类。
    /// </summary>
    public abstract class SqlObject
    {
        private string _name;
        /// <summary>
        /// 获取或设置 SQL 对象的名称。
        /// </summary>
        public virtual string Name
        {
            get { return _name; }
            protected internal set
            {
                _name = value;
            }
        }

        /// <summary>
        /// 确定指定的对象是否等于当前对象。
        /// </summary>
        /// <param name="obj">要与当前对象进行比较的对象。</param>
        /// <returns>如果指定的对象等于当前对象，则为 true；否则为 false。</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj == null || obj.GetType() != GetType()) return false;
            return Name == ((SqlObject)obj).Name;
        }

        /// <summary>
        /// 获取对象的哈希代码。
        /// </summary>
        /// <returns>当前对象的哈希代码。</returns>
        public override int GetHashCode()
        {
            return Name?.GetHashCode() ?? 0;
        }

        /// <summary>
        /// 比较两个 SqlObject 是否相等。
        /// </summary>
        public static bool operator ==(SqlObject left, SqlObject right) => Equals(left, right);

        /// <summary>
        /// 比较两个 SqlObject 是否不等。
        /// </summary>
        public static bool operator !=(SqlObject left, SqlObject right) => !Equals(left, right);

        /// <summary>
        /// 获取对象的字符串表示形式。
        /// </summary>
        /// <returns>包含名称的字符串。</returns>
        public override string ToString()
        {
            return Name;
        }
    }
}
