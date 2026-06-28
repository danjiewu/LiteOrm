using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Remote.Server
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

        private static readonly Type ExprType = typeof(Expr);

        /// <summary>
        /// 服务类型 → 方法查找表的缓存。
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Dictionary<string, MethodInfo>> _methodCache = new();

        private readonly IServiceProvider _serviceProvider;
        private readonly IRemoteServiceTypeResolver _resolver;
        private readonly ILogger<RemoteServiceDispatcher>? _logger;

        /// <summary>
        /// 初始化 <see cref="RemoteServiceDispatcher"/> 类的新实例。
        /// </summary>
        public RemoteServiceDispatcher(
            IServiceProvider serviceProvider,
            IRemoteServiceTypeResolver resolver,
            ILogger<RemoteServiceDispatcher>? logger = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _logger = logger;
        }

        /// <summary>
        /// 异步分发远程调用请求。
        /// <para>
        /// 若 <see cref="RemoteInvocationRequest.Method"/> 为 <c>null</c>（未经 <see cref="ParseRequest"/> 解析），
        /// 则通过 <see cref="RemoteInvocationRequest.ServiceName"/> 查找服务类型并按方法名解析 <see cref="MethodInfo"/>。
        /// </para>
        /// </summary>
        public async Task<RemoteInvocationResponse> InvokeAsync(RemoteInvocationRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            var response = new RemoteInvocationResponse();
            MethodInfo method = null;
            try
            {
                // 1. 解析服务类型
                var serviceType = _resolver.ResolveService(request.ServiceName!);
                if (serviceType is null)
                {
                    response.Success = false;
                    response.ErrorType = nameof(ServiceException);
                    response.ErrorMessage = $"Remote service '{request.ServiceName}' is not registered.";
                    _logger?.LogWarning("Remote service '{ServiceName}' not found by resolver.", request.ServiceName);
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

                // 3. 匹配方法：优先使用 Method（经 ParseRequest 或直连测试设置），否则通过方法名查找
                method = request.Method ?? ResolveMethod(serviceType, request.Method?.Name);
                if (method is null)
                {
                    response.Success = false;
                    response.ErrorType = nameof(ServiceException);
                    response.ErrorMessage = $"Method '{request.Method?.Name}' with matching signature not found on service '{request.ServiceName}'.";
                    _logger?.LogWarning("Method '{MethodName}' not found on service '{ServiceName}'.", request.Method?.Name, request.ServiceName);
                    return response;
                }

                // 4. 反序列化参数（Arguments 反序列化后为 JsonElement 或实际对象）
                var arguments = DeserializeArguments(method, request.Arguments, cancellationToken);

                // 5. 调用方法
                _logger?.LogDebug("Invoking {ServiceName}.{MethodName}", request.ServiceName, method.Name);
                var result = method.Invoke(serviceInstance, arguments);

                // 6. 处理返回值
                await ProcessReturnValue(result, response, method);

                // 7. 回写标记的参数（从 MethodInfo 推算回写索引）
                BuildWriteBackResponse(response, method, arguments);

                response.Success = true;
                _logger?.LogDebug("Invoked {ServiceName}.{MethodName} successfully.", request.ServiceName, method.Name);
            }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                response.Success = false;
                response.ErrorType = inner.GetType().FullName;
                response.ErrorMessage = inner.Message;
                response.ErrorStackTrace = inner.StackTrace;
                _logger?.LogError(inner, "Remote service '{ServiceName}.{MethodName}' threw an exception.", request.ServiceName, method?.Name);
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.ErrorType = ex.GetType().FullName;
                response.ErrorMessage = ex.Message;
                response.ErrorStackTrace = ex.StackTrace;
                _logger?.LogError(ex, "Failed to dispatch remote call to '{ServiceName}.{MethodName}'.", request.ServiceName, method?.Name);
            }
            return response;
        }

        /// <summary>
        /// 从 JSON 字符串解析 <see cref="RemoteInvocationRequest"/>。
        /// <para>
        /// 解析流程：
        /// 1. 读取 JSON，提取 <see cref="RemoteInvocationRequest.ServiceName"/>；
        /// 2. 通过 <see cref="IRemoteServiceTypeResolver"/> 匹配服务类型；
        /// 3. 从 JSON 读取方法名，在服务类型上查找 <see cref="MethodInfo"/>；
        /// 4. 按 <see cref="MethodInfo"/> 参数类型反序列化 <see cref="RemoteInvocationRequest.Arguments"/>。
        /// </para>
        /// <para>
        /// 此方法不使用默认的 <see cref="JsonSerializer.Deserialize{T}(string, JsonSerializerOptions)"/>，
        /// 因为 <see cref="RemoteInvocationRequest.Method"/>（<see cref="MethodInfo"/>）需要服务类型上下文才能解析。
        /// </para>
        /// </summary>
        /// <param name="json">请求 JSON 字符串。</param>
        /// <param name="options">JSON 序列化选项。</param>
        /// <returns>已解析的 <see cref="RemoteInvocationRequest"/>，<see cref="RemoteInvocationRequest.Method"/> 已赋值。</returns>
        public RemoteInvocationRequest ParseRequest(string json, JsonSerializerOptions options)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentNullException(nameof(json));

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 1. 提取 ServiceName
            var serviceName = root.GetProperty("ServiceName").GetString();
            var serviceType = _resolver.ResolveService(serviceName);
            if (serviceType is null)
                throw new ServiceException($"Remote service '{serviceName}' is not registered.");

            // 2. 提取方法名，查找 MethodInfo
            string methodName = null;
            if (root.TryGetProperty("Method", out var methodProp) && methodProp.ValueKind == JsonValueKind.String)
                methodName = methodProp.GetString();

            var method = ResolveMethod(serviceType, methodName);
            if (method is null)
                throw new ServiceException($"Method '{methodName}' with matching signature not found on service '{serviceName}'.");

            // 3. 按方法参数类型反序列化 Arguments
            object[] arguments = Array.Empty<object>();
            if (root.TryGetProperty("Arguments", out var argsProp) && argsProp.ValueKind == JsonValueKind.Array)
            {
                var paramTypes = method.GetParameters()
                    .Where(p => p.ParameterType != typeof(CancellationToken))
                    .Select(p => p.ParameterType)
                    .ToArray();

                var argList = new System.Collections.Generic.List<object>();
                int paramIndex = 0;
                foreach (var element in argsProp.EnumerateArray())
                {
                    Type declaredType = paramIndex < paramTypes.Length ? paramTypes[paramIndex] : null;
                    argList.Add(DeserializeArgumentElement(element, declaredType, options));
                    paramIndex++;
                }
                arguments = argList.ToArray();
            }

            return new RemoteInvocationRequest
            {
                ServiceName = serviceName,
                Method = method,
                Arguments = arguments,
            };
        }

        /// <summary>
        /// 反序列化单个 JSON 参数元素。含 $type → 按实际类型反序列化；否则按声明类型反序列化。
        /// </summary>
        private static object DeserializeArgumentElement(JsonElement element, Type declaredType, JsonSerializerOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
                return declaredType != null && declaredType.IsValueType
                    ? Activator.CreateInstance(declaredType)
                    : null;

            // 检查 $type 包装
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty("$type", out var typeProp))
            {
                var typeName = typeProp.GetString();
                var actualType = Type.GetType(typeName);
                if (actualType != null && element.TryGetProperty("$value", out var valueProp))
                    return JsonSerializer.Deserialize(valueProp.GetRawText(), actualType, options);
            }

            // 按声明类型反序列化（若有）；否则返回原始 JsonElement
            if (declaredType != null)
                return JsonSerializer.Deserialize(element.GetRawText(), declaredType, options);

            return element.Clone();
        }

        /// <summary>
        /// 在服务类型上按方法名查找 <see cref="MethodInfo"/>。
        /// </summary>
        private static MethodInfo? ResolveMethod(Type serviceType, string methodName)
        {
            if (string.IsNullOrEmpty(methodName)) return null;
            var lookup = _methodCache.GetOrAdd(serviceType, BuildMethodLookup);
            return lookup.TryGetValue(methodName, out var method) ? method : null;
        }

        /// <summary>
        /// 为指定服务类型构建名称 → MethodInfo 查找表。
        /// </summary>
        private static Dictionary<string, MethodInfo> BuildMethodLookup(Type serviceType)
        {
            var lookup = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);
            var unmarked = new List<MethodInfo>();

            foreach (var method in serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = method.GetCustomAttribute<ServiceMethodAttribute>(true);
                if (attr is { IsService: false }) continue;

                if (attr is { IsService: true })
                {
                    var key = !string.IsNullOrEmpty(attr.MethodName) ? attr.MethodName : method.Name;
                    if (lookup.TryGetValue(key, out var existing))
                        throw new AmbiguousMatchException(
                            $"Multiple [ServiceMethod] methods named '{key}' found on service '{serviceType.Name}'. " +
                            $"Candidates: {existing.DeclaringType?.Name}.{existing.Name}, {method.DeclaringType?.Name}.{method.Name}.");
                    lookup[key] = method;
                }
                else
                {
                    unmarked.Add(method);
                }
            }

            var unmarkedByName = new Dictionary<string, List<MethodInfo>>(StringComparer.Ordinal);
            foreach (var method in unmarked)
            {
                if (lookup.ContainsKey(method.Name)) continue;
                if (!unmarkedByName.TryGetValue(method.Name, out var list))
                {
                    list = new List<MethodInfo>();
                    unmarkedByName[method.Name] = list;
                }
                list.Add(method);
            }

            foreach (var kv in unmarkedByName)
            {
                if (kv.Value.Count > 1)
                    throw new AmbiguousMatchException(
                        $"Multiple methods named '{kv.Key}' found on service '{serviceType.Name}'. " +
                        $"Candidates: {string.Join(", ", kv.Value.Select(m => m.DeclaringType?.Name + "." + m.Name))}.");
                lookup[kv.Key] = kv.Value[0];
            }

            return lookup;
        }

        /// <summary>
        /// 反序列化请求参数为方法参数数组。
        /// <see cref="RemoteInvocationRequest.Arguments"/> 反序列化后为 <see cref="JsonElement"/> 或实际对象，
        /// 使用方法参数声明类型进行反序列化。
        /// </summary>
        private static object?[] DeserializeArguments(MethodInfo method, object[] arguments, CancellationToken cancellationToken)
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

                if (argIndex < arguments.Length)
                {
                    var arg = arguments[argIndex];
                    var declaredType = parameters[i].ParameterType;
                    result[i] = DeserializeArgumentValue(arg, declaredType);
                    argIndex++;
                }
            }
            return result;
        }

        /// <summary>
        /// 反序列化单个参数值。若为 <see cref="JsonElement"/> 则按声明类型反序列化（含 $type 包装检测）；
        /// 若已是目标类型则直接返回。
        /// </summary>
        private static object? DeserializeArgumentValue(object arg, Type declaredType)
        {
            if (arg is null)
                return declaredType.IsValueType ? Activator.CreateInstance(declaredType) : null;

            if (arg is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Null)
                    return declaredType.IsValueType ? Activator.CreateInstance(declaredType) : null;

                // 检查 $type 包装
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("$type", out var typeProp))
                {
                    var typeName = typeProp.GetString();
                    var actualType = Type.GetType(typeName);
                    if (actualType != null && element.TryGetProperty("$value", out var valueProp))
                        return JsonSerializer.Deserialize(valueProp.GetRawText(), actualType, _serializerOptions);
                }

                return JsonSerializer.Deserialize(element.GetRawText(), declaredType, _serializerOptions);
            }

            // 已是实际对象，直接返回
            return arg;
        }

        /// <summary>
        /// 处理方法返回值。支持同步返回、Task、Task&lt;T&gt;。
        /// 当实际类型与声明返回类型不一致时，以 <see cref="TypeWrappedValue"/> 包装。
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

            // 实际类型与声明返回类型一致，或声明类型为 Expr → 直接设置，无需包装
            var actualType = result.GetType();
            if (actualType == returnType || ExprType.IsAssignableFrom(returnType))
            {
                response.Result = result;
            }
            else
            {
                // 类型不一致 → 包装为 TypeWrappedValue
                response.Result = new TypeWrappedValue
                {
                    Type = actualType.AssemblyQualifiedName,
                    Value = result,
                };
            }
        }

        /// <summary>
        /// 构建回写参数响应。从 <paramref name="method"/> 的参数上推算 <see cref="ArgumentOutAttribute"/> 标记，
        /// 通过 <see cref="ArgumentOutHandlerResolver"/> 创建处理器，调用 <see cref="IArgumentOutHandler.GenerateReturnValue"/>
        /// 生成返回值并放入 <see cref="RemoteInvocationResponse.OutArguments"/>。
        /// 当返回值实际类型与 <see cref="IArgumentOutHandler.ReturnType"/> 不一致时，以 <see cref="TypeWrappedValue"/> 包装。
        /// </summary>
        private void BuildWriteBackResponse(RemoteInvocationResponse response, MethodInfo method, object?[] arguments)
        {
            var parameters = method.GetParameters();
            var writeBacks = new List<OutputArgument>();

            // 遍历参数，推算 ArgumentOutAttribute 标记的参数索引
            int argListIndex = 0;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType == typeof(CancellationToken))
                    continue;

                var attr = parameters[i].GetCustomAttribute<ArgumentOutAttribute>(true);
                if (attr is null)
                {
                    argListIndex++;
                    continue;
                }

                if (i >= arguments.Length)
                {
                    argListIndex++;
                    continue;
                }

                var value = arguments[i];
                if (value is null)
                {
                    argListIndex++;
                    continue;
                }

                var handler = ArgumentOutHandlerResolver.Resolve(attr, _serviceProvider);
                if (handler is null)
                {
                    argListIndex++;
                    continue;
                }

                if (attr.Mode == ArgumentMode.Collection)
                {
                    // 集合模式：逐项调用 GenerateReturnValue，收集为列表
                    if (value is not IEnumerable items)
                    {
                        argListIndex++;
                        continue;
                    }

                    var returnType = handler.ReturnType;
                    var listType = typeof(List<>).MakeGenericType(returnType);
                    var typedList = (IList)Activator.CreateInstance(listType)!;
                    foreach (var item in items)
                        typedList.Add(handler.GenerateReturnValue(item));

                    writeBacks.Add(new OutputArgument
                    {
                        ArgumentIndex = argListIndex,
                        Value = WrapIfNeeded(typedList, listType),
                    });
                }
                else
                {
                    // 单对象模式：直接调用 GenerateReturnValue
                    var returnValue = handler.GenerateReturnValue(value);
                    if (returnValue is null)
                    {
                        argListIndex++;
                        continue;
                    }

                    writeBacks.Add(new OutputArgument
                    {
                        ArgumentIndex = argListIndex,
                        Value = WrapIfNeeded(returnValue, handler.ReturnType),
                    });
                }

                argListIndex++;
            }

            if (writeBacks.Count > 0)
                response.OutArguments = writeBacks;
        }

        /// <summary>
        /// 当实际类型与预期类型一致，或预期类型为 Expr → 直接返回值；
        /// 否则包装为 <see cref="TypeWrappedValue"/>。
        /// </summary>
        private static object WrapIfNeeded(object value, Type expectedType)
        {
            if (value is null) return null;
            var actualType = value.GetType();
            if (actualType == expectedType || ExprType.IsAssignableFrom(expectedType))
                return value;
            return new TypeWrappedValue
            {
                Type = actualType.AssemblyQualifiedName,
                Value = value,
            };
        }
    }
}
