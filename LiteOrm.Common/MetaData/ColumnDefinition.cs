using System.Data;
using System.Reflection;

namespace LiteOrm.Common
{
    /// <summary>
    /// 数据库列信息
    /// </summary>
    public class ColumnDefinition : SqlColumn
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="property">列对应的实体属性</param>
        internal ColumnDefinition(PropertyInfo property)
            : base(property)
        {

        }

        /// <summary>
        /// 是否是主键
        /// </summary>
        public bool IsPrimaryKey { get; internal set; }

        /// <summary>
        /// 是否是自增长标识
        /// </summary>
        public bool IsIdentity { get; internal set; }

        /// <summary>
        /// 是否是时间戳
        /// </summary>
        public bool IsTimestamp { get; set; }

        /// <summary>
        /// 标识表达式
        /// </summary>
        public string IdentityExpression { get; internal set; }

        /// <summary>
        /// 是否是索引
        /// </summary>
        public bool IsIndex { get; internal set; }

        /// <summary>
        /// 是否唯一
        /// </summary>
        public bool IsUnique { get; internal set; }

        /// <summary>
        /// 长度
        /// </summary>
        public int Length { get; internal set; }

        /// <summary>
        /// 数据库类型
        /// </summary>
        public DbType DbType { get; internal set; }

        /// <summary>
        /// 是否允许为空
        /// </summary>
        public bool AllowNull { get; internal set; }

        /// <summary>
        /// 列操作模式
        /// </summary>
        public ColumnMode Mode { get; internal set; }

        /// <summary>
        /// 列的定义信息，即自身
        /// </summary>
        public override ColumnDefinition Definition => this;
    }
}
