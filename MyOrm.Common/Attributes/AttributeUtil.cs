using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MyOrm.Common
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
    }
}
