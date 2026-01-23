namespace LiteOrm.Common
{
    /// <summary>
    /// 为二元操作符提供的便捷扩展工具。
    /// </summary>
    public static class BinaryOperatorExt
    {
        /// <summary>
        /// 检查指定的操作符是否带有 NOT 标志。
        /// </summary>
        public static bool IsNot(this BinaryOperator oper)
        {
            return (oper & BinaryOperator.Not) == BinaryOperator.Not;
        }

        /// <summary>
        /// 提取剥离了 NOT 标志的正向操作符。
        /// </summary>
        public static BinaryOperator Positive(this BinaryOperator oper)
        {
            return oper & ~BinaryOperator.Not;
        }

        /// <summary>
        /// 获取当前操作符的对立版本（即取反或撤销取反）。
        /// </summary>
        public static BinaryOperator Opposite(this BinaryOperator oper)
        {
            return oper ^ BinaryOperator.Not;
        }
    }
}
