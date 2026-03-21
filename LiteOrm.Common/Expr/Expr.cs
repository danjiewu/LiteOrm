using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json.Serialization;


namespace LiteOrm.Common
{
    /// <summary>
    /// 查询表达式基类。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public abstract class Expr
    {
        internal const int HashSeed = 31;
        /// <summary>
        /// 验证 SQL 名称是否合法，允许 null 或空字符串，但如果非空则必须仅包含字母、数字或下划线，否则抛出 ArgumentException。
        /// </summary>
        /// <param name="paramName">参数名称，用于异常消息中指示哪个参数无效。</param>
        /// <param name="sqlName">待验证的 SQL 名称。</param>
        /// <exception cref="ArgumentException"></exception>
        public static void ThrowIfInvalidSqlName(string paramName, string sqlName)
        {
            if (!string.IsNullOrEmpty(sqlName) && !Constants.ValidNameRegex.IsMatch(sqlName))
            {
                throw new ArgumentException($"Name '{sqlName}' contains invalid characters, only letters, numbers, and underscores are allowed", paramName);
            }
        }
        /// <summary>
        /// 表示 SQL NULL 的表达式。
        /// </summary>
        public static readonly ValueExpr Null = new();

        /// <summary>
        /// 将多个哈希值组合成一个组合哈希值。
        /// </summary>
        /// <param name="hashcodes">要组合的哈希值序列。</param>
        /// <returns>组合后的哈希值。</returns>
        protected static int OrderedHashCodes(params int[] hashcodes)
        {
            unchecked
            {
                int hashcode = 17;
                foreach (int hc in hashcodes)
                {
                    hashcode = (hashcode * 31) + hc;
                }
                return hashcode;
            }
        }

        /// <summary>
        /// 计算序列的组合哈希值。
        /// </summary>
        /// <typeparam name="T">元素类型。</typeparam>
        /// <param name="items">序列项。</param>
        /// <returns>序列哈希值。</returns>
        protected static int SequenceHash<T>(IEnumerable<T> items)
        {
            if (items == null) return 0;
            unchecked
            {
                int hashcode = 19;
                foreach (var item in items)
                {
                    hashcode = (hashcode * 31) + (item?.GetHashCode() ?? 0);
                }
                return hashcode;
            }
        }

        /// <summary>
        /// 创建属性表达式。
        /// </summary>
        /// <param name="propertyName">属性名称。</param>
        /// <returns>属性表达式。</returns>
        public static PropertyExpr Prop(string propertyName)
        {
            return new PropertyExpr(propertyName);
        }

        /// <summary>
        /// 创建属性表达式。
        /// </summary>
        /// <param name="tableAlias">表别名。</param>
        /// <param name="propertyName">属性名称。</param>
        /// <returns>属性表达式。</returns>
        public static PropertyExpr Prop(string tableAlias, string propertyName)
        {
            var prop = new PropertyExpr(propertyName);
            prop.TableAlias = tableAlias;
            return prop;
        }

        /// <summary>
        /// 创建外键表达式，用于构建关联表的 EXISTS 查询条件。
        /// </summary>
        /// <param name="type">关联外部实体的类型</param>
        /// <param name="innerExpr">针对关联表的过滤条件表达式。</param>
        /// <param name="tableArgs">动态表名参数。</param>
        /// <returns>外键关联查询表达式。</returns>
        public static ForeignExpr Exists(Type type, LogicExpr innerExpr, params string[] tableArgs)
        {
            return new ForeignExpr(type, innerExpr);
        }

        /// <summary>
        /// 创建外键表达式，用于构建关联表的 EXISTS 查询条件，支持分表。
        /// </summary>
        /// <typeparam name="T">关联外部实体的类型</typeparam>
        /// <param name="innerExpr">针对关联表的过滤条件表达式。</param>
        /// <param name="tableArgs">动态表名参数。</param>
        /// <returns>外键关联查询表达式。</returns>
        public static ForeignExpr Exists<T>(LogicExpr innerExpr, params string[] tableArgs)
        {
            return new ForeignExpr(typeof(T), innerExpr, tableArgs);
        }

        /// <summary>
        /// 创建外键表达式，用于构建关联表的 EXISTS 查询条件，支持分表，并自动根据当前查询上下文推断关联关系。
        /// </summary>
        /// <typeparam name="T">关联外部实体的类型。</typeparam>
        /// <param name="innerExpr">针对关联表的过滤条件表达式。</param>
        /// <param name="tableArgs">动态表名参数。</param>
        /// <returns>外键关联查询表达式。</returns>
        public static ForeignExpr ExistsRelated<T>(LogicExpr innerExpr, params string[] tableArgs)
        {
            return new ForeignExpr(typeof(T), innerExpr, tableArgs) { AutoRelated = true };
        }

        /// <summary>
        /// 创建外键表达式，用于构建关联表的 EXISTS 查询条件，支持分表，并自动根据当前查询上下文推断关联关系。
        /// </summary>
        /// <param name="type">关联外部实体的类型。</param>
        /// <param name="innerExpr">针对关联表的过滤条件表达式。</param>
        /// <param name="tableArgs">动态表名参数。</param>
        /// <returns>外键关联查询表达式。</returns>
        public static ForeignExpr ExistsRelated(Type type, LogicExpr innerExpr, params string[] tableArgs)
        {
            return new ForeignExpr(type, innerExpr, tableArgs) { AutoRelated = true };
        }

        /// <summary>
        /// 创建外键 EXISTS 表达式，用于检查关联表是否存在满足条件的记录。仅用于 Lambda 表达式中构造 Expr，无实际执行逻辑。
        /// </summary>
        /// <typeparam name="T">关联外部实体的类型</typeparam>
        /// <param name="lambda">针对关联表的过滤条件表达式。</param>
        /// <returns>外键 EXISTS 表达式。</returns>
        public static bool Exists<T>(Expression<Func<T, bool>> lambda)
        {
            throw new InvalidOperationException("The Expr.Exists method is only used for parsing lambda expressions and cannot be called directly.");
        }

        /// <summary>
        /// 创建一个属性等于值的二元表达式。
        /// </summary>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="value">比较值。</param>
        /// <returns>二元表达式。</returns>
        public static LogicBinaryExpr PropEqual(string propertyName, object value)
        {
            return new LogicBinaryExpr(new PropertyExpr(propertyName), LogicOperator.Equal, new ValueExpr(value));
        }


        /// <summary>
        /// 创建一个属性等于值的二元表达式。
        /// </summary>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="value">比较值。</param>
        /// <returns>二元表达式。</returns>
        public static LogicBinaryExpr PropEqual(string propertyName, ValueTypeExpr value)
        {
            return new LogicBinaryExpr(new PropertyExpr(propertyName), LogicOperator.Equal, value);
        }

        /// <summary>
        /// 创建一个指定操作符的二元表达式。
        /// </summary>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="oper">二元操作符。</param>
        /// <param name="value">比较值。</param>
        /// <returns>二元表达式。</returns>
        public static LogicBinaryExpr Prop(string propertyName, LogicOperator oper, object value)
        {
            return new LogicBinaryExpr(new PropertyExpr(propertyName), oper, new ValueExpr(value));
        }

        /// <summary>
        /// 创建一个指定操作符的二元表达式。
        /// </summary>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="oper">二元操作符。</param>
        /// <param name="value">表示比较值的表达式。</param>
        /// <returns>二元表达式。</returns>
        public static LogicBinaryExpr Prop(string propertyName, LogicOperator oper, ValueTypeExpr value)
        {
            return new LogicBinaryExpr(new PropertyExpr(propertyName), oper, value);
        }

        /// <summary>
        /// 创建一个 IN 表达式。
        /// </summary>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="values">包含值的集合。</param>
        /// <returns>IN 表达式。</returns>
        public static LogicBinaryExpr In(string propertyName, IEnumerable values)
        {
            return new LogicBinaryExpr(new PropertyExpr(propertyName), LogicOperator.In, new ValueExpr(values));
        }

        /// <summary>
        /// 创建一个 BETWEEN 表达式， 通过组合&gt;= low和&lt;= high表示属性值在指定的下限和上限之间（包含边界）。
        /// </summary>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="low">下限值表达式。</param>
        /// <param name="high">上限值表达式。</param>
        /// <returns>BETWEEN 表达式。</returns>
        public static LogicExpr Between(string propertyName, ValueTypeExpr low, ValueTypeExpr high)
        {
            var propExpr = new PropertyExpr(propertyName);
            return propExpr >= low & propExpr <= high;
        }

        /// <summary>
        /// 从表达式树创建 Lambda 表达式封装。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="expression">Lambda 表达式。</param>
        /// <returns>表达式对象。</returns>
        public static LogicExpr Lambda<T>(Expression<Func<T, bool>> expression)
        {
            return new LambdaExprConverter(expression).ToLogicExpr();
        }

        /// <summary>
        /// 创建常量值表达式（不生成参数，直接内嵌局到 SQL 中）。
        /// </summary>
        /// <param name="value">常量值。</param>
        /// <returns>常量值表达式。</returns>
        public static ValueExpr Const(object value) => new ValueExpr(value) { IsConst = true };


        /// <summary>
        /// 创建指定实体类型的 UPDATE 表达式。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="tableArgs">动态表名参数</param>
        /// <returns>UPDATE 表达式。</returns>
        public static UpdateExpr Update<T>(params string[] tableArgs)
        {
            return new UpdateExpr(From<T>(tableArgs));
        }

        /// <summary>
        /// 创建指定类型的 UPDATE 表达式。
        /// </summary>
        /// <param name="objectType">实体类型。</param>
        /// <param name="tableArgs">动态表名参数</param>
        /// <returns>UPDATE 表达式。</returns>
        public static UpdateExpr Update(Type objectType, params string[] tableArgs)
        {
            return new UpdateExpr(From(objectType, tableArgs));
        }

        /// <summary>
        /// 创建指定实体类型的 DELETE 表达式。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="tableArgs">动态表名参数</param>
        /// <returns>DELETE 表达式。</returns>
        public static DeleteExpr Delete<T>(params string[] tableArgs)
        {
            return new DeleteExpr(From<T>(tableArgs));
        }

        /// <summary>
        /// 创建指定类型的 DELETE 表达式。
        /// </summary>
        /// <param name="objectType">实体类型。</param>
        /// <param name="tableArgs">动态表名参数</param>
        /// <returns>DELETE 表达式。</returns>
        public static DeleteExpr Delete(Type objectType, params string[] tableArgs)
        {
            return new DeleteExpr(From(objectType, tableArgs));
        }

        /// <summary>
        /// 创建变量值表达式（生成参数化查询占位符）。
        /// </summary>
        /// <param name="value">变量值。</param>
        /// <returns>变量值表达式。</returns>
        public static ValueExpr Value(object value) => new ValueExpr(value);

        /// <summary>
        /// 创建逻辑与(AND)集合。
        /// </summary>
        /// <param name="exprs">要用 AND 连接的逻辑表达式数组。</param>
        /// <returns>使用 AND 连接的逻辑集合。</returns>
        public static LogicSet And(params LogicExpr[] exprs) => new LogicSet(LogicJoinType.And, exprs);

        /// <summary>
        /// 创建逻辑与(AND)集合。
        /// </summary>
        /// <param name="exprs">要用 AND 连接的逻辑表达式集合。</param>
        /// <returns>使用 AND 连接的逻辑集合。</returns>
        public static LogicSet And(IEnumerable<LogicExpr> exprs) => new LogicSet(LogicJoinType.And, exprs);

        /// <summary>
        /// 创建逻辑或(OR)集合。
        /// </summary>
        /// <param name="exprs">要用 OR 连接的逻辑表达式数组。</param>
        /// <returns>使用 OR 连接的逻辑集合。</returns>
        public static LogicSet Or(params LogicExpr[] exprs) => new LogicSet(LogicJoinType.Or, exprs);

        /// <summary>
        /// 创建逻辑或(OR)集合。
        /// </summary>
        /// <param name="exprs">要用 OR 连接的逻辑表达式集合。</param>
        /// <returns>使用 OR 连接的逻辑集合。</returns>
        public static LogicSet Or(IEnumerable<LogicExpr> exprs) => new LogicSet(LogicJoinType.Or, exprs);

        /// <summary>
        /// 创建逻辑取反(NOT)表达式。
        /// </summary>
        /// <param name="expr">要取反的逻辑表达式。</param>
        /// <returns>逻辑取反表达式。</returns>
        public static NotExpr Not(LogicExpr expr) => new NotExpr(expr);

        /// <summary>
        /// 创建函数调用表达式。
        /// </summary>
        /// <param name="name">函数名称。</param>
        /// <param name="args">函数参数列表。</param>
        /// <returns>函数调用表达式。</returns>
        public static FunctionExpr Func(string name, params ValueTypeExpr[] args) => new FunctionExpr(name, args);

        /// <summary>
        /// 创建 COALESCE 函数表达式，返回参数列表中第一个非 NULL 的值。
        /// </summary>
        /// <param name="args">候选值表达式列表。</param>
        /// <returns>COALESCE 函数表达式。</returns>
        public static FunctionExpr Coalesce(params ValueTypeExpr[] args) => new FunctionExpr("COALESCE", args);

        /// <summary>
        /// 创建 IfNull 函数表达式，当 <paramref name="expr"/> 为 NULL 时返回 <paramref name="defaultValue"/>。
        /// </summary>
        /// <param name="expr">待检测的值表达式。</param>
        /// <param name="defaultValue">为 NULL 时的替代值表达式。</param>
        /// <returns>IfNull 函数表达式。</returns>
        public static FunctionExpr IfNull(ValueTypeExpr expr, ValueTypeExpr defaultValue) => new FunctionExpr("IfNull", expr, defaultValue);

        /// <summary>
        /// 创建简单条件表达式，等价于 CASE WHEN <paramref name="condition"/> THEN <paramref name="thenExpr"/> ELSE <paramref name="elseExpr"/> END。
        /// </summary>
        /// <param name="condition">条件表达式。</param>
        /// <param name="thenExpr">条件为真时的结果表达式。</param>
        /// <param name="elseExpr">条件为假时的结果表达式，可选。</param>
        /// <returns>CASE WHEN 函数表达式。</returns>
        public static FunctionExpr If(LogicExpr condition, ValueTypeExpr thenExpr, ValueTypeExpr elseExpr = null)
            => Case(new[] { new KeyValuePair<LogicExpr, ValueTypeExpr>(condition, thenExpr) }, elseExpr);

        /// <summary>
        /// 创建获取当前时间戳的函数表达式（CURRENT_TIMESTAMP）。
        /// </summary>
        /// <returns>Now 函数表达式。</returns>
        public static FunctionExpr Now() => new FunctionExpr("Now");

        /// <summary>
        /// 创建获取当前日期的函数表达式（CURRENT_DATE）。
        /// </summary>
        /// <returns>Today 函数表达式。</returns>
        public static FunctionExpr Today() => new FunctionExpr("Today");

        /// <summary>
        /// 创建字符串转小写函数表达式（LOWER）。
        /// </summary>
        /// <param name="expr">目标值表达式。</param>
        /// <returns>ToLower 函数表达式。</returns>
        public static FunctionExpr Lower(ValueTypeExpr expr) => new FunctionExpr("ToLower", expr);

        /// <summary>
        /// 创建获取字符串长度函数表达式（LENGTH）。
        /// </summary>
        /// <param name="expr">目标值表达式。</param>
        /// <returns>LENGTH 函数表达式。</returns>
        public static FunctionExpr Length(ValueTypeExpr expr) => new FunctionExpr("LENGTH", expr);

        /// <summary>
        /// CASE 表达式构造器，接受一个条件-结果对的集合和一个可选的 ELSE 结果表达式，生成一个表示 CASE 语句的函数表达式。SqlBuilder 会将其转换为正确的 SQL CASE 语法。
        /// </summary>
        /// <param name="cases">条件-结果对的集合。</param>
        /// <param name="elseExpr">可选的 ELSE 结果表达式。</param>
        /// <returns>表示 CASE 语句的函数表达式。</returns>
        public static FunctionExpr Case(IEnumerable<KeyValuePair<LogicExpr, ValueTypeExpr>> cases, ValueTypeExpr elseExpr = null)
        {
            FunctionExpr caseExpr = new FunctionExpr("CASE");
            foreach (var kv in cases)
            {
                caseExpr.Args.Add(kv.Key.AsValue());
                caseExpr.Args.Add(kv.Value);
            }
            if (elseExpr is not null)
            {
                caseExpr.Args.Add(elseExpr);
            }
            return caseExpr;
        }

        /// <summary>
        /// 分区窗口函数
        /// </summary>
        /// <param name="func">窗口函数表达式，必须是Sum、Rank、Lead、Lag等窗口函数</param>
        /// <param name="partitionBy">分区字段表达式数组</param>
        /// <returns>窗口函数表达式</returns>

        public static FunctionExpr Over(FunctionExpr func, ValueTypeExpr[] partitionBy)
        {
            return new FunctionExpr("Over", func, new ValueSet(partitionBy));
        }

        /// <summary>
        /// 窗口函数，支持分区和排序
        /// </summary>
        /// <param name="func">窗口函数表达式，必须是Sum、Rank、Lead、Lag等窗口函数</param>
        /// <param name="partitionBy">分区字段表达式数组</param>
        /// <param name="orderBy">排序字段表达式数组</param>
        /// <returns>窗口函数表达式</returns>
        public static FunctionExpr Over(FunctionExpr func, ValueTypeExpr[] partitionBy, OrderByItemExpr[] orderBy)
        {
            return new FunctionExpr("Over", func, new ValueSet(partitionBy), new ValueSet(orderBy));
        }

        /// <summary>
        /// 窗口函数,支持分区、排序和范围/行数限定
        /// </summary>
        /// <param name="func">窗口函数表达式，必须是Sum、Rank、Lead、Lag等窗口函数</param>
        /// <param name="partitionBy">分区字段表达式数组</param>
        /// <param name="orderBy">排序字段表达式数组</param>
        /// <param name="range">是否使用范围限定，true 表示使用 RANGE，false 表示使用 ROWS</param>
        /// <param name="begin">范围或行数的起始值，0 表示当前行，负数表示之前的行数/范围，正数表示之后的行数/范围，null 表示所有行</param>
        /// <param name="end">范围或行数的结束值，0 表示当前行，负数表示之前的行数/范围，正数表示之后的行数/范围，null 表示所有行</param>
        /// <returns>窗口函数表达式</returns>
        public static FunctionExpr Over(FunctionExpr func, ValueTypeExpr[] partitionBy, OrderByItemExpr[] orderBy, bool range, int? begin, int? end)
        {
            FunctionExpr betweenExpr = new FunctionExpr(range ? "RangeBetween" : "RowsBetween", begin, end);
            return new FunctionExpr("Over", func, new ValueSet(partitionBy), new ValueSet(orderBy), betweenExpr);
        }

        /// <summary>
        /// 创建聚合函数表达式（如 COUNT、SUM、AVG 等）。
        /// </summary>
        /// <param name="name">聚合函数名称。</param>
        /// <param name="expression">聚合操作的目标表达式。</param>
        /// <param name="isDistinct">是否对目标表达式去重，默认为 false。</param>
        /// <returns>聚合函数表达式。</returns>
        public static FunctionExpr Aggregate(string name, ValueTypeExpr expression, bool isDistinct = false) => new FunctionExpr(name, isDistinct ? expression.Distinct() : expression) { IsAggregate = true };

        /// <summary>
        /// 创建 COUNT(1) 聚合函数表达式。
        /// </summary>
        /// <returns>COUNT(1) 聚合函数表达式。</returns>
        public static FunctionExpr Count(ValueTypeExpr expression = null, bool isDistinct = false) => Aggregate("Count", expression ?? Expr.Const(1), isDistinct);

        /// <summary>
        /// 创建 SUM 聚合函数表达式。
        /// </summary>
        /// <param name="expression">目标值表达式。</param>
        /// <returns>SUM 聚合函数表达式。</returns>
        public static FunctionExpr Sum(ValueTypeExpr expression) => Aggregate("SUM", expression);

        /// <summary>
        /// 创建 AVG 聚合函数表达式。
        /// </summary>
        /// <param name="expression">目标值表达式。</param>
        /// <returns>AVG 聚合函数表达式。</returns>
        public static FunctionExpr Avg(ValueTypeExpr expression) => Aggregate("AVG", expression);

        /// <summary>
        /// 创建 MAX 聚合函数表达式。
        /// </summary>
        /// <param name="expression">目标值表达式。</param>
        /// <returns>MAX 聚合函数表达式。</returns>
        public static FunctionExpr Max(ValueTypeExpr expression) => Aggregate("MAX", expression);

        /// <summary>
        /// 创建 MIN 聚合函数表达式。
        /// </summary>
        /// <param name="expression">目标值表达式。</param>
        /// <returns>MIN 聚合函数表达式。</returns>
        public static FunctionExpr Min(ValueTypeExpr expression) => Aggregate("MIN", expression);

        /// <summary>
        /// 创建字符串拼接表达式集合（CONCAT）。
        /// </summary>
        /// <param name="exprs">要拼接的值表达式数组。</param>
        /// <returns>字符串拼接值集合表达式。</returns>
        public static ValueSet Concat(params ValueTypeExpr[] exprs) => new ValueSet(ValueJoinType.Concat, exprs);

        /// <summary>
        /// 创建值列表表达式集合，通常用于 IN 查询。
        /// </summary>
        /// <param name="exprs">列表中的值表达式数组。</param>
        /// <returns>值列表集合表达式。</returns>
        public static ValueSet List(params ValueTypeExpr[] exprs) => new ValueSet(ValueJoinType.List, exprs);

        /// <summary>
        /// 创建动态 SQL 表达式（支持运行时替换或参数化局内值）。
        /// </summary>
        /// <param name="key">SQL 片段键名或模板文本。</param>
        /// <param name="arg">动态替换参数，为 null 时不替换。</param>
        /// <returns>动态 SQL 表达式。</returns>
        public static GenericSqlExpr Sql(string key, object arg = null) => GenericSqlExpr.Get(key, arg);

        /// <summary>
        /// 获取静态 SQL 表达式（SQL 片段在构建时不会被替换）。
        /// </summary>
        /// <param name="key">SQL 片段键名或文本。</param>
        /// <returns>静态 SQL 表达式。</returns>
        public static GenericSqlExpr StaticSql(string key) => GenericSqlExpr.Get(key);

        /// <summary>
        /// 创建 From 表达式，支持动态表名参数。
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="tableArgs">动态表名参数</param>
        /// <returns>From 表达式实例</returns>
        public static FromExpr From<T>(params string[] tableArgs) => new FromExpr(typeof(T)) { TableArgs = tableArgs };

        /// <summary>
        /// 使用指定的类型创建 From 表达式。
        /// </summary>
        /// <param name="objectType">实体类型</param>
        /// <param name="tableArgs">动态表名参数</param>
        /// <returns>From 表达式实例</returns>
        public static FromExpr From(Type objectType, params string[] tableArgs) => new FromExpr(objectType) { TableArgs = tableArgs };

        /// <summary>
        /// 使用 IQueryable 形式的 Lambda 表达式创建 SQL 片段
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="expression">定义查询的 IQueryable Lambda 表达式</param>
        /// <returns>SQL 片段实例</returns>
        public static Expr Query<T>(Expression<Func<IQueryable<T>, IQueryable<T>>> expression) => LambdaExprConverter.ToSqlSegment(expression);

        /// <summary>
        /// 使用 IQueryable 形式的 Lambda 表达式创建 SQL 片段（带返回值）
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="expression">定义查询的 IQueryable Lambda 表达式</param>
        /// <returns>SQL 片段实例</returns>
        public static Expr Query<T, TResult>(Expression<Func<IQueryable<T>, TResult>> expression) => LambdaExprConverter.ToSqlSegment(expression);
    }
}
