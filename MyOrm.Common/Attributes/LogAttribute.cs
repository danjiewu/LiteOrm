using System;
using System.Collections.Generic;
using System.Text;

namespace MyOrm
{
    /// <summary>
    /// 日志特性，用于标记需要记录日志的方法、类、接口、参数或属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Parameter | AttributeTargets.Property, Inherited = true)]
    public class LogAttribute : Attribute
    {
        /// <summary>
        /// 默认构造函数，启用日志记录
        /// </summary>
        public LogAttribute() { Enabled = true; }

        /// <summary>
        /// 构造函数，指定是否启用日志记录
        /// </summary>
        /// <param name="enabled">是否启用日志记录</param>
        public LogAttribute(bool enabled) { Enabled = enabled; }

        /// <summary>
        /// 是否启用日志记录
        /// </summary>
        public bool Enabled { get; set; }
    }
}
