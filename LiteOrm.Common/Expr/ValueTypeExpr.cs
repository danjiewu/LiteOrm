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

        /// <summary>
        /// 将整数隐式转换为值表达式。
        /// </summary>
        /// <param name="value">整数值。</param>
        /// <returns>值表达式实例。</returns>
        public static implicit operator ValueTypeExpr(int value) => new ValueExpr(value);

        /// <summary>
        /// 将长整数隐式转换为值表达式。
        /// </summary>
        /// <param name="value">长整数值。</param>
        /// <returns>值表达式实例。</returns>
        public static implicit operator ValueTypeExpr(long value) => new ValueExpr(value);

        /// <summary>
        /// 将布尔值隐式转换为值表达式。
        /// </summary>
        /// <param name="value">布尔值。</param>
        /// <returns>值表达式实例。</returns>
        public static implicit operator ValueTypeExpr(bool value) => new ValueExpr(value);

        /// <summary>
        /// 将日期时间隐式转换为值表达式。
        /// </summary>
        /// <param name="value">日期时间值。</param>
        /// <returns>值表达式实例。</returns>
        public static implicit operator ValueTypeExpr(DateTime value) => new ValueExpr(value);

        /// <summary>
        /// 将双精度浮点数隐式转换为值表达式。
        /// </summary>
        /// <param name="value">双精度浮点数值。</param>
        /// <returns>值表达式实例。</returns>
        public static implicit operator ValueTypeExpr(double value) => new ValueExpr(value);

        /// <summary>
        /// 将十进制数隐式转换为值表达式。
        /// </summary>
        /// <param name="value">十进制数值。</param>
        /// <returns>值表达式实例。</returns>
        public static implicit operator ValueTypeExpr(decimal value) => new ValueExpr(value);

        /// <summary>
        /// 创建相等二元表达式。
        /// </summary>
        public static LogicExpr operator ==(ValueTypeExpr left, ValueTypeExpr right)
        {
            return new LogicBinaryExpr(left, LogicOperator.Equal, right);
        }

        /// <summary>
        /// 创建不等于二元表达式。
        /// </summary>
        public static LogicExpr operator !=(ValueTypeExpr left, ValueTypeExpr right)
        {
            return new LogicBinaryExpr(left, LogicOperator.NotEqual, right);
        }

        /// <summary>
        /// 大于比较二元运算符 >。
        /// </summary>
        public static LogicExpr operator >(ValueTypeExpr left, ValueTypeExpr right) => new LogicBinaryExpr(left, LogicOperator.GreaterThan, right);

        /// <summary>
        /// 小于比较二元运算符 &lt;。
        /// </summary>
        public static LogicExpr operator <(ValueTypeExpr left, ValueTypeExpr right) => new LogicBinaryExpr(left, LogicOperator.LessThan, right);

        /// <summary>
        /// 大于等于比较二元运算符 &gt;=。
        /// </summary>
        public static LogicExpr operator >=(ValueTypeExpr left, ValueTypeExpr right) => new LogicBinaryExpr(left, LogicOperator.GreaterThanOrEqual, right);

        /// <summary>
        /// 小于等于比较二元运算符 &lt;=。
        /// </summary>
        public static LogicExpr operator <=(ValueTypeExpr left, ValueTypeExpr right) => new LogicBinaryExpr(left, LogicOperator.LessThanOrEqual, right);

        /// <summary>
        /// 加法二元运算符 +。
        /// </summary>
        public static ValueTypeExpr operator +(ValueTypeExpr left, ValueTypeExpr right) => new ValueBinaryExpr(left, ValueOperator.Add, right);

        /// <summary>
        /// 减法二元运算符 -。
        /// </summary>
        public static ValueTypeExpr operator -(ValueTypeExpr left, ValueTypeExpr right) => new ValueBinaryExpr(left, ValueOperator.Subtract, right);

        /// <summary>
        /// 乘法二元运算符 *。
        /// </summary>
        public static ValueTypeExpr operator *(ValueTypeExpr left, ValueTypeExpr right) => new ValueBinaryExpr(left, ValueOperator.Multiply, right);

        /// <summary>
        /// 除法二元运算符 /。
        /// </summary>
        public static ValueTypeExpr operator /(ValueTypeExpr left, ValueTypeExpr right) => new ValueBinaryExpr(left, ValueOperator.Divide, right);

        /// <summary>
        /// 一元负号运算符 - 的重载。
        /// </summary>
        public static ValueTypeExpr operator -(ValueTypeExpr expr) => new UnaryExpr(UnaryOperator.Nagive, expr);

        /// <summary>
        /// 按位取反运算符 ~ 的重载。
        /// </summary>
        public static ValueTypeExpr operator ~(ValueTypeExpr expr) => new UnaryExpr(UnaryOperator.BitwiseNot, expr);
    }
}
