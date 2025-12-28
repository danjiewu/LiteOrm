using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Castle.DynamicProxy;
using System.ComponentModel;

namespace MyOrm.Service
{
    /// <summary>
    /// 服务调用代理
    /// </summary>
    public class ServiceInvokeProxy : IInterceptor
    {
        private string _serviceName;
        private readonly Dictionary<MethodInfo, ServiceDescription> _methodDescriptions = new();
        [ThreadStatic]
        private static bool InProcess;

        public ServiceInvokeProxy(string serviceName)
        {
            _serviceName = serviceName;
        }

        /// <summary>  
        /// 调用目标方法。  
        /// </summary>  
        /// <param name="invocation">目标方法</param>
        /// <returns>目标方法的返回值。</returns>  
        public void Intercept(IInvocation invocation)
        {
            ILogger logger = MyServiceProvider.Current.GetService<ILoggerFactory>()
                        .CreateLogger(_serviceName);
            var sessionManager = SessionManager.Current;
            if (InProcess)//如果已在会话中，不再记录日志和处理事务
            {
                invocation.Proceed();
            }
            else
            {
                try
                {
                    InProcess = true;
                    sessionManager.Start();
                    LogBeforeInvoke(logger, invocation);
                    var timer = Stopwatch.StartNew();
                    InvokeWithTransaction(invocation);
                    timer.Stop();
                    LogAfterInvoke(logger, invocation, timer.Elapsed);
                    InProcess = false;
                }
                catch (Exception e)
                {
                    e = e.UnwrapTargetInvocationException();
                    LogException(logger, invocation, e);
                    throw e;
                }
                finally
                {
                    sessionManager.Finish();
                }
            }
        }

        // 事务处理逻辑
        private void InvokeWithTransaction(IInvocation invocation)
        {
            var serviceDesc = GetDescription(invocation);
            var sessionManager = SessionManager.Current;

            lock (sessionManager)
            {
                if (serviceDesc.IsTransaction && !sessionManager.InTransaction)
                {
                    sessionManager.ExecuteInTransaction(sm => invocation.Proceed());
                }
                else
                {
                    invocation.Proceed();
                }
            }
        }

        #region 日志相关方法
        protected virtual void LogException(ILogger logger, IInvocation invocation, Exception e)
        {
            var serviceDesc = GetDescription(invocation);
            var innerExp = e.UnwrapTargetInvocationException();
            var argsLog = Util.GetLogString(GetLogArgs(invocation));
            if (innerExp is ServiceException)
                logger.LogWarning("<Exception>{Service}.{Method}({Args}) {Message}", _serviceName, serviceDesc.MethodName,
                    argsLog, innerExp.Message);
            else
                logger.LogError("<Exception>{Service}.{Method}({Args}) {Exception}", _serviceName, serviceDesc.MethodName,
                    argsLog, innerExp);
        }

        protected virtual void LogAfterInvoke(ILogger logger, IInvocation invocation, TimeSpan elapsedTime)
        {
            var serviceDesc = GetDescription(invocation);
            var returnLog = (serviceDesc.LogFormat & LogFormat.ReturnValue) == LogFormat.ReturnValue
                ? Util.GetLogString(invocation.ReturnValue, 0) : null;

            logger.Log(serviceDesc.LogLevel,
                "<Return>{Service}.{Method}+{Duration}:{ReturnValue}",
                 _serviceName, serviceDesc.MethodName,
                elapsedTime.TotalSeconds, returnLog);
        }

        protected virtual void LogBeforeInvoke(ILogger logger, IInvocation invocation)
        {
            var serviceDesc = GetDescription(invocation);

            if (logger.IsEnabled(serviceDesc.LogLevel))
            {
                var argsLog = (serviceDesc.LogFormat & LogFormat.Args) == LogFormat.Args
                    ? Util.GetLogString(GetLogArgs(invocation)) : null;

                logger.Log(serviceDesc.LogLevel,
                    "<Invoke>{Service}.{Method}({Args})",
                    _serviceName, serviceDesc.MethodName, argsLog);
            }
        }

        protected virtual object[] GetLogArgs(IInvocation invocation)
        {
            var serviceDesc = GetDescription(invocation);
            var logArgs = new object[invocation.Arguments.Length];

            for (int i = 0; i < invocation.Arguments.Length; i++)
                logArgs[i] = serviceDesc.ArgsLogable[i] ? invocation.Arguments[i] : "***";

            return logArgs;
        }
        #endregion

        #region 服务描述获取
        protected virtual ServiceDescription GetDescription(IInvocation invocation)
        {
            if (!_methodDescriptions.ContainsKey(invocation.Method))
            {
                lock (_methodDescriptions)
                {
                    if (!_methodDescriptions.ContainsKey(invocation.Method))
                    {
                        var desc = new ServiceDescription(invocation.Method.Name);
                        // 加载特性配置
                        desc.LoadFromAttributes(invocation);
                        // 加载参数日志配置
                        desc.LoadParameterLogConfig(invocation);

                        _methodDescriptions[invocation.Method] = desc;
                    }
                }
            }
            return _methodDescriptions[invocation.Method];
        }
        #endregion

    }

