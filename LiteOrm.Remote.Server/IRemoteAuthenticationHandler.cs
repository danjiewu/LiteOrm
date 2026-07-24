using LiteOrm.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Remote.Server
{
    /// <summary>
    /// 远程身份认证处理器接口。由服务端实现，用于在 SignIn 阶段处理凭据并签发身份票据。
    /// <para>
    /// 框架不自动注册 handler，调用方需手动注册——通常使用内置的 <see cref="IdentityRemoteAuthenticationHandler{TUser}"/>，
    /// 也可直接实现本接口自定义认证流程。
    /// </para>
    /// <para>
    /// 若使用 ASP.NET Core Identity，可直接使用 <see cref="IdentityRemoteAuthenticationHandler{TUser}"/>，
    /// 该实现从 DI 容器获取 <see cref="SignInManager{TUser}"/> 服务进行登录。
    /// </para>
    /// <para>
    /// <see cref="SignInAsync"/> 校验通过后返回身份票据字符串（如 Cookie 串、JWT token 等），
    /// 客户端在后续 <c>InvokeAsync</c> 时将该票据写入 HTTP 请求头，服务端通过认证中间件或自定义逻辑恢复用户上下文。
    /// 返回 <c>null</c> 表示登录失败。
    /// </para>
    /// </summary>
    public interface IRemoteAuthenticationHandler
    {
        /// <summary>
        /// 校验远程调用凭据并签发身份票据。
        /// </summary>
        /// <param name="credentials">客户端提交的远程调用凭据，包含授权模式及对应凭据字段。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>验证通过返回身份票据字符串；验证失败返回 <c>null</c>。</returns>
        Task<string?> SignInAsync(RemoteCredentials credentials, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 基于 ASP.NET Core Identity <see cref="SignInManager{TUser}"/> 的 <see cref="IRemoteAuthenticationHandler"/> 实现。
    /// <para>
    /// 从 DI 容器获取 <see cref="SignInManager{TUser}"/> 服务，
    /// 在 <see cref="SignInAsync"/> 中调用 <see cref="SignInManager{TUser}.PasswordSignInAsync"/> 完成登录。
    /// </para>
    /// <para>
    /// 登录成功后从响应头 <c>Set-Cookie</c> 提取 <c>name=value</c> 部分作为票据返回；客户端在 <c>InvokeAsync</c> 时
    /// 将该票据写入 <c>Cookie</c> 请求头，服务端 Cookie 认证中间件据此恢复 <c>HttpContext.User</c>。
    /// </para>
    /// <para>
    /// 使用前需配置 ASP.NET Core Identity（<c>AddIdentity&lt;TUser, TRole&gt;()</c>），
    /// 并将本处理器注册到 DI：<c>services.AddSingleton&lt;IRemoteAuthenticationHandler, IdentityRemoteAuthenticationHandler&lt;MyUser&gt;&gt;()</c>。
    /// </para>
    /// <para>
    /// <see cref="SignInAsync"/> 为虚方法，可重写以支持自定义授权模式（如 ClientCredentials）或自定义票据提取逻辑。
    /// </para>
    /// </summary>
    /// <typeparam name="TUser">ASP.NET Core Identity 的用户类型。</typeparam>
    public class IdentityRemoteAuthenticationHandler<TUser> : IRemoteAuthenticationHandler
        where TUser : class
    {
        private readonly SignInManager<TUser> _signInManager;
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// 初始化 <see cref="IdentityRemoteAuthenticationHandler{TUser}"/> 类的新实例。
        /// </summary>
        /// <param name="signInManager">ASP.NET Core Identity 的 <see cref="SignInManager{TUser}"/> 服务（从 DI 注入）。</param>
        /// <param name="httpContextAccessor">HTTP 上下文访问器，用于获取当前请求的 <see cref="HttpContext"/>。</param>
        public IdentityRemoteAuthenticationHandler(
            SignInManager<TUser> signInManager,
            IHttpContextAccessor httpContextAccessor)
        {
            _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        /// <summary>
        /// 获取当前请求关联的 <see cref="HttpContext"/>。
        /// </summary>
        protected HttpContext HttpContext
            => _httpContextAccessor.HttpContext ?? throw new InvalidOperationException(
                "IdentityRemoteAuthenticationHandler requires an active HttpContext. Ensure IHttpContextAccessor is registered and the call is within an HTTP request scope.");

        /// <inheritdoc/>
        /// <remarks>
        /// 实现流程：
        /// <list type="number">
        /// <item><see cref="AuthGrantType.Password"/> 模式：调用 <see cref="SignInManager{TUser}.PasswordSignInAsync"/> 校验用户名/密码</item>
        /// <item>登录成功后从响应头 <c>Set-Cookie</c> 提取 <c>name=value</c> 部分（多个用 <c>; </c> 连接）作为票据返回</item>
        /// </list>
        /// 非 Password 模式或登录失败返回 <c>null</c>。可重写以支持其他授权模式。
        /// </remarks>
        public virtual async Task<string?> SignInAsync(
            RemoteCredentials credentials, CancellationToken cancellationToken = default)
        {
            if (credentials is null) throw new ArgumentNullException(nameof(credentials));

            if (credentials.GrantType == AuthGrantType.Password
                && !string.IsNullOrEmpty(credentials.Username)
                && !string.IsNullOrEmpty(credentials.Password))
            {
                var result = await _signInManager.PasswordSignInAsync(
                    credentials.Username!,
                    credentials.Password!,
                    isPersistent: false,
                    lockoutOnFailure: false).ConfigureAwait(false);

                if (result.Succeeded)
                    return ExtractCookieHeader(HttpContext.Response);
            }

            return null;
        }

        /// <summary>
        /// 从 HTTP 响应头中提取 <c>Set-Cookie</c>，组装为可写入 <c>Cookie</c> 请求头的字符串。
        /// <para>仅取每个 <c>Set-Cookie</c> 的 <c>name=value</c> 部分（第一个分号前），多个用 <c>; </c> 连接。</para>
        /// </summary>
        protected static string? ExtractCookieHeader(HttpResponse response)
        {
            if (!response.Headers.TryGetValue("Set-Cookie", out var headerValues))
                return null;

            var pairs = headerValues
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(c => c.Split(';')[0].Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            return pairs.Count == 0 ? null : string.Join("; ", pairs);
        }
    }
}
