using LiteOrm.Demo.Models;
using LiteOrm.Service;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm.Demo.Services
{
    /// <summary>
    /// 部门服务实现
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Scoped)]
    public class DepartmentService : EntityService<Department, DepartmentView>, IDepartmentService
    {
    }
}