    /// <summary>
    ///  动态服务生成
    /// </summary>
    public class ServiceFactoryGenerator : IInterceptor
    {
        protected IServiceProvider ServiceProvider { get; }
        public ServiceFactoryGenerator(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }
        public void Intercept(IInvocation invocation)
        {
            invocation.ReturnValue = ServiceProvider.GetService(invocation.Method.ReturnType);
        }
    }


    public static class ServiceProxyHelper
    {
        // 创建服务调用代理
        public static T CreateServiceInvokeProxy<T>(T target, string serviceName) where T : class
        {
            return CreateServiceInvokeProxy(typeof(T), target, serviceName) as T;
        }

        // 创建服务调用代理（非泛型）
        public static object CreateServiceInvokeProxy(Type targetType, object target, string serviceName)
        {
            if (!targetType.IsInterface)
                throw new ArgumentException("Target type must be an interface", targetType.Name);
            ProxyGenerator proxy = new ProxyGenerator();
            var invokeProxy = new ServiceInvokeProxy(serviceName);
            return proxy.CreateInterfaceProxyWithTarget(targetType, target, invokeProxy);
        }

        public static IServiceCollection AddServiceFactory<T>(this IServiceCollection services) where T : class
        {
            services.AddSingleton<T>(sp =>
            {
                if (!typeof(T).IsInterface)
                    throw new ArgumentException("Target type must be an interface", typeof(T).Name);
                ProxyGenerator proxy = new ProxyGenerator();
                var factoryProxy = new ServiceFactoryGenerator(sp);
                return proxy.CreateInterfaceProxyWithoutTarget<T>(factoryProxy);
            });
            return services;
        }
        public static IServiceCollection AddServiceFactory(this IServiceCollection services, Type serviceFactoryType)
        {
            services.AddSingleton(serviceFactoryType, sp =>
            {
                if (!serviceFactoryType.IsInterface)
                    throw new ArgumentException("Target type must be an interface", serviceFactoryType.Name);
                ProxyGenerator proxy = new ProxyGenerator();
                var factoryProxy = new ServiceFactoryGenerator(sp);
                return proxy.CreateInterfaceProxyWithoutTarget(serviceFactoryType, factoryProxy);
            });
            return services;
        }

        // 服务描述扩展方法
        public static void LoadFromAttributes(this ServiceDescription desc, IInvocation invocation)
        {
            // 日志特性
            var logAtt = GetServiceAttribute<ServiceLogAttribute>(invocation);
            if (logAtt != null)
            {
                desc.LogFormat = logAtt.LogFormat;
                desc.LogLevel = logAtt.LogLevel;
            }

            // 权限特性
            var permAtt = GetServiceAttribute<ServicePermissionAttribute>(invocation);
            if (permAtt != null)
            {
                desc.AllowAnonymous = permAtt.AllowAnonymous;
                if (!string.IsNullOrEmpty(permAtt.AllowRoles))
                    desc.AllowRoles = permAtt.AllowRoles.Split(',');
            }

            // 事务特性
            var transAtt = GetServiceAttribute<TransactionAttribute>(invocation);
            if (transAtt != null)
                desc.IsTransaction = transAtt.IsTransaction;

            // 服务特性
            var serviceAtt = GetServiceAttribute<ServiceAttribute>(invocation);
            if (serviceAtt != null)
                desc.IsService = serviceAtt.IsService;
        }

        private static T GetServiceAttribute<T>(IInvocation invocation) where T : Attribute
        {
            return invocation.Method.GetCustomAttribute<T>()
                            ?? invocation.MethodInvocationTarget?.GetCustomAttribute<T>()
                            ?? invocation.TargetType.GetCustomAttribute<T>()
                            ?? invocation.Method.DeclaringType.GetCustomAttribute<T>();
        }

        // 加载参数日志配置
        public static void LoadParameterLogConfig(this ServiceDescription desc, IInvocation invocation)
        {
            var parameters = invocation.Method.GetParameters();
            desc.ArgsLogable = new bool[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var logAtts = (LogAttribute[])parameters[i].GetCustomAttributes(typeof(LogAttribute), true);

                if (logAtts.Length == 0)
                {
                    var targetMethod = invocation.MethodInvocationTarget;
                    if (targetMethod != null)
                    {
                        var targetParams = targetMethod.GetParameters();
                        logAtts = (LogAttribute[])targetParams[i].GetCustomAttributes(typeof(LogAttribute), true);
                    }
                }

                desc.ArgsLogable[i] = logAtts.Length > 0 ? logAtts[0].Enabled : true;
            }
        }
        public static Exception UnwrapTargetInvocationException(this Exception ex)
        {
            var inner = ex;
            while (inner is TargetInvocationException && inner.InnerException != null)
                inner = inner.InnerException;
            return inner;
        }
    }
}