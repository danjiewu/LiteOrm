using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
    /// var condition = Expr.Prop("Age") > 18
    ///     .And(Expr.Prop("Name").Contains("John"))
    ///     .Or(Expr.Prop("Email").EndsWith("@example.com"));
    ///
    /// // 构建排序和分页
    /// var query = table.Select("Id", "Name")
    ///     .Where(condition)
    ///     .OrderBy(Expr.Prop("CreatedDate").Desc())
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
        /// var condition = Expr.Prop("Age") > 18
        ///     .And(Expr.Prop("Name").Contains("John"));
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
        /// var condition = Expr.Prop("Age") > 18
        ///     .Or(Expr.Prop("IsVIP").Equal(true));
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
        /// var fullName = Expr.Prop("FirstName").Concat(Expr.Prop("LastName"));
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
        /// var condition = Expr.Prop("IsDeleted").Equal(true).Not();
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
        /// var condition = Expr.Prop("Id").Equal(123);
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
        /// var condition = Expr.Prop("Status").NotEqual("Inactive");
        /// </code>
        /// </example>
        public static LogicBinaryExpr NotEqual(this ValueTypeExpr left, ValueTypeExpr right) => new LogicBinaryExpr(left, LogicOperator.NotEqual, right);

        /// <summary>
        /// 创建大于比较表达式。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <param name="right">右侧值表达式。</param>
        /// <returns>大于比较逻辑表达式。</returns>
        public static LogicBinaryExpr GreaterThan(this ValueTypeExpr left, ValueTypeExpr right) => new LogicBinaryExpr(left, LogicOperator.GreaterThan, right);

        /// <summary>
        /// 创建小于比较表达式。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <param name="right">右侧值表达式。</param>
        /// <returns>小于比较逻辑表达式。</returns>
        public static LogicBinaryExpr LessThan(this ValueTypeExpr left, ValueTypeExpr right) => new LogicBinaryExpr(left, LogicOperator.LessThan, right);

        /// <summary>
        /// 创建大于等于比较表达式。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <param name="right">右侧值表达式。</param>
        /// <returns>大于等于比较逻辑表达式。</returns>
        public static LogicBinaryExpr GreaterThanOrEqual(this ValueTypeExpr left, ValueTypeExpr right) => new LogicBinaryExpr(left, LogicOperator.GreaterThanOrEqual, right);

        /// <summary>
        /// 创建小于等于比较表达式。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <param name="right">右侧值表达式。</param>
        /// <returns>小于等于比较逻辑表达式。</returns>
        public static LogicBinaryExpr LessThanOrEqual(this ValueTypeExpr left, ValueTypeExpr right) => new LogicBinaryExpr(left, LogicOperator.LessThanOrEqual, right);

        /// <summary>
        /// 创建 IN 集合包含表达式。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <param name="items">包含值的集合。</param>
        /// <returns>IN 集合包含逻辑表达式。</returns>
        /// <example>
        /// <code>
        /// var ids = new List&lt;int&gt; { 1, 2, 3 };
        /// var condition = Expr.Prop("Id").In(ids);
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
        /// var condition = Expr.Prop("Id").In(1, 2, 3, 4, 5);
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
        /// var excludedStatuses = new List&lt;string&gt; { "Deleted", "Inactive" };
        /// var condition = Expr.Prop("Status").NotIn(excludedStatuses);
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
        /// var condition = Expr.Prop("Status").NotIn("Deleted", "Inactive");
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
        /// var condition = Expr.Prop("Age").Between(18, 65);
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
        /// var condition = Expr.Prop("Name").Like("J%");
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
        /// var condition = Expr.Prop("Description").Contains("important");
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
        /// var condition = Expr.Prop("Email").StartsWith("admin");
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
        /// var condition = Expr.Prop("Email").EndsWith("@example.com");
        /// </code>
        /// </example>
        public static LogicBinaryExpr EndsWith(this ValueTypeExpr left, string text) => new LogicBinaryExpr(left, LogicOperator.EndsWith, new ValueExpr(text));

        /// <summary>
        /// 为表达式设置别名。
        /// </summary>
        /// <param name="expr">要设置别名的值类型表达式。</param>
        /// <param name="name">别名名称。</param>
        /// <returns>带有别名的选择项表达式。</returns>
        public static SelectItemExpr As(this ValueTypeExpr expr, string name) => new SelectItemExpr(expr, name);

        /// <summary>
        /// 创建 IS NULL 表达式。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <returns>IS NULL 逻辑表达式。</returns>
        /// <example>
        /// <code>
        /// var condition = Expr.Prop("DeletedAt").IsNull();
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
        /// var condition = Expr.Prop("Email").IsNotNull();
        /// </code>
        /// </example>
        public static LogicBinaryExpr IsNotNull(this ValueTypeExpr left) => new LogicBinaryExpr(left, LogicOperator.NotEqual, Expr.Null);

        /// <summary>
        /// 将表达式转换为对应的 SQL 数据源片段。
        /// </summary>
        /// <param name="expr">条件或片段表达式。</param>
        /// <typeparam name="T">目标对象类型。</typeparam>
        /// <returns>对应的 ISqlSegment 对象。</returns>
        /// <exception cref="ArgumentException">当 expr 参数类型不是 null、LogicExpr 或 ISqlSegment 时抛出。</exception>
        public static ISqlSegment ToSource<T>(this Expr expr) {
            return expr.ToSource(typeof(T));
        }

        /// <summary>
        /// 将表达式转换为对应的 SQL 数据源片段。
        /// </summary>
        /// <param name="expr">条件或片段表达式。</param>
        /// <param name="objectType">目标对象类型。</param>
        /// <returns>对应的 ISqlSegment 对象。</returns>
        /// <exception cref="ArgumentException">当 expr 参数类型不是 null、LogicExpr 或 ISqlSegment 时抛出。</exception>
        public static ISqlSegment ToSource(this Expr expr, Type objectType) {
            if (expr is null)
            {
                return new FromExpr(objectType);
            }
            else if (expr is LogicExpr logicExpr)
            {
                return new WhereExpr() { Source = new FromExpr(objectType), Where = logicExpr };
            }
            else if (expr is ISqlSegment sourceExpr)
            {
                ISqlSegment firstSource = sourceExpr;
                while (firstSource.Source is not null)
                {
                    firstSource = firstSource.Source;
                }
                if (firstSource is FromExpr fromExpr)
                {
                    fromExpr.ObjectType = objectType;
                }
                else{
                    firstSource.Source = new FromExpr(objectType);
                }
                return sourceExpr;
            }
            else
            {
                throw new ArgumentException("expr 参数类型不支持");
            }
        }
        /// <summary>
        /// 基于逻辑条件开启排序子句构建。
        /// </summary>
        /// <param name="logic">逻辑条件表达式。</param>
        /// <param name="orderBys">排序定义序列。</param>
        /// <returns>排序表达式对象。</returns>
        public static OrderByExpr OrderBy(this LogicExpr logic, params (ValueTypeExpr, bool)[] orderBys) => new OrderByExpr(new WhereExpr(null, logic), orderBys);

        /// <summary>
        /// 基于逻辑条件开启分页子句构建。
        /// </summary>
        /// <param name="logic">逻辑条件表达式。</param>
        /// <param name="skip">跳过记录数。</param>
        /// <param name="take">获取记录数。</param>
        /// <returns>分页表达式对象。</returns>
        public static SectionExpr Section(this LogicExpr logic, int skip, int take) => new SectionExpr(new WhereExpr(null, logic), skip, take);

        /// <summary>
        /// 基于逻辑条件开启选择子句构建。
        /// </summary>
        /// <param name="logic">逻辑条件表达式。</param>
        /// <param name="selects">选择项表达式序列。</param>
        /// <returns>选择表达式对象。</returns>
        public static SelectExpr Select(this LogicExpr logic, params ValueTypeExpr[] selects) => new SelectExpr(new WhereExpr(null, logic), selects);

        /// <summary>
        /// 为 SQL 语句添加 WHERE 子句。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="where">WHERE 子句的逻辑表达式。</param>
        /// <returns>包含 WHERE 子句的 SQL 表达式。</returns>
        /// <example>
        /// <code>
        /// var query = table.Where(Expr.Prop("Age") > 18);
        /// </code>
        /// </example>
        public static WhereExpr Where(this ISourceAnchor source, LogicExpr where) => new WhereExpr(source as ISqlSegment, where);

        /// <summary>
        /// 为 SQL 语句添加 GROUP BY 子句。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="groupBys">分组表达式数组。</param>
        /// <returns>包含 GROUP BY 子句的 SQL 表达式。</returns>
        /// <example>
        /// <code>
        /// var query = table.GroupBy(Expr.Prop("DepartmentId"));
        /// </code>
        /// </example>
        public static GroupByExpr GroupBy(this IGroupByAnchor source, params ValueTypeExpr[] groupBys) => new GroupByExpr(source as ISqlSegment, groupBys);

        /// <summary>
        /// 为 SQL 语句添加 GROUP BY 子句（属性名数组）。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="groupByProperties">要分组的属性名称序列。</param>
        /// <returns>包含 GROUP BY 子句的 SQL 表达式。</returns>
        public static GroupByExpr GroupBy(this IGroupByAnchor source, params string[] groupByProperties) =>
            GroupBy(source, Array.ConvertAll(groupByProperties, prop => (ValueTypeExpr)Expr.Prop(prop)));

        /// <summary>
        /// 为 SQL 语句添加 HAVING 子句。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="having">HAVING 子句的逻辑表达式。</param>
        /// <returns>包含 HAVING 子句的 SQL 表达式。</returns>
        /// <example>
        /// <code>
        /// var query = table.GroupBy(Expr.Prop("DepartmentId"))
        ///     .Having(Expr.Prop("Count").Count().GreaterThan(10));
        /// </code>
        /// </example>
        public static HavingExpr Having(this IHavingAnchor source, LogicExpr having) => new HavingExpr(source as ISqlSegment, having);

        /// <summary>
        /// 为 SQL 语句添加 SELECT 子句。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="selects">选择表达式数组。</param>
        /// <returns>包含 SELECT 子句的 SQL 表达式。</returns>
        /// <example>
        /// <code>
        /// var query = table.Select(Expr.Prop("Id"), Expr.Prop("Name"));
        /// </code>
        /// </example>
        public static SelectExpr Select(this ISelectAnchor source, params ValueTypeExpr[] selects) => new SelectExpr(source as ISqlSegment, selects);

        /// <summary>
        /// 为 SQL 语句添加 SELECT 子句（属性名数组）。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="selectProperties">要选择的属性名称序列。</param>
        /// <returns>包含 SELECT 子句的 SQL 表达式。</returns>
        public static SelectExpr Select(this ISelectAnchor source, params string[] selectProperties) => Select(source, Array.ConvertAll(selectProperties, prop => (ValueTypeExpr)Expr.Prop(prop)));

        /// <summary>
        /// 为 SQL 语句添加 ORDER BY 子句。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="orderBys">排序表达式和方向的元组数组。</param>
        /// <returns>包含 ORDER BY 子句的 SQL 表达式。</returns>
        /// <example>
        /// <code>
        /// var query = table.OrderBy(Expr.Prop("CreatedDate").Desc());
        /// </code>
        /// </example>
        public static OrderByExpr OrderBy(this IOrderByAnchor source, params (ValueTypeExpr, bool)[] orderBys)
        {
            if (source is OrderByExpr existingOrderByExpr)
            {
                existingOrderByExpr.OrderBys.AddRange(orderBys);
                return existingOrderByExpr;
            }
            return new OrderByExpr(source as ISqlSegment, orderBys);
        }

        /// <summary>
        /// 为 SQL 语句添加 ORDER BY 子句（属性名与排序方向元组数组）。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="orderBys">排序表达式和方向的元组数组（属性名, 升序/降序）。</param>
        /// <returns>包含 ORDER BY 子句的 SQL 表达式。</returns>
        /// <example>
        /// <code>
        /// var query = table.OrderBy(("CreatedDate", false));
        /// </code>
        /// </example>
        public static OrderByExpr OrderBy(this IOrderByAnchor source, params (string, bool)[] orderBys) =>
            OrderBy(source, Array.ConvertAll(orderBys, tuple => ((ValueTypeExpr)Expr.Prop(tuple.Item1), tuple.Item2)));

        /// <summary>
        /// 为 SQL 语句添加 ORDER BY 升序子句（属性名）。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="orderBy">要排序的属性名称。</param>
        /// <returns>包含 ORDER BY 子句的 SQL 表达式。</returns>
        public static OrderByExpr OrderBy(this IOrderByAnchor source, string orderBy) =>
            OrderBy(source, (Expr.Prop(orderBy), true));

        /// <summary>
        /// 为 SQL 语句添加 ORDER BY 降序子句（属性名）。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="orderBy">要排序的属性名称。</param>
        /// <returns>包含 ORDER BY 子句的 SQL 表达式。</returns>
        public static OrderByExpr OrderByDesc(this IOrderByAnchor source, string orderBy) =>
            OrderBy(source, (Expr.Prop(orderBy), false));

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
        public static SectionExpr Section(this ISectionAnchor source, int skip, int take) => new SectionExpr(source as ISqlSegment, skip, take);

        /// <summary>
        /// 将值表达式标记为升序排序。
        /// </summary>
        /// <param name="expr">值表达式。</param>
        /// <returns>包含表达式和排序方向的元组。</returns>
        /// <example>
        /// <code>
        /// var query = table.OrderBy(Expr.Prop("Name").Asc());
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
        /// var query = table.OrderBy(Expr.Prop("CreatedDate").Desc());
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
        /// var countExpr = Expr.Prop("Id").Count();
        /// var distinctCountExpr = Expr.Prop("Name").Count(true);
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
        /// var sumExpr = Expr.Prop("Salary").Sum();
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
        /// var avgExpr = Expr.Prop("Salary").Avg();
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
        /// var maxExpr = Expr.Prop("Salary").Max();
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
        /// var minExpr = Expr.Prop("Salary").Min();
        /// </code>
        /// </example>
        public static AggregateFunctionExpr Min(this ValueTypeExpr expr) => new AggregateFunctionExpr("MIN", expr);
    }
}
