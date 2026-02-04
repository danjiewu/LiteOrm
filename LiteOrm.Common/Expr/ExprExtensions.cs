using System.Collections;
using System.Collections.Generic;

namespace LiteOrm.Common
{
    /// <summary>
    /// Expr 表达式扩展方法类，为 LiteOrm 的表达式系统提供丰富的链式操作和便捷方法。
    /// </summary>
    /// <remarks>
    /// 该类包含以下几类主要功能：
    /// 1. 逻辑表达式组合（And/Or/Not）
    /// 2. 值表达式操作（Concat）
    /// 3. 比较表达式创建（Equal/NotEqual/In/NotIn/Between）
    /// 4. 字符串匹配表达式（Like/Contains/StartsWith/EndsWith）
    /// 5. NULL 值检查表达式（IsNull/IsNotNull）
    /// 6. SQL 语句构建（Where/GroupBy/Having/Select/OrderBy/Section）
    /// 7. 排序操作（Asc/Desc）
    /// 8. 聚合函数（Count/Sum/Avg/Max/Min）
    /// 
    /// 使用示例：
    /// <code>
    /// // 构建复杂查询条件
    /// var condition = Expr.Property("Age") > 18
    ///     .And(Expr.Property("Name").Contains("John"))
    ///     .Or(Expr.Property("Email").EndsWith("@example.com"));
    /// 
    /// // 构建排序和分页
    /// var query = table.Select(Expr.Property("Id"), Expr.Property("Name"))
    ///     .Where(condition)
    ///     .OrderBy(Expr.Property("CreatedDate").Desc())
    ///     .Section(0, 20);
    /// </code>
    /// </remarks>
    public static class ExprExtensions
    {
        /// <summary>
        /// 使用 AND 逻辑操作符将当前逻辑表达式与另一个逻辑表达式连接。
        /// </summary>
        /// <param name="left">当前的查询逻辑表达式。</param>
        /// <param name="right">要添加的查询逻辑表达式。</param>
        /// <returns>合并后的逻辑表达式集合（AND连接）。</returns>
        /// <example>
        /// <code>
        /// var condition = Expr.Property("Age") > 18
        ///     .And(Expr.Property("Name").Contains("John"));
        /// </code>
        /// </example>
        public static LogicSet And(this LogicExpr left, LogicExpr right) => new LogicSet(LogicJoinType.And, left, right);

        /// <summary>
        /// 使用 OR 逻辑操作符将当前逻辑表达式与另一个逻辑表达式连接。
        /// </summary>
        /// <param name="left">当前的查询逻辑表达式。</param>
        /// <param name="right">要添加的查询逻辑表达式。</param>
        /// <returns>合并后的逻辑表达式集合（OR连接）。</returns>
        /// <example>
        /// <code>
        /// var condition = Expr.Property("Age") > 18
        ///     .Or(Expr.Property("IsVIP").Equal(true));
        /// </code>
        /// </example>
        public static LogicSet Or(this LogicExpr left, LogicExpr right) => new LogicSet(LogicJoinType.Or, left, right);

        /// <summary>
        /// 使用 CONCAT（字符串拼接）操作符连接两个值表达式。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <param name="right">右侧值表达式。</param>
        /// <returns>合并后的值表达式集合（CONCAT连接）。</returns>
        /// <example>
        /// <code>
        /// var fullName = Expr.Property("FirstName").Concat(Expr.Property("LastName"));
        /// </code>
        /// </example>
        public static ValueSet Concat(this ValueTypeExpr left, ValueTypeExpr right) => new ValueSet(ValueJoinType.Concat, left, right);

        /// <summary>
        /// 执行逻辑取反操作，例如：NOT (condition)。
        /// </summary>
        /// <param name="expr">要取反的逻辑表达式。</param>
        /// <returns>取反后的 LogicUnaryExpr。</returns>
        /// <example>
        /// <code>
        /// var condition = Expr.Property("IsDeleted").Equal(true).Not();
        /// </code>
        /// </example>
        public static NotExpr Not(this LogicExpr expr) => new NotExpr(expr);

        /// <summary>
        /// 创建等于比较表达式。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <param name="right">右侧值表达式。</param>
        /// <returns>等于比较逻辑表达式。</returns>
        /// <example>
        /// <code>
        /// var condition = Expr.Property("Id").Equal(123);
        /// </code>
        /// </example>
        public static LogicBinaryExpr Equal(this ValueTypeExpr left, ValueTypeExpr right) => new LogicBinaryExpr(left, LogicOperator.Equal, right);

