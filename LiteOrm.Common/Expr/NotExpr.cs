using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 逻辑非表达式，用于表示 NOT 操作，如 NOT (a = 1)
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class NotExpr : LogicExpr
    {
        /// <summary>
        /// 初始化默认的逻辑非表达式
        /// </summary>
        public NotExpr() { }

        /// <summary>
        /// 使用指定的运算对象初始化逻辑非表达式
        /// </summary>
        /// <param name="operand">要取非的逻辑表达式</param>
        public NotExpr(LogicExpr operand)
        {
            Operand = operand;
        }

        /// <summary>
        /// 获取或设置运算对象
        /// </summary>
        public LogicExpr Operand { get; set; }

        /// <summary>
        /// 返回表达式的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString() => $"NOT {Operand}";

        /// <summary>
        /// 判断当前对象是否与指定对象相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj) => obj is NotExpr p && Equals(p.Operand, Operand);

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode() => OrderedHashCodes(GetType().GetHashCode(), (Operand?.GetHashCode() ?? 0));
    }
}
