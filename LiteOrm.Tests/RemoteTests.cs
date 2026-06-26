using Castle.DynamicProxy;
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
    /// LiteOrm.Remote 远程调用拦截器的单元测试。
    /// 使用一个 stub <see cref="IRemoteServiceTransport"/> 记录请求并返回预设响应，
    /// 不依赖真实网络或数据库。
    /// </summary>
    public class RemoteTests
    {
        private static readonly ProxyGenerator _proxyGenerator = new ProxyGenerator();

        // 测试用服务接口 - 覆盖 void / 同步返回 / Task / Task<T> / CancellationToken 传递
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
        /// stub 传输：记录最近一次请求及 CancellationToken，并按配置返回响应或抛异常。
        /// </summary>
        private sealed class StubTransport : IRemoteServiceTransport
        {
            public RemoteInvocationRequest LastRequest { get; private set; }
            public CancellationToken LastCancellationToken { get; private set; }
            public int CallCount { get; private set; }
            private readonly Func<RemoteInvocationRequest, RemoteInvocationResponse> _responder;

            public StubTransport(Func<RemoteInvocationRequest, RemoteInvocationResponse> responder)
            {
                _responder = responder;
            }

            public Task<RemoteInvocationResponse> InvokeAsync(RemoteInvocationRequest request, CancellationToken cancellationToken = default)
            {
                LastRequest = request;
                LastCancellationToken = cancellationToken;
                CallCount++;
                return Task.FromResult(_responder(request));
            }
        }

        private static (IRemoteCalculator proxy, StubTransport transport) CreateProxy(Func<RemoteInvocationRequest, RemoteInvocationResponse> responder)
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
            var provider = services.BuildServiceProvider();
            var transport = new StubTransport(responder);
            var interceptor = new RemoteServiceInvokeInterceptor(
                provider.GetRequiredService<ILoggerFactory>(),
                provider,
                transport);
            var proxy = _proxyGenerator.CreateInterfaceProxyWithoutTarget<IRemoteCalculator>(interceptor.ToInterceptor());
            return (proxy, transport);
        }

        private static RemoteInvocationResponse Ok(object result = null, Type resultType = null)
        {
            return new RemoteInvocationResponse
            {
                Success = true,
                ResultJson = result is null ? null : JsonSerializer.Serialize(result, resultType ?? result.GetType()),
                ResultTypeName = (resultType ?? result?.GetType())?.AssemblyQualifiedName,
            };
        }

        private static RemoteInvocationResponse Fail(string errorType, string message)
        {
            return new RemoteInvocationResponse
            {
                Success = false,
                ErrorType = errorType,
                ErrorMessage = message,
            };
        }

        [Fact]
        public void Void_Method_Invokes_Transport_And_Swallows_Result()
        {
            var (proxy, transport) = CreateProxy(_ => Ok());
            proxy.Clear();

            Assert.Equal(1, transport.CallCount);
            Assert.Equal(nameof(IRemoteCalculator), transport.LastRequest.ServiceName);
            Assert.Equal(nameof(IRemoteCalculator.Clear), transport.LastRequest.MethodName);
            Assert.Empty(transport.LastRequest.Arguments);
        }

        [Fact]
        public void Sync_Return_Int_Deserializes_Response()
        {
            var (proxy, transport) = CreateProxy(req =>
            {
                Assert.Equal(2, req.Arguments.Count);
                Assert.Equal(typeof(int).AssemblyQualifiedName, req.Arguments[0].TypeName);
                Assert.Equal("3", req.Arguments[0].ValueJson);
                Assert.Equal("4", req.Arguments[1].ValueJson);
                return Ok(7, typeof(int));
            });

            int result = proxy.Add(3, 4);

            Assert.Equal(7, result);
            Assert.Equal(nameof(IRemoteCalculator.Add), transport.LastRequest.MethodName);
        }

        [Fact]
        public void Sync_Return_String_Deserializes_Response()
        {
            var (proxy, transport) = CreateProxy(_ => Ok("hello-back", typeof(string)));

            string result = proxy.Echo("hello");

            Assert.Equal("hello-back", result);
        }

        [Fact]
        public async Task Async_Task_Method_Awaits_Transport()
        {
            var (proxy, transport) = CreateProxy(_ => Ok());

            await proxy.ResetAsync();

            Assert.Equal(1, transport.CallCount);
            Assert.Equal(nameof(IRemoteCalculator.ResetAsync), transport.LastRequest.MethodName);
        }

        [Fact]
        public async Task Async_TaskT_Method_Deserializes_Response()
        {
            var (proxy, transport) = CreateProxy(req =>
            {
                // CancellationToken 被过滤，只剩两个 int 参数
                Assert.Equal(2, req.Arguments.Count);
                return Ok(42, typeof(int));
            });

            int result = await proxy.MultiplyAsync(6, 7);

            Assert.Equal(42, result);
            Assert.Equal(nameof(IRemoteCalculator.MultiplyAsync), transport.LastRequest.MethodName);
        }

        [Fact]
        public async Task CancellationToken_Is_Filtered_From_Arguments_But_Passed_To_Transport()
        {
            var (proxy, transport) = CreateProxy(_ => Ok("hi", typeof(string)));

            using var cts = new CancellationTokenSource();
            string result = await proxy.GreetAsync("world", cts.Token);

            Assert.Equal("hi", result);
            // CancellationToken 不应出现在序列化参数中
            Assert.Single(transport.LastRequest.Arguments);
            Assert.Equal(typeof(string).AssemblyQualifiedName, transport.LastRequest.Arguments[0].TypeName);
            Assert.Equal("\"world\"", transport.LastRequest.Arguments[0].ValueJson);
            // CancellationToken 应被传递给 InvokeAsync
            Assert.Equal(cts.Token, transport.LastCancellationToken);
        }

        [Fact]
        public async Task CancellationToken_Default_Passed_As_None_When_No_Token_Parameter()
        {
            var (proxy, transport) = CreateProxy(_ => Ok(42, typeof(int)));

            await proxy.MultiplyAsync(6, 7);

            Assert.Equal(CancellationToken.None, transport.LastCancellationToken);
        }

        [Fact]
        public void Failed_Response_Throws_ServiceException()
        {
            var (proxy, transport) = CreateProxy(_ => Fail("System.InvalidOperationException", "boom"));

            var ex = Assert.Throws<ServiceException>(() => proxy.Add(1, 2));
            Assert.Contains("System.InvalidOperationException", ex.Message);
            Assert.Contains("boom", ex.Message);
        }

        [Fact]
        public async Task Failed_Response_On_Async_Method_Throws_ServiceException()
        {
            var (proxy, transport) = CreateProxy(_ => Fail("System.ArgumentException", "bad arg"));

            await Assert.ThrowsAsync<ServiceException>(() => proxy.MultiplyAsync(1, 2));
        }

        [Fact]
        public void Transport_Throws_NonBusiness_Exception_Wrapped_As_RemoteTransportException()
        {
            var (proxy, transport) = CreateProxy(_ => throw new InvalidOperationException("network down"));

            var ex = Assert.Throws<RemoteTransportException>(() => proxy.Add(1, 2));
            Assert.Contains("network down", ex.Message);
        }

        [Fact]
        public void Null_Response_Throws_RemoteTransportException()
        {
            var (proxy, transport) = CreateProxy(_ => null!);

            Assert.Throws<RemoteTransportException>(() => proxy.Add(1, 2));
        }

        [Fact]
        public void Global_ExceptionHandling_Event_Can_Handle_Exception()
        {
            var (proxy, transport) = CreateProxy(_ => Fail("System.Exception", "fail"));
            EventHandler<ServiceExceptionContext> handler = (s, ctx) => ctx.Handle(999);
            try
            {
                RemoteServiceInvokeInterceptor.ExceptionHandling += handler;

                int result = proxy.Add(1, 2);

                Assert.Equal(999, result);
            }
            finally
            {
                RemoteServiceInvokeInterceptor.ExceptionHandling -= handler;
            }
        }

        [Fact]
        public void RegisterLiteOrmRemote_Without_Transport_Or_Uri_Throws()
        {
            var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder();
            var ex = Assert.Throws<InvalidOperationException>(() =>
                host.RegisterLiteOrmRemote().Build());
            Assert.Contains("RemoteServiceUri", ex.Message);
        }

        [Fact]
        public async Task RegisterLiteOrmRemote_With_Custom_Transport_Registers_It()
        {
            var tcs = new TaskCompletionSource<RemoteInvocationRequest>();
            var stub = new StubTransport(req =>
            {
                tcs.TrySetResult(req);
                return Ok(5, typeof(int));
            });

            var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .RegisterLiteOrmRemote(opts => opts.Transport = stub)
                .Build();

            try
            {
                using var scope = host.Services.CreateScope();
                var resolvedTransport = scope.ServiceProvider.GetRequiredService<IRemoteServiceTransport>();
                Assert.Same(stub, resolvedTransport);

                var interceptor = scope.ServiceProvider.GetRequiredService<RemoteServiceInvokeInterceptor>();
                var proxy = _proxyGenerator.CreateInterfaceProxyWithoutTarget<IRemoteCalculator>(interceptor.ToInterceptor());

                int result = await proxy.MultiplyAsync(2, 3);

                Assert.Equal(5, result);
                var captured = await tcs.Task;
                Assert.Equal(nameof(IRemoteCalculator.MultiplyAsync), captured.MethodName);
            }
            finally
            {
                await host.StopAsync();
                host.Dispose();
            }
        }
    }
}
