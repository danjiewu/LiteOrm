using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MyOrm
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Parameter | AttributeTargets.Property, Inherited = true)]
    public class ServiceAttribute : Attribute
    {
        public ServiceAttribute()
        {
            IsService = true;
        }

        public ServiceAttribute(bool isService)
        {
            IsService = isService;
        }

        public bool IsService { get; set; }

        public string Name { get; set; }
    }
}
