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
    /// 表示一个常量值或一组集合值（用于 IN 查询）的表达式。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class ValueExpr : Expr
    {
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
        /// 指示该表达式代表一个值。
        /// </summary>
        [JsonIgnore]
        public override bool IsValue => true;

        /// <summary>
        /// 获取或设置该表达式持有的原始对象值。
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// 
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

