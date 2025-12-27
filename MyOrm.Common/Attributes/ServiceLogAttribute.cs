using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MyOrm
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface)]
    public class ServiceLogAttribute : Attribute
    {
        public ServiceLogAttribute() { LogLevel = LogLevel.Information; LogFormat = LogFormat.Full; }

        public LogLevel LogLevel { get; set; }
        public LogFormat LogFormat { get; set; }
    }

    [Flags]
    public enum LogFormat
    {
        None = 0,
        Args = 1,
        ReturnValue = 2,
        Full = Args | ReturnValue
    }
}
