using LiteOrm.Common;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;

namespace LiteOrm
{
    /// <summary>
    /// 工具类，提供各种实用方法
    /// </summary>
    public static class Util
    {
        // 缓存枚举类型与其值的显示名称映射，提高性能
        private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<Enum, string>> _enumTypeName = new ConcurrentDictionary<Type, ConcurrentDictionary<Enum, string>>();
        private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, Enum>> _enumNameValue = new ConcurrentDictionary<Type, ConcurrentDictionary<string, Enum>>();

        /// <summary>
        /// 获取服务类型的短名称。
        /// 对于泛型类型，会返回类似 "GenericType&lt;T&gt;" 的可读格式。
        /// </summary>
        /// <param name="serviceType">目标服务类型。</param>
        /// <returns>格式化后的服务名称。</returns>
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
        
        /// <summary>
        /// 最大允许展开并记录日志的集合长度。
        /// 超过此长度的集合将只记录类型和计数，以避免日志文件过大。
        /// </summary>
        public static int MaxExpandedLogLength { get; set; } = 10;
        
        /// <summary>
        /// 通过枚举的显示名称（DisplayAttribute 等）反向解析为枚举值。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="displayName">显示名称。</param>
        /// <returns>匹配的枚举值；若未找到则返回该类型的默认值。</returns>
        public static T Parse<T>(string displayName) where T : struct, Enum
        {
            if (!_enumNameValue.ContainsKey(typeof(T)))
            {
                InitializeEnumName(typeof(T));
            }
            if (_enumNameValue[typeof(T)].TryGetValue(displayName, out Enum r)) return (T)r;
            T res;
            if (Enum.TryParse<T>(displayName, false, out res)) return res;
            if (Enum.TryParse<T>(displayName, true, out res)) return res;
            return res;
        }

        /// <summary>
        /// 解析具有指定类型的枚举显示名称。
        /// </summary>
        /// <param name="enumType">枚举的 Type 对象。</param>
        /// <param name="displayName">显示名称。</param>
        /// <returns>枚举项对象。</returns>
        public static object Parse(Type enumType, string displayName)
        {
            if (!_enumTypeName.ContainsKey(enumType))
            {
                InitializeEnumName(enumType);
            }
            foreach (KeyValuePair<Enum, string> pair in _enumTypeName[enumType])
            {
                if (pair.Value == displayName) return pair.Key;
            }
            return Enum.Parse(enumType, displayName, false);
        }

        /// <summary>
        /// 获取枚举值的显示文本。
        /// 优先读取 [Display(Name=...)]、[DisplayName] 或 [Description] 特性，若无则返回字段名。
        /// </summary>
        /// <param name="value">枚举值。</param>
        /// <returns>显示名称字符串。</returns>
        public static string GetDisplayName(Enum value)
        {
            if (value is null) return null;

            Type enumType = value.GetType();
            if (!_enumTypeName.ContainsKey(enumType))
            {
                InitializeEnumName(enumType);
            }
            ConcurrentDictionary<Enum, string> enumNames = _enumTypeName[enumType];
            if (!enumNames.ContainsKey(value)) enumNames[value] = value.ToString();
            return enumNames[value];
        }

        /// <summary>
        /// 扫描枚举类型的所有字段，并初始化其显示名称与值的双向缓存。
        /// </summary>
        /// <param name="enumType">枚举类型。</param>
        private static void InitializeEnumName(Type enumType)
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
            _enumTypeName[enumType] = enumNames;
            _enumNameValue[enumType] = nameValues;
        }

        /// <summary>
        /// 将任意值转换为适合 UI 显示的文本（处理枚举、布尔及空值）。
        /// </summary>
        /// <param name="value">原始值。</param>
        /// <returns>“是”/“否”、“空”或枚举显示名等友好文本。</returns>
        public static string ToDisplayText(object value)
        {
            if (value is null) return "空";
            if (value is Enum) return GetDisplayName((Enum)value);
            else if (value is bool) return (bool)value ? "是" : "否";
            return Convert.ToString(value);
        }

