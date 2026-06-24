using Castle.DynamicProxy;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
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
    /// 通过 <see cref="InterceptAttribute"/> 特性应用。
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
    [AutoRegister(Lifetime = Lifetime.Scoped)]
    public class ServiceInvokeInterceptor : IInterceptor, IAsyncInterceptor
    {
        /// <summary>
        /// 全局服务异常处理事件。
        /// </summary>
        public static event EventHandler<ServiceExceptionContext> ExceptionHandling;

        /// <summary>
        /// 设置慢查询阈值，超过该时间的方法调用将被记录为慢查询日志。默认值为3秒。
        /// </summary>
        public static TimeSpan SlowQueryThreshold = TimeSpan.FromSeconds(3);
        /// <summary>
        /// 最大允许展开并记录日志的集合长度。
        /// 超过此长度的集合将只记录类型和计数，以避免日志文件过大。
        /// </summary>
        public static int MaxExpandedLogLength { get; set; } = 10;
        private static readonly ConcurrentDictionary<(Type TargetType, MethodInfo Method), ServiceDescription> _methodDescriptions = new();
        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly SessionManager _sessionManager;
        private bool _inProcess;
        /// <summary>
        /// 初始化 <see cref="ServiceInvokeInterceptor"/> 类的新实例。
        /// </summary>
        /// <param name="loggerFactory">日志工厂</param>
        /// <param name="serviceProvider">服务提供者</param>
        /// <param name="sessionManager">会话管理器</param>
        public ServiceInvokeInterceptor(ILoggerFactory loggerFactory, IServiceProvider serviceProvider, SessionManager sessionManager)
        {
            if (loggerFactory is null) throw new ArgumentNullException(nameof(loggerFactory));
            if (serviceProvider is null) throw new ArgumentNullException(nameof(serviceProvider));
            if (sessionManager is null) throw new ArgumentNullException(nameof(sessionManager));
            _logger = loggerFactory.CreateLogger<ServiceInvokeInterceptor>();
            _serviceProvider = serviceProvider;
            _sessionManager = sessionManager;
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
            if (_inProcess)//如果已在会话中，不再记录日志和处理事务
            {
                try
                {
                    InvokeWithTransaction(invocation);
                }
                catch (Exception e)
                {
                    e = e.UnwrapTargetInvocationException();
                    if (TryHandleException(invocation, e, out object handledResult))
                    {
                        invocation.ReturnValue = handledResult;
                        return;
                    }
                    LogException(invocation, e);
                    ExceptionDispatchInfo.Capture(e).Throw();
                    return;
                }
            }
            else
            {
                try
                {
                    _inProcess = true;
                    EnsureCurrentSessionManager();
                    _sessionManager.Reset();
                    LogBeforeInvoke(invocation);
                    var timer = Stopwatch.StartNew();
                    InvokeWithTransaction(invocation);
                    timer.Stop();
                    LogAfterInvoke(invocation, invocation.ReturnValue, timer.Elapsed);

                }
                catch (Exception e)
                {
                    e = e.UnwrapTargetInvocationException();
                    if (TryHandleException(invocation, e, out object handledResult))
                    {
                        invocation.ReturnValue = handledResult;
                        return;
                    }
                    LogException(invocation, e);
                    ExceptionDispatchInfo.Capture(e).Throw();
                    return;
                }
                finally
                {
                    _inProcess = false;
                }
            }
        }

        /// <summary>
        /// 确保当前异步上下文的 ServiceProvider 和 SessionManager 指向当前 Scope 的实例
        /// </summary>
        private void EnsureCurrentSessionManager()
        {
            // 将当前作用域的 ServiceProvider 设置到 AsyncLocal，
            // 供 DAOBase、EntityService 等属性注入的后备机制使用
            ServiceProviderHolder.SetCurrentScope(_serviceProvider);

            if (SessionManager.Current == null)
            {
                SessionManager.SetCurrentFactory(() => _sessionManager);
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
            // 检查是否已经在处理中
            if (_inProcess)
            {
                await InvokeWithTransactionAsync(invocation);
            }
            else
            {
                try
                {
                    _inProcess = true;
                    EnsureCurrentSessionManager();
                    _sessionManager.Reset();
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
                    if (TryHandleException(invocation, e, out _))
                        return;
                    LogException(invocation, e);
                    ExceptionDispatchInfo.Capture(e).Throw();
                    return;
                }
                finally
                {
                    _inProcess = false;
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
            if (_inProcess)
            {
                return await InvokeWithTransactionAsync<TResult>(invocation);
            }
            else
            {
                try
                {
                    _inProcess = true;
                    EnsureCurrentSessionManager();
                    _sessionManager.Reset();
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
                    if (TryHandleException(invocation, e, out object handledResult))
                        return (TResult)handledResult;
                    LogException(invocation, e);
                    ExceptionDispatchInfo.Capture(e).Throw();
                    return default; // 这行实际上永远不会执行，因为上面会抛出异常，但编译器需要一个返回值
                }
                finally
                {
                    _inProcess = false;
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

            // 使用SemaphoreSlim替代lock，以支持异步等待
            if (serviceDesc.IsTransaction && !_sessionManager.InTransaction)
            {
                await _sessionManager.ExecuteInTransactionAsync(async sm =>
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

            if (serviceDesc.IsTransaction && !_sessionManager.InTransaction)
            {
                TResult result = default;
                await _sessionManager.ExecuteInTransactionAsync(async sm =>
                {
                    invocation.Proceed();
                    result = await (Task<TResult>)invocation.ReturnValue;
                    return result;
                }, serviceDesc.IsolationLevel);
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
            if (serviceDesc.IsTransaction && !_sessionManager.InTransaction)
            {
                _sessionManager.ExecuteInTransaction(sm => invocation.Proceed(), serviceDesc.IsolationLevel);
            }
            else
            {
                invocation.Proceed();
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
            if (_logger.IsEnabled((LogLevel)serviceDesc.LogLevel))
            {
                var argsLog = (serviceDesc.LogFormat & LogFormat.Args) == LogFormat.Args
                    ? GetLogString(GetLogArgs(invocation)) : null;

                _logger.Log((LogLevel)serviceDesc.LogLevel,
                    "[{SessionID}]<Invoke>{Service}.{Method}({Args})", _sessionManager.SessionID,
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
            if (_logger.IsEnabled((LogLevel)serviceDesc.LogLevel))
            {
                string returnLog = null;
                if ((serviceDesc.LogFormat & LogFormat.ReturnValue) == LogFormat.ReturnValue)
                {
                    returnLog = GetLogString(result, 0);
                }
                _logger.Log((LogLevel)serviceDesc.LogLevel,
                    "[{SessionID}]<Return>{Service}.{Method}+{Duration}:{ReturnValue}",
                     _sessionManager.SessionID, serviceDesc.ServiceName, serviceDesc.MethodName,
                    elapsedTime.TotalSeconds, returnLog);
            }
            if (elapsedTime > SlowQueryThreshold)//记录慢查询日志
            {
                _logger.LogWarning("[{SessionID}]<Slow>{Service}.{Method} took {Duration} seconds", _sessionManager.SessionID, serviceDesc.ServiceName, serviceDesc.MethodName, elapsedTime.TotalSeconds);
                ValueStringBuilder sb = ValueStringBuilder.Create(512);
                int row = 1;
                foreach (var sql in _sessionManager.SqlStack.Reverse() ?? Array.Empty<string>())
                {
                    if (sb.Length > 0) { sb.Append("\n"); }
                    sb.Append($"{row++}:");
                    sb.Append(sql);
                }
                _logger.LogWarning("[{SessionID}]<SlowSQL>{SQL}", _sessionManager.SessionID, sb.ToString());
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
            string argsLog = GetLogString(GetLogArgs(invocation));
            if (innerExp is ServiceException)
                _logger.LogWarning("[{SessionID}]<Exception>{Service}.{Method}({Args}) {Message}", _sessionManager.SessionID, serviceDesc.ServiceName, serviceDesc.MethodName,
                    argsLog, innerExp.Message);
            else
                _logger.LogError("[{SessionID}]<Exception>{Service}.{Method}({Args}) {Exception}", _sessionManager.SessionID, serviceDesc.ServiceName, serviceDesc.MethodName,
                    argsLog, innerExp);
        }

        /// <summary>
        /// 尝试通过方法级 hook 或全局事件处理异常。
        /// </summary>
        protected virtual bool TryHandleException(IInvocation invocation, Exception exception, out object handledResult)
        {
            var context = CreateExceptionContext(invocation, exception);
            InvokeServiceExceptionHooks(invocation, context);
            OnExceptionHandling(context);

            if (!context.Handled)
            {
                handledResult = null;
                return false;
            }

            handledResult = BuildHandledReturnValue(context);
            return true;
        }

        /// <summary>
        /// 创建异常处理上下文。
        /// </summary>
        protected virtual ServiceExceptionContext CreateExceptionContext(IInvocation invocation, Exception exception)
        {
            var serviceDesc = GetDescription(invocation);
            var method = invocation.MethodInvocationTarget ?? invocation.Method;
            var methodReturnType = method.ReturnType;
            return new ServiceExceptionContext(
                exception,
                invocation.InvocationTarget,
                invocation.TargetType,
                serviceDesc.ServiceName,
                method,
                invocation.Arguments?.ToArray() ?? Array.Empty<object>(),
                GetLogArgs(invocation),
                _sessionManager.SessionID,
                _sessionManager.SqlStack?.ToArray() ?? Array.Empty<string>(),
                methodReturnType,
                GetHandledResultType(methodReturnType));
        }

        /// <summary>
        /// 执行方法级异常 hook。
        /// </summary>
        protected virtual void InvokeServiceExceptionHooks(IInvocation invocation, ServiceExceptionContext context)
        {
            var serviceDesc = GetDescription(invocation);
            foreach (var hookAttribute in serviceDesc.ExceptionHooks ?? Array.Empty<ExceptionHookAttribute>())
            {
                var hook = ResolveExceptionHook(hookAttribute.HookType);
                hook.OnException(context);

                if (hookAttribute.Mode == ServiceExceptionHookMode.Notify && context.Handled)
                {
                    throw new InvalidOperationException($"Exception hook {hookAttribute.HookType.FullName} is configured as Notify but marked {context.ServiceName}.{context.MethodName} as handled.");
                }

                if (context.Handled)
                    break;
            }
        }

        /// <summary>
        /// 触发全局异常处理事件。
        /// </summary>
        protected virtual void OnExceptionHandling(ServiceExceptionContext context)
        {
            ExceptionHandling?.Invoke(this, context);
        }

        /// <summary>
        /// 构建处理后的返回值。
        /// </summary>
        protected virtual object BuildHandledReturnValue(ServiceExceptionContext context)
        {
            if (!context.HasResult)
            {
                if (context.MethodReturnType == typeof(Task))
                    return Task.CompletedTask;
                return null;
            }

            if (!context.ResultAssigned)
                throw new InvalidOperationException($"Method {context.ServiceName}.{context.MethodName} was marked handled, but no result was assigned.");

            return context.Result;
        }

        private IServiceExceptionHook ResolveExceptionHook(Type hookType)
        {
            var hook = ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider, hookType);
            if (hook is not IServiceExceptionHook serviceExceptionHook)
                throw new InvalidOperationException($"Resolved exception hook {hookType.FullName} does not implement {typeof(IServiceExceptionHook).FullName}.");
            return serviceExceptionHook;
        }

        private static Type GetHandledResultType(Type returnType)
        {
            if (returnType == typeof(void) || returnType == typeof(Task))
                return null;
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                return returnType.GetGenericArguments()[0];
            return returnType;
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
                logArgs[i] = serviceDesc.ArgsLoggable[i] ? invocation.Arguments[i] : "*";

            return logArgs;
        }


        /// <summary>
        /// 生成对象列表用于日志记录的字符串。
        /// 自动处理集合的深度展开（在限制长度内）。
        /// </summary>
        /// <param name="values">待记录日志对象数组</param>
        /// <param name="expandDepth"></param>
        /// <returns>日志字符串</returns>
        public static string GetLogString(object[] values, int expandDepth = 1)
        {
            var sb = ValueStringBuilder.Create(128);
            int expand = values.Length > MaxExpandedLogLength ? 0 : expandDepth;
            foreach (var o in values)
            {
                if (sb.Length > 0) sb.Append(",");
                GetLogString(ref sb, o, expand);
            }
            string result = sb.ToString();
            sb.Dispose();
            return result;
        }

        /// <summary>
        /// 生成对象用于日志记录的字符串。
        /// </summary>
        /// <param name="obj">待记录日志对象</param>
        /// <param name="expandDepth">当前递归展开深度，默认为1。超过最大展开长度的集合将不再展开。</param>
        /// <returns></returns>
        public static string GetLogString(object obj, int expandDepth = 1)
        {
            var sb = ValueStringBuilder.Create(128);
            GetLogString(ref sb, obj, expandDepth);
            string result = sb.ToString();
            sb.Dispose();
            return result;
        }

        /// <summary>
        /// 递归获取对象的日志详细信息。
        /// 处理字节数组（Base64 转码）、集合（展开）、实现了 ILogable 的对象及值类型。
        /// </summary>
        /// <param name="sb">用于构建日志字符串的 StringBuilder。</param>
        /// <param name="obj">目标对象。</param>
        /// <param name="expandDepth">当前递归展开深度。</param>
        /// <returns>日志文本。</returns>
        public static void GetLogString(ref ValueStringBuilder sb, object obj, int expandDepth)
        {
            if (obj is null)
            {
                sb.Append("null");
                return;
            }

            if (obj is string str)
            {
                sb.Append(str);
                return;
            }

            if (obj is byte[])
            {
                byte[] bytes = (byte[])obj;
                if (bytes.Length > 1024)
                {
                    sb.Append("[bytes:");
                    sb.Append(bytes.Length.ToString());
                    sb.Append("]");
                    return;
                }
                else
                {
                    sb.Append(Convert.ToBase64String(bytes));
                    return;
                }
            }

            if (obj is ILogable logable)
            {
                sb.Append("{");
                sb.Append(logable.ToLog());
                sb.Append("}");
                return;
            }

            if (obj is Array || obj is ICollection)
            {
                int count = obj is Array ? ((Array)obj).Length : ((ICollection)obj).Count;
                if (expandDepth > 0 && count <= MaxExpandedLogLength)
                {
                    sb.Append("{");
                    bool first = true;
                    foreach (object value in (IEnumerable)obj)
                    {
                        if (!first) sb.Append(",");
                        first = false;
                        GetLogString(ref sb, value, expandDepth - 1);
                    }
                    sb.Append("}");
                    return;
                }
                else
                {
                    sb.Append(obj.GetType().Name);
                    sb.Append("[");
                    sb.Append(count.ToString());
                    sb.Append("]");
                    return;
                }
            }

            if (obj.GetType().IsValueType)
            {
                sb.Append(Convert.ToString(obj));
                return;
            }

            sb.Append("{");
            sb.Append(Convert.ToString(obj));
            sb.Append("}");
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
            return _methodDescriptions.GetOrAdd((invocation.TargetType, invocation.Method), _ =>
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
        private static readonly HashSet<Type> _exclusions = new HashSet<Type> { typeof(CancellationToken) };
        /// <summary>
        /// 从方法调用信息加载服务描述
        /// </summary>
        /// <param name="desc">服务描述对象</param>
        /// <param name="invocation">方法调用信息</param>
        public static void LoadFrom(this ServiceDescription desc, IInvocation invocation)
        {
            desc.ServiceName = GetServiceName(invocation.TargetType);
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
            {
                desc.IsolationLevel = transAtt.IsolationLevel;
                desc.IsTransaction = transAtt.IsTransaction;
            }

            // 服务特性
            var serviceAtt = GetServiceAttribute<ServiceAttribute>(invocation);
            if (serviceAtt is not null)
                desc.IsService = serviceAtt.IsService;

            desc.ExceptionHooks = GetServiceAttributes<ExceptionHookAttribute>(invocation).ToArray();

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

                desc.ArgsLoggable[i] = logAtts.Length > 0 ? logAtts[0].Enabled : !_exclusions.Contains(parameters[i].ParameterType);
            }
        }
        /// <summary>
        /// 获取服务类型的短名称。
        /// 对于泛型类型，会返回类似 "GenericType&lt;T&gt;" 的可读格式。
        /// </summary>
        /// <param name="serviceType">目标服务类型。</param>
        /// <returns>格式化后的服务名称。</returns>
        private static string GetServiceName(Type serviceType)
        {
            if (serviceType.IsGenericType)
            {
                int backtickIndex = serviceType.Name.IndexOf('`');
                return serviceType.Name.Substring(0, backtickIndex) + "<" + String.Join(",", from t in serviceType.GetGenericArguments() select t.Name) + ">";
            }
            else
            {
                return serviceType.Name;
            }
        }
        private static T GetServiceAttribute<T>(IInvocation invocation) where T : Attribute
        {
            return GetServiceAttributes<T>(invocation).FirstOrDefault();
        }

        private static IEnumerable<T> GetServiceAttributes<T>(IInvocation invocation) where T : Attribute
        {
            IEnumerable<T> methodAttributes = invocation.Method.GetCustomAttributes<T>();
            IEnumerable<T> targetMethodAttributes = invocation.MethodInvocationTarget is not null && invocation.MethodInvocationTarget != invocation.Method
                ? invocation.MethodInvocationTarget.GetCustomAttributes<T>()
                : Array.Empty<T>();
            IEnumerable<T> targetTypeAttributes = invocation.TargetType.GetCustomAttributes<T>();
            IEnumerable<T> declaringTypeAttributes = invocation.Method.DeclaringType is not null && invocation.Method.DeclaringType != invocation.TargetType
                ? invocation.Method.DeclaringType.GetCustomAttributes<T>()
                : Array.Empty<T>();

            return methodAttributes
                .Concat(targetMethodAttributes)
                .Concat(targetTypeAttributes)
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

    /// <summary>
    ///  动态服务生成
    /// </summary>
    [AutoRegister(Lifetime = Lifetime.Singleton)]
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
        /// 拦截方法调用，从服务提供者获取请求的服务实例。
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        /// <remarks>
        /// 该方法会拦截对服务工厂接口的调用，根据方法的返回类型从服务提供者中获取对应的服务实例。
        /// </remarks>
        public void Intercept(IInvocation invocation)
        {
            invocation.ReturnValue = ServiceProvider.GetService(invocation.Method.ReturnType);
        }
    }
}