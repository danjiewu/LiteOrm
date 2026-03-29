using LiteOrm.Common;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace LiteOrm
{
    /// <summary>
    /// 提供枚举类型与其显示名称之间的双向转换功能。
    /// </summary>
    public static class EnumUtil
    {
        // 缓存枚举类型与其值的显示名称映射，提高性能
        private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<Enum, string>> _enumTypeName = new ConcurrentDictionary<Type, ConcurrentDictionary<Enum, string>>();
        private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, Enum>> _enumNameValue = new ConcurrentDictionary<Type, ConcurrentDictionary<string, Enum>>();

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
        /// 优先读取[DisplayName] 或 [Description] 特性，若无则返回字段名。
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
                object[] displayAttrs = field.GetCustomAttributes(typeof(Enum), true);
                object[] displayNameAttrs = field.GetCustomAttributes(typeof(DisplayNameAttribute), true);
                object[] descriptionAtts = field.GetCustomAttributes(typeof(DescriptionAttribute), true);
                string displayName = null;
                if (displayNameAttrs.Length > 0)
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
    }
}
