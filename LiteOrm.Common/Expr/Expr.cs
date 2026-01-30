using System;
using System.Collections;
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
        /// <summary>
        /// 表示 SQL NULL 的表达式。
        /// </summary>
        public static readonly ValueExpr Null = new ValueExpr();

        /// <summary>
        /// 指示当前表达式是否代表一个具体的值（而非谓词/条件）。
        /// </summary>
        [JsonIgnore]
        public virtual bool IsValue => false;

        /// <summary>
        /// 用于哈希计算的种子值。
        /// </summary>
        protected const int HashSeed = 31;

        /// <summary>
        /// 将多个哈希值组合成一个组合哈希值。
        /// </summary>
        /// <param name="hashcodes">要组合的哈希值序列。</param>
        /// <returns>组合后的哈希值。</returns>
        protected static int OrderedHashCodes(params int[] hashcodes)
        {
            unchecked
            {
                int hashcode = 0;
                foreach (int hc in hashcodes)
                {
                    hashcode = (hashcode * HashSeed) + hc;
                }
                return hashcode;
            }
        }

        /// <summary>
        /// 创建属性表达式。
        /// </summary>
        /// <param name="propertyName">属性名称。</param>
        /// <returns>属性表达式。</returns>
        public static PropertyExpr Property(string propertyName)
        {
            return new PropertyExpr(propertyName);
        }

        /// <summary>
        /// 创建外键表达式，用于构建关联表的 EXISTS 查询条件。
        /// </summary>
        /// <param name="foreign">关联外部实体的别名</param>
        /// <param name="innerExpr">针对关联表的过滤条件表达式。</param>
        /// <returns>外键表达式。</returns>
        public static ForeignExpr Foreign(string foreign, LogicExpr innerExpr)
        {
            return new ForeignExpr(foreign, innerExpr);
        }

        /// <summary>
        /// 创建一个属性等于值的二元表达式。
        /// </summary>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="value">比较值。</param>
        /// <returns>二元表达式。</returns>
        public static LogicBinaryExpr Property(string propertyName, object value)
        {
            return new LogicBinaryExpr(new PropertyExpr(propertyName), LogicOperator.Equal, new ValueExpr(value));
        }

        /// <summary>
        /// 创建一个指定操作符的二元表达式。
        /// </summary>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="oper">二元操作符。</param>
        /// <param name="value">比较值。</param>
        /// <returns>二元表达式。</returns>
        public static LogicBinaryExpr Property(string propertyName, LogicOperator oper, object value)
        {
            return new LogicBinaryExpr(new PropertyExpr(propertyName), oper, new ValueExpr(value));
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
        /// 从表达式树创建 Lambda 表达式封装。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="expression">Lambda 表达式。</param>
        /// <returns>表达式对象。</returns>
        public static LogicExpr Exp<T>(Expression<Func<T, bool>> expression)
        {
            return new LambdaExprConverter(expression).ToExpr();
        }
    }
}
