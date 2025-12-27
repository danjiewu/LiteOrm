using MyOrm.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace MyOrm
{
    public static class Util
    {
        private static Dictionary<Type, Dictionary<Enum, string>> enumTypeName = new Dictionary<Type, Dictionary<Enum, string>>();
        public static int MaxExpandedLogLength { get; set; } = 10;
        public static T Parse<T>(string displayName) where T : struct, Enum
        {
            if (!enumTypeName.ContainsKey(typeof(T)))
            {
                InitlizeEnumName(typeof(T));
            }
            foreach (KeyValuePair<Enum, string> pair in enumTypeName[typeof(T)])
            {
                if (pair.Value == displayName) return (T)pair.Key;
            }
            T res;
            if (Enum.TryParse<T>(displayName, false, out res)) return res;
            if (Enum.TryParse<T>(displayName, true, out res)) return res;
            return res;
        }

        public static object Parse(Type enumType, string displayName)
        {
            if (!enumTypeName.ContainsKey(enumType))
            {
                InitlizeEnumName(enumType);
            }
            foreach (KeyValuePair<Enum, string> pair in enumTypeName[enumType])
            {
                if (pair.Value == displayName) return pair.Key;
            }
            return Enum.Parse(enumType, displayName, false);
        }

        public static string GetDisplayName(Enum value)
        {
            if (value == null) return null;

            Type enumType = value.GetType();
            if (!enumTypeName.ContainsKey(enumType))
            {
                InitlizeEnumName(enumType);
            }
            Dictionary<Enum, string> enumNames = enumTypeName[enumType];
            if (!enumNames.ContainsKey(value)) enumNames[value] = value.ToString();
            return enumNames[value];
        }

        private static void InitlizeEnumName(Type enumType)
        {
            Dictionary<Enum, string> enumNames = new Dictionary<Enum, string>();
            foreach (FieldInfo field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                object[] displayAttrs = field.GetCustomAttributes(typeof(DisplayAttribute), true);
                object[] displayNameAttrs = field.GetCustomAttributes(typeof(DisplayNameAttribute), true);
                object[] descriptionAtts = field.GetCustomAttributes(typeof(DescriptionAttribute), true);
                if (displayAttrs.Length > 0)
                {
                    DisplayAttribute att = (DisplayAttribute)displayAttrs[0];
                    enumNames[(Enum)field.GetValue(null)] = att.Name ?? field.Name;
                }
                else if (displayNameAttrs.Length > 0)
                {
                    DisplayNameAttribute att = (DisplayNameAttribute)displayNameAttrs[0];
                    enumNames[(Enum)field.GetValue(null)] = att.DisplayName ?? field.Name;
                }
                else if (descriptionAtts.Length > 0)
                {
                    DescriptionAttribute att = (DescriptionAttribute)descriptionAtts[0];
                    enumNames[(Enum)field.GetValue(null)] = att.Description ?? field.Name;
                }
                else
                    enumNames[(Enum)field.GetValue(null)] = field.Name;
            }
            enumTypeName[enumType] = enumNames;
        }

        public static string ToDisplayText(Type type, SimpleCondition condtion)
        {
            return GetProperty(type, condtion.Property).DisplayName + ToDisplayText(condtion.Operator, condtion.Opposite, condtion.Value);
        }

        /// <summary>
        ///  根据条件生成用于显示的文本
        /// </summary>
        /// <param name="op">条件类型</param>
        /// <param name="opposite">是否为非</param>
        /// <param name="value">用于比较的值</param>
        /// <returns></returns>
        public static string ToDisplayText(ConditionOperator op, bool opposite, object value)
        {
            StringBuilder sb = new StringBuilder();
            if (opposite) sb.AppendFormat("不");
            switch (op)
            {
                case ConditionOperator.In:
                    List<string> values = new List<string>();
                    foreach (object o in value as IEnumerable)
                    {
                        values.Add(ToDisplayText(o));
                    }
                    sb.AppendFormat("在{0}中", String.Join(",", values.ToArray()));
                    break;
                case ConditionOperator.LargerThan:
                    sb.AppendFormat("大于{0}", ToDisplayText(value));
                    break;
                case ConditionOperator.SmallerThan:
                    sb.AppendFormat("小于{0}", ToDisplayText(value));
                    break;
                case ConditionOperator.Contains:
                    sb.AppendFormat("包含{0}", ToDisplayText(value));
                    break;
                case ConditionOperator.Like:
                    sb.AppendFormat("匹配{0}", ToDisplayText(value));
                    break;
                case ConditionOperator.RegexpLike:
                    sb.AppendFormat("正则匹配{0}", ToDisplayText(value));
                    break;
                case ConditionOperator.Equals:
                    sb.AppendFormat("等于{0}", ToDisplayText(value));
                    break;
                case ConditionOperator.EndsWith:
                    sb.AppendFormat("以{0}结尾", ToDisplayText(value));
                    break;
                case ConditionOperator.StartsWith:
                    sb.AppendFormat("以{0}开头", ToDisplayText(value));
                    break;
                default:
                    sb.Append(ToDisplayText(value)); break;
            }
            return sb.ToString();
        }

        /// <summary>
        /// 根据值生成显示的文本
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static string ToDisplayText(object value)
        {
            if (value == null) return "空";
            if (value is Enum) return GetDisplayName((Enum)value);
            else if (value is bool) return (bool)value ? "是" : "否";
            return Convert.ToString(value);
        }

        /// <summary>
        /// 根据值生成文本
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static string ToText(object value)
        {
            if (value == null) return "";
            if (value is Enum) return GetDisplayName((Enum)value);
            else if (value is bool) return (bool)value ? "是" : "否";
            return Convert.ToString(value);
        }
        public static string GetLogString(object[] values)
        {
            var sb = new StringBuilder();
            int expand = values.Length > MaxExpandedLogLength ? 0 : 1;
            foreach (var o in values)
            {
                if (sb.Length > 0) sb.Append(",");
                sb.Append(Util.GetLogString(o, expand));
            }
            return sb.ToString();
        }

        public static string GetLogString(ICollection values)
        {
            var sb = new StringBuilder();
            int expand = values.Count > MaxExpandedLogLength ? 0 : 1;
            foreach (var o in values)
            {
                if (sb.Length > 0) sb.Append(",");
                sb.Append(Util.GetLogString(o, expand));
            }
            return sb.ToString();
        }

        public static string GetLogString(object o, int expandDepth)
        {
            if (o == null) return "null";
            else if (o is String) return (string)o;
            else if (o is byte[])
            {
                byte[] bytes = (byte[])o;
                if (bytes.Length > 1024)
                    return "[bytes:" + bytes.Length + "]";
                else
                    return Convert.ToBase64String(bytes);
            }
            else if (o is ILogable) return "{" + ((ILogable)o).ToLog() + "}";
            else if (o is Array || o is ICollection)
            {
                int count = o is Array ? ((Array)o).Length : ((ICollection)o).Count;
                if (expandDepth > 0 && count <= MaxExpandedLogLength)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (object value in (IEnumerable)o)
                    {
                        if (sb.Length > 0) sb.Append(",");
                        sb.Append(GetLogString(value, expandDepth - 1));
                    }
                    return "{" + sb.ToString() + "}";
                }
                else
                {
                    return o.GetType().Name + "[" + count + "]";
                }
            }
            else if (o.GetType().IsValueType) return Convert.ToString(o);
            else return "{" + Convert.ToString(o) + "}";
        }

        public static List<SimpleCondition> ParseQueryCondition(IEnumerable<KeyValuePair<string, string>> queryString, Type type)
        {
            List<SimpleCondition> conditions = new List<SimpleCondition>();
            foreach (KeyValuePair<string, string> param in queryString)
            {
                PropertyDescriptor property = GetFilterProperties(type).Find(param.Key, true);
                if (property != null)
                    conditions.Add(ConditionConvert.ParseCondition(property, param.Value));
            }
            return conditions;
        }

        private static Dictionary<Type, PropertyDescriptorCollection> typeProperties = new Dictionary<Type, PropertyDescriptorCollection>();

        public static PropertyDescriptorCollection GetFilterProperties(Type type)
        {
            if (!typeProperties.ContainsKey(type))
            {
                GenerateProperties(type);
            }
            return typeProperties[type];
        }

        private static void GenerateProperties(Type type)
        {
            List<PropertyDescriptor> properties = new List<PropertyDescriptor>();
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(type))
            {
                if (property.Attributes[typeof(ColumnAttribute)] == null)
                    properties.Add(property);
                else
                {
                    ColumnAttribute att = (ColumnAttribute)property.Attributes[typeof(ColumnAttribute)];
                    if (att.IsColumn && att.ColumnMode.CanRead())
                    {
                        properties.Add(property);
                    }
                }
            }
            properties.Sort((p1, p2) => String.Compare(p1.DisplayName, p2.DisplayName, true));
            typeProperties[type] = new PropertyDescriptorCollection(properties.ToArray(), true);
        }

        public static PropertyDescriptor GetProperty(Type type, string property)
        {
            if (!typeProperties.ContainsKey(type))
            {
                GenerateProperties(type);
            }
            return typeProperties[type].Find(property, true);
        }
    }
}
