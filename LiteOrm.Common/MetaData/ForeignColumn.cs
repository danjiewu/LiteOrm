using System.Reflection;

namespace LiteOrm.Common
{
    /// <summary>
    /// 关联外表的列信息
    /// </summary>
    public class ForeignColumn : SqlColumn
    {
        internal ForeignColumn(PropertyInfo property) : base(property) { }

        /// <summary>
        /// 指向的列
        /// </summary>
        public ColumnRef TargetColumn { get; internal set; }



        /// <summary>
        /// 名称
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
        /// 目标列的定义
        /// </summary>
        public override ColumnDefinition Definition => TargetColumn.Column.Definition;
    }
}
