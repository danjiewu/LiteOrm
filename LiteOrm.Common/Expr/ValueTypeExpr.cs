using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示值类型的表达式基类。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public abstract class ValueTypeExpr : Expr
    {
        /// <summary>
        /// 将字符串隐式转换为值表达式。
        /// </summary>
        /// <param name="value">字符串值。</param>
        /// <returns>值表达式实例。</returns>
        public static implicit operator ValueTypeExpr(string value) => new ValueExpr(value);
        public static implicit operator ValueTypeExpr(int value) => new ValueExpr(value);
        public static implicit operator ValueTypeExpr(long value) => new ValueExpr(value);
        public static implicit operator ValueTypeExpr(bool value) => new ValueExpr(value);
        public static implicit operator ValueTypeExpr(DateTime value) => new ValueExpr(value);
        public static implicit operator ValueTypeExpr(double value) => new ValueExpr(value);
        public static implicit operator ValueTypeExpr(decimal value) => new ValueExpr(value);

        /// <summary>
        /// 创建相等二元表达式。
        /// </summary>
        public static LogicExpr operator ==(ValueTypeExpr left, ValueTypeExpr right)
        {
            return new LogicBinaryExpr(left, LogicBinaryOperator.Equal, right);
        }

        /// <summary>
        /// 创建不等于二元表达式。
        /// </summary>
        public static LogicExpr operator !=(ValueTypeExpr left, ValueTypeExpr right)
        {
            return new LogicBinaryExpr(left, LogicBinaryOperator.NotEqual, right);
        }

        /// <summary>
        /// 大于比较二元运算符 >。
        /// </summary>
        public static LogicExpr operator >(ValueTypeExpr left, ValueTypeExpr right) => new LogicBinaryExpr(left, LogicBinaryOperator.GreaterThan, right);

        /// <summary>
        /// 小于比较二元运算符 &lt;。
        /// </summary>
        public static LogicExpr operator <(ValueTypeExpr left, ValueTypeExpr right) => new LogicBinaryExpr(left, LogicBinaryOperator.LessThan, right);

        /// <summary>
        /// 大于等于比较二元运算符 &gt;=。
        /// </summary>
        public static LogicExpr operator >=(ValueTypeExpr left, ValueTypeExpr right) => new LogicBinaryExpr(left, LogicBinaryOperator.GreaterThanOrEqual, right);

        /// <summary>
        /// 小于等于比较二元运算符 &lt;=。
        /// </summary>
        public static LogicExpr operator <=(ValueTypeExpr left, ValueTypeExpr right) => new LogicBinaryExpr(left, LogicBinaryOperator.LessThanOrEqual, right);

        /// <summary>
        /// 加法二元运算符 +。
        /// </summary>
        public static ValueTypeExpr operator +(ValueTypeExpr left, ValueTypeExpr right) => new ValueBinaryExpr(left, ValueBinaryOperator.Add, right);

        /// <summary>
        /// 减法二元运算符 -。
        /// </summary>
        public static ValueTypeExpr operator -(ValueTypeExpr left, ValueTypeExpr right) => new ValueBinaryExpr(left, ValueBinaryOperator.Subtract, right);

        /// <summary>
        /// 乘法二元运算符 *。
        /// </summary>
        public static ValueTypeExpr operator *(ValueTypeExpr left, ValueTypeExpr right) => new ValueBinaryExpr(left, ValueBinaryOperator.Multiply, right);

        /// <summary>
        /// 除法二元运算符 /。
        /// </summary>
        public static ValueTypeExpr operator /(ValueTypeExpr left, ValueTypeExpr right) => new ValueBinaryExpr(left, ValueBinaryOperator.Divide, right);

        /// <summary>
        /// 一元负号运算符 - 的重载。
        /// </summary>
        public static ValueTypeExpr operator -(ValueTypeExpr expr) => new ValueUnaryExpr(ValueUnaryOperator.Nagive, expr);

        /// <summary>
        /// 按位取反运算符 ~ 的重载。
        /// </summary>
        public static ValueTypeExpr operator ~(ValueTypeExpr expr) => new ValueUnaryExpr(ValueUnaryOperator.BitwiseNot, expr);


        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