        /// <summary>
        /// 获取对象的通用文本表示。
        /// </summary>
        public static string ToText(object value)
        {
            if (value is null) return "";
            if (value is Enum) return GetDisplayName((Enum)value);
            else if (value is bool) return (bool)value ? "是" : "否";
            return Convert.ToString(value);
        }
        
        /// <summary>
        /// 生成用于日志记录的对象列表字符串。
        /// 自动处理集合的深度展开（在限制长度内）。
        /// </summary>
        /// <param name="values">对象数组</param>
        /// <returns>日志字符串</returns>
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

        /// <summary>
        /// 获取集合的日志展示字符串。
        /// </summary>
        /// <param name="values">集合</param>
        /// <returns>日志字符串</returns>
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

        /// <summary>
        /// 递归获取对象的日志详细信息。
        /// 处理字节数组（Base64 转码）、集合（展开）、实现了 ILogable 的对象及值类型。
        /// </summary>
        /// <param name="o">目标对象。</param>
        /// <param name="expandDepth">当前递归展开深度。</param>
        /// <returns>日志文本。</returns>
        public static string GetLogString(object o, int expandDepth)
        {
            if (o is null) return "null";
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

        /// <summary>
        /// 从 URL 查询字符串或简单的键值对集合中，解析出实体属性对应的过滤条件集合。
        /// </summary>
        /// <param name="queryString">外部传入的查询键值。键应与实体的属性名匹配。</param>
        /// <param name="type">实体类型，用于元数据验证。</param>
        /// <returns>转换后的 Expr 表达式列表。</returns>
        public static List<Expr> ParseQueryCondition(IEnumerable<KeyValuePair<string, string>> queryString, Type type)
        {
            List<Expr> conditions = new List<Expr>();
            foreach (KeyValuePair<string, string> param in queryString)
            {
                PropertyDescriptor property = GetFilterProperties(type).Find(param.Key, true);
                if (property is not null)
                    conditions.Add(ExprConvert.Parse(property, param.Value));
            }
            return conditions;
        }

        private static readonly ConcurrentDictionary<Type, PropertyDescriptorCollection> _typeProperties = new ConcurrentDictionary<Type, PropertyDescriptorCollection>();

        /// <summary>
        /// 获取实体类中参与过滤查询的所有属性描述符集合。
        /// 排除掉被标记为非列或不可读的属性。
        /// </summary>
        /// <param name="type">实体类型</param>
        /// <returns>属性描述符集合</returns>
        public static PropertyDescriptorCollection GetFilterProperties(Type type)
        {
            if (!_typeProperties.ContainsKey(type))
            {
                GenerateProperties(type);
            }
            return _typeProperties[type];
        }

        /// <summary>
        /// 扫描并生成实体类的可用过滤属性缓存。
        /// </summary>
        /// <param name="type">实体类型</param>
        private static void GenerateProperties(Type type)
        {
            List<PropertyDescriptor> properties = new List<PropertyDescriptor>();
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(type))
            {
                if (property.Attributes[typeof(ColumnAttribute)] is null)
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
            _typeProperties[type] = new PropertyDescriptorCollection(properties.ToArray(), true);
        }

        /// <summary>
        /// 获取类型的指定属性
        /// </summary>
        /// <param name="type">实体类型</param>
        /// <param name="property">属性名称</param>
        /// <returns>属性描述符</returns>
        public static PropertyDescriptor GetProperty(Type type, string property)
        {
            if (!_typeProperties.ContainsKey(type))
            {
                GenerateProperties(type);
            }
            return _typeProperties[type].Find(property, true);
        }

        /// <summary>
        /// 向键值对集合添加新项（扩展方法）
        /// </summary>
        /// <param name="list">键值对集合</param>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        public static void Add(this ICollection<KeyValuePair<string, object>> list, string key, object value)
        {
            list.Add(new KeyValuePair<string, object>(key, value));
        }
    }
}
