using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;


namespace MyOrm.Common
{
    /// <summary>
    /// 抽象表达式基类。子类应实现 <see cref="ToSql"/> 将表达式转换为 SQL 片段并把所需参数写入。
    /// </summary>
    public abstract class Expr
    {
        /// <summary>
        /// 从值类型到表达式的隐式转换
        /// </summary>
        /// <param name="value">值类型值</param>
        /// <returns>值表达式</returns>
        public static implicit operator Expr(ValueType value) => new ValueExpr(value);

        /// <summary>
        /// 表示空值的表达式
        /// </summary>
        public static readonly ValueExpr Null = new ValueExpr();

        /// <summary>
        /// 用于哈希计算的种子值。
        /// </summary>
        protected const int HashSeed = 31;

        /// <summary>
        /// 对一组哈希值进行排序后的组合哈希计算。
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
        /// 将当前表达式转换为 SQL 字符串片段。
        /// </summary>
        /// <param name="context">生成 SQL 所需的上下文（表定义、别名等）。</param>
        /// <param name="sqlBuilder">提供数据库特定 SQL 生成辅助的方法。</param>
        /// <param name="outputParams">输出参数集合，方法应在此集合中添加本表达式所需的参数（键为参数名，值为参数值）。</param>
        /// <returns>表示本表达式的 SQL 字符串片段（不包含外层分号）。</returns>
        public abstract string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams);
        /// <summary>
        /// 创建一个属性表达式
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        /// <returns>属性表达式</returns>
        public static PropertyExpr Property(string propertyName)
        {
            return new PropertyExpr(propertyName);
        }

        /// <summary>
        /// 创建一个属性等于值的二元表达式
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        /// <param name="value">属性值</param>
        /// <returns>二元表达式</returns>
        public static BinaryExpr Property(string propertyName, object value)
        {
            return new BinaryExpr()
            {
                Left = new PropertyExpr(propertyName),
                Right = new ValueExpr(value)
            };
        }

        /// <summary>
        /// 创建一个属性与值的二元表达式
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        /// <param name="oper">二元操作符</param>
        /// <param name="value">属性值</param>
        /// <returns>二元表达式</returns>
        public static BinaryExpr Property(string propertyName, BinaryOperator oper, object value)
        {
            return new BinaryExpr()
            {
                Left = new PropertyExpr(propertyName),
                Operator = oper,
                Right = new ValueExpr(value)
            };
        }

        /// <summary>
        /// 从表达式创建 Lambda 表达式语句
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="expression">Lambda表达式</param>
        /// <returns>表达式语句</returns>
        public static LamdaExpr<T> Exp<T>(Expression<Func<T, bool>> expression)
        {
            return new LamdaExpr<T>(expression);
        }

        /// <summary>
        /// 创建相等二元表达式
        /// </summary>
        /// <param name="left">左值</param>
        /// <param name="right">右值</param>
        /// <returns>相等二元表达式</returns>
        public static Expr operator ==(Expr left, Expr right)
        {
            return new BinaryExpr(left, BinaryOperator.Equal, right);
        }

        /// <summary>
        /// 创建不等于二元表达式
        /// </summary>
        /// <param name="left">左值</param>
        /// <param name="right">右值</param>
        /// <returns>不等于二元表达式</returns>
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
        /// 重载true运算符
        /// </summary>
        /// <param name="a">表达式</param>
        /// <returns>如果表达式为null则返回true</returns>
        public static bool operator true(Expr a) => a is null;

        /// <summary>
        /// 重载false运算符
        /// </summary>
        /// <param name="a">表达式</param>
        /// <returns>总是返回false</returns>
        public static bool operator false(Expr a) => false;

        /// <summary>
        /// 重载与运算符 &amp;
        /// </summary>
        /// <param name="left">左操作数</param>
        /// <param name="right">右操作数</param>
        /// <returns>两个表达式的逻辑与组合</returns>
        public static Expr operator &(Expr left, Expr right)
        {
            if (left == null) return right;
            else if (right == null) return left;
            else
                return left.And(right);
        }

        /// <summary>
        /// 重载或运算符|
        /// </summary>
        /// <param name="left">左操作数</param>
        /// <param name="right">右操作数</param>
        /// <returns>两个表达式的逻辑或组合</returns>
        public static Expr operator |(Expr left, Expr right)
        {
            if (left == null || right == null) return Null;
            return left.Or(right);
        }

