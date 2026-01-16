using System;
using System.Collections;
using System.Collections.Generic;
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
        /// 将值类型隐式转换为值表达式。
        /// </summary>
        /// <param name="value">值类型数值。</param>
        /// <returns>值表达式。</returns>
        public static implicit operator Expr(ValueType value) => new ValueExpr(value);

        /// <summary>
        /// 将字符串隐式转换为值表达式。
        /// </summary>
        /// <param name="value">字符串。</param>
        /// <returns>值表达式。</returns>
        public static implicit operator Expr(string value) => new ValueExpr(value);

        /// <summary>
        /// 表示空值的表达式。
        /// </summary>
        public static readonly ValueExpr Null = new ValueExpr();

        /// <summary>
        /// 指示当前表达式是否为值类型表达式。
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
        /// 创建一个属性等于值的二元表达式。
        /// </summary>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="value">比较值。</param>
        /// <returns>二元表达式。</returns>
        public static BinaryExpr Property(string propertyName, object value)
        {
            return new BinaryExpr(new PropertyExpr(propertyName), BinaryOperator.Equal, new ValueExpr(value));
        }

        /// <summary>
        /// 创建一个指定操作符的二元表达式。
        /// </summary>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="oper">二元操作符。</param>
        /// <param name="value">比较值。</param>
        /// <returns>二元表达式。</returns>
        public static BinaryExpr Property(string propertyName, BinaryOperator oper, object value)
        {
            return new BinaryExpr(new PropertyExpr(propertyName), oper, new ValueExpr(value));
        }

        /// <summary>
        /// 从表达式树创建 Lambda 表达式封装。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="expression">Lambda 表达式。</param>
        /// <returns>表达式对象。</returns>
        public static Expr Exp<T>(Expression<Func<T, bool>> expression)
        {
            return new LambdaExprConverter(expression).ToExpr();
        }

        /// <summary>
        /// 创建相等二元表达式。
        /// </summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>相等二元表达式。</returns>
        public static Expr operator ==(Expr left, Expr right)
        {
            return new BinaryExpr(left, BinaryOperator.Equal, right);
        }

        /// <summary>
        /// 创建不等于二元表达式。
        /// </summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>不等于二元表达式。</returns>
        public static Expr operator !=(Expr left, Expr right)
        {
            return new BinaryExpr(left, BinaryOperator.NotEqual, right);
        }

        /// <summary>
        /// 确定指定的对象是否等于当前对象。
        /// </summary>
        /// <param name="obj">要比较的对象。</param>
        /// <returns>如果相等则为 true。</returns>
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        /// <summary>
        /// 获取当前对象的哈希代码。
        /// </summary>
        /// <returns>哈希代码。</returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// 重载 true 运算符。
        /// </summary>
        /// <param name="a">表达式。</param>
        /// <returns>如果表达式为 null 则返回 true。</returns>
        public static bool operator true(Expr a) => a is null;

        /// <summary>
        /// 重载 false 运算符。
        /// </summary>
        /// <param name="a">表达式。</param>
        /// <returns>始终返回 false。</returns>
        public static bool operator false(Expr a) => false;

        /// <summary>
        /// 逻辑与运算符 &amp;。
        /// </summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>包含逻辑与的表达式集合。</returns>
        public static Expr operator &(Expr left, Expr right)
        {
            if (left is null) return right;
            else if (right is null) return left;
            else return left.And(right);
        }

        /// <summary>
        /// 逻辑或运算符 |。
        /// </summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>包含逻辑或的表达式集合。</returns>
        public static Expr operator |(Expr left, Expr right)
        {
            if (left is null || right is null) return Null;
            return left.Or(right);
        }

        /// <summary>
        /// 加法二元运算符 +。
        /// </summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>加法二元表达式。</returns>
        public static Expr operator +(Expr left, Expr right) => new BinaryExpr(left, BinaryOperator.Add, right);

        /// <summary>
        /// 减法二元运算符 -。
        /// </summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>减法二元表达式。</returns>
        public static Expr operator -(Expr left, Expr right) => new BinaryExpr(left, BinaryOperator.Subtract, right);

        /// <summary>
        /// 乘法二元运算符 *。
        /// </summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>乘法二元表达式。</returns>
        public static Expr operator *(Expr left, Expr right) => new BinaryExpr(left, BinaryOperator.Multiply, right);

        /// <summary>
        /// 除法二元运算符 /。
        /// </summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>除法二元表达式。</returns>
        public static Expr operator /(Expr left, Expr right) => new BinaryExpr(left, BinaryOperator.Divide, right);

        /// <summary>
        /// 大于比较二元运算符 >。
        /// </summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>大于比较二元表达式。</returns>
        public static Expr operator >(Expr left, Expr right) => new BinaryExpr(left, BinaryOperator.GreaterThan, right);

        /// <summary>
        /// 小于比较二元运算符 &lt;。
        /// </summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>小于比较二元表达式。</returns>
        public static Expr operator <(Expr left, Expr right) => new BinaryExpr(left, BinaryOperator.LessThan, right);

        /// <summary>
        /// 大于等于比较二元运算符 &gt;=。
        /// </summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>大于等于比较二元表达式。</returns>
        public static Expr operator >=(Expr left, Expr right) => new BinaryExpr(left, BinaryOperator.GreaterThanOrEqual, right);

        /// <summary>
        /// 小于等于比较二元运算符 &lt;=。
        /// </summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>小于等于比较二元表达式。</returns>
        public static Expr operator <=(Expr left, Expr right) => new BinaryExpr(left, BinaryOperator.LessThanOrEqual, right);

        /// <summary>
        /// 逻辑非运算符 !。
        /// </summary>
        /// <param name="expr">要取反的表达式。</param>
        /// <returns>一个新的表达式，表示指定表达式的逻辑非。</returns>
        public static Expr operator !(Expr expr) => expr?.Not();
    }
    /// <summary>
    /// Expr 类的扩展方法，提供便捷的表达式组合功能。
    /// </summary>
    public static class ExprExtensions
    {
        /// <summary>
        /// 使用 AND 逻辑组合两个表达式。
        /// </summary>
        /// <param name="left">左端查询表达式。</param>
        /// <param name="right">右端查询表达式。</param>
        /// <returns>组合后的表达式集合。</returns>
        public static ExprSet And(this Expr left, Expr right) => Join(left, right, ExprJoinType.And);

        /// <summary>
        /// 使用 OR 逻辑组合两个表达式。
        /// </summary>
        /// <param name="left">左端查询表达式。</param>
        /// <param name="right">右端查询表达式。</param>
        /// <returns>组合后的表达式集合。</returns>
        public static ExprSet Or(this Expr left, Expr right) => Join(left, right, ExprJoinType.Or);

        /// <summary>
        /// 使用 CONCAT 逻辑组合两个表达式。
        /// </summary>
        /// <param name="left">左端查询表达式。</param>
        /// <param name="right">右端查询表达式。</param>
        /// <returns>组合后的表达式集合。</returns>
        public static ExprSet Concat(this Expr left, Expr right) => Join(left, right, ExprJoinType.Concat);

        /// <summary>
        /// 使用指定的连接类型组合两个表达式。
        /// </summary>
        /// <param name="left">左端查询表达式。</param>
        /// <param name="right">右端查询表达式。</param>
        /// <param name="joinType">表达式集合类型，默认为 List。</param>
        /// <returns>组合后的表达式集合。</returns>
        public static ExprSet Join(this Expr left, Expr right, ExprJoinType joinType = ExprJoinType.List) => new ExprSet(joinType, left, right);

        /// <summary>
        /// 取反操作。
        /// </summary>
        /// <param name="expr">要取反的表达式。</param>
        /// <returns>一个新的表达式，表示指定表达式的逻辑非。</returns>
        public static Expr Not(this Expr expr)
        {
            if (expr is BinaryExpr binaryExpr)
                return new BinaryExpr(binaryExpr.Left, binaryExpr.Operator.Opposite(), binaryExpr.Right);
            else
                return new UnaryExpr(UnaryOperator.Not, expr);
        }

        /// <summary>
        /// 创建 In 二元表达式。
        /// </summary>
        /// <param name="expr">要匹配的表达式。</param>
        /// <param name="values">查询值的集合。</param>
        /// <returns>In 二元表达式。</returns>
        public static BinaryExpr In(this Expr expr, IEnumerable values) => new BinaryExpr(expr, BinaryOperator.In, new ValueExpr(values));

        /// <summary>
        /// 创建 Not In 二元表达式。
        /// </summary>
        /// <param name="expr">要匹配的表达式。</param>
        /// <param name="values">查询值的一个数组。</param>
        /// <returns>NotIn 二元表达式。</returns>
        public static BinaryExpr In(this Expr expr, params object[] values) => new BinaryExpr(expr, BinaryOperator.NotIn, new ValueExpr(values));

        /// <summary>
        /// 创建 Like 二元表达式。
        /// </summary>
        /// <param name="expr">要匹配的表达式。</param>
        /// <param name="like">Like 匹配模式。</param>
        /// <returns>Like 二元表达式。</returns>
        public static BinaryExpr Like(this Expr expr, string like) => new BinaryExpr(expr, BinaryOperator.Like, new ValueExpr(like));

        /// <summary>
        /// 正则表达式匹配二元表达式。
        /// </summary>
        /// <param name="expr">要匹配的表达式。</param>
        /// <param name="regex">正则表达式。</param>
        /// <returns>RegexpLike 二元表达式。</returns>
        public static BinaryExpr RegexpLike(this Expr expr, string regex) => new BinaryExpr(expr, BinaryOperator.RegexpLike, new ValueExpr(regex));

        /// <summary>
        /// 包含匹配（LIKE '%str%'）的二元表达式。
        /// </summary>
        /// <param name="expr">要匹配的表达式。</param>
        /// <param name="str">要包含的字符串。</param>
        /// <returns>Contains 二元表达式。</returns>
        public static BinaryExpr Contains(this Expr expr, string str) => new BinaryExpr(expr, BinaryOperator.Contains, new ValueExpr(str));

        /// <summary>
        /// 前缀匹配（LIKE 'str%'）的二元表达式。
        /// </summary>
        /// <param name="expr">要匹配的表达式。</param>
        /// <param name="str">开始的字符串。</param>
        /// <returns>StartsWith 二元表达式。</returns>
        public static BinaryExpr StartsWith(this Expr expr, string str) => new BinaryExpr(expr, BinaryOperator.StartsWith, new ValueExpr(str));

        /// <summary>
        /// 后缀匹配（LIKE '%str'）的二元表达式。
        /// </summary>
        /// <param name="expr">要匹配的表达式。</param>
        /// <param name="str">结尾的字符串。</param>
        /// <returns>EndsWith 二元表达式。</returns>
        public static BinaryExpr EndsWith(this Expr expr, string str) => new BinaryExpr(expr, BinaryOperator.EndsWith, new ValueExpr(str));

        /// <summary>
        /// 使用指定的二元操作符组合两个表达式。
        /// </summary>
        /// <param name="left">左端表达式。</param>
        /// <param name="op">二元操作符。</param>
        /// <param name="right">右端表达式。</param>
        /// <returns>二元表达式。</returns>
        public static BinaryExpr Union(this Expr left, BinaryOperator op, Expr right) => new BinaryExpr(left, op, right);
    }
}
