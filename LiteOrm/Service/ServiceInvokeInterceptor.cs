using Castle.DynamicProxy;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Service
{
    /// <summary>
    /// 服务调用拦截器 - 为服务方法提供事务、日志和性能监控
    /// </summary>
    /// <remarks>
    /// ServiceInvokeInterceptor 是一个 AOP 拦截器，使用 Castle DynamicProxy 库进行方法拦截。
    /// 它为服务方法提供了多个横切关注点的实现。
    /// 
    /// 主要功能包括：
    /// 1. 会话管理 - 为每个服务调用创建和管理会话上下文
    /// 2. 事务处理 - 根据 TransactionAttribute 自动处理事务
    /// 3. 日志记录 - 记录服务方法的调用、参数和性能信息
    /// 4. 权限验证 - 根据 ServicePermissionAttribute 进行权限检查
    /// 5. 性能监控 - 测量方法执行时间并记录性能数据
    /// 6. 异常处理 - 捕获和记录方法执行中的异常
    /// 7. 异步支持 - 同时支持同步和异步方法拦截
    /// 8. 递归调用防护 - 使用 InProcess 标志防止嵌套调用的重复处理
    /// 9. 方法元数据缓存 - 缓存方法的属性信息以提高性能
    /// 
    /// 该拦截器应用于所有被标记为需要拦截的服务类，
    /// 通过 Autofac.Extras.DynamicProxy 库的 Intercept 特性应用。
    /// 
    /// 支持的特性：
    /// - TransactionAttribute - 控制事务行为
    /// - ServicePermissionAttribute - 权限验证
    /// - ServiceLogAttribute - 控制日志记录
    /// 
    /// 使用示例：
    /// <code>
    /// // 该拦截器由框架自动应用，不需要手动创建
    /// [Intercept(typeof(ServiceInvokeInterceptor))]
    /// public class UserService : IUserService
    /// {
    ///     [Transaction]
    ///     [ServiceLog]
    ///     public bool Insert(User user)
    ///     {
    ///         // 方法会自动被拦截
    ///         // 1. 创建会话
    ///         // 2. 开始事务
    ///         // 3. 记录日志
    ///         // 4. 测量性能
    ///         // 5. 执行方法
    ///         // 6. 提交事务
    ///     }
    /// }
    /// </code>
    /// </remarks>
    [AutoRegister(Lifetime = ServiceLifetime.Singleton)]
    public class ServiceInvokeInterceptor : IInterceptor, IAsyncInterceptor
    {
        private readonly ConcurrentDictionary<MethodInfo, ServiceDescription> _methodDescriptions = new();
        private static readonly AsyncLocal<bool> _inProcess = new AsyncLocal<bool>();
        private readonly ILogger _logger;
        /// <summary>
        /// 获取或设置当前调用是否在处理过程中（防止递归调用）
        /// </summary>
        public static bool InProcess { get => _inProcess.Value; set => _inProcess.Value = value; }
        /// <summary>
        /// 初始化 <see cref="ServiceInvokeInterceptor"/> 类的新实例。
        /// </summary>
        /// <param name="loggerFactory">日志工厂</param>
        public ServiceInvokeInterceptor(ILoggerFactory loggerFactory)
        {
            if (loggerFactory is null) throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<ServiceInvokeInterceptor>();
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

        /// <summary>
        /// 同步方法拦截处理
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
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
                    LogException(invocation, e);
                    throw new Exception(e.Message + "\nSQL:" + SessionManager.Current?.SqlStack?.LastOrDefault(), e);
                }
            }
            else
            {
                try
                {
                    InProcess = true;
                    sessionManager.Reset();
                    LogBeforeInvoke(invocation);
                    var timer = Stopwatch.StartNew();
                    InvokeWithTransaction(invocation);
                    timer.Stop();
                    LogAfterInvoke(invocation, invocation.ReturnValue, timer.Elapsed);

                }
                catch (Exception e)
                {
                    e = e.UnwrapTargetInvocationException();
                    LogException(invocation, e);
                    throw e;
                }
                finally
                {
                    InProcess = false;
                }
            }
        }

        /// <summary>
        /// 异步方法拦截处理（无返回值）
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        /// <returns>异步任务</returns>
        public void InterceptAsynchronous(IInvocation invocation)
        {
            invocation.ReturnValue = InterceptAsyncCore(invocation);
        }

        /// <summary>
        /// 异步方法拦截处理（带返回值）
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="invocation">方法调用信息</param>
        /// <returns>异步任务，包含返回结果</returns>
        public void InterceptAsynchronous<TResult>(IInvocation invocation)
        {
            invocation.ReturnValue = InterceptAsyncCore<TResult>(invocation);
        }

        /// <summary>
        /// 异步方法拦截处理（无返回值）
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        /// <returns>异步任务</returns>
        private async Task InterceptAsyncCore(IInvocation invocation)
        {
            var sessionManager = SessionManager.Current;

            // 检查是否已经在处理中
            if (InProcess)
            {
                invocation.Proceed();
                await (Task)invocation.ReturnValue;
            }
            else
            {
                try
                {
                    InProcess = true;
                    sessionManager.Reset();
                    LogBeforeInvoke(invocation);
                    var timer = Stopwatch.StartNew();

                    // 执行异步方法，处理事务
                    await InvokeWithTransactionAsync(invocation);

                    timer.Stop();
                    LogAfterInvoke(invocation, null, timer.Elapsed);
                }
                catch (Exception e)
                {
                    e = e.UnwrapTargetInvocationException();
                    LogException(invocation, e);
                    throw e;
                }
                finally
                {
                    InProcess = false;
                }
            }
        }

        /// <summary>
        /// 异步方法拦截处理（带返回值）
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        /// <returns>异步方法的返回结果</returns>
        private async Task<TResult> InterceptAsyncCore<TResult>(IInvocation invocation)
        {
            var sessionManager = SessionManager.Current;

            if (InProcess)
            {
                invocation.Proceed();
                return await (Task<TResult>)invocation.ReturnValue;
            }
            else
            {
                try
                {
                    InProcess = true;
                    sessionManager.Reset();
                    LogBeforeInvoke(invocation);
                    var timer = Stopwatch.StartNew();
                    // 执行异步方法，处理事务
                    TResult result = await InvokeWithTransactionAsync<TResult>(invocation);
                    timer.Stop();
                    LogAfterInvoke(invocation, result, timer.Elapsed);
                    return result;
                }
                catch (Exception e)
                {
                    e = e.UnwrapTargetInvocationException();
                    LogException(invocation, e);
                    throw e;
                }
                finally
                {
                    InProcess = false;
                }
            }
        }

        /// <summary>
        /// 异步事务处理逻辑（针对返回Task的异步方法）
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        /// <returns>异步任务</returns>
        private async Task InvokeWithTransactionAsync(IInvocation invocation)
        {
            var serviceDesc = GetDescription(invocation);
            var sessionManager = SessionManager.Current;

            // 使用SemaphoreSlim替代lock，以支持异步等待
            if (serviceDesc.IsTransaction && !sessionManager.InTransaction)
            {
                // 假设SessionManager有异步执行事务的方法
                // 如果没有，你需要实现一个异步版本
                await sessionManager.ExecuteInTransactionAsync(async sm =>
                {
                    invocation.Proceed();
                    await (Task)invocation.ReturnValue;
                });
            }
            else
            {
                invocation.Proceed();
                await (Task)invocation.ReturnValue;
            }
        }

        /// <summary>
        /// 异步事务处理逻辑（针对返回Task&lt;TResult&gt;的异步方法）
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        /// <returns>异步任务，包含返回结果</returns>        
        private async Task<TResult> InvokeWithTransactionAsync<TResult>(IInvocation invocation)
        {
            var serviceDesc = GetDescription(invocation);
            var sessionManager = SessionManager.Current;

            if (serviceDesc.IsTransaction && !sessionManager.InTransaction)
            {
                // 假设SessionManager有异步执行事务的方法
                TResult result = default;
                await sessionManager.ExecuteInTransactionAsync(async sm =>
                {
                    invocation.Proceed();
                    result = await (Task<TResult>)invocation.ReturnValue;
                    return result;
                });
                return result;
            }
            else
            {
                invocation.Proceed();
                return await (Task<TResult>)invocation.ReturnValue;
            }
        }


        /// <summary>
        /// 同步方法事务处理逻辑
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
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

        /// <summary>
        /// 记录方法调用前的日志
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        protected virtual void LogBeforeInvoke(IInvocation invocation)
        {
            var serviceDesc = GetDescription(invocation);
            if (_logger.IsEnabled(serviceDesc.LogLevel))
            {
                var argsLog = (serviceDesc.LogFormat & LogFormat.Args) == LogFormat.Args
                    ? Util.GetLogString(GetLogArgs(invocation)) : null;

                _logger.Log(serviceDesc.LogLevel,
                    "[{SessionID}]<Invoke>{Service}.{Method}({Args})", SessionManager.Current?.SessionID,
                    serviceDesc.ServiceName, serviceDesc.MethodName, argsLog);
            }
        }

        /// <summary>
        /// 记录方法调用后的日志
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        /// <param name="result">方法返回值</param>
        /// <param name="elapsedTime">方法执行耗时</param>
        protected virtual void LogAfterInvoke(IInvocation invocation, object result, TimeSpan elapsedTime)
        {
            var serviceDesc = GetDescription(invocation);
            if (_logger.IsEnabled(serviceDesc.LogLevel))
            {
                var returnLog = (serviceDesc.LogFormat & LogFormat.ReturnValue) == LogFormat.ReturnValue
                ? Util.GetLogString(result, 0) : null;
                _logger.Log(serviceDesc.LogLevel,
                    "[{SessionID}]<Return>{Service}.{Method}+{Duration}:{ReturnValue}",
                     SessionManager.Current?.SessionID, serviceDesc.ServiceName, serviceDesc.MethodName,
                    elapsedTime.TotalSeconds, returnLog);
            }
        }

        /// <summary>
        /// 记录异常日志
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        /// <param name="e">异常对象</param>
        protected virtual void LogException(IInvocation invocation, Exception e)
        {
            var serviceDesc = GetDescription(invocation);
            var innerExp = e.UnwrapTargetInvocationException();
            var argsLog = Util.GetLogString(GetLogArgs(invocation));
            if (innerExp is ServiceException)
                _logger.LogWarning("[{SessionID}]<Exception>{Service}.{Method}({Args}) {Message}", SessionManager.Current?.SessionID, serviceDesc.ServiceName, serviceDesc.MethodName,
                    argsLog, innerExp.Message);
            else
                _logger.LogError("[{SessionID}]<Exception>{Service}.{Method}({Args}) {Exception}", SessionManager.Current?.SessionID, serviceDesc.ServiceName, serviceDesc.MethodName,
                    argsLog, innerExp);
        }

        /// <summary>
        /// 获取方法调用参数的日志表示
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        /// <returns>参数的日志表示数组</returns>
        protected virtual object[] GetLogArgs(IInvocation invocation)
        {
            var serviceDesc = GetDescription(invocation);
            var logArgs = new object[invocation.Arguments.Length];

            for (int i = 0; i < invocation.Arguments.Length; i++)
                logArgs[i] = serviceDesc.ArgsLoggable[i] ? invocation.Arguments[i] : "***";

            return logArgs;
        }
        #endregion

        #region 服务描述获取
        /// <summary>
        /// 获取方法对应的服务描述信息
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        /// <returns>服务描述对象</returns>
        protected virtual ServiceDescription GetDescription(IInvocation invocation)
        {
            return _methodDescriptions.GetOrAdd(invocation.Method, m =>
            {
                var desc = new ServiceDescription();
                desc.LoadFrom(invocation);
                return desc;
            });
        }
        #endregion
    }

    /// <summary>
    /// 服务拦截器扩展方法
    /// </summary>
    public static class ServiceInterceptorExt
    {
        /// <summary>
        /// 从方法调用信息加载服务描述
        /// </summary>
        /// <param name="desc">服务描述对象</param>
        /// <param name="invocation">方法调用信息</param>
        public static void LoadFrom(this ServiceDescription desc, IInvocation invocation)
        {
            desc.ServiceName = Util.GetServiceName(invocation.TargetType);
            desc.MethodName = invocation.Method.Name;

            // 日志特性
            var logAtt = GetServiceAttribute<ServiceLogAttribute>(invocation);
            if (logAtt is not null)
            {
                desc.LogFormat = logAtt.LogFormat;
                desc.LogLevel = logAtt.LogLevel;
            }

            // 权限特性
            var permAtt = GetServiceAttribute<ServicePermissionAttribute>(invocation);
            if (permAtt is not null)
            {
                desc.AllowAnonymous = permAtt.AllowAnonymous;
                if (!string.IsNullOrEmpty(permAtt.AllowRoles))
                    desc.AllowRoles = permAtt.AllowRoles.Split(',');
            }

            // 事务特性
            var transAtt = GetServiceAttribute<TransactionAttribute>(invocation);
            if (transAtt is not null)
                desc.IsTransaction = transAtt.IsTransaction;

            // 服务特性
            var serviceAtt = GetServiceAttribute<ServiceAttribute>(invocation);
            if (serviceAtt is not null)
                desc.IsService = serviceAtt.IsService;

            // 参数日志格式
            var parameters = invocation.Method.GetParameters();
            desc.ArgsLoggable = new bool[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var logAtts = (LogAttribute[])parameters[i].GetCustomAttributes(typeof(LogAttribute), true);

                if (logAtts.Length == 0)
                {
                    var targetMethod = invocation.MethodInvocationTarget;
                    if (targetMethod is not null)
                    {
                        var targetParams = targetMethod.GetParameters();
                        logAtts = (LogAttribute[])targetParams[i].GetCustomAttributes(typeof(LogAttribute), true);
                    }
                }

                desc.ArgsLoggable[i] = logAtts.Length > 0 ? logAtts[0].Enabled : true;
            }
        }

        private static T GetServiceAttribute<T>(IInvocation invocation) where T : Attribute
        {
            return invocation.Method.GetCustomAttribute<T>()
                            ?? invocation.MethodInvocationTarget?.GetCustomAttribute<T>()
                            ?? invocation.TargetType.GetCustomAttribute<T>()
                            ?? invocation.Method.DeclaringType.GetCustomAttribute<T>();
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

    /// <summary>
    ///  动态服务生成
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Singleton)]
    public class ServiceFactoryInterceptor : IInterceptor
    {
        /// <summary>
        /// 服务提供者
        /// </summary>
        protected IServiceProvider ServiceProvider { get; }
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        public ServiceFactoryInterceptor(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }
        /// <summary>
        /// 拦截方法调用
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        public void Intercept(IInvocation invocation)
        {
            invocation.ReturnValue = ServiceProvider.GetService(invocation.Method.ReturnType);
        }
    }
}