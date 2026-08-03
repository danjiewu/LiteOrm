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
    [AutoRegister(ServiceLifetime.Scoped, typeof(IDepartmentService))]
    public class DepartmentService : EntityService<Department, DepartmentView>, IDepartmentService
    {
        public DepartmentService(ObjectDAO<Department> objectDAO, ObjectViewDAO<DepartmentView> objectViewDAO)
            : base(objectDAO, objectViewDAO)
        {
        }
    }
}
