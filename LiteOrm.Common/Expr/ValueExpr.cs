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
            if (!(obj is ValueExpr vs)) return false;
            return ValuesEquals(Value, vs.Value);
        }

        internal static bool ValuesEquals(object val1, object val2)
        {
            if (val1 == null) return val2 == null;
            if (val2 == null) return false;

            if (val1.Equals(val2)) return true;

            // 处理数值比较（例如 int 和 long 的比较）
            if (IsNumeric(val1) && IsNumeric(val2))
            {
                try
                {
                    return Convert.ToDecimal(val1) == Convert.ToDecimal(val2);
                }
                catch
                {
                    return Convert.ToDouble(val1) == Convert.ToDouble(val2);
                }
            }

            // 处理集合相等（用于 IN 查询）
            if (val1 is IEnumerable valSeq && val2 is IEnumerable objSeq && !(val1 is string) && !(val2 is string))
            {
                var enum1 = valSeq.GetEnumerator();
                var enum2 = objSeq.GetEnumerator();
                while (true)
                {
                    bool next1 = enum1.MoveNext();
                    bool next2 = enum2.MoveNext();
                    if (next1 != next2) return false;
                    if (!next1) return true;
                    if (!ValuesEquals(enum1.Current, enum2.Current)) return false;
                }
            }

            return false;
        }

        internal static bool IsNumeric(object value)
        {
            return value is sbyte || value is byte || value is short || value is ushort ||
                   value is int || value is uint || value is long || value is ulong ||
                   value is float || value is double || value is decimal;
        }

        /// <summary>
        /// 生成哈希值。
        /// </summary>
        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), GetValueHashCode(Value));
        }

        internal static int GetValueHashCode(object val)
        {
            if (val == null) return 0;

            if (IsNumeric(val))
            {
                try { return Convert.ToDecimal(val).GetHashCode(); }
                catch { return Convert.ToDouble(val).GetHashCode(); }
            }

            if (val is IEnumerable enumerable && !(val is string))
            {
                int h = 0;
                foreach (var item in enumerable)
                {
                    h = (h * HashSeed) + GetValueHashCode(item);
                }
                return h;
            }

            return val.GetHashCode();
        }
    }
}
