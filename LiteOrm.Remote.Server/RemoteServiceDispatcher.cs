using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Service
{
    /// <summary>
    /// 远程服务调用分发器。接收 <see cref="RemoteInvocationRequest"/>，解析服务与方法，
    /// 从 DI 容器获取服务实例并调用，返回 <see cref="RemoteInvocationResponse"/>。
    /// </summary>
    public class RemoteServiceDispatcher
    {
        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true,
        };

        private readonly IServiceProvider _serviceProvider;
        private readonly RemoteServiceRegistry _registry;
        private readonly ILogger<RemoteServiceDispatcher>? _logger;

        /// <summary>
        /// 初始化 <see cref="RemoteServiceDispatcher"/> 类的新实例。
        /// </summary>
        /// <param name="serviceProvider">DI 服务提供者，用于解析服务实例。</param>
        /// <param name="registry">服务注册表。</param>
        /// <param name="logger">日志记录器（可选）。</param>
        public RemoteServiceDispatcher(
            IServiceProvider serviceProvider,
            RemoteServiceRegistry registry,
            ILogger<RemoteServiceDispatcher>? logger = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _logger = logger;
        }

        /// <summary>
        /// 异步分发远程调用请求。
        /// </summary>
        /// <param name="request">远程调用请求。</param>
        /// <param name="cancellationToken">取消令牌，将传递给服务的 CancellationToken 参数（如有）。</param>
        /// <returns>远程调用响应。</returns>
        public async Task<RemoteInvocationResponse> InvokeAsync(RemoteInvocationRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            var response = new RemoteInvocationResponse();
            try
            {
                // 1. 解析服务类型
                if (!_registry.TryGetServiceType(request.ServiceName!, out var serviceType))
                {
                    response.Success = false;
                    response.ErrorType = nameof(ServiceException);
                    response.ErrorMessage = $"Remote service '{request.ServiceName}' is not registered.";
                    _logger?.LogWarning("Remote service '{ServiceName}' not found in registry.", request.ServiceName);
                    return response;
                }

                // 2. 从 DI 解析服务实例
                var serviceInstance = _serviceProvider.GetService(serviceType);
                if (serviceInstance is null)
                {
                    response.Success = false;
                    response.ErrorType = nameof(ServiceException);
                    response.ErrorMessage = $"Service implementation for '{request.ServiceName}' ({serviceType.FullName}) is not registered in DI container.";
                    _logger?.LogWarning("Service implementation for '{ServiceName}' ({ServiceType}) not resolved from DI.", request.ServiceName, serviceType.FullName);
                    return response;
                }

                // 3. 匹配方法
                var method = ResolveMethod(serviceType, request.MethodName!, request.Arguments);
                if (method is null)
                {
                    response.Success = false;
                    response.ErrorType = nameof(ServiceException);
                    response.ErrorMessage = $"Method '{request.MethodName}' with matching signature not found on service '{request.ServiceName}'.";
                    _logger?.LogWarning("Method '{MethodName}' not found on service '{ServiceName}'.", request.MethodName, request.ServiceName);
                    return response;
                }

                // 4. 反序列化参数
                var arguments = DeserializeArguments(method, request.Arguments, cancellationToken);

                // 5. 调用方法
                _logger?.LogDebug("Invoking {ServiceName}.{MethodName}", request.ServiceName, request.MethodName);
                var result = method.Invoke(serviceInstance, arguments);

                // 6. 处理返回值
                await ProcessReturnValue(result, response, method);

                // 7. 回写标记的参数（服务端可能修改了参数对象的属性，如自增 ID）
                BuildWriteBackResponse(response, request, method, arguments);

                response.Success = true;
                _logger?.LogDebug("Invoked {ServiceName}.{MethodName} successfully.", request.ServiceName, request.MethodName);
            }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                response.Success = false;
                response.ErrorType = inner.GetType().FullName;
                response.ErrorMessage = inner.Message;
                response.ErrorStackTrace = inner.StackTrace;
                _logger?.LogError(inner, "Remote service '{ServiceName}.{MethodName}' threw an exception.", request.ServiceName, request.MethodName);
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.ErrorType = ex.GetType().FullName;
                response.ErrorMessage = ex.Message;
                response.ErrorStackTrace = ex.StackTrace;
                _logger?.LogError(ex, "Failed to dispatch remote call to '{ServiceName}.{MethodName}'.", request.ServiceName, request.MethodName);
            }
            return response;
        }

        /// <summary>
        /// 在服务类型上匹配方法。按方法名和参数数量（排除 CancellationToken）匹配；
        /// 若有多个候选，则按参数类型兼容性选择最佳匹配。
        /// </summary>
        private static MethodInfo? ResolveMethod(Type serviceType, string methodName, System.Collections.Generic.IList<RemoteArgument> arguments)
        {
            var argCount = arguments?.Count ?? 0;
            var candidates = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == methodName)
                .ToList();

            // 按非 CancellationToken 参数数量过滤
            var matched = candidates
                .Where(m => m.GetParameters().Count(p => p.ParameterType != typeof(CancellationToken)) == argCount)
                .ToList();

            if (matched.Count == 0) return null;
            if (matched.Count == 1) return matched[0];

            // 多个候选：按参数类型兼容性评分选择最佳
            MethodInfo? best = null;
            int bestScore = -1;
            foreach (var m in matched)
            {
                int score = ScoreMethod(m, arguments!);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = m;
                }
            }
            return best;
        }

        /// <summary>
        /// 为方法评分：每个参数的声明类型能从请求参数的实际类型赋值得分+1。
        /// </summary>
        private static int ScoreMethod(MethodInfo method, System.Collections.Generic.IList<RemoteArgument> arguments)
        {
            var parameters = method.GetParameters()
                .Where(p => p.ParameterType != typeof(CancellationToken))
                .ToArray();

            int score = 0;
            for (int i = 0; i < parameters.Length && i < arguments.Count; i++)
            {
                var argType = Type.GetType(arguments[i].TypeName ?? string.Empty);
                if (argType is null) continue;
                if (parameters[i].ParameterType.IsAssignableFrom(argType))
                    score++;
            }
            return score;
        }

        /// <summary>
        /// 反序列化请求参数为方法参数数组。CancellationToken 参数注入传入的 <paramref name="cancellationToken"/>。
        /// </summary>
        private static object?[] DeserializeArguments(MethodInfo method, System.Collections.Generic.IList<RemoteArgument> arguments, CancellationToken cancellationToken)
        {
            var parameters = method.GetParameters();
            var result = new object?[parameters.Length];

            int argIndex = 0;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType == typeof(CancellationToken))
                {
                    result[i] = cancellationToken;
                    continue;
                }

                if (argIndex < arguments.Count)
                {
                    var arg = arguments[argIndex];
                    var declaredType = parameters[i].ParameterType;
                    // 优先使用请求中携带的实际类型，若加载失败则退回到参数声明类型
                    var actualType = Type.GetType(arg.TypeName ?? string.Empty) ?? declaredType;
                    result[i] = string.IsNullOrEmpty(arg.ValueJson)
                        ? (declaredType.IsValueType ? Activator.CreateInstance(declaredType) : null)
                        : JsonSerializer.Deserialize(arg.ValueJson!, actualType, _serializerOptions);
                    argIndex++;
                }
            }
            return result;
        }

        /// <summary>
        /// 处理方法返回值，将其序列化到响应中。支持同步返回、Task、Task&lt;T&gt;。
        /// </summary>
        private static async Task ProcessReturnValue(object? result, RemoteInvocationResponse response, MethodInfo method)
        {
            var returnType = method.ReturnType;

            if (returnType == typeof(void))
                return;

            if (returnType == typeof(Task))
            {
                await ((Task)result!).ConfigureAwait(false);
                return;
            }

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var task = (Task)result!;
                await task.ConfigureAwait(false);
                var resultProperty = returnType.GetProperty("Result")!;
                result = resultProperty.GetValue(task);
                returnType = returnType.GetGenericArguments()[0];
            }

            if (result is null)
                return;

            var actualType = result.GetType();
            response.ResultTypeName = actualType.AssemblyQualifiedName;
            response.ResultJson = JsonSerializer.Serialize(result, actualType, _serializerOptions);
        }

        /// <summary>
        /// 构建回写参数响应。对请求 <see cref="RemoteInvocationRequest.WriteBackArgumentIndices"/> 指定的参数，
        /// 读取参数上的 <see cref="ArgumentOutAttribute"/>，通过 <see cref="ArgumentOutHandlerResolver"/> 创建处理器，
        /// 调用 <see cref="IArgumentOutHandler.GenerateReturnValue"/> 生成返回值并按其 <see cref="IArgumentOutHandler.ReturnType"/> 序列化，
        /// 放入 <see cref="RemoteInvocationResponse.WriteBackArguments"/>。
        /// 若参数未标记特性、处理器无法创建或生成返回值为 null，则跳过该参数。
        /// 集合模式（<see cref="ArgumentMode.Collection"/>）下，逐项调用处理器生成返回值列表并序列化。
        /// </summary>
        private void BuildWriteBackResponse(RemoteInvocationResponse response, RemoteInvocationRequest request, MethodInfo method, object?[] arguments)
        {
            if (request.WriteBackArgumentIndices is null || request.WriteBackArgumentIndices.Count == 0)
                return;

            var parameters = method.GetParameters();
            var writeBacks = new List<OutputArgument>();

            // 重建 Arguments 索引 → 方法参数索引 的映射（跳过 CancellationToken）
            int argListIndex = 0;
            var argListIndexToParamIndex = new Dictionary<int, int>();
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType == typeof(CancellationToken))
                    continue;
                argListIndexToParamIndex[argListIndex] = i;
                argListIndex++;
            }

            foreach (var idx in request.WriteBackArgumentIndices)
            {
                if (!argListIndexToParamIndex.TryGetValue(idx, out var paramIndex))
                    continue;
                if (paramIndex >= arguments.Length)
                    continue;

                var value = arguments[paramIndex];
                if (value is null)
                    continue;

                // 读取参数上的 ArgumentOutAttribute，通过 Resolver 创建处理器（DI 优先 + 构造函数传参）
                var attr = parameters[paramIndex].GetCustomAttribute<ArgumentOutAttribute>(true);
                var handler = ArgumentOutHandlerResolver.Resolve(attr, _serviceProvider);
                if (handler is null)
                    continue;

                if (attr.Mode == ArgumentMode.Collection)
                {
                    // 集合模式：逐项调用 GenerateReturnValue，收集为列表后序列化
                    if (value is not IEnumerable items)
                        continue;

                    var returnType = handler.ReturnType;
                    var listType = typeof(List<>).MakeGenericType(returnType);
                    var typedList = (IList)Activator.CreateInstance(listType)!;
                    foreach (var item in items)
                        typedList.Add(handler.GenerateReturnValue(item));

                    writeBacks.Add(new OutputArgument
                    {
                        ArgumentIndex = idx,
                        TypeName = listType.AssemblyQualifiedName,
                        ValueJson = JsonSerializer.Serialize(typedList, listType, _serializerOptions),
                    });
                }
                else
                {
                    // 单对象模式：直接调用 GenerateReturnValue
                    var returnValue = handler.GenerateReturnValue(value);
                    if (returnValue is null) continue;
                    var returnType = handler.ReturnType;

                    writeBacks.Add(new OutputArgument
                    {
                        ArgumentIndex = idx,
                        TypeName = returnType.AssemblyQualifiedName,
                        ValueJson = JsonSerializer.Serialize(returnValue, returnType, _serializerOptions),
                    });
                }
            }

            if (writeBacks.Count > 0)
                response.WriteBackArguments = writeBacks;
        }
    }
}
