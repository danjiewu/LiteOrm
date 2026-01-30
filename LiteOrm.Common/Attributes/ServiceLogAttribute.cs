using Microsoft.Extensions.Logging;
using System;

namespace LiteOrm
{
    /// <summary>
    /// 服务日志特性，用于配置服务方法的日志记录级别和格式
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface)]
    public class ServiceLogAttribute : Attribute
    {
        /// <summary>
        /// 默认构造函数，设置日志级别为Information，日志格式为Full
        /// </summary>
        public ServiceLogAttribute() { LogLevel = LogLevel.Information; LogFormat = LogFormat.Full; }

        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel LogLevel { get; set; }

        /// <summary>
        /// 日志格式
        /// </summary>
        public LogFormat LogFormat { get; set; }
    }

    /// <summary>
    /// 日志格式枚举，定义日志记录的内容
    /// </summary>
    [Flags]
    public enum LogFormat
    {
        /// <summary>
        /// 不记录任何内容
        /// </summary>
        None = 0,

        /// <summary>
        /// 记录方法参数
        /// </summary>
        Args = 1,

        /// <summary>
        /// 记录返回值
        /// </summary>
        ReturnValue = 2,

        /// <summary>
        /// 记录完整的调用信息（参数和返回值）
        /// </summary>
        Full = Args | ReturnValue
    }
}
