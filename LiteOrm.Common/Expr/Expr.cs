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
        /// 创建外键表达式，用于构建关联表的 EXISTS 查询条件。
        /// </summary>
        /// <param name="type">关联外部实体的类型</param>
        /// <param name="innerExpr">针对关联表的过滤条件表达式。</param>
        /// <returns>外键表达式。</returns>
        public static ForeignExpr Foreign(Type type, LogicExpr innerExpr)
        {
            return new ForeignExpr(type, innerExpr);
        }

        /// <summary>
        /// 创建外键表达式，用于构建关联表的 EXISTS 查询条件。
        /// </summary>
        /// <typeparam name="T">关联外部实体的类型</typeparam>
        /// <param name="innerExpr">针对关联表的过滤条件表达式。</param>
        /// <returns>外键表达式。</returns>
        public static ForeignExpr Foreign<T>(LogicExpr innerExpr)
        {
            return new ForeignExpr(typeof(T), innerExpr);
        }

        /// <summary>
        /// 创建外键表达式，用于构建关联表的 EXISTS 查询条件。
        /// </summary>
        /// <typeparam name="T">关联外部实体的类型</typeparam>
        /// <param name="alias">外部表别名。</param>
        /// <param name="innerExpr">针对关联表的过滤条件表达式。</param>
        /// <returns>外键表达式。</returns>
        public static ForeignExpr Foreign<T>(string alias, LogicExpr innerExpr)
        {
            return new ForeignExpr(typeof(T), alias, innerExpr);
        }

        /// <summary>
        /// 创建外键 EXISTS 表达式，用于检查关联表是否存在满足条件的记录。仅用于 Lambda 表达式中构造 Expr，无实际执行逻辑。
        /// </summary>
        /// <typeparam name="T">关联外部实体的类型</typeparam>
        /// <param name="lambda">针对关联表的过滤条件表达式。</param>
        /// <returns>外键 EXISTS 表达式。</returns>
        public static bool ForeignExists<T>(Expression<Func<T, bool>> lambda)
        {
            return lambda.Compile()(default);
        }

        /// <summary>
        /// 创建一个属性等于值的二元表达式。
        /// </summary>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="value">比较值。</param>
        /// <returns>二元表达式。</returns>
        public static LogicBinaryExpr Prop(string propertyName, object value)
        {
            return new LogicBinaryExpr(new PropertyExpr(propertyName), LogicOperator.Equal, new ValueExpr(value));
        }


        /// <summary>
        /// 创建一个属性等于值的二元表达式。
        /// </summary>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="value">比较值。</param>
        /// <returns>二元表达式。</returns>
        public static LogicBinaryExpr Prop(string propertyName, ValueTypeExpr value)
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
        /// 创建一个 IN 表达式。
        /// </summary>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="values">包含值的集合表达式。</param>
        /// <returns>IN 表达式。</returns>
        public static LogicBinaryExpr In(string propertyName, ValueTypeExpr values)
        {
            return new LogicBinaryExpr(new PropertyExpr(propertyName), LogicOperator.In, values);
        }

        /// <summary>
        /// 从表达式树创建 Lambda 表达式封装。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="expression">Lambda 表达式。</param>
        /// <returns>表达式对象。</returns>
        public static LogicExpr Exp<T>(Expression<Func<T, bool>> expression)
        {
            return new LambdaExprConverter(expression).ToLogicExpr();
        }

        /// <summary>
        /// 创建常量值表达式。
        /// <param name="value"> 参数表示常量值。</param>
        /// <returns>常量值表达式。</returns>
        /// </summary>
        public static ValueExpr Const(object value) => new ValueExpr(value) { IsConst = true };

        /// <summary>
        /// 创建变量值表达式
        /// </summary>
        /// <param name="value"> 参数表示变量值。 </param>
        /// <returns></returns>
        public static ValueExpr Value(object value) => new ValueExpr(value);

        /// <summary>
        /// 创建逻辑与(AND)集合。
        /// </summary>
        public static LogicSet And(params LogicExpr[] exprs) => new LogicSet(LogicJoinType.And, exprs);

        /// <summary>
        /// 创建逻辑或(OR)集合。
        /// </summary>
        public static LogicSet Or(params LogicExpr[] exprs) => new LogicSet(LogicJoinType.Or, exprs);

        /// <summary>
        /// 创建逻辑取反(NOT)表达式。
        /// </summary>
        public static NotExpr Not(LogicExpr expr) => new NotExpr(expr);

        /// <summary>
        /// 创建函数调用表达式。
        /// </summary>
        public static FunctionExpr Func(string name, params ValueTypeExpr[] args) => new FunctionExpr(name, args);

        /// <summary>
        /// 创建聚合函数表达式。
        /// </summary>
        public static AggregateFunctionExpr Aggregate(string name, ValueTypeExpr expression, bool isDistinct = false) => new AggregateFunctionExpr(name, expression, isDistinct);

        /// <summary>
        /// 创建字符串拼接表达式集合 (CONCAT)。
        /// </summary>
        public static ValueSet Concat(params ValueTypeExpr[] exprs) => new ValueSet(ValueJoinType.Concat, exprs);

        /// <summary>
        /// 创建值列表表达式集合 (List)。
        /// </summary>
        public static ValueSet List(params ValueTypeExpr[] exprs) => new ValueSet(ValueJoinType.List, exprs);

        /// <summary>
        /// 创建动态 SQL 表达式。
        /// </summary>
        public static GenericSqlExpr Sql(string key, object arg = null) => GenericSqlExpr.Get(key, arg);

        /// <summary>
        /// 获取静态 SQL 表达式。
        /// </summary>
        public static GenericSqlExpr StaticSql(string key) => GenericSqlExpr.GetStaticSqlExpr(key);

        /// <summary>
        /// 创建 From 表达式。
        /// </summary>
        public static FromExpr From<T>(params string[] tableArgs) => new FromExpr(typeof(T)) { TableArgs = tableArgs };

        /// <summary>
        /// 使用指定的类型创建 From 表达式。
        /// </summary>
        /// <param name="objectType">实体类型</param>
        /// <param name="tableArgs">动态表名参数</param>
        /// <returns>From 表达式实例</returns>
        public static FromExpr From(Type objectType, params string[] tableArgs) => new FromExpr(objectType) { TableArgs = tableArgs };

        /// <summary>
        /// 使用指定的 Lambda 表达式创建 WHERE 条件表达式
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="expression">定义筛选条件的 Lambda 表达式</param>
        /// <returns>WHERE 条件表达式实例</returns>
        public static WhereExpr Where<T>(Expression<Func<T, bool>> expression) => new WhereExpr() { Where = Exp(expression) };

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

        /// <summary>
        /// 创建范围查询表达式 (BETWEEN)。
        /// </summary>
        public static LogicExpr Between(string propertyName, object low, object high) => Prop(propertyName).Between(low, high);
    }
}
