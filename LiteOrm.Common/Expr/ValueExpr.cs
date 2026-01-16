using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示一个值（或一组常量值，如 IN 列表）的表达式。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class ValueExpr : Expr
    {
        /// <summary>
        /// 无参构造。
        /// </summary>
        public ValueExpr()
        {
        }

        /// <summary>
        /// 使用值构造 ValueExpr。
        /// </summary>
        /// <param name="value">值，可以是单个值或可枚举集合（用于 IN）</param>
        public ValueExpr(object value)
        {
            Value = value;
        }

        /// <summary>
        /// 表示这是一个值类型表达式。
        /// </summary>
        [JsonIgnore]
        public override bool IsValue => true;

        /// <summary>
        /// 表达式包含的实际值。
        /// </summary>
        public object Value { get; set; }

        /// <inheritdoc/>
        /// <remarks>
        /// - ?? null "NULL"。
        /// - 如果值为集合（且不是字符串），返回类似 "( val1, val2, ... )" 的字符串，适用于 IN 表达式。
        /// - 否则返回值的字符串表示形式。
        /// </remarks>
        public override string ToString()
        {
            if (Value is null) return "NULL";
            else if (Value is IEnumerable enumerable && !(Value is string))
            {
                StringBuilder sb = new StringBuilder(); 
                foreach (var item in enumerable)
                {
                    if (sb.Length > 0) sb.Append(",");
                    sb.Append(item?.ToString() ?? "NULL");
                }
                return $"({sb})";
            }
            else
                return Value.ToString();
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is ValueExpr vs && Equals(Value, vs.Value);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), Value?.GetHashCode() ?? 0);
        }
    }
}
