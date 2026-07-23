using System.Collections.Generic;

namespace LiteOrm.Common
{
    /// <summary>
    /// 远程调用凭据。用于远程服务传输的 Connect 阶段身份认证。
    /// </summary>
    public class RemoteCredentials
    {
        /// <summary>用户名。</summary>
        public string? Username { get; set; }

        /// <summary>密码。</summary>
        public string? Password { get; set; }

        /// <summary>
        /// 自定义扩展字段。可用于传递额外的身份信息（如租户 ID、令牌等），
        /// 服务端 <c>IRemoteAuthenticationHandler.ValidateCredentialsAsync</c> 可读取这些字段。
        /// </summary>
        public Dictionary<string, object?>? Extensions { get; set; }
    }
}
