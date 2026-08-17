using LiteOrm.AotDemo.Models;
using LiteOrm.Service;

namespace LiteOrm.AotDemo.Services
{
    /// <summary>
    /// 用户业务逻辑接口，继承 LiteOrm 内置的实体 CRUD / 查询接口。
    /// </summary>
    public interface IAotUserService :
        IEntityService<AotUser>, IEntityServiceAsync<AotUser>,
        IEntityViewService<AotUserView>, IEntityViewServiceAsync<AotUserView>
    {
    }
}
