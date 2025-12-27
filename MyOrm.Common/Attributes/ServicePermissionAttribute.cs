using System;
using System.Collections.Generic;
using System.Text;

namespace MyOrm
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface, Inherited = true)]
    public class ServicePermissionAttribute : Attribute
    {
        public ServicePermissionAttribute()
        {
        }

        public ServicePermissionAttribute(bool allowAnonymous)
        {
            AllowAnonymous = allowAnonymous;
        }

        public bool AllowAnonymous { get; set; }
        public string AllowRoles { get; set; }
    }
}
