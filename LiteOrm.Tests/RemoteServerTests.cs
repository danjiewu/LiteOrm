using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// 服务端 <see cref="RemoteServiceDispatcher"/> 的单元测试。
    /// 直接测试分发逻辑，不经过 HTTP 层。
    /// </summary>
    public class RemoteServerTests
    {
        /// <summary>
        /// 测试用的计算器服务接口（与客户端使用相同的 ServiceName）。
        /// </summary>
        public interface IRemoteCalculator
        {
            void Clear();
            int Add(int a, int b);
            string Echo(string message);
            Task ResetAsync();
            Task<int> MultiplyAsync(int a, int b);
            Task<string> GreetAsync(string name, CancellationToken cancellationToken);
        }

        /// <summary>
        /// 计算器服务的实现。
        /// </summary>
        private sealed class CalculatorImpl : IRemoteCalculator
        {
            public int LastCancellationTokenIsCanceled { get; private set; }

            public void Clear() { }

            public int Add(int a, int b) => a + b;

            public string Echo(string message) => $"echo:{message}";

            public Task ResetAsync() => Task.CompletedTask;

            public Task<int> MultiplyAsync(int a, int b) => Task.FromResult(a * b);

            public Task<string> GreetAsync(string name, CancellationToken cancellationToken)
            {
                LastCancellationTokenIsCanceled = cancellationToken.IsCancellationRequested ? 1 : 0;
                return Task.FromResult($"hello,{name}");
            }
        }

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true,
        };

        private static (RemoteServiceDispatcher dispatcher, CalculatorImpl impl) CreateDispatcher()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
            services.AddScoped<IRemoteCalculator, CalculatorImpl>();
            var provider = services.BuildServiceProvider();

            var resolver = new DelegateRemoteServiceTypeResolver(name =>
                name == ServiceNameUtil.GetServiceName(typeof(IRemoteCalculator)) ? typeof(IRemoteCalculator) : null);

            var dispatcher = new RemoteServiceDispatcher(
                provider,
                resolver,
                provider.GetRequiredService<ILoggerFactory>().CreateLogger<RemoteServiceDispatcher>());

            var impl = provider.GetRequiredService<IRemoteCalculator>() as CalculatorImpl;
            return (dispatcher, impl!);
        }

        /// <summary>
        /// 通过方法名与参数构建 <see cref="RemoteInvocationRequest"/>。
        /// 通过反射设置 <see cref="RemoteInvocationRequest.Method"/>（<see cref="MethodInfo"/>），
        /// 模拟经 <see cref="RemoteServiceDispatcher.ParseRequest"/> 解析后的请求。
        /// </summary>
        private static RemoteInvocationRequest Request(string method, params object[] args)
        {
            var methodInfo = typeof(IRemoteCalculator).GetMethod(method, BindingFlags.Public | BindingFlags.Instance);
            return new RemoteInvocationRequest
            {
                ServiceName = nameof(IRemoteCalculator),
                Method = methodInfo,
                Arguments = args,
            };
        }

        /// <summary>
        /// 将响应中的 <see cref="RemoteInvocationResponse.Result"/> 反序列化为强类型值。
        /// 直连 dispatcher 测试时 Result 为原始对象（如 boxed int）；
        /// 经 HTTP 传输后 Result 为 <see cref="JsonElement"/>（可能含 $type 包装）。
        /// </summary>
        private static T ReadResult<T>(RemoteInvocationResponse response)
        {
            Assert.NotNull(response.Result);
            if (response.Result is T typed)
                return typed;
            if (response.Result is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("$type", out var typeProp))
                {
                    var actualType = Type.GetType(typeProp.GetString());
                    if (actualType != null && element.TryGetProperty("$value", out var valueProp))
                        return (T)JsonSerializer.Deserialize(valueProp.GetRawText(), actualType, _jsonOptions);
                }
                return JsonSerializer.Deserialize<T>(element.GetRawText(), _jsonOptions);
            }
            return (T)Convert.ChangeType(response.Result, typeof(T));
        }

        [Fact]
        public async Task Void_Method_Returns_Success_With_No_Result()
        {
            var (dispatcher, _) = CreateDispatcher();

            var response = await dispatcher.InvokeAsync(Request(nameof(IRemoteCalculator.Clear)));

            Assert.True(response.Success);
            Assert.Null(response.Result);
        }

        [Fact]
        public async Task Sync_Return_Int_Returns_Serialized_Result()
        {
            var (dispatcher, _) = CreateDispatcher();

            var response = await dispatcher.InvokeAsync(
                Request(nameof(IRemoteCalculator.Add), 3, 4));

            Assert.True(response.Success);
            Assert.Equal(7, ReadResult<int>(response));
        }

        [Fact]
        public async Task Sync_Return_String_Returns_Serialized_Result()
        {
            var (dispatcher, _) = CreateDispatcher();

            var response = await dispatcher.InvokeAsync(
                Request(nameof(IRemoteCalculator.Echo), "hello"));

            Assert.True(response.Success);
            Assert.Equal("echo:hello", ReadResult<string>(response));
        }

        [Fact]
        public async Task Async_Task_Method_Awaits_And_Returns_Success()
        {
            var (dispatcher, _) = CreateDispatcher();

            var response = await dispatcher.InvokeAsync(
                Request(nameof(IRemoteCalculator.ResetAsync)));

            Assert.True(response.Success);
            Assert.Null(response.Result);
        }

        [Fact]
        public async Task Async_TaskT_Method_Returns_Serialized_Result()
        {
            var (dispatcher, _) = CreateDispatcher();

            var response = await dispatcher.InvokeAsync(
                Request(nameof(IRemoteCalculator.MultiplyAsync), 6, 7));

            Assert.True(response.Success);
            Assert.Equal(42, ReadResult<int>(response));
        }

        [Fact]
        public async Task CancellationToken_Is_Injected_From_Dispatch_Parameter()
        {
            var (dispatcher, impl) = CreateDispatcher();

            using var cts = new CancellationTokenSource();
            // 请求中没有 CancellationToken 参数（客户端会过滤掉），但 dispatcher 会将传入的 token 注入
            var response = await dispatcher.InvokeAsync(
                Request(nameof(IRemoteCalculator.GreetAsync), "world"),
                cts.Token);

            Assert.True(response.Success);
            Assert.Equal("hello,world", ReadResult<string>(response));
            Assert.Equal(0, impl.LastCancellationTokenIsCanceled); // token 未取消
        }

        [Fact]
        public async Task CancellationToken_Canceled_Is_Reflected_In_Service()
        {
            var (dispatcher, impl) = CreateDispatcher();

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var response = await dispatcher.InvokeAsync(
                Request(nameof(IRemoteCalculator.GreetAsync), "world"),
                cts.Token);

            Assert.True(response.Success);
            Assert.Equal(1, impl.LastCancellationTokenIsCanceled); // token 已取消
        }

        [Fact]
        public async Task Unknown_ServiceName_Returns_Failure()
        {
            var (dispatcher, _) = CreateDispatcher();

            var request = new RemoteInvocationRequest
            {
                ServiceName = "IUnknownService",
                Method = typeof(IRemoteCalculator).GetMethod(nameof(IRemoteCalculator.Clear), BindingFlags.Public | BindingFlags.Instance),
            };

            var response = await dispatcher.InvokeAsync(request);

            Assert.False(response.Success);
            Assert.Contains("IUnknownService", response.ErrorMessage!);
        }

        [Fact]
        public void Unknown_Method_Returns_Failure()
        {
            var (dispatcher, _) = CreateDispatcher();

            // 模拟 HTTP 传输：方法名在 JSON 中，由 ParseRequest 解析。
            // 方法不存在时 ParseRequest 抛出 ServiceException
            var json = $"{{\"ServiceName\":\"{nameof(IRemoteCalculator)}\",\"Method\":\"NonExistentMethod\",\"Arguments\":[]}}";
            var ex = Assert.Throws<ServiceException>(() => dispatcher.ParseRequest(json, _jsonOptions));
            Assert.Contains("NonExistentMethod", ex.Message);
        }

        [Fact]
        public async Task Service_Throws_Exception_Returns_Failure_With_ErrorInfo()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
            services.AddScoped<IRemoteCalculator, ThrowingCalculator>();
            var provider = services.BuildServiceProvider();

            var resolver = new DelegateRemoteServiceTypeResolver(name =>
                name == ServiceNameUtil.GetServiceName(typeof(IRemoteCalculator)) ? typeof(IRemoteCalculator) : null);

            var dispatcher = new RemoteServiceDispatcher(provider, resolver);

            var response = await dispatcher.InvokeAsync(
                Request(nameof(IRemoteCalculator.Add), 1, 2));

            Assert.False(response.Success);
            Assert.Contains("deliberate", response.ErrorMessage!);
            Assert.Equal(nameof(InvalidOperationException), response.ErrorType?.Split('.').Last());
        }

        private sealed class ThrowingCalculator : IRemoteCalculator
        {
            public void Clear() { }
            public int Add(int a, int b) => throw new InvalidOperationException("deliberate failure");
            public string Echo(string message) => message;
            public Task ResetAsync() => Task.CompletedTask;
            public Task<int> MultiplyAsync(int a, int b) => Task.FromResult(a * b);
            public Task<string> GreetAsync(string name, CancellationToken cancellationToken) => Task.FromResult(name);
        }
    }
}
