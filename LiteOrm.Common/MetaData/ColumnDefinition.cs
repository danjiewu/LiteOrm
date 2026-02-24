using System.Data;
using System.Reflection;

namespace LiteOrm.Common
{
    /// <summary>
    /// 数据库列定义信息。
    /// 包含列的结构信息，如是否为主键、是否自增、数据类型等。
    /// </summary>
    public class ColumnDefinition : SqlColumn
    {
        /// <summary>
        /// 初始化 <see cref="ColumnDefinition"/> 类的新实例。
        /// </summary>
        /// <param name="property">实体对应的属性信息。</param>
        internal ColumnDefinition(PropertyInfo property)
            : base(property)
        {

        }

        /// <summary>
        /// 获取或设置一个值，指示该列是否为主键。
        /// </summary>
        public bool IsPrimaryKey { get; internal set; }

        /// <summary>
        /// 获取或设置一个值，指示该列是否为自增标识列。
        /// </summary>
        public bool IsIdentity { get; internal set; }

        /// <summary>
        /// 获取或设置一个值，指示该列是否为时间戳列。
        /// </summary>
        public bool IsTimestamp { get; set; }

        /// <summary>
        /// 获取或设置标识列的表达式（如序列名称）。
        /// </summary>
        public string IdentityExpression { get; internal set; }

        /// <summary>
        /// 获取或设置一个值，指示该列是否应创建索引。
        /// </summary>
        public bool IsIndex { get; internal set; }

        /// <summary>
        /// 获取或设置一个值，指示该列是否具有唯一约束。
        /// </summary>
        public bool IsUnique { get; internal set; }

        /// <summary>
        /// 获取或设置数据库列的长度。
        /// </summary>
        public int Length { get; internal set; }

        /// <summary>
        /// 获取或设置数据库列的数据类型。
        /// </summary>
        public DbType DbType { get; internal set; }

        /// <summary>
        /// 获取或设置一个值，指示该列是否允许为空。
        /// </summary>
        public bool AllowNull { get; internal set; }

        /// <summary>
        /// 获取或设置列映射模式。
        /// </summary>
        public ColumnMode Mode { get; internal set; }

        /// <summary>
        /// 获取当前列的定义信息。
        /// </summary>
        public override ColumnDefinition Definition => this;
    }
}