        /// <summary>
        /// 创建不等于比较表达式。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <param name="right">右侧值表达式。</param>
        /// <returns>不等于比较逻辑表达式。</returns>
        /// <example>
        /// <code>
        /// var condition = Expr.Property("Status").NotEqual("Inactive");
        /// </code>
        /// </example>
        public static LogicBinaryExpr NotEqual(this ValueTypeExpr left, ValueTypeExpr right) => new LogicBinaryExpr(left, LogicOperator.NotEqual, right);

        /// <summary>
        /// 创建 IN 集合包含表达式。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <param name="items">包含值的集合。</param>
        /// <returns>IN 集合包含逻辑表达式。</returns>
        /// <example>
        /// <code>
        /// var ids = new List<int> { 1, 2, 3 };
        /// var condition = Expr.Property("Id").In(ids);
        /// </code>
        /// </example>
        public static LogicBinaryExpr In(this ValueTypeExpr left, IEnumerable items) => new LogicBinaryExpr(left, LogicOperator.In, new ValueExpr(items));

        /// <summary>
        /// 创建 IN 集合包含表达式（参数数组版本）。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <param name="items">包含值的参数数组。</param>
        /// <returns>IN 集合包含逻辑表达式。</returns>
        /// <example>
        /// <code>
        /// var condition = Expr.Property("Id").In(1, 2, 3, 4, 5);
        /// </code>
        /// </example>
        public static LogicBinaryExpr In(this ValueTypeExpr left, params object[] items) => new LogicBinaryExpr(left, LogicOperator.In, new ValueExpr(items));

        /// <summary>
        /// 创建 NOT IN 集合不包含表达式。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <param name="items">不包含值的集合。</param>
        /// <returns>NOT IN 集合不包含逻辑表达式。</returns>
        /// <example>
        /// <code>
        /// var excludedStatuses = new List<string> { "Deleted", "Inactive" };
        /// var condition = Expr.Property("Status").NotIn(excludedStatuses);
        /// </code>
        /// </example>
        public static LogicBinaryExpr NotIn(this ValueTypeExpr left, IEnumerable items) => new LogicBinaryExpr(left, LogicOperator.NotIn, new ValueExpr(items));

        /// <summary>
        /// 创建 NOT IN 集合不包含表达式（参数数组版本）。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <param name="items">不包含值的参数数组。</param>
        /// <returns>NOT IN 集合不包含逻辑表达式。</returns>
        /// <example>
        /// <code>
        /// var condition = Expr.Property("Status").NotIn("Deleted", "Inactive");
        /// </code>
        /// </example>
        public static LogicBinaryExpr NotIn(this ValueTypeExpr left, params object[] items) => new LogicBinaryExpr(left, LogicOperator.NotIn, new ValueExpr(items));

        /// <summary>
        /// 创建范围查询表达式 (BETWEEN)。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <param name="low">范围的下限值。</param>
        /// <param name="high">范围的上限值。</param>
        /// <returns>范围查询逻辑表达式。</returns>
        /// <example>
        /// <code>
        /// var condition = Expr.Property("Age").Between(18, 65);
        /// </code>
        /// </example>
        public static LogicExpr Between(this ValueTypeExpr left, object low, object high)
        {
            return (left >= (ValueTypeExpr)new ValueExpr(low)) & (left <= (ValueTypeExpr)new ValueExpr(high));
        }

        /// <summary>
        /// 创建模式匹配表达式 (LIKE)。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <param name="pattern">LIKE 模式字符串。</param>
        /// <returns>模式匹配逻辑表达式。</returns>
        /// <example>
        /// <code>
        /// var condition = Expr.Property("Name").Like("J%");
        /// </code>
        /// </example>
        public static LogicBinaryExpr Like(this ValueTypeExpr left, string pattern) => new LogicBinaryExpr(left, LogicOperator.Like, new ValueExpr(pattern));

        /// <summary>
        /// 创建包含字符串表达式 (Contains)。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <param name="text">要包含的文本。</param>
        /// <returns>包含字符串逻辑表达式。</returns>
        /// <example>
        /// <code>
        /// var condition = Expr.Property("Description").Contains("important");
        /// </code>
        /// </example>
        public static LogicBinaryExpr Contains(this ValueTypeExpr left, string text) => new LogicBinaryExpr(left, LogicOperator.Contains, new ValueExpr(text));

