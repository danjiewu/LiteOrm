using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Demo.Models;

namespace LiteOrm.Demo.Services;

public interface IDemoDepartmentService :
    IEntityServiceAsync<DemoDepartment>,
    IEntityViewServiceAsync<DemoDepartment>
{
}

