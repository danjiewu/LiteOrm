using System.Security.Principal;

namespace LiteOrm.Service
{
    /// <summary>
    /// 用户上下文服务接口，向服务鉴权提供当前调用的用户主体。
    /// </summary>
    /// <remarks>
    /// 由宿主应用实现并通过 <see cref="LiteOrm.DependencyInjection.LiteOrmServiceExtensions.LiteOrmOptions.RegisterUserContext{TUserContext}(LiteOrm.Common.Lifetime)"/>
    /// 注册，或直接在 DI 容器中注册为 <see cref="IUserContext"/> 服务。
    /// <see cref="ServiceInvokeInterceptor"/> 在方法声明了 <c>[ServicePermission]</c> 时解析本服务，
    /// 从 <see cref="UserPrincipal"/> 读取认证状态与角色完成校验；未声明权限特性的方法不依赖本服务。
    /// <para>
    /// 典型实现从当前请求（如 <c>IHttpContextAccessor</c>）、线程上下文或会话中取得主体：
    /// <code>
    /// public class HttpUserContext : IUserContext
    /// {
    ///     private readonly IHttpContextAccessor _accessor;
    ///     public HttpUserContext(IHttpContextAccessor accessor) => _accessor = accessor;
    ///
    ///     public IPrincipal? UserPrincipal => _accessor.HttpContext?.User;
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public interface IUserContext
    {
        /// <summary>
        /// 获取当前用户主体；无可用主体时返回 null（视为未认证）。
        /// 认证状态取 <c>Identity.IsAuthenticated</c>，角色匹配走 <c>IsInRole</c>。
        /// </summary>
        IPrincipal? UserPrincipal { get; }
    }
}
