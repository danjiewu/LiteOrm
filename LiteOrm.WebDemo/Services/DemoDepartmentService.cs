using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.WebDemo.Models;

namespace LiteOrm.WebDemo.Services;

public interface IDemoDepartmentService :
    IEntityServiceAsync<DemoDepartment>,
    IEntityViewServiceAsync<DemoDepartment>
{
}

public class DemoDepartmentService : EntityService<DemoDepartment>, IDemoDepartmentService
{
    public DemoDepartmentService(ObjectDAO<DemoDepartment> objectDAO, ObjectViewDAO<DemoDepartment> objectViewDAO)
        : base(objectDAO, objectViewDAO)
    {
    }
}
