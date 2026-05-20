using System;
using System.Collections.Generic;
using System.Reflection;

namespace LiteOrm.Service
{
    /// <summary>
    /// 服务异常处理上下文。
    /// </summary>
    [Serializable]
    public class ServiceExceptionContext : EventArgs
    {
        private bool _handled;
        private bool _resultAssigned;
        private object _result;

        /// <summary>
        /// 初始化 <see cref="ServiceExceptionContext"/>。
        /// </summary>
        public ServiceExceptionContext(
            Exception exception,
            object target,
            Type serviceType,
            string serviceName,
            MethodInfo method,
            object[] arguments,
            object[] logArguments,
            string sessionId,
            IReadOnlyList<string> sqlStack,
            Type methodReturnType,
            Type resultType)
        {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
            ServiceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
            Method = method ?? throw new ArgumentNullException(nameof(method));
            MethodName = method.Name;
            Arguments = arguments ?? Array.Empty<object>();
            LogArguments = logArguments ?? Array.Empty<object>();
            SessionId = sessionId;
            SqlStack = sqlStack ?? Array.Empty<string>();
            MethodReturnType = methodReturnType ?? throw new ArgumentNullException(nameof(methodReturnType));
            ResultType = resultType;
            Target = target;
        }

        /// <summary>
        /// 当前异常。
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// 当前服务实例。
        /// </summary>
        public object Target { get; }

        /// <summary>
        /// 服务类型。
        /// </summary>
        public Type ServiceType { get; }

        /// <summary>
        /// 服务名。
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        /// 方法信息。
        /// </summary>
        public MethodInfo Method { get; }

        /// <summary>
        /// 方法名。
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// 原始参数。
        /// </summary>
        public object[] Arguments { get; }

        /// <summary>
        /// 用于日志的参数。
        /// </summary>
        public object[] LogArguments { get; }

        /// <summary>
        /// 当前会话 ID。
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// 当前 SQL 栈快照。
        /// </summary>
        public IReadOnlyList<string> SqlStack { get; }

        /// <summary>
        /// 方法返回类型。
        /// </summary>
        public Type MethodReturnType { get; }

        /// <summary>
        /// 真实结果类型；无结果时为 null。
        /// </summary>
        public Type ResultType { get; }

        /// <summary>
        /// 是否存在返回结果。
        /// </summary>
        public bool HasResult => ResultType is not null;

        /// <summary>
        /// 是否已被处理。
        /// </summary>
        public bool Handled => _handled;

        /// <summary>
        /// 是否已显式设置结果。
        /// </summary>
        public bool ResultAssigned => _resultAssigned;

        /// <summary>
        /// 已设置的处理结果。
        /// </summary>
        public object Result => _result;

        /// <summary>
        /// 将当前异常标记为已处理（仅适用于无返回值方法）。
        /// </summary>
        public void Handle()
        {
            if (HasResult)
                throw new InvalidOperationException($"Method {ServiceName}.{MethodName} requires a handled result of type {ResultType}.");

            _handled = true;
            _resultAssigned = false;
            _result = null;
        }

        /// <summary>
        /// 将当前异常标记为已处理，并设置返回结果。
        /// </summary>
        public void Handle(object result)
        {
            if (!HasResult)
                throw new InvalidOperationException($"Method {ServiceName}.{MethodName} does not define a return result.");

            EnsureResultAssignable(result);
            _handled = true;
            _resultAssigned = true;
            _result = result;
        }

        private void EnsureResultAssignable(object result)
        {
            if (result is null)
            {
                if (ResultType.IsValueType && Nullable.GetUnderlyingType(ResultType) is null)
                    throw new InvalidOperationException($"Method {ServiceName}.{MethodName} requires a non-null result of type {ResultType}.");
                return;
            }

            if (!ResultType.IsInstanceOfType(result))
                throw new InvalidOperationException($"Handled result type {result.GetType()} cannot be assigned to {ResultType} for method {ServiceName}.{MethodName}.");
        }
    }
}
