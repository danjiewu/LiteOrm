using System.Reflection;

namespace LiteOrm.Common
{
    /// <summary>
    /// ¹ØÁªÍâ±íµÄÁÐÐÅÏ¢
    /// </summary>
    public class ForeignColumn : SqlColumn
    {
        internal ForeignColumn(PropertyInfo property) : base(property) { }

        /// <summary>
        /// Ö¸ÏòµÄÁÐ
        /// </summary>
        public ColumnRef TargetColumn { get; internal set; }



        /// <summary>
        /// Ãû³Æ
        /// </summary>
        public override string Name
        {
            get
            {
                return TargetColumn == null ? null : TargetColumn.Name;
            }
            protected internal set
            {
            }
        }

        /// <summary>
        /// Ä¿±êÁÐµÄ¶¨Òå
        /// </summary>
        public override ColumnDefinition Definition => TargetColumn.Column.Definition;
    }
}