        /// <summary>
        /// 重载加法运算符+
        /// </summary>
        /// <param name="left">左操作数</param>
        /// <param name="right">右操作数</param>
        /// <returns>加法二元表达式</returns>
        public static Expr operator +(Expr left, Expr right) => new BinaryExpr(left, BinaryOperator.Add, right);

        /// <summary>
        /// 重载减法运算符-
        /// </summary>
        /// <param name="left">左操作数</param>
        /// <param name="right">右操作数</param>
        /// <returns>减法二元表达式</returns>
        public static Expr operator -(Expr left, Expr right) => new BinaryExpr(left, BinaryOperator.Subtract, right);

        /// <summary>
        /// 重载乘法运算符*
        /// </summary>
        /// <param name="left">左操作数</param>
        /// <param name="right">右操作数</param>
        /// <returns>乘法二元表达式</returns>
        public static Expr operator *(Expr left, Expr right) => new BinaryExpr(left, BinaryOperator.Multiply, right);

        /// <summary>
        /// 重载除法运算符/
        /// </summary>
        /// <param name="left">左操作数</param>
        /// <param name="right">右操作数</param>
        /// <returns>除法二元表达式</returns>
        public static Expr operator /(Expr left, Expr right) => new BinaryExpr(left, BinaryOperator.Divide, right);

        /// <summary>
        /// 重载大于运算符>
        /// </summary>
        /// <param name="left">左操作数</param>
        /// <param name="right">右操作数</param>
        /// <returns>大于比较二元表达式</returns>
        public static Expr operator >(Expr left, Expr right) => new BinaryExpr(left, BinaryOperator.GreaterThan, right);

        /// <summary>
        /// 重载小于运算符 &lt;
        /// </summary>
        /// <param name="left">左操作数</param>
        /// <param name="right">右操作数</param>
        /// <returns>小于比较二元表达式</returns>
        public static Expr operator <(Expr left, Expr right) => new BinaryExpr(left, BinaryOperator.LessThan, right);

        /// <summary>
        /// 重载大于等于运算符 &gt;=
        /// </summary>
        /// <param name="left">左操作数</param>
        /// <param name="right">右操作数</param>
        /// <returns>大于等于比较二元表达式</returns>
        public static Expr operator >=(Expr left, Expr right) => new BinaryExpr(left, BinaryOperator.GreaterThanOrEqual, right);

        /// <summary>
        /// 重载小于等于运算符 &lt;=
        /// </summary>
        /// <param name="left">左操作数</param>
        /// <param name="right">右操作数</param>
        /// <returns>小于等于比较二元表达式</returns>
        public static Expr operator <=(Expr left, Expr right) => new BinaryExpr(left, BinaryOperator.LessThanOrEqual, right);

