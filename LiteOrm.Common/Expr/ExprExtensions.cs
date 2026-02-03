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
        /// 使用 AND 逻辑连接当前表达式与另一个表达式。
        /// </summary>
        /// <param name="left">当前查询表达式。</param>
        /// <param name="right">要连接的查询表达式。</param>
        /// <returns>合并后的表达式集合（AND类型）。</returns>
        public static LogicSet And(this LogicExpr left, LogicExpr right) => new LogicSet(LogicJoinType.And, left, right);

        /// <summary>
        /// 使用 OR 逻辑连接当前表达式与另一个表达式。
        /// </summary>
        /// <param name="left">当前查询表达式。</param>
        /// <param name="right">要连接的查询表达式。</param>
        /// <returns>合并后的表达式集合（OR类型）。</returns>
        public static LogicSet Or(this LogicExpr left, LogicExpr right) => new LogicSet(LogicJoinType.Or, left, right);

        /// <summary>
        /// 使用 CONCAT (字符串拼接) 逻辑连接左右表达式。
        /// </summary>
        /// <param name="left">左端表达式。</param>
        /// <param name="right">右端表达式。</param>
        /// <returns>合并后的表达式集合（CONCAT类型）。</returns>
        public static ValueSet Concat(this ValueTypeExpr left, ValueTypeExpr right) => new ValueSet(ValueJoinType.Concat, left, right);

        /// <summary>
        /// 执行逻辑取反操作，例如：NOT (condition)。
        /// </summary>
        /// <param name="expr">要取反的表达式。</param>
        /// <returns>取反后的 LogicUnaryExpr。</returns>
        public static NotExpr Not(this LogicExpr expr) => new NotExpr(expr);

        /// <summary>
        /// 创建相等比较表达式。
        /// </summary>
        public static LogicBinaryExpr Equal(this ValueTypeExpr left, ValueTypeExpr right) => new LogicBinaryExpr(left, LogicOperator.Equal, right);

        /// <summary>
        /// 创建不等比较表达式。
        /// </summary>
        public static LogicBinaryExpr NotEqual(this ValueTypeExpr left, ValueTypeExpr right) => new LogicBinaryExpr(left, LogicOperator.NotEqual, right);

        /// <summary>
        /// 创建 IN 集合包含表达式。
        /// </summary>
        public static LogicBinaryExpr In(this ValueTypeExpr left, IEnumerable items) => new LogicBinaryExpr(left, LogicOperator.In, new ValueExpr(items));

        /// <summary>
        /// 创建 IN 集合包含表达式。
        /// </summary>
        public static LogicBinaryExpr In(this ValueTypeExpr left, params object[] items) => new LogicBinaryExpr(left, LogicOperator.In, new ValueExpr(items));

        /// <summary>
        /// 创建 NOT IN 集合不包含表达式。
        /// </summary>
        public static LogicBinaryExpr NotIn(this ValueTypeExpr left, IEnumerable items) => new LogicBinaryExpr(left, LogicOperator.NotIn, new ValueExpr(items));

        /// <summary>
        /// 创建 NOT IN 集合不包含表达式。
        /// </summary>
        public static LogicBinaryExpr NotIn(this ValueTypeExpr left, params object[] items) => new LogicBinaryExpr(left, LogicOperator.NotIn, new ValueExpr(items));

        /// <summary>
        /// 创建范围查询表达式 (BETWEEN)。
        /// </summary>
        public static LogicExpr Between(this ValueTypeExpr left, object low, object high)
        {
            return (left >= (ValueTypeExpr)new ValueExpr(low)) & (left <= (ValueTypeExpr)new ValueExpr(high));
        }

        /// <summary>
        /// 创建模糊匹配表达式 (LIKE)。
        /// </summary>
        public static LogicBinaryExpr Like(this ValueTypeExpr left, string pattern) => new LogicBinaryExpr(left, LogicOperator.Like, new ValueExpr(pattern));

        /// <summary>
        /// 创建包含字符串表达式 (Contains)。
        /// </summary>
        public static LogicBinaryExpr Contains(this ValueTypeExpr left, string text) => new LogicBinaryExpr(left, LogicOperator.Contains, new ValueExpr(text));

        /// <summary>
        /// 创建以指定字符串开通的表达式 (StartsWith)。
        /// </summary>
        public static LogicBinaryExpr StartsWith(this ValueTypeExpr left, string text) => new LogicBinaryExpr(left, LogicOperator.StartsWith, new ValueExpr(text));

        /// <summary>
        /// 创建以指定字符串结尾的表达式 (EndsWith)。
        /// </summary>
        public static LogicBinaryExpr EndsWith(this ValueTypeExpr left, string text) => new LogicBinaryExpr(left, LogicOperator.EndsWith, new ValueExpr(text));

        /// <summary>
        /// 创建 IS NULL 表达式。
        /// </summary>
        public static LogicBinaryExpr IsNull(this ValueTypeExpr left) => new LogicBinaryExpr(left, LogicOperator.Equal, Expr.Null);

        /// <summary>
        /// 创建 IS NOT NULL 表达式。
        /// </summary>
        public static LogicBinaryExpr IsNotNull(this ValueTypeExpr left) => new LogicBinaryExpr(left, LogicOperator.NotEqual, Expr.Null);

        /// <summary>
        /// 创建 COUNT 聚合。
        /// </summary>
        public static AggregateFunctionExpr Count(this ValueTypeExpr expr, bool isDistinct = false) => new AggregateFunctionExpr("COUNT", expr, isDistinct);

        /// <summary>
        /// 创建 SUM 聚合。
        /// </summary>
        public static AggregateFunctionExpr Sum(this ValueTypeExpr expr, bool isDistinct = false) => new AggregateFunctionExpr("SUM", expr, isDistinct);

        /// <summary>
        /// 创建 AVG 聚合。
        /// </summary>
        public static AggregateFunctionExpr Avg(this ValueTypeExpr expr, bool isDistinct = false) => new AggregateFunctionExpr("AVG", expr, isDistinct);

        /// <summary>
        /// 创建 MAX 聚合。
        /// </summary>
        public static AggregateFunctionExpr Max(this ValueTypeExpr expr) => new AggregateFunctionExpr("MAX", expr);

        /// <summary>
        /// 创建 MIN 聚合。
        /// </summary>
        public static AggregateFunctionExpr Min(this ValueTypeExpr expr) => new AggregateFunctionExpr("MIN", expr);

        /// <summary>
        /// 创建升序排序项。
        /// </summary>
        public static (ValueTypeExpr, bool) Asc(this ValueTypeExpr expr) => (expr, true);

        /// <summary>
        /// 创建降序排序项。
        /// </summary>
        public static (ValueTypeExpr, bool) Desc(this ValueTypeExpr expr) => (expr, false);

        /// <summary>
        /// 添加 WHERE 子句。
        /// </summary>
        public static WhereExpr Where(this SourceExpr from, LogicExpr where) => new WhereExpr { Source = from, Where = where };

        /// <summary>
        /// 添加 ORDER BY 子句。
        /// </summary>
        public static OrderByExpr OrderBy(this OrderBySourceExpr from, params (ValueTypeExpr, bool)[] orders) => new OrderByExpr { Source = from, OrderBys = new List<(ValueTypeExpr, bool)>(orders) };

        /// <summary>
        /// 添加 GROUP BY 子句。
        /// </summary>
        public static GroupByExpr GroupBy(this GroupBySourceExpr from, params ValueTypeExpr[] groups) => new GroupByExpr { Source = from, GroupBys = new List<ValueTypeExpr>(groups) };

        /// <summary>
        /// 添加分页(SKIP/TAKE)子句。
        /// </summary>
        public static SectionExpr Section(this SectionSourceExpr from, int skip, int take) => new SectionExpr(skip, take) { Source = from };

        /// <summary>
        /// 添加 SELECT 子句。
        /// </summary>
        public static SelectExpr Select(this SelectSourceExpr from, params ValueTypeExpr[] selects) => new SelectExpr { Source = from, Selects = new List<ValueTypeExpr>(selects) };
    }
}
