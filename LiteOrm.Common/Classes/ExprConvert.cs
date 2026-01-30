using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 从字符串生成条件，条件转换为字符串，以及判定对象是否符合条件等操作的静态类。
    /// </summary>
    public static class ExprConvert
    {
        /// <summary>
        /// 将属性和字符串转换为简单查询条件。
        /// </summary>
        /// <param name="property">属性描述符。</param>
        /// <param name="text">表示查询语句的字符串，可以使用 "="、"&lt;"、"&gt;"、"!"、"%"、"*"、"&lt;="、"&gt;=" 为起始字符表示条件符号。</param>
        /// <returns>二元表达式查询条件。</returns>
        public static LogicBinaryExpr Parse(PropertyDescriptor property, string text)
        {
            if (String.IsNullOrEmpty(text)) return Expr.Property(property.Name, null);
            if (text.Length > 1)
            {
                switch (text.Substring(0, 2))
                {
                    case "<=":
                        return Expr.Property(property.Name, LogicBinaryOperator.LessThanOrEqual, ParseValue(property, text.Substring(2)));
                    case ">=":
                        return Expr.Property(property.Name, LogicBinaryOperator.GreaterThanOrEqual, ParseValue(property, text.Substring(2)));
                }
            }
            LogicBinaryOperator mask = LogicBinaryOperator.Equal;
            if (text.Length > 0 && text[0] == '!')
            {
                mask = LogicBinaryOperator.Not;
                text = text.Substring(1);
            }

            if (text.Length > 0)
            {
                switch (text[0])
                {
                    case '=':
                        return Expr.Property(property.Name, LogicBinaryOperator.Equal | mask, ParseValue(property, text.Substring(1)));
                    case '>':
                        return Expr.Property(property.Name, LogicBinaryOperator.GreaterThan | mask, ParseValue(property, text.Substring(1)));
                    case '<':
                        return Expr.Property(property.Name, LogicBinaryOperator.LessThan | mask, ParseValue(property, text.Substring(1)));
                    case '%':
                        return Expr.Property(property.Name, LogicBinaryOperator.Contains | mask, text.Substring(1).Trim());
                    case '*':
                        return Expr.Property(property.Name, LogicBinaryOperator.Like | mask, text.Substring(1).Trim());
                    case '$':
                        return Expr.Property(property.Name, LogicBinaryOperator.RegexpLike | mask, text.Substring(1).Trim());
                }
            }
            else
            {
                return Expr.Property(property.Name, LogicBinaryOperator.Equal | mask, null);
            }
            if (text.IndexOf(',') >= 0)
            {
                List<object> values = new List<object>();
                foreach (string value in text.Split(','))
                {
                    values.Add(ParseValue(property, value));
                }
                return Expr.Property(property.Name, LogicBinaryOperator.In | mask, values.ToArray());
            }
            return Expr.Property(property.Name, LogicBinaryOperator.Equal | mask, ParseValue(property, text));
        }

        /// <summary>
        /// 字符串转化为对应属性类型的值。
        /// </summary>
        /// <param name="property">属性描述符。</param>
        /// <param name="value">输入字符串。</param>
        /// <returns>可被属性接受的值。</returns>
        private static object ParseValue(PropertyDescriptor property, string value)
        {
            if (String.IsNullOrEmpty(value)) return null;
            if (property.PropertyType == typeof(string)) return value;
            value = value.Trim();
            if (value.Length == 0) return null;
            Type type = property.PropertyType.GetUnderlyingType();
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
                if ("YT1是对".IndexOf(ch) >= 0) return true;
                else if ("NF0否非错".IndexOf(ch) >= 0) return false;
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
        /// 根据条件生成用于解析的字符串。
        /// </summary>
        /// <param name="op">条件操作符。</param>
        /// <param name="value">用于比较的值。</param>
        /// <returns>生成的字符串表示形式。</returns>
        public static string ToText(LogicBinaryOperator op, object value)
        {
            switch (op)
            {
                case LogicBinaryOperator.Equal:
                    string str = ToText(value);
                    if (String.IsNullOrEmpty(str) || "!<>=*%$".IndexOf(str[0]) >= 0 || str.IndexOf(',') >= 0)
                        return '=' + str;
                    else
                        return str;
                case LogicBinaryOperator.NotEqual:
                    str = ToText(value);
                    if (!String.IsNullOrEmpty(str) && ("<>=*%$".IndexOf(str[0]) >= 0 || str.IndexOf(',') >= 0))
                        return "!=" + str;
                    else
                        return "!" + str;
                case LogicBinaryOperator.In:
                    List<string> values = new List<string>();
                    foreach (object o in value as IEnumerable)
                    {
                        values.Add(ToText(o));
                    }
                    return ToText(String.Join(",", values), "!<>=*%$".ToCharArray());
                case LogicBinaryOperator.NotIn:
                    values = new List<string>();
                    foreach (object o in value as IEnumerable)
                    {
                        values.Add(ToText(o));
                    }
                    return "!" + ToText(String.Join(",", values), "<>=*%$".ToCharArray());
                case LogicBinaryOperator.GreaterThan: return ">" + ToText(value, '=');
                case LogicBinaryOperator.GreaterThanOrEqual: return ">=" + ToText(value);
                case LogicBinaryOperator.LessThan: return "<" + ToText(value, '=');
                case LogicBinaryOperator.LessThanOrEqual: return "<=" + ToText(value);
                case LogicBinaryOperator.Like: return "*" + ToText(value);
                case LogicBinaryOperator.NotLike: return "!*" + ToText(value);
                case LogicBinaryOperator.Contains: return "%" + ToText(value);
                case LogicBinaryOperator.NotContains: return "!%" + ToText(value);
                case LogicBinaryOperator.RegexpLike: return "$" + ToText(value);
                case LogicBinaryOperator.NotRegexpLike: return "!$" + ToText(value);
                default: return ToText(value, "!<>=*%$".ToCharArray());
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
