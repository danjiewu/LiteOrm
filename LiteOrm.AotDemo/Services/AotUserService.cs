using LiteOrm.AotDemo.Models;
using LiteOrm.Common;
using LiteOrm.Service;

namespace LiteOrm.AotDemo.Services
{
    /// <summary>
    /// 用户服务实现。通过 <c>[AutoRegister]</c> 标记，
    /// 由 LiteOrm.Generators 源生成器在 AOT 模式下编译期生成 MS DI 注册代码；
    /// JIT 模式下由 <see cref="LiteOrmAutoRegistration"/> 运行时扫描程序集注册。
    /// </summary>
    public class AotUserService(IServiceProvider serviceProvider)
        : EntityService<AotUser, AotUserView>(serviceProvider), IAotUserService
    {
    }
}
