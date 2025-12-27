using System;
using System.Collections.Generic;
using System.Text;

namespace MyOrm.Common
{
    /// <summary>
    /// 根据属性名访问属性值
    /// </summary>
    public interface IIndexedProperty
    {
        /// <summary>
        /// 根据属性名设置和获取属性值
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <returns>属性的值</returns>
        object this[string propertyName] { get; set; }
    }
}
