using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Framework;
using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm.Demo.Services
{
    /// <summary>
    /// 部门服务实现
    /// </summary>
    [AutoRegister(Lifetime.Scoped, typeof(IDepartmentService))]
    public class DepartmentService : EntityService<Department, DepartmentView>, IDepartmentService
    {
        public DepartmentService(ObjectDAO<Department> objectDAO, ObjectViewDAO<DepartmentView> objectViewDAO)
            : base(objectDAO, objectViewDAO)
        {
        }
    }
}