        /// <summary>
        /// 创建以指定字符串开头的表达式 (StartsWith)。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <param name="text">要以其开头的文本。</param>
        /// <returns>以指定字符串开头的逻辑表达式。</returns>
        /// <example>
        /// <code>
        /// var condition = Expr.Property("Email").StartsWith("admin");
        /// </code>
        /// </example>
        public static LogicBinaryExpr StartsWith(this ValueTypeExpr left, string text) => new LogicBinaryExpr(left, LogicOperator.StartsWith, new ValueExpr(text));

        /// <summary>
        /// 创建以指定字符串结尾的表达式 (EndsWith)。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <param name="text">要以其结尾的文本。</param>
        /// <returns>以指定字符串结尾的逻辑表达式。</returns>
        /// <example>
        /// <code>
        /// var condition = Expr.Property("Email").EndsWith("@example.com");
        /// </code>
        /// </example>
        public static LogicBinaryExpr EndsWith(this ValueTypeExpr left, string text) => new LogicBinaryExpr(left, LogicOperator.EndsWith, new ValueExpr(text));

        /// <summary>
        /// 创建 IS NULL 表达式。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <returns>IS NULL 逻辑表达式。</returns>
        /// <example>
        /// <code>
        /// var condition = Expr.Property("DeletedAt").IsNull();
        /// </code>
        /// </example>
        public static LogicBinaryExpr IsNull(this ValueTypeExpr left) => new LogicBinaryExpr(left, LogicOperator.Equal, Expr.Null);

        /// <summary>
        /// 创建 IS NOT NULL 表达式。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <returns>IS NOT NULL 逻辑表达式。</returns>
        /// <example>
        /// <code>
        /// var condition = Expr.Property("Email").IsNotNull();
        /// </code>
        /// </example>
        public static LogicBinaryExpr IsNotNull(this ValueTypeExpr left) => new LogicBinaryExpr(left, LogicOperator.NotEqual, Expr.Null);

        /// <summary>
        /// 为 SQL 语句添加 WHERE 子句。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="where">WHERE 子句的逻辑表达式。</param>
        /// <returns>包含 WHERE 子句的 SQL 表达式。</returns>
        /// <example>
        /// <code>
        /// var query = table.Where(Expr.Property("Age") > 18);
        /// </code>
        /// </example>
        public static WhereExpr Where(this ISourceAnchor source, LogicExpr where) => new WhereExpr(source as SqlSegment, where);

        /// <summary>
        /// 为 SQL 语句添加 GROUP BY 子句。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="groupBys">分组表达式数组。</param>
        /// <returns>包含 GROUP BY 子句的 SQL 表达式。</returns>
        /// <example>
        /// <code>
        /// var query = table.GroupBy(Expr.Property("DepartmentId"));
        /// </code>
        /// </example>
        public static GroupByExpr GroupBy(this IGroupByAnchor source, params ValueTypeExpr[] groupBys) => new GroupByExpr(source as SqlSegment, groupBys);

        /// <summary>
        /// 为 SQL 语句添加 HAVING 子句。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="having">HAVING 子句的逻辑表达式。</param>
        /// <returns>包含 HAVING 子句的 SQL 表达式。</returns>
        /// <example>
        /// <code>
        /// var query = table.GroupBy(Expr.Property("DepartmentId"))
        ///     .Having(Expr.Property("Count").Count().GreaterThan(10));
        /// </code>
        /// </example>
        public static HavingExpr Having(this IHavingAnchor source, LogicExpr having) => new HavingExpr(source as SqlSegment, having);

        /// <summary>
        /// 为 SQL 语句添加 SELECT 子句。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="selects">选择表达式数组。</param>
        /// <returns>包含 SELECT 子句的 SQL 表达式。</returns>
        /// <example>
        /// <code>
        /// var query = table.Select(Expr.Property("Id"), Expr.Property("Name"));
        /// </code>
        /// </example>
        public static SelectExpr Select(this ISelectAnchor source, params ValueTypeExpr[] selects) => new SelectExpr(source as SqlSegment, selects);

