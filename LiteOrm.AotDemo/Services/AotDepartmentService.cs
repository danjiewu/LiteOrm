using LiteOrm.AotDemo.Models;
using LiteOrm.Common;
using LiteOrm.Service;

namespace LiteOrm.AotDemo.Services
{
    /// <summary>
    /// 部门服务实现，<c>[AutoRegister]</c> 由源生成器在 AOT 下转为编译期的 MS DI 注册。
    /// 生命周期取 <see cref="Lifetime.Scoped"/>，与 <see cref="LiteOrm.SessionManager"/> / DAO 保持一致。
    /// </summary>
    [AutoRegister(Lifetime.Scoped)]
    public class AotDepartmentService(IServiceProvider serviceProvider)
        : EntityService<AotDepartment, AotDepartmentView>(serviceProvider), IAotDepartmentService
    {
    }
}
