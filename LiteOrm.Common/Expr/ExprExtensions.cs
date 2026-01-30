using System.Collections;

namespace LiteOrm.Common
{
    /// <summary>
    /// Expr 类的扩展方法，提供流式调用的表达式组合功能。
    /// </summary>
    public static class ExprExtensions
    {
        /// <summary>
        /// 使用 AND 逻辑连接当前表达式与另一个表达式。
        /// </summary>
        /// <param name="left">当前查询表达式。</param>
        /// <param name="right">要连接的查询表达式。</param>
        /// <returns>合并后的表达式集合（AND类型）。</returns>
        public static LogicExprSet And(this LogicExpr left, LogicExpr right) => new LogicExprSet(LogicJoinType.And, left, right);

        /// <summary>
        /// 使用 OR 逻辑连接当前表达式与另一个表达式。
        /// </summary>
        /// <param name="left">当前查询表达式。</param>
        /// <param name="right">要连接的查询表达式。</param>
        /// <returns>合并后的表达式集合（OR类型）。</returns>
        public static LogicExprSet Or(this LogicExpr left, LogicExpr right) => new LogicExprSet(LogicJoinType.Or, left, right);

        /// <summary>
        /// 使用 CONCAT (字符串拼接) 逻辑连接左右表达式。
        /// </summary>
        /// <param name="left">左端表达式。</param>
        /// <param name="right">右端表达式。</param>
        /// <returns>合并后的表达式集合（CONCAT类型）。</returns>
        public static ValueExprSet Concat(this ValueTypeExpr left, ValueTypeExpr right) => new ValueExprSet(ValueJoinType.Concat, left, right);

        /// <summary>
        /// 执行逻辑取反操作，例如：NOT (condition)。
        /// </summary>
        /// <param name="expr">要取反的表达式。</param>
        /// <returns>取反后的 LogicUnaryExpr。</returns>
        public static NotExpr Not(this LogicExpr expr) => new NotExpr(expr);

        /// <summary>
        /// 创建相等比较表达式。
        /// </summary>
        public static LogicBinaryExpr Equal(this ValueTypeExpr left, ValueTypeExpr right) => new LogicBinaryExpr(left, LogicBinaryOperator.Equal, right);

        /// <summary>
        /// 创建不等比较表达式。
        /// </summary>
        public static LogicBinaryExpr NotEqual(this ValueTypeExpr left, ValueTypeExpr right) => new LogicBinaryExpr(left, LogicBinaryOperator.NotEqual, right);

        /// <summary>
        /// 创建 IN 集合包含表达式。
        /// </summary>
        public static LogicBinaryExpr In(this ValueTypeExpr left, IEnumerable items) => new LogicBinaryExpr(left, LogicBinaryOperator.In, new ValueExpr(items));

        /// <summary>
        /// 创建 IN 集合包含表达式。
        /// </summary>
        public static LogicBinaryExpr In(this ValueTypeExpr left, params object[] items) => new LogicBinaryExpr(left, LogicBinaryOperator.In, new ValueExpr(items));

        /// <summary>
        /// 创建 NOT IN 集合不包含表达式。
        /// </summary>
        public static LogicBinaryExpr NotIn(this ValueTypeExpr left, IEnumerable items) => new LogicBinaryExpr(left, LogicBinaryOperator.NotIn, new ValueExpr(items));

        /// <summary>
        /// 创建 NOT IN 集合不包含表达式。
        /// </summary>
        public static LogicBinaryExpr NotIn(this ValueTypeExpr left, params object[] items) => new LogicBinaryExpr(left, LogicBinaryOperator.NotIn, new ValueExpr(items));

        /// <summary>
        /// 创建范围查询表达式 (BETWEEN)。
        /// </summary>
        public static LogicExpr Between(this ValueTypeExpr left, object low, object high)
        {
            return (left >= new ValueExpr(low)) & (left <= new ValueExpr(high));
        }

        /// <summary>
        /// 创建模糊匹配表达式 (LIKE)。
        /// </summary>
        public static LogicBinaryExpr Like(this ValueTypeExpr left, string pattern) => new LogicBinaryExpr(left, LogicBinaryOperator.Like, new ValueExpr(pattern));

        /// <summary>
        /// 创建包含字符串表达式 (Contains)。
        /// </summary>
        public static LogicBinaryExpr Contains(this ValueTypeExpr left, string text) => new LogicBinaryExpr(left, LogicBinaryOperator.Contains, new ValueExpr(text));

        /// <summary>
        /// 创建以指定字符串开通的表达式 (StartsWith)。
        /// </summary>
        public static LogicBinaryExpr StartsWith(this ValueTypeExpr left, string text) => new LogicBinaryExpr(left, LogicBinaryOperator.StartsWith, new ValueExpr(text));

        /// <summary>
        /// 创建以指定字符串结尾的表达式 (EndsWith)。
        /// </summary>
        public static LogicBinaryExpr EndsWith(this ValueTypeExpr left, string text) => new LogicBinaryExpr(left, LogicBinaryOperator.EndsWith, new ValueExpr(text));
    }
}
