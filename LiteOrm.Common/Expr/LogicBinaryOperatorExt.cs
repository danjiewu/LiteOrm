namespace LiteOrm.Common
{
    /// <summary>
    /// 为逻辑二元操作符提供的便捷扩展工具。
    /// </summary>
    public static class LogicBinaryOperatorExt
    {
        /// <summary>
        /// 检查指定的操作符是否含有 NOT 标志。
        /// </summary>
        public static bool IsNot(this LogicOperator oper)
        {
            return (oper & LogicOperator.Not) == LogicOperator.Not;
        }

        /// <summary>
        /// 获取去掉 NOT 标志后的正向操作符。
        /// </summary>
        public static LogicOperator Positive(this LogicOperator oper)
        {
            return oper & ~LogicOperator.Not;
        }

        /// <summary>
        /// 获取当前操作符的反向版本（取反）。
        /// </summary>
        public static LogicOperator Opposite(this LogicOperator oper)
        {
            return oper ^ LogicOperator.Not;
        }
    }
}
