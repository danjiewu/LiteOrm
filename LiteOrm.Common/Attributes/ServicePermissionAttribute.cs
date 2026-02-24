using System;

namespace LiteOrm
{
    /// <summary>
    /// 服务权限特性，用于配置服务方法的访问权限
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface, Inherited = true)]
    public class ServicePermissionAttribute : Attribute
    {
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public ServicePermissionAttribute()
        {
        }

        /// <summary>
        /// 构造函数，指定是否允许匿名访问
        /// </summary>
        /// <param name="allowAnonymous">是否允许匿名访问</param>
        public ServicePermissionAttribute(bool allowAnonymous)
        {
            AllowAnonymous = allowAnonymous;
        }

        /// <summary>
        /// 是否允许匿名访问
        /// </summary>
        public bool AllowAnonymous { get; set; }

        /// <summary>
        /// 允许的角色，多个角色用逗号分隔
        /// </summary>
        public string AllowRoles { get; set; }
    }
}
