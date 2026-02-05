using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 一元运算符表达式，用于表示一元运算操作，如负数（-a）、按位取反（~a）等
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class UnaryExpr : ValueTypeExpr
    {
        /// <summary>
        /// 初始化默认的一元表达式
        /// </summary>
        public UnaryExpr() { }

        /// <summary>
        /// 使用指定的一元运算符和操作数初始化一元表达式
        /// </summary>
        /// <param name="oper">一元运算符</param>
        /// <param name="operand">操作数表达式</param>
        public UnaryExpr(UnaryOperator oper, ValueTypeExpr operand)
        {
            Operator = oper;
            Operand = operand;
        }

        /// <summary>
        /// 获取或设置一元运算符
        /// </summary>
        public UnaryOperator Operator { get; set; }

        /// <summary>
        /// 获取或设置操作数表达式
        /// </summary>
        public ValueTypeExpr Operand { get; set; }

        /// <summary>
        /// 获取一个值，指示此表达式是否为值类型
        /// </summary>
        public override bool IsValue => true;

        /// <summary>
        /// 返回表达式的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString() => Operator == UnaryOperator.Nagive ? $"-{Operand}" : $"~{Operand}";

        /// <summary>
        /// 判断当前对象是否与指定对象相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj) => obj is UnaryExpr p && p.Operator == Operator && Equals(p.Operand, Operand);

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode() => OrderedHashCodes(GetType().GetHashCode(), (int)Operator, (Operand?.GetHashCode() ?? 0));
    }
}
