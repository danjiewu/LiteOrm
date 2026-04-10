using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

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
    /// 9. 字符串函数（Upper/Lower/Length）
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
        public static LogicExpr And(this LogicExpr left, LogicExpr right) => left is null ? right : new AndExpr(left, right);

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
        public static LogicExpr Or(this LogicExpr left, LogicExpr right) => left is null ? right : new OrExpr(left, right);

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
        public static LogicExpr Not(this LogicExpr expr) => expr is NotExpr notExpr ? notExpr.Operand : new NotExpr(expr);

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
        /// 创建 IN 集合包含表达式（表达式版本）。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <param name="expr">表示集合的表达式，如ValueSet、SelectExpr。</param>
        /// <returns>IN 集合包含逻辑表达式。</returns>
        /// <example>
        /// <code>
        /// var condition = Expr.Prop("UserId").In(Expr.From&lt;User&gt;().Where(Expr.Prop("Age") &gt;= 18).Select(nameof(User.Id));
        /// </code>
        /// </example>
        public static LogicBinaryExpr In(this ValueTypeExpr left, Expr expr) => new LogicBinaryExpr(left, LogicOperator.In, expr.AsValue());
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
        /// 通过组合&gt;= low和&lt;= high创建范围查询表达式 (BETWEEN)。
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
        public static LogicExpr Between(this ValueTypeExpr left, ValueTypeExpr low, ValueTypeExpr high)
        {
            return (left >= low) & (left <= high);
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
        /// 创建正则表达式匹配表达式 (REGEXP_LIKE)。
        /// </summary>
        /// <param name="left">左侧值表达式。</param>
        /// <param name="pattern">正则表达式模式字符串。</param>
        /// <returns>正则表达式匹配逻辑表达式。</returns>
        /// <example>
        /// <code>
        /// var condition = Expr.Prop("Email").RegexpLike(@"^[\w.-]+@[\w.-]+\.\w+$");
        /// </code>
        /// </example>
        public static LogicBinaryExpr RegexpLike(this ValueTypeExpr left, string pattern) => new LogicBinaryExpr(left, LogicOperator.RegexpLike, new ValueExpr(pattern));

        /// <summary>
        /// 为表达式设置别名。
        /// </summary>
        /// <param name="expr">要设置别名的值类型表达式。</param>
        /// <param name="name">别名名称。</param>
        /// <returns>带有别名的选择项表达式。</returns>
        public static SelectItemExpr As(this ValueTypeExpr expr, string name) => new SelectItemExpr(expr, name);

        /// <summary>
        /// 为From表达式设置别名。
        /// </summary>
        /// <param name="fromExpr">From表达式</param>
        /// <param name="alias">别名</param>
        /// <returns>带有别名的From表达式</returns>
        public static FromExpr As(this FromExpr fromExpr, string alias)
        {
            if (fromExpr?.Table != null)
            {
                fromExpr.Table.Alias = alias;
            }
            return fromExpr;
        }

        /// <summary>
        /// 为Select表达式设置别名。
        /// </summary>
        /// <param name="selectExpr">Select表达式</param>
        /// <param name="alias">别名</param>
        /// <returns>带有别名的Select表达式</returns>
        public static SelectExpr As(this SelectExpr selectExpr, string alias)
        {
            selectExpr.Alias = alias;
            return selectExpr;
        }

        /// <summary>
        /// 为Foreign表达式设置别名。
        /// </summary>
        /// <param name="foreignExpr">Foregin表达式</param>
        /// <param name="alias">别名</param>
        /// <returns></returns>
        public static ForeignExpr As(this ForeignExpr foreignExpr, string alias)
        {
            foreignExpr.Alias = alias;
            return foreignExpr;
        }

        /// <summary>
        /// 将任意表达式转换为值类型表达式，如果已经是值类型表达式则直接返回，否则包装成 ValueExpr。
        /// </summary>
        /// <param name="expr">要转换的表达式。</param>
        /// <returns>值类型表达式。</returns>
        public static ValueTypeExpr AsValue(this Expr expr) => expr is ValueTypeExpr valueTypeExpr ? valueTypeExpr : new ValueExpr(expr);

        /// <summary>
        /// 将任意表达式转换为逻辑表达式，如果已经是逻辑表达式则直接返回，如果是值类型表达式则转换为非零即真，否则抛出异常。
        /// </summary>
        /// <param name="expr">要转换的表达式。</param>
        /// <returns>逻辑表达式。</returns>
        /// <exception cref="NotSupportedException">当 expr 参数类型不是逻辑表达式或值类型表达式时抛出。</exception>
        public static LogicExpr AsLogic(this Expr expr)
        {
            if (expr is null) return null;
            if (expr is LogicExpr logicExpr) return logicExpr;
            if (expr is ValueExpr ve && ve.Value is LogicExpr logicExpr1) return logicExpr1;
            if (expr is ValueTypeExpr vte) return vte != 0;
            throw new NotSupportedException($"Expression {expr} of type {expr?.GetType().Name} cannot be converted to LogicExpr.");
        }

        /// <summary>
        /// 仅用于Lambda表达式解析场景，表示将表达式转换为指定类型的占位符方法，实际调用时会被表达式解析器识别并处理，不会执行该方法体。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="expr">要转换的表达式。</param>
        /// <returns>指定类型的占位符值。</returns>
        /// <exception cref="NotSupportedException">当在非Lambda表达式解析场景中调用时抛出。</exception>
        public static T To<T>(this Expr expr)
        {
            throw new NotSupportedException("Only supported in Lambda expression parsing scenarios.");
        }

        /// <summary>
        /// 将表达式值转换为小写
        /// </summary>
        /// <param name="expr">值表达式</param>
        /// <returns>小写函数表达式</returns>
        public static FunctionExpr Lower(this ValueBinaryExpr expr) => new FunctionExpr("LOWER", expr);

        /// <summary>
        /// 将表达式值转换为大写
        /// </summary>
        /// <param name="expr">值表达式</param>
        /// <returns>大写函数表达式</returns>
        public static FunctionExpr Upper(this ValueBinaryExpr expr) => new FunctionExpr("UPPER", expr);

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
        /// 创建 IfNull 函数表达式，当 <paramref name="expr"/> 为 NULL 时返回 <paramref name="defaultValue"/>。
        /// </summary>
        /// <param name="expr">待检测的值表达式。</param>
        /// <param name="defaultValue">为 NULL 时的替代值表达式。</param>
        /// <returns>IfNull 函数表达式。</returns>
        public static FunctionExpr IfNull(this ValueTypeExpr expr, ValueTypeExpr defaultValue) => new FunctionExpr("IfNull", expr, defaultValue);

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
        public static SqlSegment ToSource<T>(this Expr expr)
        {
            return expr.ToSource(typeof(T));
        }

        /// <summary>
        /// 将表达式转换为对应的 SQL 数据源片段。
        /// </summary>
        /// <param name="expr">条件或片段表达式。</param>
        /// <param name="objectType">目标对象类型。</param>
        /// <returns>对应的 ISqlSegment 对象。</returns>
        /// <exception cref="ArgumentException">当 expr 参数类型不是 null、LogicExpr 或 ISqlSegment 时抛出。</exception>
        public static SqlSegment ToSource(this Expr expr, Type objectType)
        {
            if (expr is null)
            {
                return Expr.From(objectType);
            }
            else if (expr is LogicExpr logicExpr)
            {
                return new WhereExpr() { Source = new FromExpr(objectType), Where = logicExpr };
            }
            else if (expr is SqlSegment sourceExpr)
            {
                SqlSegment firstSource = sourceExpr;
                while (firstSource is SqlSegment chainedSegment && chainedSegment.Source is not null)
                {
                    firstSource = chainedSegment.Source;
                }
                if (firstSource is FromExpr fromExpr)
                {
                    fromExpr.Table = new TableExpr(objectType);
                }
                else if (firstSource is TableExpr tableExpr)
                {
                    tableExpr.Type = objectType;
                }
                else if (firstSource is SqlSegment chainedSegment)
                {
                    chainedSegment.Source = Expr.From(objectType);
                }
                return sourceExpr;
            }
            else
            {
                throw new ArgumentException("Unsupported expr type");
            }
        }
        /// <summary>
        /// 基于逻辑条件开启排序子句构建。
        /// </summary>
        /// <param name="logic">逻辑条件表达式。</param>
        /// <param name="orderBys">排序定义序列。</param>
        /// <returns>排序表达式对象。</returns>
        public static OrderByExpr OrderBy(this LogicExpr logic, params OrderByItemExpr[] orderBys) => new OrderByExpr(new WhereExpr(null, logic), orderBys);

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
        public static WhereExpr Where(this ISourceAnchor source, LogicExpr where)
        {
            if (source is WhereExpr whereExpr)
            {
                whereExpr.Where = whereExpr.Where is null ? where : whereExpr.Where.And(where);
                return whereExpr;
            }
            else return new WhereExpr(source as SqlSegment, where);
        }

        /// <summary>
        /// 使用指定的 Lambda 表达式创建 WHERE 条件表达式
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="source">SQL 语句构建起点</param>
        /// <param name="expression">定义筛选条件的 Lambda 表达式</param>
        /// <returns>WHERE 条件表达式实例</returns>
        public static WhereExpr Where<T>(this ISourceAnchor source, Expression<Func<T, bool>> expression) => new WhereExpr(source as SqlSegment, Expr.Lambda(expression));

        /// <summary>
        /// 为 UPDATE 表达式添加 WHERE 条件。
        /// </summary>
        /// <param name="source">UPDATE 表达式。</param>
        /// <param name="where">条件表达式。</param>
        /// <returns>包含 WHERE 子句的 UPDATE 表达式。</returns>
        public static UpdateExpr Where(this UpdateExpr source, LogicExpr where)
        {
            source.Where = source.Where is null ? where : source.Where.And(where);
            return source;
        }

        /// <summary>
        /// 为 DELETE 表达式添加 WHERE 条件。
        /// </summary>
        /// <param name="source">DELETE 表达式。</param>
        /// <param name="where">条件表达式。</param>
        /// <returns>包含 WHERE 子句的 DELETE 表达式。</returns>
        public static DeleteExpr Where(this DeleteExpr source, LogicExpr where)
        {
            source.Where = source.Where is null ? where : source.Where.And(where);
            return source;
        }

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
        public static GroupByExpr GroupBy(this IGroupByAnchor source, params ValueTypeExpr[] groupBys) => new GroupByExpr(source as SqlSegment, groupBys);

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
        public static HavingExpr Having(this IHavingAnchor source, LogicExpr having) => new HavingExpr(source as SqlSegment, having);


        /// <summary>
        /// 创建 SELECT 表达式，指定要选择的项。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="selects">选择项表达式数组。</param>
        /// <returns>包含 SELECT 子句的 SQL 表达式。</returns>
        public static SelectExpr Select(this ISelectAnchor source, params SelectItemExpr[] selects)
        {
            return new SelectExpr(source as SqlSegment, selects);
        }
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
        public static SelectExpr Select(this ISelectAnchor source, params ValueTypeExpr[] selects) => new SelectExpr(source as SqlSegment, selects);

        /// <summary>
        /// 为 SQL 语句添加 SELECT 子句（属性名数组）。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="selectProperties">要选择的属性名称序列。</param>
        /// <returns>包含 SELECT 子句的 SQL 表达式。</returns>
        public static SelectExpr Select(this ISelectAnchor source, params string[] selectProperties) => Select(source, Array.ConvertAll(selectProperties, prop => (ValueTypeExpr)Expr.Prop(prop)));

        /// <summary>
        /// 将当前 SelectExpr 与另一个 SelectExpr 以 UNION 连接。
        /// 若 <paramref name="source"/> 为 null 则返回 <paramref name="next"/>；若 <paramref name="next"/> 为 null 则返回 <paramref name="source"/>。
        /// 否则将连接类型写入 <paramref name="next"/>.SetType 并追加到 <paramref name="source"/> 的 NextSelects 列表。
        /// <remarks>
        /// 本方法对 null 参数采用容错处理：不会抛出异常，若两者皆为 null 则返回 null。
        /// </remarks>
        /// </summary>
        public static SelectExpr Union(this SelectExpr source, SelectExpr next)
        {
            if (source is null) return next;
            if (next is null) return source;
            next.SetType = SelectSetType.Union;
            source.NextSelects.Add(next);
            return source;
        }

        /// <summary>
        /// 将当前 SelectExpr 与另一个 SelectExpr 以 UNION ALL 连接。
        /// 若 <paramref name="source"/> 为 null 则返回 <paramref name="next"/>；若 <paramref name="next"/> 为 null 则返回 <paramref name="source"/>。
        /// 否则将连接类型写入 <paramref name="next"/>.SetType 并追加到 <paramref name="source"/> 的 NextSelects 列表。
        /// <remarks>
        /// 本方法对 null 参数采用容错处理：不会抛出异常，若两者皆为 null 则返回 null。
        /// </remarks>
        /// </summary>
        public static SelectExpr UnionAll(this SelectExpr source, SelectExpr next)
        {
            if (source is null) return next;
            if (next is null) return source;
            next.SetType = SelectSetType.UnionAll;
            source.NextSelects.Add(next);
            return source;
        }

        /// <summary>
        /// 将当前 SelectExpr 与另一个 SelectExpr 以 INTERSECT 连接。
        /// 行为：若 <paramref name="source"/> 为 null 或 <paramref name="next"/> 为 null，将抛出 <see cref="ArgumentNullException"/>；
        /// 否则将连接类型写入 <paramref name="next"/>.SetType 并追加到 <paramref name="source"/> 的 NextSelects 列表。
        /// <exception cref="ArgumentNullException">当 <paramref name="source"/> 或 <paramref name="next"/> 为 null 时抛出。</exception>
        /// </summary>
        public static SelectExpr Intersect(this SelectExpr source, SelectExpr next)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (next is null) throw new ArgumentNullException(nameof(next));
            next.SetType = SelectSetType.Intersect;
            source.NextSelects.Add(next);
            return source;
        }

        /// <summary>
        /// 将当前 SelectExpr 与另一个 SelectExpr 以 EXCEPT 连接。
        /// 行为：若 <paramref name="source"/> 为 null 将抛出 <see cref="ArgumentNullException"/>；
        /// 若 <paramref name="next"/> 为 null 则直接返回 <paramref name="source"/>；
        /// 否则将连接类型写入 <paramref name="next"/>.SetType 并追加到 <paramref name="source"/> 的 NextSelects 列表。
        /// <exception cref="ArgumentNullException">当 <paramref name="source"/> 为 null 时抛出。</exception>
        /// </summary>
        public static SelectExpr Except(this SelectExpr source, SelectExpr next)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (next is null) return source;
            next.SetType = SelectSetType.Except;
            source.NextSelects.Add(next);
            return source;
        }

        /// <summary>
        /// 为 SQL 语句添加 SELECT 子句（选择项数组）。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="selects">选择项表达式数组。</param>
        /// <returns>包含 SELECT 子句的 SQL 表达式。</returns>
        public static SelectExpr SelectMore(this SelectExpr source, params SelectItemExpr[] selects)
        {
            source.Selects.AddRange(selects);
            return source;
        }

        /// <summary>
        /// 向已有 SELECT 表达式追加更多值表达式。
        /// </summary>
        /// <param name="source">已有的选择表达式。</param>
        /// <param name="selects">追加的值表达式数组。</param>
        /// <returns>追加后的选择表达式。</returns>
        public static SelectExpr SelectMore(this SelectExpr source, params ValueTypeExpr[] selects)
        {
            source.Selects.AddRange(selects.Select(s => s is SelectItemExpr si ? si : new SelectItemExpr(s)));
            return source;
        }

        /// <summary>
        /// 向已有 SELECT 表达式追加更多属性名。
        /// </summary>
        /// <param name="source">已有的选择表达式。</param>
        /// <param name="selectProperties">追加的属性名称数组。</param>
        /// <returns>追加后的选择表达式。</returns>
        public static SelectExpr SelectMore(this SelectExpr source, params string[] selectProperties)
        {
            source.Selects.AddRange(Array.ConvertAll(selectProperties, prop => new SelectItemExpr(Expr.Prop(prop))));
            return source;
        }

        /// <summary>
        /// 更新表达式添加 SET 子句。
        /// </summary>
        /// <param name="source">更新表达式。</param>
        /// <param name="assignments">属性名称与值表达式的元组数组。</param>
        /// <returns>包含 SET 子句的更新表达式。</returns>
        public static UpdateExpr Set(this UpdateExpr source, params (string, ValueTypeExpr)[] assignments)
        {
            foreach (var (propName, valueExpr) in assignments)
            {
                source.Sets.Add((Expr.Prop(propName), valueExpr));
            }
            return source;
        }

        /// <summary>
        /// 当条件为真时，向更新表达式追加一个 SET 子句。
        /// </summary>
        /// <param name="source">更新表达式。</param>
        /// <param name="condition">为 true 时才追加该赋值。</param>
        /// <param name="propName">属性名称。</param>
        /// <param name="valueExpr">要设置的值表达式。</param>
        /// <returns>更新表达式（链式调用）。</returns>
        /// <example>
        /// <code>
        /// update.SetIf(newEmail != null, "Email", newEmail);
        /// </code>
        /// </example>
        public static UpdateExpr SetIf(this UpdateExpr source, bool condition, string propName, ValueTypeExpr valueExpr)
        {
            if (condition) source.Sets.Add((Expr.Prop(propName), valueExpr));
            return source;
        }
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
        public static OrderByExpr OrderBy(this IOrderByAnchor source, params OrderByItemExpr[] orderBys)
        {
            if (source is OrderByExpr existingOrderByExpr)
            {
                existingOrderByExpr.OrderBys.AddRange(orderBys);
                return existingOrderByExpr;
            }
            return new OrderByExpr(source as SqlSegment, orderBys);
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
            OrderBy(source, Array.ConvertAll(orderBys, tuple => new OrderByItemExpr(Expr.Prop(tuple.Item1), tuple.Item2)));

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
        public static SectionExpr Section(this ISectionAnchor source, int skip, int take) => new SectionExpr(source as SqlSegment, skip, take);

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
        public static OrderByItemExpr Asc(this ValueTypeExpr expr) => new OrderByItemExpr(expr, true);

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
        public static OrderByItemExpr Desc(this ValueTypeExpr expr) => new OrderByItemExpr(expr, false);

        /// <summary>
        /// 创建 DISTINCT 表达式，用于表示查询结果的唯一性约束，通常用于 SELECT 子句中以去除重复记录。
        /// </summary>
        /// <param name="expr"></param>
        /// <returns></returns>
        public static UnaryExpr Distinct(this ValueTypeExpr expr) => new UnaryExpr(UnaryOperator.Distinct, expr);
        /// <summary>
        /// 创建 COUNT 聚合函数表达式。
        /// </summary>
        /// <param name="expr">要计数的值表达式。</param>
        /// <returns>COUNT 聚合函数表达式。</returns>
        /// <example>
        /// <code>
        /// var countExpr = Expr.Prop("Id").Count();
        /// var distinctCountExpr = Expr.Prop("Name").Count(true);
        /// </code>
        /// </example>
        public static FunctionExpr Count(this ValueTypeExpr expr) => new FunctionExpr("COUNT", expr) { IsAggregate = true };

        /// <summary>
        /// 创建 COUNT 聚合函数表达式（支持去重）。
        /// </summary>
        /// <param name="expr">要计数的值表达式。</param>
        /// <param name="isDistinct">是否去重计数。</param>
        /// <returns>COUNT 聚合函数表达式。</returns>
        public static FunctionExpr Count(this ValueTypeExpr expr, bool isDistinct) => new FunctionExpr("COUNT", isDistinct ? expr.Distinct() : expr) { IsAggregate = true };

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
        public static FunctionExpr Sum(this ValueTypeExpr expr) => new FunctionExpr("SUM", expr) { IsAggregate = true };

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
        public static FunctionExpr Avg(this ValueTypeExpr expr) => new FunctionExpr("AVG", expr) { IsAggregate = true };

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
        public static FunctionExpr Max(this ValueTypeExpr expr) => new FunctionExpr("MAX", expr) { IsAggregate = true };

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
        public static FunctionExpr Min(this ValueTypeExpr expr) => new FunctionExpr("MIN", expr) { IsAggregate = true };

        /// <summary>
        /// 当条件为真时，使用 AND 逻辑将另一个表达式追加到当前逻辑表达式。
        /// </summary>
        /// <param name="left">当前逻辑表达式。</param>
        /// <param name="condition">为 true 时才追加 <paramref name="right"/>。</param>
        /// <param name="right">条件成立时追加的逻辑表达式。</param>
        /// <returns>合并后的逻辑表达式，若条件为 false 则返回原表达式。</returns>
        /// <example>
        /// <code>
        /// var condition = Expr.Prop("Age") > 18
        ///     .AndIf(nameFilter != null, Expr.Prop("Name").Contains(nameFilter));
        /// </code>
        /// </example>
        public static LogicExpr AndIf(this LogicExpr left, bool condition, LogicExpr right) => condition ? left.And(right) : left;

        /// <summary>
        /// 当条件为真时，使用 OR 逻辑将另一个表达式追加到当前逻辑表达式。
        /// </summary>
        /// <param name="left">当前逻辑表达式。</param>
        /// <param name="condition">为 true 时才追加 <paramref name="right"/>。</param>
        /// <param name="right">条件成立时追加的逻辑表达式。</param>
        /// <returns>合并后的逻辑表达式，若条件为 false 则返回原表达式。</returns>
        public static LogicExpr OrIf(this LogicExpr left, bool condition, LogicExpr right) => condition ? left.Or(right) : left;

        /// <summary>
        /// 当条件为真时，为 SQL 语句添加 WHERE 子句；否则直接返回原数据源。
        /// </summary>
        /// <param name="source">SQL 语句构建起点。</param>
        /// <param name="condition">为 true 时才添加 WHERE 子句。</param>
        /// <param name="where">WHERE 子句的逻辑表达式。</param>
        /// <returns>添加了 WHERE 条件的表达式，或原数据源（条件为 false 时）。</returns>
        /// <example>
        /// <code>
        /// var query = table.WhereIf(minAge.HasValue, Expr.Prop("Age") >= minAge ?? 0);
        /// </code>
        /// </example>
        public static ISourceAnchor WhereIf(this ISourceAnchor source, bool condition, LogicExpr where)
            => condition ? (ISourceAnchor)new WhereExpr(source as SqlSegment, where) : source;

        /// <summary>
        /// 将聚合或窗口函数应用到分区窗口（OVER PARTITION BY）。
        /// </summary>
        /// <param name="func">窗口函数表达式（如 SUM、RANK 等）。</param>
        /// <param name="partitionBy">分区字段表达式数组。</param>
        /// <returns>窗口函数表达式。</returns>
        /// <example>
        /// <code>
        /// var windowExpr = Expr.Prop("Salary").Sum().Over(Expr.Prop("DepartmentId"));
        /// </code>
        /// </example>
        public static FunctionExpr Over(this FunctionExpr func, params ValueTypeExpr[] partitionBy) => new FunctionExpr("Over", func, new ValueSet(partitionBy));

        /// <summary>
        /// 将聚合或窗口函数应用到分区和排序窗口（OVER PARTITION BY ... ORDER BY ...）。
        /// </summary>
        /// <param name="func">窗口函数表达式（如 SUM、RANK 等）。</param>
        /// <param name="partitionBy">分区字段表达式数组。</param>
        /// <param name="orderBy">窗口内排序字段数组。</param>
        /// <returns>窗口函数表达式。</returns>
        /// <example>
        /// <code>
        /// var windowExpr = Expr.Prop("Salary").Sum().Over(
        ///     new[] { Expr.Prop("DepartmentId") },
        ///     new[] { Expr.Prop("HireDate").Asc() });
        /// </code>
        /// </example>
        public static FunctionExpr Over(this FunctionExpr func, ValueTypeExpr[] partitionBy, params OrderByItemExpr[] orderBy) => new FunctionExpr("Over", func, new ValueSet(partitionBy), new ValueSet(orderBy));

        /// <summary>
        /// 将聚合或窗口函数应用到带范围/行数限定的窗口（OVER PARTITION BY ... ORDER BY ... ROWS/RANGE BETWEEN ...）。
        /// </summary>
        /// <param name="func">窗口函数表达式（如 SUM、RANK 等）。</param>
        /// <param name="partitionBy">分区字段表达式数组。</param>
        /// <param name="orderBy">窗口内排序字段数组。</param>
        /// <param name="range">true 表示使用 RANGE，false 表示使用 ROWS。</param>
        /// <param name="begin">窗口起始边界：0 当前行，负数向前，正数向后，null 无边界。</param>
        /// <param name="end">窗口结束边界：0 当前行，负数向前，正数向后，null 无边界。</param>
        /// <returns>窗口函数表达式。</returns>
        public static FunctionExpr Over(this FunctionExpr func, ValueTypeExpr[] partitionBy, OrderByItemExpr[] orderBy, bool range, int? begin, int? end) =>
            new FunctionExpr("Over", func, new ValueSet(partitionBy), new ValueSet(orderBy), new FunctionExpr(range ? "RangeBetween" : "RowsBetween", begin, end));
    }
}
