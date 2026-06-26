using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
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

            var registry = new RemoteServiceRegistry();
            registry.Register<IRemoteCalculator>();

            var dispatcher = new RemoteServiceDispatcher(
                provider,
                registry,
                provider.GetRequiredService<ILoggerFactory>().CreateLogger<RemoteServiceDispatcher>());

            var impl = provider.GetRequiredService<IRemoteCalculator>() as CalculatorImpl;
            return (dispatcher, impl!);
        }

        private static RemoteArgument Arg<T>(T value)
        {
            var type = value?.GetType() ?? typeof(T);
            return new RemoteArgument
            {
                TypeName = type.AssemblyQualifiedName,
                ValueJson = JsonSerializer.Serialize(value, type, _jsonOptions),
            };
        }

        private static RemoteInvocationRequest Request(string method, params RemoteArgument[] args)
        {
            return new RemoteInvocationRequest
            {
                ServiceName = nameof(IRemoteCalculator),
                MethodName = method,
                Arguments = args,
            };
        }

        [Fact]
        public async Task Void_Method_Returns_Success_With_No_Result()
        {
            var (dispatcher, _) = CreateDispatcher();

            var response = await dispatcher.InvokeAsync(Request(nameof(IRemoteCalculator.Clear)));

            Assert.True(response.Success);
            Assert.Null(response.ResultJson);
        }

        [Fact]
        public async Task Sync_Return_Int_Returns_Serialized_Result()
        {
            var (dispatcher, _) = CreateDispatcher();

            var response = await dispatcher.InvokeAsync(
                Request(nameof(IRemoteCalculator.Add), Arg(3), Arg(4)));

            Assert.True(response.Success);
            Assert.Equal(7, JsonSerializer.Deserialize<int>(response.ResultJson!, _jsonOptions));
        }

        [Fact]
        public async Task Sync_Return_String_Returns_Serialized_Result()
        {
            var (dispatcher, _) = CreateDispatcher();

            var response = await dispatcher.InvokeAsync(
                Request(nameof(IRemoteCalculator.Echo), Arg("hello")));

            Assert.True(response.Success);
            Assert.Equal("echo:hello", JsonSerializer.Deserialize<string>(response.ResultJson!, _jsonOptions));
        }

        [Fact]
        public async Task Async_Task_Method_Awaits_And_Returns_Success()
        {
            var (dispatcher, _) = CreateDispatcher();

            var response = await dispatcher.InvokeAsync(
                Request(nameof(IRemoteCalculator.ResetAsync)));

            Assert.True(response.Success);
            Assert.Null(response.ResultJson);
        }

        [Fact]
        public async Task Async_TaskT_Method_Returns_Serialized_Result()
        {
            var (dispatcher, _) = CreateDispatcher();

            var response = await dispatcher.InvokeAsync(
                Request(nameof(IRemoteCalculator.MultiplyAsync), Arg(6), Arg(7)));

            Assert.True(response.Success);
            Assert.Equal(42, JsonSerializer.Deserialize<int>(response.ResultJson!, _jsonOptions));
        }

        [Fact]
        public async Task CancellationToken_Is_Injected_From_Dispatch_Parameter()
        {
            var (dispatcher, impl) = CreateDispatcher();

            using var cts = new CancellationTokenSource();
            // 请求中没有 CancellationToken 参数（客户端会过滤掉），但 dispatcher 会将传入的 token 注入
            var response = await dispatcher.InvokeAsync(
                Request(nameof(IRemoteCalculator.GreetAsync), Arg("world")),
                cts.Token);

            Assert.True(response.Success);
            Assert.Equal("hello,world", JsonSerializer.Deserialize<string>(response.ResultJson!, _jsonOptions));
            Assert.Equal(0, impl.LastCancellationTokenIsCanceled); // token 未取消
        }

        [Fact]
        public async Task CancellationToken_Canceled_Is_Reflected_In_Service()
        {
            var (dispatcher, impl) = CreateDispatcher();

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var response = await dispatcher.InvokeAsync(
                Request(nameof(IRemoteCalculator.GreetAsync), Arg("world")),
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
                MethodName = "Foo",
            };

            var response = await dispatcher.InvokeAsync(request);

            Assert.False(response.Success);
            Assert.Contains("IUnknownService", response.ErrorMessage!);
        }

        [Fact]
        public async Task Unknown_Method_Returns_Failure()
        {
            var (dispatcher, _) = CreateDispatcher();

            var response = await dispatcher.InvokeAsync(Request("NonExistentMethod"));

            Assert.False(response.Success);
            Assert.Contains("NonExistentMethod", response.ErrorMessage!);
        }

        [Fact]
        public async Task Service_Throws_Exception_Returns_Failure_With_ErrorInfo()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
            services.AddScoped<IRemoteCalculator, ThrowingCalculator>();
            var provider = services.BuildServiceProvider();

            var registry = new RemoteServiceRegistry();
            registry.Register<IRemoteCalculator>();

            var dispatcher = new RemoteServiceDispatcher(provider, registry);

            var response = await dispatcher.InvokeAsync(
                Request(nameof(IRemoteCalculator.Add), Arg(1), Arg(2)));

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
