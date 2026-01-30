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
            internal set
            {
                _name = value;
            }
        }


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
