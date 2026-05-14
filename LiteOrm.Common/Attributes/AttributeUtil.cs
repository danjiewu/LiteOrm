using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LiteOrm.Common
{
    /// <summary>
    /// 属性工具类，提供属性相关的扩展方法
    /// </summary>
    public static class AttributeUtil
    {
        /// <summary>
        /// 获取成员上的指定类型的特性
        /// </summary>
        /// <typeparam name="T">特性类型</typeparam>
        /// <param name="memberInfo">成员信息</param>
        /// <returns>找到的特性，如果未找到则返回null</returns>
        public static T GetAttribute<T>(this MemberInfo memberInfo) where T : System.Attribute
        {
            return memberInfo.GetCustomAttribute<T>(true);
        }

        /// <summary>
        /// 按照PropertyOrder的Before、After及Order属性值对实体属性进行排序
        /// </summary>
        /// <typeparam name="TList">属性列表类型</typeparam>
        /// <param name="properties">属性列表</param>
        /// <returns>排序后的属性列表</returns>
        /// <exception cref="InvalidOperationException">当检测到循环依赖时抛出</exception>
        public static TList SortProperty<TList>(this TList properties) where TList : IList<PropertyInfo>
        {            
            if (properties == null || properties.Count <= 1)
            {
                return properties;
            }

            Dictionary<string, PropertyInfo> propertyDict = new Dictionary<string, PropertyInfo>();
            Dictionary<string, int> indegreeDict = new Dictionary<string, int>();
            Dictionary<string, HashSet<string>> dependencyDict = new Dictionary<string, HashSet<string>>();
            Dictionary<string, int> orderDict = new Dictionary<string, int>();
            Dictionary<string, int> indexDict = new Dictionary<string, int>();

            for (int i = 0; i < properties.Count; i++)
            {
                PropertyInfo property = properties[i];
                propertyDict[property.Name] = property;
                indegreeDict[property.Name] = 0;
                dependencyDict[property.Name] = new HashSet<string>();
                orderDict[property.Name] = property.GetAttribute<PropertyOrderAttribute>()?.Order ?? 0;
                indexDict[property.Name] = i;
            }

            foreach (PropertyInfo property in properties)
            {
                PropertyOrderAttribute orderAttribute = property.GetAttribute<PropertyOrderAttribute>();
                if (orderAttribute == null)
                {
                    continue;
                }

                AddDependency(property.Name, orderAttribute.Before);
                AddDependency(orderAttribute.After, property.Name);
            }

            List<string> availableProperties = indegreeDict
                .Where(item => item.Value == 0)
                .Select(item => item.Key)
                .ToList();
            List<PropertyInfo> sortedProperties = new List<PropertyInfo>(properties.Count);

            while (availableProperties.Count > 0)
            {
                string propertyName = availableProperties
                    .OrderBy(name => orderDict[name])
                    .ThenBy(name => indexDict[name])
                    .First();

                availableProperties.Remove(propertyName);
                sortedProperties.Add(propertyDict[propertyName]);

                foreach (string dependency in dependencyDict[propertyName])
                {
                    indegreeDict[dependency]--;
                    if (indegreeDict[dependency] == 0)
                    {
                        availableProperties.Add(dependency);
                    }
                }
            }

            if (sortedProperties.Count != properties.Count)
            {
                string circularProperties = string.Join(", ", indegreeDict.Where(item => item.Value > 0).Select(item => item.Key));
                throw new InvalidOperationException($"Detected circular property order dependency: {circularProperties}");
            }

            for (int i = 0; i < sortedProperties.Count; i++)
            {
                properties[i] = sortedProperties[i];
            }

            return properties;

            void AddDependency(string fromProperty, string toProperty)
            {
                if (string.IsNullOrWhiteSpace(fromProperty) || string.IsNullOrWhiteSpace(toProperty))
                {
                    return;
                }

                if (!propertyDict.ContainsKey(fromProperty) || !propertyDict.ContainsKey(toProperty))
                {
                    return;
                }

                if (dependencyDict[fromProperty].Add(toProperty))
                {
                    indegreeDict[toProperty]++;
                }
            }
        }
    }
}
