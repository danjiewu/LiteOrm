using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MyOrm.Service
{
    [Serializable]
    public class ServiceDescription
    {
        public string ServiceName { get; set; }
        public string MethodName { get; set; }
        public LogLevel LogLevel { get; set; } = LogLevel.Debug;
        public LogFormat LogFormat { get; set; } = LogFormat.Full;
        public bool[] ArgsLogable { get; set; }
        public bool IsTransaction { get; set; }
        public bool IsService { get; set; }
        public bool AllowAnonymous { get; set; }
        public string[] AllowRoles { get; set; }
    }
}
