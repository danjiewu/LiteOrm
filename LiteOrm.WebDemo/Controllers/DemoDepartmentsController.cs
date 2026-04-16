using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.WebDemo.Models;

namespace LiteOrm.WebDemo.Controllers;

public class DemoDepartmentsController : EntityControllerBase<DemoDepartment, DemoDepartment>
{
    public DemoDepartmentsController(
        IEntityServiceAsync<DemoDepartment> entityService,
        IEntityViewServiceAsync<DemoDepartment> viewService)
        : base(entityService, viewService) { }
}
