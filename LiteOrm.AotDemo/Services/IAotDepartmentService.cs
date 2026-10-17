using LiteOrm.AotDemo.Models;
using LiteOrm.Service;

namespace LiteOrm.AotDemo.Services
{
    /// <summary>
    /// 部门业务接口。
    /// </summary>
    public interface IAotDepartmentService :
        IEntityService<AotDepartment>, IEntityServiceAsync<AotDepartment>,
        IEntityViewService<AotDepartmentView>, IEntityViewServiceAsync<AotDepartmentView>
    {
    }
}
