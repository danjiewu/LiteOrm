using Microsoft.Extensions.Logging;
using System;

namespace LiteOrm.Service
{
    /// <summary>
    /// 服务描述类，用于描述服务的配置信息
    /// </summary>
    [Serializable]
    public class ServiceDescription
    {
        /// <summary>
        /// 服务名称
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// 方法名称
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// 日志级别，默认为Debug
        /// </summary>
        public LogLevel LogLevel { get; set; } = LogLevel.Debug;

        /// <summary>
        /// 日志格式，默认为Full
        /// </summary>
        public LogFormat LogFormat { get; set; } = LogFormat.Full;

        /// <summary>
        /// 参数是否可记录日志的数组
        /// </summary>
        public bool[] ArgsLoggable { get; set; }

        /// <summary>
        /// 是否启用事务
        /// </summary>
        public bool IsTransaction { get; set; }

        /// <summary>
        /// 是否为服务方法
        /// </summary>
        public bool IsService { get; set; }

        /// <summary>
        /// 是否允许匿名访问
        /// </summary>
        public bool AllowAnonymous { get; set; }

        /// <summary>
        /// 允许访问的角色数组
        /// </summary>
        public string[] AllowRoles { get; set; }
    }
}
