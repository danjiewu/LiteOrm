using System;
using System.Collections.Generic;
using System.Text;

namespace LiteOrm.Common
{
    /// <summary>
    /// 用于指定实体属性的排序顺序。
    /// 排序规则：首先按 Before/After 指定的拓扑依赖关系排序，同一层级按 Order 值升序排列，最后按属性原始声明顺序排列。
    /// 当检测到循环依赖时，将抛出 <see cref="InvalidOperationException"/> 异常。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class PropertyOrderAttribute : Attribute
    {
        /// <summary>
        /// 初始化 <see cref="PropertyOrderAttribute"/> 类的新实例。
        /// </summary>
        /// <param name="order">排序优先级，数值越小越靠前，默认值为 0。</param>
        public PropertyOrderAttribute(int order = 0)
        {
            Order = order;
        }

        /// <summary>
        /// 获取排序优先级，数值越小越靠前，默认值为 0。
        /// 在同一拓扑层级中，Order 值较小的属性优先排列。
        /// </summary>
        public int Order { get; }

        /// <summary>
        /// 获取或设置一个属性名，指示当前属性应排在该属性之后。
        /// </summary>
        public string After { get; set; }

        /// <summary>
        /// 获取或设置一个属性名，指示当前属性应排在该属性之前。
        /// </summary>
        public string Before { get; set; }
    }
}
