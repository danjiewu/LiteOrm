using LiteOrm.Common;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Remote.Server
{
    /// <summary>
    /// 远程身份验证接口。由使用者实现，用于在 Connect 阶段校验凭据并返回身份声明。
    /// <para>
    /// 根据 <see cref="RemoteCredentials.GrantType"/> 区分两种授权模式：
    /// <list type="bullet">
    /// <item><see cref="AuthGrantType.Password"/>：使用 <c>Username</c>/<c>Password</c> 认证用户身份</item>
    /// <item><see cref="AuthGrantType.ClientCredentials"/>：使用 <c>ClientId</c>/<c>ClientSecret</c> 认证客户端身份</item>
    /// </list>
    /// 框架在调用 <see cref="ValidateCredentialsAsync"/> 前已按授权模式校验必填字段是否完整，
    /// 实现方可在方法内根据 <see cref="RemoteCredentials.GrantType"/> 执行不同的验证逻辑。
    /// </para>
    /// <para>
    /// 校验通过后，框架自动调用 <see cref="Microsoft.AspNetCore.Http.HttpContext.SignInAsync"/>
    /// 传入返回的 <see cref="ClaimsPrincipal"/> 创建身份认证票据（Cookie/Token），
    /// 后续请求中通过 <see cref="Microsoft.AspNetCore.Http.HttpContext.User"/> 恢复用户上下文。
    /// </para>
    /// <para>
    /// 返回 null 表示验证失败。若未注册该接口的实现，服务端允许匿名连接（不验证身份）。
    /// </para>
    /// </summary>
    public interface IRemoteAuthenticationHandler
    {
        /// <summary>
        /// 验证远程调用凭据并返回用户身份声明。
        /// </summary>
        /// <param name="credentials">客户端提交的远程调用凭据，包含授权模式及对应凭据字段。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>验证通过返回 <see cref="ClaimsPrincipal"/>；验证失败返回 null。</returns>
        Task<ClaimsPrincipal?> ValidateCredentialsAsync(RemoteCredentials credentials, CancellationToken cancellationToken = default);
    }
}
