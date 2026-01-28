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
        /// 将值类型（如 int, DateTime, bool）隐式转换为值表达式。
        /// </summary>
        /// <param name="value">值类型数值。</param>
        /// <returns>值表达式实例。</returns>
        public static implicit operator Expr(ValueType value) => new ValueExpr(value);

        /// <summary>
        /// 将字符串隐式转换为值表达式。
        /// </summary>
        /// <param name="value">字符串值。</param>
        /// <returns>值表达式实例。</returns>
        public static implicit operator Expr(string value) => new ValueExpr(value);

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
        /// <param name="foreignPropertyName">当前实体中具有 ForeignType 特性的外键属性名称。</param>
        /// <param name="innerExpr">针对关联表的过滤条件表达式。</param>
        /// <returns>外键表达式。</returns>
        public static ForeignExpr Foreign(string foreignPropertyName, Expr innerExpr)
        {
            return new ForeignExpr(foreignPropertyName, innerExpr);
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
        /// 创建一个 IN 表达式。
        /// </summary>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="values">包含值的集合。</param>
        /// <returns>IN 表达式。</returns>
        public static Expr In(string propertyName, IEnumerable values)
        {
            return new BinaryExpr(new PropertyExpr(propertyName), BinaryOperator.In, new ValueExpr(values));
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
        /// <returns>二者内容逻辑相等则为 true。</returns>
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        /// <summary>
        /// 获取当前对象的哈希代码。
        /// </summary>
        /// <returns>基于内容生成的哈希代码。</returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// 逻辑与运算符 &amp; 的重载。
        /// 允许使用 expr1 &amp; expr2 构建复合条件。
        /// </summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>组合后的 AND 表达式。</returns>
        public static Expr operator &(Expr left, Expr right)
        {
            if (left is null) return right;
            else if (right is null) return left;
            else return left.And(right);
        }

        /// <summary>
        /// 逻辑或运算符 | 的重载。
        /// 允许使用 expr1 | expr2 构建复合条件。
        /// </summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>组合后的 OR 表达式。</returns>
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
        /// 逻辑非运算符 ! 的重载。
        /// </summary>
        /// <param name="expr">要取反的表达式。</param>
        /// <returns>逻辑取反后的表达式。</returns>
        public static Expr operator !(Expr expr) => expr?.Not();
    }
}
