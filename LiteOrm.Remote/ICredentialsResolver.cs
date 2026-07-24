using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Remote
{
    /// <summary>
    /// 远程调用凭据解析器接口。负责为远程服务调用提供身份认证票据。
    /// <para>
    /// 实现方根据当前调用上下文返回已认证的票据字符串（如 Cookie 串、JWT token 等），
    /// 传输层在 <c>InvokeAsync</c> 时将票据写入 HTTP 请求头（默认为 <c>Cookie</c> 头），
    /// 服务端通过认证中间件或自定义逻辑恢复用户上下文。
    /// </para>
    /// <para>
    /// 返回 <c>null</c> 表示匿名连接（不带票据发起请求）。
    /// </para>
    /// <para>
    /// 框架内置实现：
    /// <list type="bullet">
    /// <item><see cref="StaticCredentialsResolver"/>：静态票据模式，启动时调用 <c>LoginAsync</c> 一次，
    /// 登录成功后服务端返回的票据被保存到本地，后续所有 <c>InvokeAsync</c> 复用该票据</item>
    /// </list>
    /// </para>
    /// </summary>
    public interface ICredentialsResolver
    {
        /// <summary>
        /// 获取当前会话的身份认证票据。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>身份票据字符串；返回 <c>null</c> 表示匿名连接（不带票据发起请求）。</returns>
        Task<string?> GetTicketAsync(CancellationToken cancellationToken = default);
    }
}
