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
        public ServiceLogAttribute() { LogLevel = ServiceLogLevel.Information; LogFormat = LogFormat.Full; }

        /// <summary>
        /// 日志级别
        /// </summary>
        public ServiceLogLevel LogLevel { get; set; }

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
    /// <summary>
    /// 服务日志级别枚举
    /// </summary>
    public enum ServiceLogLevel
    {
        /// <summary>
        /// 跟踪级别
        /// </summary>
        Trace,
        /// <summary>
        /// 调试级别
        /// </summary>
        Debug,
        /// <summary>
        /// 信息级别
        /// </summary>
        Information,
        /// <summary>
        /// 警告级别
        /// </summary>
        Warning,
        /// <summary>
        /// 错误级别
        /// </summary>
        Error,
        /// <summary>
        /// 严重级别
        /// </summary>
        Critical,
        /// <summary>
        /// 不记录日志
        /// </summary>
        None
    }
}