        /// <summary>
        /// 为 SQL 语句添加 ORDER BY 子句。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="orderBys">排序表达式和方向的元组数组。</param>
        /// <returns>包含 ORDER BY 子句的 SQL 表达式。</returns>
        /// <example>
        /// <code>
        /// var query = table.OrderBy(Expr.Property("CreatedDate").Desc());
        /// </code>
        /// </example>
        public static OrderByExpr OrderBy(this IOrderByAnchor source, params (ValueTypeExpr, bool)[] orderBys) => new OrderByExpr(source as SqlSegment, orderBys);

        /// <summary>
        /// 为 SQL 语句添加分页子句。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="skip">跳过的记录数。</param>
        /// <param name="take">获取的记录数。</param>
        /// <returns>包含分页子句的 SQL 表达式。</returns>
        /// <example>
        /// <code>
        /// var query = table.Section(0, 20); // 获取前 20 条记录
        /// </code>
        /// </example>
        public static SectionExpr Section(this ISectionAnchor source, int skip, int take) => new SectionExpr(source as SqlSegment, skip, take);

        /// <summary>
        /// 将值表达式标记为升序排序。
        /// </summary>
        /// <param name="expr">值表达式。</param>
        /// <returns>包含表达式和排序方向的元组。</returns>
        /// <example>
        /// <code>
        /// var query = table.OrderBy(Expr.Property("Name").Asc());
        /// </code>
        /// </example>
        public static (ValueTypeExpr, bool) Asc(this ValueTypeExpr expr) => (expr, true);

        /// <summary>
        /// 将值表达式标记为降序排序。
        /// </summary>
        /// <param name="expr">值表达式。</param>
        /// <returns>包含表达式和排序方向的元组。</returns>
        /// <example>
        /// <code>
        /// var query = table.OrderBy(Expr.Property("CreatedDate").Desc());
        /// </code>
        /// </example>
        public static (ValueTypeExpr, bool) Desc(this ValueTypeExpr expr) => (expr, false);

        /// <summary>
        /// 创建 COUNT 聚合函数表达式。
        /// </summary>
        /// <param name="expr">要计数的值表达式。</param>
        /// <param name="isDistinct">是否去重计数。</param>
        /// <returns>COUNT 聚合函数表达式。</returns>
        /// <example>
        /// <code>
        /// var countExpr = Expr.Property("Id").Count();
        /// var distinctCountExpr = Expr.Property("Name").Count(true);
        /// </code>
        /// </example>
        public static AggregateFunctionExpr Count(this ValueTypeExpr expr, bool isDistinct = false) => new AggregateFunctionExpr("COUNT", expr, isDistinct);

        /// <summary>
        /// 创建 SUM 聚合函数表达式。
        /// </summary>
        /// <param name="expr">要求和的值表达式。</param>
        /// <returns>SUM 聚合函数表达式。</returns>
        /// <example>
        /// <code>
        /// var sumExpr = Expr.Property("Salary").Sum();
        /// </code>
        /// </example>
        public static AggregateFunctionExpr Sum(this ValueTypeExpr expr) => new AggregateFunctionExpr("SUM", expr);

        /// <summary>
        /// 创建 AVG 聚合函数表达式。
        /// </summary>
        /// <param name="expr">要求平均值的值表达式。</param>
        /// <returns>AVG 聚合函数表达式。</returns>
        /// <example>
        /// <code>
        /// var avgExpr = Expr.Property("Salary").Avg();
        /// </code>
        /// </example>
        public static AggregateFunctionExpr Avg(this ValueTypeExpr expr) => new AggregateFunctionExpr("AVG", expr);

        /// <summary>
        /// 创建 MAX 聚合函数表达式。
        /// </summary>
        /// <param name="expr">要求最大值的值表达式。</param>
        /// <returns>MAX 聚合函数表达式。</returns>
        /// <example>
        /// <code>
        /// var maxExpr = Expr.Property("Salary").Max();
        /// </code>
        /// </example>
        public static AggregateFunctionExpr Max(this ValueTypeExpr expr) => new AggregateFunctionExpr("MAX", expr);

        /// <summary>
        /// 创建 MIN 聚合函数表达式。
        /// </summary>
        /// <param name="expr">要求最小值的值表达式。</param>
        /// <returns>MIN 聚合函数表达式。</returns>
        /// <example>
        /// <code>
        /// var minExpr = Expr.Property("Salary").Min();
        /// </code>
        /// </example>
        public static AggregateFunctionExpr Min(this ValueTypeExpr expr) => new AggregateFunctionExpr("MIN", expr);
    }
}
