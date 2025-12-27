using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MyOrm.Service
{
    [Serializable]
    public class ServiceDescription
    {
        public ServiceDescription(string methodName)
        {
            MethodName = methodName;
            LogLevel = LogLevel.Debug;
            LogFormat = LogFormat.Full;
        }

        public string MethodName { get; private set; }
        public LogLevel LogLevel { get; set; }
        public LogFormat LogFormat { get; set; }
        public bool[] ArgsLogable { get; set; }
        public bool IsTransaction { get; set; }
        public bool IsService { get; set; }
        public bool AllowAnonymous { get; set; }
        public string[] AllowRoles { get; set; }
    }
}
