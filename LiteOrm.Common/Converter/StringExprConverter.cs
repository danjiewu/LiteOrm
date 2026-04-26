using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace LiteOrm.Common
{
    /// <summary>
    /// 从字符串生成条件，条件转换为字符串，以及判定对象是否符合条件等操作的静态类。
    /// </summary>
    public static class StringExprConverter
    {
        /// <summary>
        /// 从字符串查询条件生成表达式树。字符串查询条件的格式为：属性名=值，属性名可以使用大小写混合，值可以使用以下格式：
        /// <see cref="Parse(PropertyInfo, string)"/> 
        /// </summary>
        /// <param name="objectType">实体类型。</param>
        /// <param name="query">查询条件的键值对集合。</param>
        /// <returns>生成的逻辑表达式。</returns>
        public static LogicExpr Parse(Type objectType, IEnumerable<KeyValuePair<string, string>> query)
        {
            LogicExpr expr = null;
            foreach (var kv in query)
            {
                if (objectType.GetProperty(kv.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) is PropertyInfo property)
                {
                    LogicExpr e = Parse(property, kv.Value);
                    expr &= e;
                }
            }
            return expr;
        }

        /// <summary>
        /// 从字符串查询条件生成表达式树。字符串查询条件的格式为：属性名=值，属性名可以使用大小写混合，值可以使用以下格式：
        /// <see cref="Parse(PropertyInfo, string)"/> 
        /// </summary>
        /// <typeparam name="T">实体类型参数。</typeparam>
        /// <param name="query">查询条件的键值对集合。</param>
        /// <returns>生成的逻辑表达式。</returns>
        public static LogicExpr Parse<T>(IEnumerable<KeyValuePair<string, string>> query)
        {
            return Parse(typeof(T), query);
        }

        /// <summary>
        /// 生成分页查询的表达式树。字符串查询条件的格式为：属性名=值，属性名可以使用大小写混合，值可以使用以下格式：
        /// <see cref="Parse(PropertyInfo, string)"/>   
        /// </summary>
        /// <param name="objectType">实体类型。</param>
        /// <param name="query">查询条件的键值对集合。</param>
        /// <param name="pagesize">每页的记录数。</param>
        /// <returns>生成的分页查询表达式。</returns>
        public static Expr ParsePagedQuery(Type objectType, IEnumerable<KeyValuePair<string, string>> query, int pagesize = 10)
        {
            LogicExpr expr = null;
            List<OrderByItemExpr> orderBys = new List<OrderByItemExpr>();
            int startoffset = 0;
            foreach (var kv in query)
            {
                if (kv.Key.Equals("orderby", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string item in kv.Value.Split(','))
                    {
                        string[] parts = item.Trim().Split(' ');
                        if (parts.Length > 0 && objectType.GetProperty(parts[0], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) != null)
                        {
                            orderBys.Add(new OrderByItemExpr
                            {
                                Field = Expr.Prop(parts[0]),
                                Ascending = parts.Length < 2 || !parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase)
                            });
                        }
                    }
                }
                else if (kv.Key.Equals("page", StringComparison.OrdinalIgnoreCase))
                {
                    startoffset = (int.Parse(kv.Value) - 1) * pagesize;
                }
                else if (objectType.GetProperty(kv.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) is PropertyInfo property)
                {
                    expr &= Parse(property, kv.Value);
                }
            }
            return expr.OrderBy(orderBys.ToArray()).Section(startoffset, pagesize);
        }

        /// <summary>
        /// 生成分页查询的表达式树。字符串查询条件的格式为：属性名=值，属性名可以使用大小写混合，值可以使用以下格式：
        /// <see cref="Parse(PropertyInfo, string)"/>
        /// </summary>
        /// <typeparam name="T">实体类型参数。</typeparam>
        /// <param name="query">查询条件的键值对集合。</param>
        /// <param name="pagesize">每页的记录数。</param>
        /// <returns>生成的分页查询表达式。</returns>
        public static Expr ParsePagedQuery<T>(IEnumerable<KeyValuePair<string, string>> query, int pagesize = 10)
        {
            return ParsePagedQuery(typeof(T), query, pagesize);
        }

        /// <summary>
        /// 将属性和字符串转换为简单查询条件。
        /// </summary>
        /// <param name="property">属性描述符。</param>
        /// <param name="text">表示查询语句的字符串，可以使用 "="、"&lt;"、"&gt;"、"!"、"%"、"*"、"&lt;="、"&gt;=" 为起始字符表示条件符号。</param>
        /// <returns>二元表达式查询条件。</returns>
        public static LogicBinaryExpr Parse(PropertyInfo property, string text)
        {
            if (String.IsNullOrEmpty(text)) return Expr.PropEqual(property.Name, null);
            if (text.Length > 1)
            {
                switch (text.Substring(0, 2))
                {
                    case "<=":
                        return Expr.Prop(property.Name, LogicOperator.LessThanOrEqual, ParseValue(property, text.Substring(2)));
                    case ">=":
                        return Expr.Prop(property.Name, LogicOperator.GreaterThanOrEqual, ParseValue(property, text.Substring(2)));
                }
            }
            LogicOperator mask = LogicOperator.Equal;
            if (text.Length > 0 && text[0] == '!')
            {
                mask = LogicOperator.Not;
                text = text.Substring(1);
            }

            if (text.Length > 0)
            {
                switch (text[0])
                {
                    case '=':
                        return Expr.Prop(property.Name, LogicOperator.Equal | mask, ParseValue(property, text.Substring(1)));
                    case '>':
                        return Expr.Prop(property.Name, LogicOperator.GreaterThan | mask, ParseValue(property, text.Substring(1)));
                    case '<':
                        return Expr.Prop(property.Name, LogicOperator.LessThan | mask, ParseValue(property, text.Substring(1)));
                    case '%':
                        return Expr.Prop(property.Name, LogicOperator.Contains | mask, text.Substring(1).Trim());
                    case '*':
                        return Expr.Prop(property.Name, LogicOperator.Like | mask, text.Substring(1).Trim());
                    case '$':
                        return Expr.Prop(property.Name, LogicOperator.RegexpLike | mask, text.Substring(1).Trim());
                }
            }
            else
            {
                return Expr.Prop(property.Name, LogicOperator.Equal | mask, null);
            }
            if (text.IndexOf(',') >= 0)
            {
                List<object> values = new List<object>();
                foreach (string value in text.Split(','))
                {
                    values.Add(ParseValue(property, value));
                }
                return Expr.Prop(property.Name, LogicOperator.In | mask, values.ToArray());
            }
            return Expr.Prop(property.Name, LogicOperator.Equal | mask, ParseValue(property, text));
        }

        /// <summary>
        /// 字符串转化为对应属性类型的值。
        /// </summary>
        /// <param name="property">属性描述符。</param>
        /// <param name="value">输入字符串。</param>
        /// <returns>可被属性接受的值。</returns>
        private static object ParseValue(PropertyInfo property, string value)
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
                    return EnumUtil.Parse(type, value) ?? Enum.Parse(type, value);
                }
            }
            else if (type == typeof(bool))
            {
                char ch = char.ToUpper(value[0]);
                if ("YT1是对".IndexOf(ch) >= 0) return true;
                else if ("NF0否错".IndexOf(ch) >= 0) return false;
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
        public static string ToText(LogicOperator op, object value)
        {
            switch (op)
            {
                case LogicOperator.Equal:
                    string str = ToText(value);
                    if (String.IsNullOrEmpty(str) || "!<>=*%$".IndexOf(str[0]) >= 0 || str.IndexOf(',') >= 0)
                        return '=' + str;
                    else
                        return str;
                case LogicOperator.NotEqual:
                    str = ToText(value);
                    if (!String.IsNullOrEmpty(str) && ("<>=*%$".IndexOf(str[0]) >= 0 || str.IndexOf(',') >= 0))
                        return "!=" + str;
                    else
                        return "!" + str;
                case LogicOperator.In:
                    List<string> values = new List<string>();
                    foreach (object o in value as IEnumerable)
                    {
                        values.Add(ToText(o));
                    }
                    return ToText(String.Join(",", values), "!<>=*%$".ToCharArray());
                case LogicOperator.NotIn:
                    values = new List<string>();
                    foreach (object o in value as IEnumerable)
                    {
                        values.Add(ToText(o));
                    }
                    return "!" + ToText(String.Join(",", values), "<>=*%$".ToCharArray());
                case LogicOperator.GreaterThan: return ">" + ToText(value, '=');
                case LogicOperator.GreaterThanOrEqual: return ">=" + ToText(value);
                case LogicOperator.LessThan: return "<" + ToText(value, '=');
                case LogicOperator.LessThanOrEqual: return "<=" + ToText(value);
                case LogicOperator.Like: return "*" + ToText(value);
                case LogicOperator.NotLike: return "!*" + ToText(value);
                case LogicOperator.Contains: return "%" + ToText(value);
                case LogicOperator.NotContains: return "!%" + ToText(value);
                case LogicOperator.RegexpLike: return "$" + ToText(value);
                case LogicOperator.NotRegexpLike: return "!$" + ToText(value);
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
}
