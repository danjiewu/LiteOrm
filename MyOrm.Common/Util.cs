using MyOrm.Common;
using System;
using System.Collections;
using System.Collections.Concurrent;
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
        private static ConcurrentDictionary<Type, ConcurrentDictionary<Enum, string>> enumTypeName = new ConcurrentDictionary<Type, ConcurrentDictionary<Enum, string>>();
        private static ConcurrentDictionary<Type, ConcurrentDictionary<string, Enum>> enumNameValue = new ConcurrentDictionary<Type, ConcurrentDictionary<string, Enum>>();

        public static string GetServiceName(Type serviceType)
        {
            if (serviceType.IsGenericType)
            {
                int backtickIndex = serviceType.Name.IndexOf('`');
                return serviceType.Name.Substring(0, backtickIndex) + "<" + String.Join(",", from t in serviceType.GetGenericArguments() select t.Name) + ">";
            }
            else
            {
                return serviceType.Name;
            }
        }
        public static int MaxExpandedLogLength { get; set; } = 10;
        public static T Parse<T>(string displayName) where T : struct, Enum
        {
            if (!enumNameValue.ContainsKey(typeof(T)))
            {
                InitlizeEnumName(typeof(T));
            }
            if (enumNameValue[typeof(T)].TryGetValue(displayName, out Enum r)) return (T)r;
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
            ConcurrentDictionary<Enum, string> enumNames = enumTypeName[enumType];
            if (!enumNames.ContainsKey(value)) enumNames[value] = value.ToString();
            return enumNames[value];
        }

        private static void InitlizeEnumName(Type enumType)
        {
            ConcurrentDictionary<Enum, string> enumNames = new ConcurrentDictionary<Enum, string>();
            ConcurrentDictionary<string, Enum> nameValues = new ConcurrentDictionary<string, Enum>();
            foreach (FieldInfo field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                object[] displayAttrs = field.GetCustomAttributes(typeof(DisplayAttribute), true);
                object[] displayNameAttrs = field.GetCustomAttributes(typeof(DisplayNameAttribute), true);
                object[] descriptionAtts = field.GetCustomAttributes(typeof(DescriptionAttribute), true);
                string displayName = null;
                if (displayAttrs.Length > 0)
                {
                    DisplayAttribute att = (DisplayAttribute)displayAttrs[0];
                    displayName = att.Name ?? field.Name;
                }
                else if (displayNameAttrs.Length > 0)
                {
                    DisplayNameAttribute att = (DisplayNameAttribute)displayNameAttrs[0];
                    displayName = att.DisplayName ?? field.Name;
                }
                else if (descriptionAtts.Length > 0)
                {
                    DescriptionAttribute att = (DescriptionAttribute)descriptionAtts[0];
                    displayName = att.Description ?? field.Name;
                }
                else
                    displayName = field.Name;
                enumNames[(Enum)field.GetValue(null)] = displayName;
                nameValues[displayName] = (Enum)field.GetValue(null);
            }
            enumTypeName[enumType] = enumNames;
            enumNameValue[enumType] = nameValues;
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

        public static List<Statement> ParseQueryCondition(IEnumerable<KeyValuePair<string, string>> queryString, Type type)
        {
            List<Statement> conditions = new List<Statement>();
            foreach (KeyValuePair<string, string> param in queryString)
            {
                PropertyDescriptor property = GetFilterProperties(type).Find(param.Key, true);
                if (property != null)
                    conditions.Add(StatementConvert.Parse(property, param.Value));
            }
            return conditions;
        }

        private static ConcurrentDictionary<Type, PropertyDescriptorCollection> typeProperties = new ConcurrentDictionary<Type, PropertyDescriptorCollection>();

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
