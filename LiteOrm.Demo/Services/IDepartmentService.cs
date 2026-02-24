using LiteOrm.Demo.Models;
using LiteOrm.Service;

namespace LiteOrm.Demo.Services
{
    /// <summary>
    /// 部门业务服务接口
    /// </summary>
    public interface IDepartmentService :
        IEntityService<Department>, IEntityServiceAsync<Department>,
        IEntityViewService<DepartmentView>, IEntityViewServiceAsync<DepartmentView>
    {
    }
}
