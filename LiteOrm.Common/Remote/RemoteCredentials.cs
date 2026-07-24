using System.Collections.Generic;

namespace LiteOrm.Common
{
    /// <summary>
    /// 远程调用凭据。用于远程服务 SignIn 阶段的身份认证。
    /// <para>
    /// 根据 <see cref="GrantType"/> 区分两种授权模式：
    /// <list type="bullet">
    /// <item><see cref="AuthGrantType.Password"/>：使用 <see cref="Username"/> + <see cref="Password"/> 进行用户身份认证</item>
    /// <item><see cref="AuthGrantType.ClientCredentials"/>：使用 <see cref="ClientId"/> + <see cref="ClientSecret"/> 进行客户端身份认证</item>
    /// </list>
    /// </para>
    /// </summary>
    public class RemoteCredentials
    {
        /// <summary>授权模式类型，默认为 <see cref="AuthGrantType.Password"/></summary>
        public AuthGrantType GrantType { get; set; } = AuthGrantType.Password;

        /// <summary>用户名（<see cref="AuthGrantType.Password"/> 模式必填）</summary>
        public string? Username { get; set; }

        /// <summary>密码（<see cref="AuthGrantType.Password"/> 模式必填）</summary>
        public string? Password { get; set; }

        /// <summary>客户端 ID（<see cref="AuthGrantType.ClientCredentials"/> 模式必填）</summary>
        public string? ClientId { get; set; }

        /// <summary>客户端密钥（<see cref="AuthGrantType.ClientCredentials"/> 模式必填）</summary>
        public string? ClientSecret { get; set; }

        /// <summary>
        /// 自定义扩展字段。可用于传递额外的身份信息（如租户 ID、令牌等），
        /// 服务端 <see cref="IRemoteAuthenticationHandler.SignInAsync"/> 可读取这些字段。
        /// </summary>
        public Dictionary<string, string> Extensions { get; set; }
    }

    /// <summary>
    /// 远程调用授权模式类型。
    /// </summary>
    public enum AuthGrantType
    {
        /// <summary>密码模式，使用用户名 + 密码认证用户身份。</summary>
        Password,

        /// <summary>客户端凭据模式，使用 ClientId + ClientSecret 认证客户端身份。</summary>
        ClientCredentials,
    }
}
