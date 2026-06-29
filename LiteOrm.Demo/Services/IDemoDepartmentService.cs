using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Demo.Models;

namespace LiteOrm.Demo.Services;

[Service]
public interface IDemoDepartmentService :
    IEntityServiceAsync<DemoDepartment>,
    IEntityViewServiceAsync<DemoDepartment>
{
}

