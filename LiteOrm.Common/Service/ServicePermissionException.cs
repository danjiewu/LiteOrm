using System;

namespace LiteOrm.Service
{
    /// <summary>
    /// 服务权限校验未通过时抛出的异常（如未认证、角色不匹配）。
    /// 继承 <see cref="ServiceException"/>，在服务拦截器中按 Warning 级别记录日志。
    /// </summary>
    [Serializable]
    public class ServicePermissionException : ServiceException
    {
        /// <summary>
        /// 初始化服务权限异常。
        /// </summary>
        public ServicePermissionException() : base()
        {
        }

        /// <summary>
        /// 使用指定错误消息初始化服务权限异常。
        /// </summary>
        /// <param name="message">错误消息</param>
        public ServicePermissionException(string message) : base(message)
        {
        }

        /// <summary>
        /// 使用指定错误消息与内部异常初始化服务权限异常。
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="inner">内部异常</param>
        public ServicePermissionException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
