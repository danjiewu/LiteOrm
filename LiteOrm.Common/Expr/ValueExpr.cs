using System;
using System.Collections;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示一个常量值或一组集合值（用于 IN 查询）的表达式。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class ValueExpr : ValueTypeExpr
    {
        /// <summary>
        /// 将字符串隐式转换为值表达式。
        /// </summary>
        /// <param name="value">字符串值。</param>
        /// <returns>值表达式实例。</returns>
        public static implicit operator ValueExpr(string value) => new ValueExpr(value);

        /// <summary>
        /// 将整数隐式转换为值表达式。
        /// </summary>
        /// <param name="value">整数值。</param>
        /// <returns>值表达式实例。</returns>
        public static implicit operator ValueExpr(int value) => new ValueExpr(value);

        /// <summary>
        /// 将长整数隐式转换为值表达式。
        /// </summary>
        /// <param name="value">长整数值。</param>
        /// <returns>值表达式实例。</returns>
        public static implicit operator ValueExpr(long value) => new ValueExpr(value);

        /// <summary>
        /// 将布尔值隐式转换为值表达式。
        /// </summary>
        /// <param name="value">布尔值。</param>
        /// <returns>值表达式实例。</returns>
        public static implicit operator ValueExpr(bool value) => new ValueExpr(value);

        /// <summary>
        /// 将日期时间隐式转换为值表达式。
        /// </summary>
        /// <param name="value">日期时间值。</param>
        /// <returns>值表达式实例。</returns>
        public static implicit operator ValueExpr(DateTime value) => new ValueExpr(value);

        /// <summary>
        /// 将双精度浮点数隐式转换为值表达式。
        /// </summary>
        /// <param name="value">双精度浮点数值。</param>
        /// <returns>值表达式实例。</returns>
        public static implicit operator ValueExpr(double value) => new ValueExpr(value);

        /// <summary>
        /// 将十进制数隐式转换为值表达式。
        /// </summary>
        /// <param name="value">十进制数值。</param>
        /// <returns>值表达式实例。</returns>
        public static implicit operator ValueExpr(decimal value) => new ValueExpr(value);
        /// <summary>
        /// 创建空的 ValueExpr。
        /// </summary>
        public ValueExpr()
        {
        }

        /// <summary>
        /// 使用指定的值初始化 ValueExpr。
        /// </summary>
        /// <param name="value">具体的值（如数字、字符串、日期）或可枚举集合（用于 IN 子句）。</param>
        public ValueExpr(object value)
        {
            Value = value;
        }

        /// <summary>
        /// 获取或设置该表达式持有的原始对象值。
        /// </summary>
        public new object Value { get; set; }

        /// <summary>
        /// 指示该值是否为常量。
        /// 常量值在 SQL 生成时会直接嵌入 SQL 语句中，而非作为参数。
        /// </summary>
        public bool IsConst { get; set; }

        /// <summary>
        /// 返回当前值的预览字符串。
        /// </summary>
        /// <returns>
        /// - 值为 null 时返回 "NULL"。
        /// - 值为集合（非字符串）时返回类似 "(v1, v2, v3)" 的格式。
        /// - 否则返回值的字符串形式。
        /// </returns>
        public override string ToString()
        {
            if (Value is null) return "NULL";
            else if (Value is IEnumerable enumerable && !(Value is string))
            {
                Span<char> initialBuffer = stackalloc char[128];
                var sb = new ValueStringBuilder(initialBuffer);
                foreach (var item in enumerable)
                {
                    if (sb.Length > 0) sb.Append(",");
                    sb.Append(item?.ToString() ?? "NULL");
                }
                string result = $"({sb.ToString()})";
                sb.Dispose();
                return result;
            }
            else
                return Value.ToString();
        }

        /// <summary>
        /// 确定值是否相等。
        /// </summary>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj is not ValueExpr vs) return false;
            return ValueEquality.ValueEquals(Value, vs.Value);
        }

        /// <summary>
        /// 生成哈希值。
        /// </summary>
        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), ValueEquality.GetValueHashCode(Value));
        }
    }
}

