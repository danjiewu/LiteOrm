using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyOrm.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyOrm.Service
{
    /// <summary>
    /// 服务调用代理
    /// </summary>
    public class ServiceInvokeInterceptor : IInterceptor, IAsyncInterceptor
    {
        private readonly ConcurrentDictionary<MethodInfo, ServiceDescription> _methodDescriptions = new();
        private static readonly AsyncLocal<bool> _inProcess = new AsyncLocal<bool>();
        private ILogger logger;
        public static bool InProcess { get => _inProcess.Value; set => _inProcess.Value = value; }
        public ServiceInvokeInterceptor(ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
            logger = loggerFactory.CreateLogger<ServiceInvokeInterceptor>();
        }

        /// <summary>  
        /// 调用目标方法。  
        /// </summary>  
        /// <param name="invocation">目标方法</param>
        /// <returns>目标方法的返回值。</returns>  
        public void Intercept(IInvocation invocation)
        {
            this.ToInterceptor().Intercept(invocation);
        }

        public void InterceptSynchronous(IInvocation invocation)
        {
            var sessionManager = SessionManager.Current;
            if (InProcess)//如果已在会话中，不再记录日志和处理事务
            {
                try
                {
                    invocation.Proceed();
                }
                catch (Exception e)
                {
                    e = e.UnwrapTargetInvocationException();
                    LogException(logger, invocation, e);
                    throw e;
                }
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

                }
                catch (Exception e)
                {
                    e = e.UnwrapTargetInvocationException();
                    LogException(logger, invocation, e);
                    throw e;
                }
                finally
                {
                    InProcess = false;
                    sessionManager.Finish();

                }
            }
        }

        public void InterceptAsynchronous(IInvocation invocation)
        {
            invocation.ReturnValue = InterceptAsyncCore(invocation);
        }

        public void InterceptAsynchronous<TResult>(IInvocation invocation)
        {
            invocation.ReturnValue = InterceptAsyncCore<TResult>(invocation);
        }

        private async Task InterceptAsyncCore(IInvocation invocation)
        {
            Console.WriteLine($"[Async Before] {invocation.Method.Name}");
            invocation.Proceed();
            await (Task)invocation.ReturnValue;
            Console.WriteLine($"[Async After] {invocation.Method.Name}");
        }

        private async Task<TResult> InterceptAsyncCore<TResult>(IInvocation invocation)
        {
            Console.WriteLine($"[Async<T> Before] {invocation.Method.Name}");
            invocation.Proceed();
            TResult result = await (Task<TResult>)invocation.ReturnValue;
            Console.WriteLine($"[Async<T> After] {invocation.Method.Name} => {result}");
            return result;
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
                logger.LogWarning("<Exception>{Service}.{Method}({Args}) {Message}", serviceDesc.ServiceName, serviceDesc.MethodName,
                    argsLog, innerExp.Message);
            else
                logger.LogError("<Exception>{Service}.{Method}({Args}) {Exception}", serviceDesc.ServiceName, serviceDesc.MethodName,
                    argsLog, innerExp);
        }

        protected virtual void LogAfterInvoke(ILogger logger, IInvocation invocation, TimeSpan elapsedTime)
        {
            var serviceDesc = GetDescription(invocation);
            var returnLog = (serviceDesc.LogFormat & LogFormat.ReturnValue) == LogFormat.ReturnValue
                ? Util.GetLogString(invocation.ReturnValue, 0) : null;
            logger.Log(serviceDesc.LogLevel,
                "<Return>{Service}.{Method}+{Duration}:{ReturnValue}",
                 serviceDesc.ServiceName, serviceDesc.MethodName,
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
                    serviceDesc.ServiceName, serviceDesc.MethodName, argsLog);
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
                        var desc = new ServiceDescription();
                        // 加载属性值
                        desc.LoadFrom(invocation);

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
    public class ServiceFactoryInterceptor : IInterceptor
    {
        protected IServiceProvider ServiceProvider { get; }
        public ServiceFactoryInterceptor(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }
        public void Intercept(IInvocation invocation)
        {
            invocation.ReturnValue = ServiceProvider.GetService(invocation.Method.ReturnType);
        }
    }

    public static class ServiceInterceptorExt
    {
        // 服务描述扩展方法
        public static void LoadFrom(this ServiceDescription desc, IInvocation invocation)
        {
            desc.ServiceName = Util.GetServiceName(invocation.TargetType);
            desc.MethodName = invocation.Method.Name;

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

            // 参数日志格式
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

        private static T GetServiceAttribute<T>(IInvocation invocation) where T : Attribute
        {
            return invocation.Method.GetCustomAttribute<T>()
                            ?? invocation.MethodInvocationTarget?.GetCustomAttribute<T>()
                            ?? invocation.TargetType.GetCustomAttribute<T>()
                            ?? invocation.Method.DeclaringType.GetCustomAttribute<T>();
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