        /// <summary>
        /// 重载逻辑非运算符!
        /// </summary>
        /// <param name="expr">要取反的表达式</param>
        /// <returns>一个新的表达式，表示指定表达式的逻辑非</returns>
        public static Expr operator !(Expr expr) => expr?.Not();
    }
    /// <summary>
    /// Expr类的扩展方法，提供便捷的表达式组合操作
    /// </summary>
    public static class ExprExt
    {
        /// <summary>
        /// 使用AND逻辑运算符组合两个表达式
        /// </summary>
        /// <param name="left">左操作数表达式</param>
        /// <param name="right">右操作数表达式</param>
        /// <returns>组合后的表达式集合</returns>
        public static ExprSet And(this Expr left, Expr right) => Join(left, right, ExprJoinType.And);

        /// <summary>
        /// 使用OR逻辑运算符组合两个表达式
        /// </summary>
        /// <param name="left">左操作数表达式</param>
        /// <param name="right">右操作数表达式</param>
        /// <returns>组合后的表达式集合</returns>
        public static ExprSet Or(this Expr left, Expr right) => Join(left, right, ExprJoinType.Or);

        /// <summary>
        /// 使用CONCAT运算符组合两个表达式
        /// </summary>
        /// <param name="left">左操作数表达式</param>
        /// <param name="right">右操作数表达式</param>
        /// <returns>组合后的表达式集合</returns>
        public static ExprSet Concat(this Expr left, Expr right) => Join(left, right, ExprJoinType.Concat);

        /// <summary>
        /// 使用指定的连接类型组合两个表达式
        /// </summary>
        /// <param name="left">左操作数表达式</param>
        /// <param name="right">右操作数表达式</param>
        /// <param name="joinType">表达式连接类型，默认为Default</param>
        /// <returns>组合后的表达式集合</returns>
        public static ExprSet Join(this Expr left, Expr right, ExprJoinType joinType = ExprJoinType.Default) => new ExprSet(joinType, left, right);

        /// <summary>
        /// 取反操作
        /// </summary>
        /// <param name="expr">要取反的表达式</param>
        /// <returns>一个新的表达式，表示指定表达式的逻辑非</returns>
        public static Expr Not(this Expr expr)
        {
            if (expr is BinaryExpr binaryExpr)
                return new BinaryExpr(binaryExpr.Left, binaryExpr.Operator.Opposite(), binaryExpr.Right);
            else
                return new UnaryExpr(UnaryOperator.Not, expr);
        }

        /// <summary>
        /// 创建 In 二元表达式
        /// </summary>
        /// <param name="expr">要检查的表达式</param>
        /// <param name="values">包含值的集合</param>
        /// <returns>In 二元表达式</returns>
        public static BinaryExpr In(this Expr expr, IEnumerable values) => new BinaryExpr(expr, BinaryOperator.In, new ValueExpr(values));

        /// <summary>
        /// 创建 Not In 二元表达式
        /// </summary>
        /// <param name="expr">要检查的表达式</param>
        /// <param name="values">包含值的一组对象</param>
        /// <returns>NotIn 二元表达式</returns>
        public static BinaryExpr In(this Expr expr, params object[] values) => new BinaryExpr(expr, BinaryOperator.NotIn, new ValueExpr(values));

        /// <summary>
        /// 创建 Like 二元表达式
        /// </summary>
        /// <param name="expr">要检查的表达式</param>
        /// <param name="like">Like 匹配模式</param>
        /// <returns>Like 二元表达式</returns>
        public static BinaryExpr Like(this Expr expr, string like) => new BinaryExpr(expr, BinaryOperator.Like, new ValueExpr(like));

        /// <summary>
        /// 创建正则表达式匹配二元表达式
        /// </summary>
        /// <param name="expr">要检查的表达式</param>
        /// <param name="regex">正则表达式</param>
        /// <returns>RegexpLike 二元表达式</returns>
        public static BinaryExpr RegexpLike(this Expr expr, string regex) => new BinaryExpr(expr, BinaryOperator.RegexpLike, new ValueExpr(regex));

        /// <summary>
        /// 创建包含匹配（LIKE '%str%'）二元表达式
        /// </summary>
        /// <param name="expr">要检查的表达式</param>
        /// <param name="str">包含的子字符串</param>
        /// <returns>Contains 二元表达式</returns>
        public static BinaryExpr Contains(this Expr expr, string str) => new BinaryExpr(expr, BinaryOperator.Contains, new ValueExpr(str));

        /// <summary>
        /// 创建起始匹配（LIKE 'str%'）二元表达式
        /// </summary>
        /// <param name="expr">要检查的表达式</param>
        /// <param name="str">起始子字符串</param>
        /// <returns>StartsWith 二元表达式</returns>
        public static BinaryExpr StartsWith(this Expr expr, string str) => new BinaryExpr(expr, BinaryOperator.StartsWith, new ValueExpr(str));

        /// <summary>
        /// 创建结尾匹配（LIKE '%str'）二元表达式
        /// </summary>
        /// <param name="expr">要检查的表达式</param>
        /// <param name="str">结尾子字符串</param>
        /// <returns>EndsWith 二元表达式</returns>
        public static BinaryExpr EndsWith(this Expr expr, string str) => new BinaryExpr(expr, BinaryOperator.EndsWith, new ValueExpr(str));

        /// <summary>
        /// 使用指定的二元操作符连接两个表达式
        /// </summary>
        /// <param name="left">左端表达式</param>
        /// <param name="op">二元操作符</param>
        /// <param name="right">右端表达式</param>
        /// <returns>二元表达式</returns>
        public static BinaryExpr Union(this Expr left, BinaryOperator op, Expr right) => new BinaryExpr(left, op, right);

    }
}
