using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.DependencyInjection;
using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm.Demo.Services
{
    /// <summary>
    /// 部门服务实现
    /// </summary>
    [AutoRegister(RegisterPolicy.Interface, Lifetime = Lifetime.Scoped)]
    public class DepartmentService : EntityService<Department, DepartmentView>, IDepartmentService
    {
        public DepartmentService(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }
    }
}
