using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LiteOrm
{
    /// <summary>
    /// 服务特性，用于标记服务相关的方法、类、接口、参数或属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Parameter | AttributeTargets.Property, Inherited = true)]
    public class ServiceAttribute : Attribute
    {
        /// <summary>
        /// 默认构造函数，标记为服务
        /// </summary>
        public ServiceAttribute()
        {
            IsService = true;
        }

        /// <summary>
        /// 构造函数，指定是否为服务
        /// </summary>
        /// <param name="isService">是否为服务</param>
        public ServiceAttribute(bool isService)
        {
            IsService = isService;
        }

        /// <summary>
        /// 是否为服务
        /// </summary>
        public bool IsService { get; set; }

        /// <summary>
        /// 服务名称
        /// </summary>
        public string Name { get; set; }
    }
}
