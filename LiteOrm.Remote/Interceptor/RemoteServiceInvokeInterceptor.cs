using Castle.DynamicProxy;
using LiteOrm.Common;
using LiteOrm.Service;
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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Remote
{
    /// <summary>
    /// 远程服务调用拦截器 - 通过 <see cref="IRemoteServiceTransport"/> 将接口方法调用转发到远程服务。
    /// </summary>
    /// <remarks>
    /// RemoteServiceInvokeInterceptor 是一个 AOP 拦截器，使用 Castle DynamicProxy 拦截接口方法调用，
    /// 将服务名、方法名、参数序列化后通过 <see cref="IRemoteServiceTransport"/> 发送到远程服务，
    /// 并将响应反序列化后赋值给 <see cref="IInvocation.ReturnValue"/>。
    /// 
    /// 主要功能包括：
    /// 1. 请求构建 - 将服务类型、方法名、参数序列化为 <see cref="RemoteInvocationRequest"/>
    /// 2. 远程调用 - 通过注入的传输层异步调用远程服务
    /// 3. 结果反序列化 - 将远程响应反序列化为方法返回类型，支持 void / Task / Task&lt;T&gt; / 同步返回
    /// 4. 异常处理 - 远程异常封装为 <see cref="ServiceException"/> 抛出，并触发本地异常 hook
    /// 5. 日志记录 - 记录调用前后、参数、耗时、慢调用、异常等
    /// 6. 异步支持 - 同时支持同步和异步方法拦截
    /// 7. 方法元数据缓存 - 缓存方法的特性信息以提高性能
    /// </remarks>
    [AutoRegister(Lifetime = Lifetime.Scoped)]
    public class RemoteServiceInvokeInterceptor : IInterceptor, IAsyncInterceptor
    {
        /// <summary>
        /// 全局服务异常处理事件。
        /// </summary>
        public static event EventHandler<ServiceExceptionContext> ExceptionHandling;

        /// <summary>
        /// 设置慢调用阈值，超过该时间的方法调用将被记录为慢调用日志。默认值为3秒。
        /// </summary>
        public static TimeSpan SlowQueryThreshold = TimeSpan.FromSeconds(3);

        /// <summary>
        /// 最大允许展开并记录日志的集合长度。
        /// 超过此长度的集合将只记录类型和计数，以避免日志文件过大。
        /// </summary>
        public static int MaxExpandedLogLength { get; set; } = 10;

        private static readonly ConcurrentDictionary<(Type TargetType, MethodInfo Method), ServiceDescription> _methodDescriptions = new();
        /// <summary>
        /// 代理类型 → 推断的服务接口类型缓存。<see cref="IInvocation.TargetType"/> 在
        /// <c>CreateInterfaceProxyWithoutTarget</c> 场景下为 null，此时需从代理对象实现的接口中
        /// 推断最派生的服务接口（叶子接口），避免回退到方法声明所在的基接口。
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Type> _proxyServiceTypeCache = new();
        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        // 反射缓存：用于在运行时以结果类型调用 InvokeTransportTaskAsync<TResult>
        private static readonly MethodInfo _invokeTransportTaskTAsyncMethod =
            (from m in typeof(RemoteServiceInvokeInterceptor).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
             where m.Name == nameof(InvokeTransportTaskAsync) && m.IsGenericMethod
             select m).First();

        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IRemoteServiceTransport _transport;

        /// <summary>
        /// 初始化 <see cref="RemoteServiceInvokeInterceptor"/> 类的新实例。
        /// </summary>
        /// <param name="loggerFactory">日志工厂</param>
        /// <param name="serviceProvider">服务提供者</param>
        /// <param name="transport">远程调用传输层</param>
        public RemoteServiceInvokeInterceptor(ILoggerFactory loggerFactory, IServiceProvider serviceProvider, IRemoteServiceTransport transport)
        {
            if (loggerFactory is null) throw new ArgumentNullException(nameof(loggerFactory));
            if (serviceProvider is null) throw new ArgumentNullException(nameof(serviceProvider));
            if (transport is null) throw new ArgumentNullException(nameof(transport));
            _logger = loggerFactory.CreateLogger<RemoteServiceInvokeInterceptor>();
            _serviceProvider = serviceProvider;
            _transport = transport;
        }

        /// <summary>
        /// 调用目标方法，将调用转发到远程服务。
        /// </summary>
        /// <param name="invocation">目标方法</param>
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
            try
            {
                LogBeforeInvoke(invocation);
                var timer = Stopwatch.StartNew();
                RemoteInvokeCore(invocation);
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
        }

        /// <summary>
        /// 异步方法拦截处理（无返回值）
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        public void InterceptAsynchronous(IInvocation invocation)
        {
            invocation.ReturnValue = InterceptAsyncCore(invocation);
        }

        /// <summary>
        /// 异步方法拦截处理（带返回值）
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="invocation">方法调用信息</param>
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
            try
            {
                LogBeforeInvoke(invocation);
                var timer = Stopwatch.StartNew();

                // 执行异步方法，远程调用
                await InvokeCoreAsync(invocation);

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
        }

        /// <summary>
        /// 异步方法拦截处理（带返回值）
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        /// <returns>异步方法的返回结果</returns>
        private async Task<TResult> InterceptAsyncCore<TResult>(IInvocation invocation)
        {
            try
            {
                LogBeforeInvoke(invocation);
                var timer = Stopwatch.StartNew();
                // 执行异步方法，远程调用
                TResult result = await InvokeCoreAsync<TResult>(invocation);
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
        }

        /// <summary>
        /// 异步远程调用处理逻辑（针对返回Task的异步方法）
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        /// <returns>异步任务</returns>
        private async Task InvokeCoreAsync(IInvocation invocation)
        {
            // RemoteInvokeCore 会将 ReturnValue 设置为表示异步远程调用的 Task
            RemoteInvokeCore(invocation);
            await (Task)invocation.ReturnValue;
        }

        /// <summary>
        /// 异步远程调用处理逻辑（针对返回Task&lt;TResult&gt;的异步方法）
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        /// <returns>异步任务，包含返回结果</returns>
        private async Task<TResult> InvokeCoreAsync<TResult>(IInvocation invocation)
        {
            RemoteInvokeCore(invocation);
            return await (Task<TResult>)invocation.ReturnValue;
        }


        /// <summary>
        /// 远程调用核心逻辑。根据方法返回类型决定调用方式：
        /// - void：同步等待远程调用完成
        /// - Task：将 ReturnValue 设置为表示异步远程调用的 Task
        /// - Task&lt;T&gt;：将 ReturnValue 设置为表示异步远程调用并携带结果的 Task&lt;T&gt;
        /// - 其它：同步等待远程调用完成并反序列化结果赋值给 ReturnValue
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        private void RemoteInvokeCore(IInvocation invocation)
        {
            var method = invocation.Method;
            var returnType = method.ReturnType;
            var request = BuildRequest(invocation);
            var writeBackPlan = BuildWriteBackPlan(invocation);
            var cancellationToken = ExtractCancellationToken(invocation);
            var serviceInfo = $"{request.ServiceName}.{method.Name}";

            if (returnType == typeof(void))
            {
                var response = InvokeTransportSync(request, serviceInfo, cancellationToken);
                EnsureSuccess(response, serviceInfo);
                ApplyWriteBack(response, writeBackPlan, invocation);
                return;
            }

            if (returnType == typeof(Task))
            {
                invocation.ReturnValue = InvokeTransportTaskAsync(request, serviceInfo, cancellationToken, writeBackPlan, invocation);
                return;
            }

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = returnType.GetGenericArguments()[0];
                var genericMethod = _invokeTransportTaskTAsyncMethod.MakeGenericMethod(resultType);
                invocation.ReturnValue = genericMethod.Invoke(this, new object[] { request, serviceInfo, cancellationToken, writeBackPlan, invocation });
                return;
            }

            {
                var response = InvokeTransportSync(request, serviceInfo, cancellationToken);
                EnsureSuccess(response, serviceInfo);
                ApplyWriteBack(response, writeBackPlan, invocation);
                invocation.ReturnValue = DeserializeResult(response, returnType);
            }
        }

        /// <summary>
        /// 从方法调用参数中提取 <see cref="CancellationToken"/>。若方法无 CancellationToken 参数则返回 <see cref="CancellationToken.None"/>。
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        /// <returns>提取到的 CancellationToken，或 None。</returns>
        private static CancellationToken ExtractCancellationToken(IInvocation invocation)
        {
            var parameters = invocation.Method.GetParameters();
            var arguments = invocation.Arguments ?? Array.Empty<object>();
            for (int i = 0; i < arguments.Length && i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType == typeof(CancellationToken) && arguments[i] is CancellationToken token)
                    return token;
            }
            return CancellationToken.None;
        }

        /// <summary>
        /// 根据参数上的 <see cref="ArgumentOutAttribute"/> 标记构建回写计划。
        /// 回写参数索引从 <see cref="MethodInfo"/> 推算，不再存储在请求中。
        /// </summary>
        private static List<WriteBackEntry> BuildWriteBackPlan(IInvocation invocation)
        {
            var plan = new List<WriteBackEntry>();
            var parameters = invocation.Method.GetParameters();
            var arguments = invocation.Arguments ?? Array.Empty<object>();

            int argListIndex = 0;
            for (int i = 0; i < arguments.Length && i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                if (paramType == typeof(CancellationToken))
                    continue;

                var paramAttr = parameters[i].GetCustomAttribute<ArgumentOutAttribute>(true);
                if (paramAttr is not null)
                {
                    plan.Add(new WriteBackEntry(argListIndex, i, paramAttr));
                }

                argListIndex++;
            }
            return plan;
        }

        /// <summary>
        /// 将服务端响应中的回写参数值应用到客户端原始参数对象。
        /// 通过 <see cref="ArgumentOutHandlerResolver"/> 创建处理器实例，调用 <see cref="IArgumentOutHandler.WriteBack"/> 回写。
        /// 集合模式（<see cref="ArgumentMode.Collection"/>）下，反序列化返回值列表后逐项回写。
        /// <see cref="OutputArgument.Value"/> 反序列化后为 <see cref="JsonElement"/>，可能含 $type 包装。
        /// </summary>
        private void ApplyWriteBack(RemoteInvocationResponse response, List<WriteBackEntry> plan, IInvocation invocation)
        {
            if (plan is null || plan.Count == 0) return;
            if (response?.OutArguments is null || response.OutArguments.Count == 0) return;

            foreach (var wb in response.OutArguments)
            {
                var entryIndex = -1;
                for (int i = 0; i < plan.Count; i++)
                {
                    if (plan[i].ArgListIndex == wb.ArgumentIndex) { entryIndex = i; break; }
                }
                if (entryIndex < 0) continue;
                var entry = plan[entryIndex];

                if (entry.OrigArgIndex >= invocation.Arguments.Length) continue;
                var origArg = invocation.Arguments[entry.OrigArgIndex];
                if (origArg is null) continue;

                // 通过 Resolver 创建处理器实例（DI 优先 + 构造函数传参）
                var handler = ArgumentOutHandlerResolver.Resolve(entry.Attribute);
                if (handler is null) continue;

                // Value 反序列化后为 JsonElement，可能含 $type 包装
                var valueElement = ToJsonElement(wb.Value);
                if (valueElement is null) continue;

                if (entry.Attribute.Mode == ArgumentMode.Collection)
                {
                    // 集合模式：反序列化返回值列表，逐项回写到原始集合对应元素
                    if (origArg is not IList origList)
                        continue;

                    var listType = typeof(List<>).MakeGenericType(handler.ReturnType);

                    var returnValues = DeserializeTypedValue(valueElement.Value, listType) as IList;
                    if (returnValues is null) continue;

                    // 逐项回写（按索引对应）
                    for (int i = 0; i < origList.Count && i < returnValues.Count; i++)
                    {
                        handler.WriteBack(origList[i], returnValues[i]);
                    }
                }
                else
                {
                    // 单对象模式：按 ReturnType 反序列化并回写
                    var returnValue = DeserializeTypedValue(valueElement.Value, handler.ReturnType);
                    if (returnValue is null) continue;

                    handler.WriteBack(origArg, returnValue);
                }
            }
        }

        /// <summary>
        /// 将 <see cref="OutputArgument.Value"/>（反序列化后可能为 <see cref="JsonElement"/> 或 <see cref="TypeWrappedValue"/>）
        /// 转换为 <see cref="JsonElement"/>。
        /// </summary>
        private static JsonElement? ToJsonElement(object value)
        {
            if (value is null) return null;
            if (value is JsonElement element) return element;
            // 如果是其他类型，序列化为 JsonElement
            var json = JsonSerializer.Serialize(value, value.GetType(), _serializerOptions);
            return JsonDocument.Parse(json).RootElement.Clone();
        }

        /// <summary>
        /// 反序列化类型化值。若 <paramref name="element"/> 为 <c>$type</c> 包装，则按实际类型反序列化；
        /// 否则按 <paramref name="expectedType"/> 反序列化。
        /// </summary>
        private static object DeserializeTypedValue(JsonElement element, Type expectedType)
        {
            if (element.ValueKind == JsonValueKind.Null)
                return expectedType.IsValueType ? Activator.CreateInstance(expectedType) : null;

            // 检查 $type 包装
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty("$type", out var typeProp))
            {
                var typeName = typeProp.GetString();
                var actualType = Type.GetType(typeName);
                if (actualType != null && element.TryGetProperty("$value", out var valueProp))
                    return JsonSerializer.Deserialize(valueProp.GetRawText(), actualType, _serializerOptions);
            }

            return JsonSerializer.Deserialize(element.GetRawText(), expectedType, _serializerOptions);
        }

        /// <summary>
        /// 回写计划项：记录每个需要回写的参数在请求 Arguments 列表中的索引、
        /// 在 invocation.Arguments 中的原始索引，以及回写特性配置。
        /// </summary>
        private readonly struct WriteBackEntry
        {
            public int ArgListIndex { get; }
            public int OrigArgIndex { get; }
            public ArgumentOutAttribute Attribute { get; }

            public WriteBackEntry(int argListIndex, int origArgIndex, ArgumentOutAttribute attribute)
            {
                ArgListIndex = argListIndex;
                OrigArgIndex = origArgIndex;
                Attribute = attribute;
            }
        }

        /// <summary>
        /// 从当前方法调用构建远程调用请求。
        /// 直接使用 <see cref="MethodInfo"/> 作为 <see cref="RemoteInvocationRequest.Method"/>，
        /// 序列化时由 <see cref="RemoteInvocationRequestConverter"/> 从中提取方法名与参数类型。
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        /// <returns>远程调用请求</returns>
        protected virtual RemoteInvocationRequest BuildRequest(IInvocation invocation)
        {
            var serviceType = GetServiceType(invocation);
            var method = invocation.Method;
            var parameters = method.GetParameters();
            var arguments = invocation.Arguments ?? Array.Empty<object>();

            // 过滤 CancellationToken 参数，保留其余参数原值
            var args = new System.Collections.Generic.List<object>(arguments.Length);
            for (int i = 0; i < arguments.Length && i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                if (paramType == typeof(CancellationToken))
                    continue;
                args.Add(arguments[i]);
            }

            var request = new RemoteInvocationRequest
            {
                ServiceName = TypeResolverHelper.GetName(serviceType),
                Method = method,
                Arguments = args.ToArray(),
            };
            return request;
        }

        /// <summary>
        /// 同步调用传输层（在客户端线程上阻塞等待）。用于同步拦截路径。
        /// </summary>
        /// <param name="request">远程调用请求</param>
        /// <param name="serviceInfo">用于异常信息的服务描述</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>远程调用响应</returns>
        protected virtual RemoteInvocationResponse InvokeTransportSync(RemoteInvocationRequest request, string serviceInfo, CancellationToken cancellationToken)
        {
            try
            {
                return _transport.InvokeAsync(request, cancellationToken).GetAwaiter().GetResult();
            }
            catch (RemoteTransportException)
            {
                throw;
            }
            catch (Exception ex) when (!(ex is ServiceException))
            {
                throw new RemoteTransportException($"Failed to invoke remote service {serviceInfo}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 异步调用传输层，返回表示远程调用过程的 Task（用于返回 Task 的方法）。
        /// </summary>
        /// <param name="request">远程调用请求</param>
        /// <param name="serviceInfo">用于异常信息的服务描述</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="writeBackPlan">回写计划，调用成功后应用到原始参数对象。</param>
        /// <param name="invocation">方法调用信息，用于访问原始参数对象。</param>
        private async Task InvokeTransportTaskAsync(RemoteInvocationRequest request, string serviceInfo, CancellationToken cancellationToken, List<WriteBackEntry> writeBackPlan, IInvocation invocation)
        {
            RemoteInvocationResponse response;
            try
            {
                response = await _transport.InvokeAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (RemoteTransportException)
            {
                throw;
            }
            catch (Exception ex) when (!(ex is ServiceException))
            {
                throw new RemoteTransportException($"Failed to invoke remote service {serviceInfo}: {ex.Message}", ex);
            }
            EnsureSuccess(response, serviceInfo);
            ApplyWriteBack(response, writeBackPlan, invocation);
        }

        /// <summary>
        /// 异步调用传输层，返回携带结果的 Task&lt;TResult&gt;（用于返回 Task&lt;T&gt; 的方法）。
        /// </summary>
        /// <param name="request">远程调用请求</param>
        /// <param name="serviceInfo">用于异常信息的服务描述</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="writeBackPlan">回写计划，调用成功后应用到原始参数对象。</param>
        /// <param name="invocation">方法调用信息，用于访问原始参数对象。</param>
        private async Task<TResult> InvokeTransportTaskAsync<TResult>(RemoteInvocationRequest request, string serviceInfo, CancellationToken cancellationToken, List<WriteBackEntry> writeBackPlan, IInvocation invocation)
        {
            RemoteInvocationResponse response;
            try
            {
                response = await _transport.InvokeAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (RemoteTransportException)
            {
                throw;
            }
            catch (Exception ex) when (!(ex is ServiceException))
            {
                throw new RemoteTransportException($"Failed to invoke remote service {serviceInfo}: {ex.Message}", ex);
            }
            EnsureSuccess(response, serviceInfo);
            ApplyWriteBack(response, writeBackPlan, invocation);
            return (TResult)DeserializeResult(response, typeof(TResult));
        }

        /// <summary>
        /// 校验远程响应是否成功，失败则抛出 <see cref="ServiceException"/>。
        /// </summary>
        protected static void EnsureSuccess(RemoteInvocationResponse response, string serviceInfo)
        {
            if (response is null)
                throw new RemoteTransportException($"Remote service {serviceInfo} returned null response.");
            if (!response.Success)
                throw new ServiceException(
                    $"Remote service {serviceInfo} threw {response.Error?.ErrorType}: {response.Error?.ErrorMessage}");
        }

        /// <summary>
        /// 将远程响应中的 Result 反序列化为目标返回类型。
        /// <see cref="RemoteInvocationResponse.Result"/> 反序列化后为 <see cref="JsonElement"/>，可能含 $type 包装。
        /// </summary>
        /// <param name="response">远程调用响应</param>
        /// <param name="returnType">方法声明的返回类型</param>
        /// <returns>反序列化后的返回值</returns>
        protected static object DeserializeResult(RemoteInvocationResponse response, Type returnType)
        {
            var element = ToJsonElement(response.Result);
            if (element is null || element.Value.ValueKind == JsonValueKind.Null)
                return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;

            return DeserializeTypedValue(element.Value, returnType);
        }

        #region 日志相关方法

        /// <summary>
        /// 记录方法调用前的日志
        /// </summary>
        /// <param name="invocation">方法调用信息</param>
        protected virtual void LogBeforeInvoke(IInvocation invocation)
        {
            var serviceDesc = GetDescription(invocation);
            LogLevel level = GetLogLevel(serviceDesc.LogLevel);
            if (_logger.IsEnabled(level))
            {
                var argsLog = (serviceDesc.LogFormat & LogFormat.Args) == LogFormat.Args
                    ? GetLogString(GetLogArgs(invocation)) : null;

                _logger.Log(level,
                    "<Invoke>{Service}.{Method}({Args})",
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
            LogLevel level = GetLogLevel(serviceDesc.LogLevel);
            if (_logger.IsEnabled(level))
            {
                string returnLog = null;
                if ((serviceDesc.LogFormat & LogFormat.ReturnValue) == LogFormat.ReturnValue)
                {
                    returnLog = GetLogString(result, 0);
                }
                _logger.Log(level,
                    "<Return>{Service}.{Method}+{Duration}:{ReturnValue}",
                     serviceDesc.ServiceName, serviceDesc.MethodName,
                    elapsedTime.TotalSeconds, returnLog);
            }
            if (elapsedTime > SlowQueryThreshold)//记录慢调用日志
            {
                _logger.LogWarning("<Slow>{Service}.{Method} took {Duration} seconds",
                    serviceDesc.ServiceName, serviceDesc.MethodName, elapsedTime.TotalSeconds);
            }
        }

        static LogLevel GetLogLevel(ServiceLogLevel level)
        {
            return level switch
            {
                ServiceLogLevel.Trace => LogLevel.Trace,
                ServiceLogLevel.Debug => LogLevel.Debug,
                ServiceLogLevel.Information => LogLevel.Information,
                ServiceLogLevel.Warning => LogLevel.Warning,
                ServiceLogLevel.Error => LogLevel.Error,
                ServiceLogLevel.Critical => LogLevel.Critical,
                _ => LogLevel.None,
            };
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
                _logger.LogWarning("<Exception>{Service}.{Method}({Args}) {Message}",
                    serviceDesc.ServiceName, serviceDesc.MethodName, argsLog, innerExp.Message);
            else
                _logger.LogError("<Exception>{Service}.{Method}({Args}) {Exception}",
                    serviceDesc.ServiceName, serviceDesc.MethodName, argsLog, innerExp);
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
                GetServiceType(invocation),
                serviceDesc.ServiceName,
                method,
                invocation.Arguments?.ToArray() ?? Array.Empty<object>(),
                GetLogArgs(invocation),
                sessionId: null,
                sqlStack: Array.Empty<string>(),
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
            return _methodDescriptions.GetOrAdd((GetServiceType(invocation), invocation.Method), _ =>
            {
                var desc = new ServiceDescription();
                desc.LoadFrom(invocation);
                return desc;
            });
        }
        #endregion

        /// <summary>
        /// 解析当前调用所属的服务接口类型。
        /// <para>
        /// 优先使用 <see cref="IInvocation.TargetType"/>；
        /// 当其为 null（<c>CreateInterfaceProxyWithoutTarget</c> 拦截继承自基接口的方法时）
        /// 从代理对象实现的接口中推断最派生的服务接口（不被其他实现接口继承的叶子接口），
        /// 避免回退到 <see cref="MethodInfo.DeclaringType"/>（方法声明所在的基接口，如
        /// <c>IEntityViewServiceAsync</c>），导致 ServiceName 丢失派生接口信息（如 <c>IDemoUserService</c>）。
        /// </para>
        /// <para>
        /// 推断结果按代理类型缓存。无法唯一确定叶子接口时回退到 <see cref="MethodInfo.DeclaringType"/>。
        /// </para>
        /// </summary>
        /// <param name="invocation">方法调用信息。</param>
        /// <returns>解析到的服务接口类型。</returns>
        internal static Type GetServiceType(IInvocation invocation)
        {
            if (invocation.TargetType is not null)
                return invocation.TargetType;

            var proxyType = invocation.Proxy.GetType();
            return _proxyServiceTypeCache.GetOrAdd(proxyType, pt =>
            {
                var interfaces = pt.GetInterfaces();
                // 叶子接口：不被代理实现的其他接口继承；排除 System 命名空间（Castle 内部接口等）
                var candidates = interfaces
                    .Where(i => (invocation.Method.DeclaringType.IsAssignableFrom(i) || i.IsAssignableFrom(invocation.Method.DeclaringType)) && (i.Namespace is null || !i.Namespace.StartsWith("System", StringComparison.Ordinal)))
                    .Where(i => !interfaces.Any(other => other != i && i.IsAssignableFrom(other)))
                    .ToList();
                return candidates.Count > 0 ? candidates[0] : invocation.Method.DeclaringType;
            });
        }
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
            desc.ServiceName = GetServiceName(RemoteServiceInvokeInterceptor.GetServiceType(invocation));
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
        /// 获取服务类型的服务名称。委托给 <see cref="ServiceNameUtil.GetServiceName"/>，
        /// 固定使用类型短名生成服务名称。
        /// </summary>
        /// <param name="serviceType">目标服务类型。</param>
        /// <returns>格式化后的服务名称。</returns>
        private static string GetServiceName(Type serviceType)
            => TypeResolverHelper.GetName(serviceType);
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
            IEnumerable<T> targetTypeAttributes = invocation.TargetType?.GetCustomAttributes<T>() ?? Array.Empty<T>();
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
