using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading;

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
        /// 日志级别，默认为Information
        /// </summary>
        public ServiceLogLevel LogLevel { get; set; } = ServiceLogLevel.Information;

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
        /// 获取或设置事务的隔离级别，默认为 ReadCommitted
        /// </summary>
        public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;

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

    /// <summary>
    /// 服务扩展方法
    /// </summary>
    public static class ServiceExt
    {
        private static readonly HashSet<Type> _exclusions = new HashSet<Type> { typeof(CancellationToken) };
        /// <summary>
        /// 从方法调用信息加载服务描述
        /// </summary>
        /// <param name="desc">服务描述对象</param>
        /// <param name="method">方法信息</param>
        public static void LoadFrom(this ServiceDescription desc, MethodInfo method)
        {
            // 日志特性
            var logAtt = GetServiceAttribute<ServiceLogAttribute>(method);
            if (logAtt is not null)
            {
                desc.LogFormat = logAtt.LogFormat;
                desc.LogLevel = logAtt.LogLevel;
            }

            // 权限特性
            var permAtt = GetServiceAttribute<ServicePermissionAttribute>(method);
            if (permAtt is not null)
            {
                desc.AllowAnonymous = permAtt.AllowAnonymous;
                if (!string.IsNullOrEmpty(permAtt.AllowRoles))
                    desc.AllowRoles = permAtt.AllowRoles.Split(',');
            }

            // 事务特性
            var transAtt = GetServiceAttribute<TransactionAttribute>(method);
            if (transAtt is not null)
            {
                desc.IsolationLevel = transAtt.IsolationLevel;
                desc.IsTransaction = transAtt.IsTransaction;
            }

            // 服务特性
            var serviceAtt = GetServiceAttribute<ServiceAttribute>(method);
            if (serviceAtt is not null)
            {
                desc.IsService = serviceAtt.IsService;
            }
            desc.ServiceName = serviceAtt?.Name ?? TypeResolverHelper.GetName(method.DeclaringType);

            var serviceMethodAtt = GetServiceAttribute<ServiceMethodAttribute>(method);
            if (serviceMethodAtt is not null)
                desc.IsService = serviceMethodAtt.IsService;
            desc.MethodName = serviceMethodAtt?.MethodName ?? method.Name;

            // 参数日志格式
            var parameters = method.GetParameters();
            desc.ArgsLoggable = new bool[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var paramLogAttr = parameters[i].GetCustomAttribute(typeof(LogAttribute), true) as LogAttribute;
                desc.ArgsLoggable[i] = paramLogAttr == null ? !_exclusions.Contains(parameters[i].ParameterType) : paramLogAttr.Enabled;
            }
        }

        private static T GetServiceAttribute<T>(MethodInfo method) where T : Attribute
        {
            return GetServiceAttributes<T>(method).FirstOrDefault();
        }

        private static IEnumerable<T> GetServiceAttributes<T>(MethodInfo method) where T : Attribute
        {
            IEnumerable<T> methodAttributes = method.GetCustomAttributes<T>();
            IEnumerable<T> declaringTypeAttributes = method.DeclaringType is not null ? method.DeclaringType.GetCustomAttributes<T>()
                : Array.Empty<T>();

            return methodAttributes
                .Concat(declaringTypeAttributes);
        }

        /// <summary>
        /// 展开 TargetInvocationException 获取真实异常
        /// </summary>
        /// <param name="ex">异常对象</param>
        /// <returns>真实异常对象</returns>
        public static Exception UnwrapTargetInvocationException(this Exception ex)
        {
            var inner = ex;
            while (inner is TargetInvocationException && inner.InnerException is not null)
                inner = inner.InnerException;
            return inner;
        }
    }
}
