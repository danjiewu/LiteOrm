using LiteOrm.AotDemo.Models;
using LiteOrm.Common;
using LiteOrm.Service;

namespace LiteOrm.AotDemo.Services
{
    /// <summary>
    /// 用户服务实现。通过 <c>[AutoRegister]</c> 标记，
    /// 由 LiteOrm.Generators 源生成器在 AOT 模式下编译期生成 MS DI 注册代码；
    /// JIT 模式下由 <see cref="LiteOrmAutoRegistration"/> 运行时扫描程序集注册。
    /// <para>
    /// 生命周期必须是 <see cref="Lifetime.Scoped"/>：<see cref="EntityService{T,TView}"/> 依赖
    /// <see cref="LiteOrm.SessionManager"/> 与 DAO，而它们都是 Scoped。若用默认的 Singleton，
    /// 单例会在根容器里再解析出一个会话，导致事务、连接复用与实际请求用的会话不是同一个。
    /// </para>
    /// </summary>
    [AutoRegister(Lifetime.Scoped)]
    public class AotUserService(IServiceProvider serviceProvider)
        : EntityService<AotUser, AotUserView>(serviceProvider), IAotUserService
    {
    }
}
