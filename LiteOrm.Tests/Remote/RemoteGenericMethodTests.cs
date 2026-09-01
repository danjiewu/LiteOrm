using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Remote;
using LiteOrm.Remote.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// 泛型方法远程调用测试。验证泛型方法（如 <c>Task&lt;T&gt; GetConfigAsync&lt;T&gt;(string key)</c>）经
    /// 客户端序列化 → 服务端 <see cref="RemoteServiceDispatcher.ParseRequest"/> 解析 → 调用的完整链路，
    /// 修复服务端直接 Invoke 开放泛型方法定义导致 "ContainsGenericParameters" 异常的问题。
    /// </summary>
    public class RemoteGenericMethodTests
    {
        [Service]
        public interface IGenericConfigService
        {
            Task<T> GetConfigAsync<T>(string key);
            T EchoGeneric<T>(T value);
            [ServiceMethod(MethodName = "FetchValue")]
            Task<T> FetchValueAsync<T>(string key);
        }

        private sealed class GenericConfigService : IGenericConfigService
        {
            public Task<T> GetConfigAsync<T>(string key) => Task.FromResult((T)Convert.ChangeType(key, typeof(T)));
            public T EchoGeneric<T>(T value) => value;
            public Task<T> FetchValueAsync<T>(string key) => Task.FromResult((T)Convert.ChangeType(key, typeof(T)));
        }

        public class MyConfig
        {
            public string Key { get; set; } = string.Empty;
            public int Value { get; set; }
        }

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true,
        };

        private static ITypeNameResolver CreateResolver()
        {
            return new DelegateTypeNameResolver(
                TypeResolverHelper.GetName,
                name => name switch
                {
                    "IGenericConfigService" => typeof(IGenericConfigService),
                    "string" => typeof(string),
                    "int" => typeof(int),
                    "bool" => typeof(bool),
                    "MyConfig" => typeof(MyConfig),
                    _ => null,
                });
        }

        /// <summary>
        /// 模拟 HTTP 传输的内存通道：客户端请求 JSON 序列化 → 服务端 ParseRequest 解析 → 执行。
        /// </summary>
        private sealed class StubTransport : IRemoteServiceTransport
        {
            private readonly RemoteServiceDispatcher _dispatcher;
            public string? LastJson;
            public StubTransport(RemoteServiceDispatcher dispatcher) => _dispatcher = dispatcher;

            public Task<RemoteInvocationResponse> InvokeAsync(RemoteInvocationRequest request, CancellationToken cancellationToken = default)
            {
                var json = JsonSerializer.Serialize(request, _jsonOptions);
                LastJson = json;
                var parsed = _dispatcher.ParseRequest(json, _jsonOptions);
                return _dispatcher.InvokeAsync(parsed, cancellationToken);
            }
        }

        private static (IGenericConfigService proxy, StubTransport transport) CreateProxy()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
            services.AddScoped<IGenericConfigService, GenericConfigService>();
            var provider = services.BuildServiceProvider();

            var dispatcher = new RemoteServiceDispatcher(
                provider, CreateResolver(),
                provider.GetRequiredService<ILoggerFactory>().CreateLogger<RemoteServiceDispatcher>());

            var transport = new StubTransport(dispatcher);
            var interceptor = new RemoteServiceInvokeInterceptor(provider.GetRequiredService<ILoggerFactory>(), transport);
            var proxy = RemoteProxyGenerator.CreateRemoteServiceProxy<IGenericConfigService>(interceptor);
            return (proxy, transport);
        }

        /// <summary>
        /// 复现用户场景：异步泛型方法 GetConfigAsync&lt;string&gt; 端到端调用成功返回结果。
        /// </summary>
        [Fact]
        public async Task Generic_Async_Method_EndToEnd_Returns_Result()
        {
            var (proxy, transport) = CreateProxy();

            var result = await proxy.GetConfigAsync<string>("hello");

            Assert.Equal("hello", result);
            // 类型参数名使用 CLR 短名（TypeResolverHelper.GetName），如 String/Int32
            Assert.Contains("\"GetConfigAsync<String>\"", transport.LastJson!);
        }

        [Fact]
        public async Task Generic_Async_Method_Int_Converts_Type()
        {
            var (proxy, _) = CreateProxy();

            var result = await proxy.GetConfigAsync<int>("42");

            Assert.Equal(42, result);
        }

        [Fact]
        public async Task Generic_Sync_Method_UserDefinedType_EndToEnd()
        {
            var (proxy, _) = CreateProxy();
            var input = new MyConfig { Key = "k", Value = 7 };

            var result = await Task.Run(() => proxy.EchoGeneric(input));

            Assert.NotNull(result);
            Assert.Equal("k", result.Key);
            Assert.Equal(7, result.Value);
        }

        [Fact]
        public async Task Generic_Async_Method_With_Configured_MethodName_EndToEnd()
        {
            var (proxy, transport) = CreateProxy();

            var result = await proxy.FetchValueAsync<string>("abc");

            Assert.Equal("abc", result);
            // 方法名序列化优先使用 [ServiceMethod(MethodName)] 配置名
            Assert.Contains("\"FetchValue<String>\"", transport.LastJson!);
        }

        [Fact]
        public void Serialized_Method_Contains_Generic_Type_Args()
        {
            var method = typeof(IGenericConfigService).GetMethod(nameof(IGenericConfigService.GetConfigAsync))!;
            var closed = method.MakeGenericMethod(typeof(string));
            var request = new RemoteInvocationRequest
            {
                ServiceName = TypeResolverHelper.GetName(typeof(IGenericConfigService)),
                Method = closed,
                Arguments = new object[] { "hello" },
            };

            var json = JsonSerializer.Serialize(request, _jsonOptions);

            Assert.True(json.Contains("GetConfigAsync<"), "JSON: " + json);
        }

        [Fact]
        public void Serialized_Method_Prefers_ServiceMethod_Configured_Name()
        {
            var method = typeof(IGenericConfigService).GetMethod(nameof(IGenericConfigService.FetchValueAsync))!;
            var closed = method.MakeGenericMethod(typeof(string));
            var request = new RemoteInvocationRequest
            {
                ServiceName = TypeResolverHelper.GetName(typeof(IGenericConfigService)),
                Method = closed,
                Arguments = new object[] { "hello" },
            };

            var json = JsonSerializer.Serialize(request, _jsonOptions);

            // 配置名优先于实际方法名
            Assert.Contains("\"FetchValue<String>\"", json);
            Assert.DoesNotContain("\"FetchValueAsync<String>\"", json);
        }
    }
}
