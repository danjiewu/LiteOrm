using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace MyOrm.Common
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
    public class AutoRegisterAttribute : Attribute
    {
        public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;

        // 支持多个服务类型
        public Type[] ServiceTypes { get; set; }
        public bool Enabled { get; } = true;
        // 构造函数重载
        public AutoRegisterAttribute() { }
        public AutoRegisterAttribute(bool enabled) { Enabled = enabled; }
        public AutoRegisterAttribute(ServiceLifetime lifetime) => Lifetime = lifetime;
        public AutoRegisterAttribute(params Type[] serviceTypes) => ServiceTypes = serviceTypes;
        public AutoRegisterAttribute(ServiceLifetime lifetime, params Type[] serviceTypes)
        {
            Lifetime = lifetime;
            ServiceTypes = serviceTypes;
        }
    }
}
