using System;
using System.Collections.Generic;
using System.Text;
using MyOrm.Common;
using System.ComponentModel;
using System.Collections;
using System.Globalization;

namespace MyOrm.Common
{
    /// <summary>
    /// 从字符串生成条件，条件转换为字符串，以及判定对象是否符合条件等操作的静态类
    /// </summary>
    public static class StatementConvert
    {
        /// <summary>
        /// 将属性和字符串转换为简单查询条件
        /// </summary>
        /// <param name="property">属性</param>
        /// <param name="text">表示查询语句的字符串,可以使用"=","&lt;","&gt;","!","%","*","&lt;=","&gt;="为起始字符表示条件符号 </param>
        /// <returns>简单查询条件</returns>
        public static BinaryStatement Parse(PropertyDescriptor property, string text)
        {
            if (text == null) return Statement.Property(property.Name, null);
            if (text.Length > 1)
            {
                switch (text.Substring(0, 2))
                {
                    case "<=":
                        return Statement.Property(property.Name, BinaryOperator.LessThanOrEqual, ParseValue(property, text.Substring(2)));
                    case ">=":
                        return Statement.Property(property.Name, BinaryOperator.GreaterThanOrEqual, ParseValue(property, text.Substring(2)));
                }
            }
            BinaryOperator mask = BinaryOperator.Equal;
            if (text.Length > 0 && text[0] == '!')
            {
                mask = BinaryOperator.Not;
                text = text.Substring(1);
            }

            if (text.Length > 0)
            {
                switch (text[0])
                {
                    case '=':
                        return Statement.Property(property.Name, BinaryOperator.Equal | mask, ParseValue(property, text.Substring(1)));
                    case '>':
                        return Statement.Property(property.Name, BinaryOperator.GreaterThan | mask, ParseValue(property, text.Substring(1)));
                    case '<':
                        return Statement.Property(property.Name, BinaryOperator.LessThan | mask, ParseValue(property, text.Substring(1)));
                    case '%':
                        return Statement.Property(property.Name, BinaryOperator.Contains | mask, text.Substring(1).Trim());
                    case '*':
                        return Statement.Property(property.Name, BinaryOperator.Like | mask, text.Substring(1).Trim());
                    case '$':
                        return Statement.Property(property.Name, BinaryOperator.RegexpLike | mask, text.Substring(1).Trim());
                }
            }
            else
            {
                return Statement.Property(property.Name, BinaryOperator.Equal | mask, null);
            }
            if (text.IndexOf(',') >= 0)
            {
                List<object> values = new List<object>();
                foreach (string value in text.Split(','))
                {
                    values.Add(ParseValue(property, value));
                }
                return Statement.Property(property.Name, BinaryOperator.In | mask, values.ToArray());
            }
            return Statement.Property(property.Name, BinaryOperator.Equal | mask, ParseValue(property, text));
        }

        /// <summary>
        /// 字符串转化为对应属性类型的值
        /// </summary>
        /// <param name="property">属性定义</param>
        /// <param name="value">输入字符串</param>
        /// <returns>可被属性接受的值</returns>
        private static object ParseValue(PropertyDescriptor property, string value)
        {
            if (String.IsNullOrEmpty(value)) return null;
            if (property.PropertyType == typeof(string)) return value;
            value = value.Trim();
            if (value.Length == 0) return null;
            Type type = property.PropertyType;
            if (Nullable.GetUnderlyingType(type) != null) type = Nullable.GetUnderlyingType(type);
            if (type.IsEnum)
            {
                if (Int32.TryParse(value, out int i)) return Enum.ToObject(type, i);
                else
                {
                    return Util.Parse(type, value) ?? Enum.Parse(type, value);
                }
            }
            else if (type == typeof(bool))
            {
                char ch = char.ToUpper(value[0]);
                if ("YT1是对".IndexOf(ch) > 0) return true;
                else if ("NF0否非错".IndexOf(ch) > 0) return false;
            }
            else if (type == typeof(DateTime))
            {
                DateTime result;
                if (DateTime.TryParse(value, out result) || DateTime.TryParseExact(value, new string[] { "y", "D", "d", "G", "g", "F", "f", "s", "yyyy-MM-dd", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss" }, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out result))
                    return result;
            }
            return Convert.ChangeType(value, type);
        }

        /// <summary>
        /// 根据条件生成用于解析的字符串
        /// </summary>
        /// <param name="op">条件类型</param>
        /// <param name="opposite">是否为非</param>
        /// <param name="value">用于比较的值</param>
        /// <returns></returns>
        public static string ToText(BinaryOperator op, bool opposite, object value)
        {
            switch (op)
            {
                case BinaryOperator.In:
                    List<string> values = new List<string>();
                    foreach (object o in value as IEnumerable)
                    {
                        values.Add(ToText(o));
                    }
                    string str = String.Join(",", values.ToArray());
                    return opposite ? "!" + ToText(str, "<>=*%".ToCharArray()) : ToText(str, "!<>=*%".ToCharArray());
                case BinaryOperator.GreaterThan: return opposite ? "<=" + ToText(value) : ">" + ToText(value, '=');
                case BinaryOperator.LessThan: return opposite ? ">=" + ToText(value) : "<" + ToText(value, '=');
                case BinaryOperator.Like: return (opposite ? "!*" : "*") + ToText(value);
                case BinaryOperator.Contains: return (opposite ? "!%" : "%") + ToText(value);
                case BinaryOperator.RegexpLike: return (opposite ? "!$" : "$") + ToText(value);
                case BinaryOperator.Equal:
                    str = ToText(value);
                    if (value != null && (str == String.Empty || "<>=*%$".IndexOf(str[0]) >= 0 || (str[0] == '!' && !opposite) || str.IndexOf(',') >= 0))
                        str = '=' + str;
                    return (opposite ? "!" : "") + str;
                default: return (opposite ? "!" : "") + ToText(value, "!<>=*%$".ToCharArray());
            }
        }

        private static string ToText(object value, params char[] escapeChars)
        {
            if (value is Enum) return ((int)value).ToString();
            else if (value is bool) return (bool)value ? "1" : "0";
            string text = Convert.ToString(value);
            if (String.IsNullOrEmpty(text)) return text;
            if (Array.IndexOf(escapeChars, text[0]) >= 0) return ' ' + text;
            else return text;
        }
    }

    /// <summary>
    /// 条件判定结果
    /// </summary>
    public enum EnsureResult
    {
        /// <summary>
        /// 不满足条件
        /// </summary>
        False,
        /// <summary>
        /// 满足条件
        /// </summary>
        True,
        /// <summary>
        /// 无法确定
        /// </summary>
        Undetermined
    }
}
