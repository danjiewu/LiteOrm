using System;
using System.Collections;
using System.Collections.Generic;

namespace LiteOrm.Common
{
    /// <summary>
    /// Expr 类的扩展方法，提供流式调用的表达式组合功能。
    /// </summary>
    public static class ExprExtensions
    {
        /// <summary>
        /// 使用 AND 逻辑组合当前表达式与另一个表达式。
        /// </summary>
        /// <param name="left">当前查询表达式。</param>
        /// <param name="right">待组合的查询表达式。</param>
        /// <returns>组合后的表达式集合（AND）。</returns>
        public static ExprSet And(this Expr left, Expr right) => Join(left, right, ExprJoinType.And);

        /// <summary>
        /// 使用 OR 逻辑组合当前表达式与另一个表达式。
        /// </summary>
        /// <param name="left">当前查询表达式。</param>
        /// <param name="right">待组合的查询表达式。</param>
        /// <returns>组合后的表达式集合（OR）。</returns>
        public static ExprSet Or(this Expr left, Expr right) => Join(left, right, ExprJoinType.Or);

        /// <summary>
        /// 使用 CONCAT (字符串拼接) 逻辑组合两个表达式。
        /// </summary>
        /// <param name="left">左端表达式。</param>
        /// <param name="right">右端表达式。</param>
        /// <returns>组合后的表达式集合（CONCAT）。</returns>
        public static ExprSet Concat(this Expr left, Expr right) => Join(left, right, ExprJoinType.Concat);

        /// <summary>
        /// 使用指定的连接类型组合两个表达式。
        /// </summary>
        /// <param name="left">左端表达式。</param>
        /// <param name="right">右端表达式。</param>
        /// <param name="joinType">连接方式（And/Or/List/Concat）。</param>
        /// <returns>新的表达式集合。</returns>
        public static ExprSet Join(this Expr left, Expr right, ExprJoinType joinType = ExprJoinType.List) => new ExprSet(joinType, left, right);

        /// <summary>
        /// 执行逻辑取反操作（如：NOT (condition)）。
        /// </summary>
        /// <param name="expr">要取反的表达式。</param>
        /// <returns>取反后的 UnaryExpr。</returns>
        public static UnaryExpr Not(this Expr expr) => new UnaryExpr(UnaryOperator.Not, expr);

        /// <summary>
        /// 创建相等比较表达式。
        /// </summary>
        public static BinaryExpr Equal(this Expr left, Expr right) => new BinaryExpr(left, BinaryOperator.Equal, right);

        /// <summary>
        /// 创建不相等比较表达式。
        /// </summary>
        public static BinaryExpr NotEqual(this Expr left, Expr right) => new BinaryExpr(left, BinaryOperator.NotEqual, right);

        /// <summary>
        /// 创建 IN 集合包含表达式。
        /// </summary>
        public static BinaryExpr In(this Expr left, IEnumerable items) => new BinaryExpr(left, BinaryOperator.In, new ValueExpr(items));

        /// <summary>
        /// 创建 IN 集合包含表达式。
        /// </summary>
        public static BinaryExpr In(this Expr left, params object[] items) => new BinaryExpr(left, BinaryOperator.In, new ValueExpr(items));

        /// <summary>
        /// 创建 NOT IN 集合不包含表达式。
        /// </summary>
        public static BinaryExpr NotIn(this Expr left, IEnumerable items) => new BinaryExpr(left, BinaryOperator.NotIn, new ValueExpr(items));

        /// <summary>
        /// 创建 NOT IN 集合不包含表达式。
        /// </summary>
        public static BinaryExpr NotIn(this Expr left, params object[] items) => new BinaryExpr(left, BinaryOperator.NotIn, new ValueExpr(items));

        /// <summary>
        /// 创建范围查询表达式 (BETWEEN)。
        /// </summary>
        public static Expr Between(this Expr left, object low, object high)
        {
            return (left >= new ValueExpr(low)) & (left <= new ValueExpr(high));
        }

        /// <summary>
        /// 创建模糊匹配表达式 (LIKE)。
        /// </summary>
        public static BinaryExpr Like(this Expr left, string pattern) => new BinaryExpr(left, BinaryOperator.Like, pattern);

        /// <summary>
        /// 创建包含字符串表达式 (Contains)。
        /// </summary>
        public static BinaryExpr Contains(this Expr left, string text) => new BinaryExpr(left, BinaryOperator.Contains, text);

        /// <summary>
        /// 创建以指定字符串开头的表达式 (StartsWith)。
        /// </summary>
        public static BinaryExpr StartsWith(this Expr left, string text) => new BinaryExpr(left, BinaryOperator.StartsWith, text);

        /// <summary>
        /// 创建以指定字符串结尾的表达式 (EndsWith)。
        /// </summary>
        public static BinaryExpr EndsWith(this Expr left, string text) => new BinaryExpr(left, BinaryOperator.EndsWith, text);
    }
}
