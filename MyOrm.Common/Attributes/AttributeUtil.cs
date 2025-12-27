using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MyOrm.Common
{
    public static class AttributeUtil
    {
        public static T GetAttribute<T>(this MemberInfo memberInfo) where T : System.Attribute
        {
            return memberInfo.GetCustomAttribute<T>(true);
        }
    }
}
