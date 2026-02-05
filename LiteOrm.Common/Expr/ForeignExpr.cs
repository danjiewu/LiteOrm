using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示外键关联查询表达式。
    /// 该表达式通常用于构建基于 EXISTS 子查询的关联表过滤条件。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class ForeignExpr : LogicExpr
    {
        /// <summary>
        /// 获取或设置针对关联表的内部过滤表达式。
        /// </summary>
        public LogicExpr InnerExpr { get; set; }

        /// <summary>
        /// 获取或设置当前实体中关联的外部实体别名。
        /// </summary>
        public new string Foreign { get; set; }

        /// <summary>
        /// 初始化 <see cref="ForeignExpr"/> 类的新实例。
        /// </summary>
        public ForeignExpr() { }

        /// <summary>
        /// 使用指定的外部实体别名和内部表达式初始化 <see cref="ForeignExpr"/> 类的新实例。
        /// </summary>
        /// <param name="foreign">外部实体别名。</param>
        /// <param name="expr">内部过滤表达式。</param>
        public ForeignExpr(string foreign, LogicExpr expr)
        {
            Foreign = foreign;
            InnerExpr = expr;
        }

        /// <summary>
        /// 比较两个 ForeignExpr 是否相等。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is ForeignExpr f && f.Foreign == Foreign && Equals(f.InnerExpr, InnerExpr);
        }

        /// <summary>
        /// 生成哈希值。
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), Foreign?.GetHashCode() ?? 0, InnerExpr?.GetHashCode() ?? 0);
        }
        /// <summary>
        /// 返回表达式的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            return $"Foreign {Foreign}{{ {InnerExpr} }}";
        }
    }
}
