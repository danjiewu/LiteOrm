using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    /// <summary>
    /// 表示一个值常量或一组常量（用于 IN 列表）的语句。
    /// </summary>
    public sealed class ValueStatement : Statement
    {
        /// <summary>
        /// 无参构造。
        /// </summary>
        public ValueStatement()
        {
        }

        /// <summary>
        /// 使用值构造 ValueStatement。
        /// </summary>
        /// <param name="value">值，可以是单个值或可枚举集合（用于 IN）</param>
        public ValueStatement(object value)
        {
            Value = value;
        }

        /// <summary>
        /// 常量值或集合
        /// </summary>
        public object Value { get; set; }

        /// <inheritdoc/>
        /// <remarks>
        /// - 如果值为 null，返回 "NULL"。
        /// - 如果值为集合（且不是字符串），将为集合中每个元素生成参数并返回类似 "( @p0, @p1, ... )" 的字符串，适用于 IN 表达式。
        /// - 否则生成单个参数并返回对应的参数占位符。
        /// </remarks>
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (Value == null) return "NULL";
            else if (Value is IEnumerable enumerable && !(Value is string))
            {
                StringBuilder sb = new StringBuilder();
                foreach (var item in enumerable)
                {
                    if (sb.Length > 0) sb.Append(", ");
                    if (item is Statement s)
                    {
                        sb.Append(s.ToSql(context, sqlBuilder, outputParams));
                    }
                    else
                    {
                        string paramName = outputParams.Count.ToString();
                        outputParams.Add(new(sqlBuilder.ToParamName(paramName), item));
                        sb.Append(sqlBuilder.ToSqlParam(paramName));
                    }
                }
                return $"({sb})";
            }
            else
            {
                string paramName = outputParams.Count.ToString();
                outputParams.Add(new(sqlBuilder.ToParamName(paramName), Value));
                return sqlBuilder.ToSqlParam(paramName);
            }
        }

        public override string ToString()
        {
            if (Value == null) return "NULL";
            else if (Value is IEnumerable enumerable && !(Value is string))
            {
                return $"({String.Join(",", enumerable)})";
            }
            else
                return Value.ToString();
        }

        public override bool Equals(object obj)
        {
            return obj is ValueStatement vs && Equals(Value, vs.Value);
        }

        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), Value?.GetHashCode() ?? 0);
        }
    }
}
