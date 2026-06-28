using Castle.DynamicProxy;
using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// 远程调用参数属性回写功能测试。
    /// 验证 <see cref="ArgumentOutAttribute"/> 通过 <see cref="IArgumentOutHandler"/> 处理回写，
    /// 覆盖 <see cref="IdentityOutAttribute"/>、自定义处理器、不同 <see cref="IArgumentOutHandler.ReturnType"/>、
    /// 以及 <see cref="CopyableOutAttribute"/> + <see cref="ICopyable"/> 等场景，
    /// 包含服务端 <see cref="IArgumentOutHandler.GenerateReturnValue"/> 与客户端 <see cref="IArgumentOutHandler.WriteBack"/> 两阶段流程。
    /// </summary>
    public class RemoteWriteBackTests
    {
        /// <summary>
        /// 测试用实体。Id 标记为自增主键，配合 <see cref="IdentityOutAttribute"/> 使用。
        /// </summary>
        [Table("Users")]
        public class User
        {
            public string Name { get; set; } = string.Empty;

            [Column(IsIdentity = true)]
            public long Id { get; set; }

            public DateTime CreatedAt { get; set; }
            public string ServerOnly { get; set; } = string.Empty;
        }

        /// <summary>
        /// 实现了 <see cref="ICopyable"/> 的测试实体，配合 <see cref="CopyableOutAttribute"/> 整体回写。
        /// </summary>
        public class CopyableUser : ICopyable
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }

            public void CopyFrom(object source)
            {
                var s = (CopyableUser)source;
                Id = s.Id;
                Name = s.Name;
                CreatedAt = s.CreatedAt;
            }
        }

        /// <summary>
        /// 处理器返回的精简 DTO（演示 <see cref="IArgumentOutHandler.ReturnType"/> 与参数类型不同）。
        /// </summary>
        public class UserDelta
        {
            public long Id { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        /// <summary>
        /// 服务接口：参数级回写。
        /// </summary>
        public interface IUserService
        {
            // IdentityOutAttribute：仅回写自增主键（Id），返回值类型为 long
            Task CreateAsync([IdentityOut] User user);
            Task<long> CreateAndReturnIdAsync([IdentityOut] User user);
            // 自定义处理器：返回值类型为 User
            Task CreateWithHandlerAsync([ArgumentOut(typeof(ToUpperNameHandler), typeof(User))] User user);
            Task CreateIdOnlyAsync([ArgumentOut(typeof(IdOnlyHandler), typeof(User))] User user);
            // 自定义处理器：返回值类型为 UserDelta
            Task CreateDeltaAsync([ArgumentOut(typeof(DeltaHandler), typeof(UserDelta))] User user);
            // 集合模式：逐项回写 Identity
            Task CreateBatchAsync([IdentityOut(Mode = ArgumentMode.Collection)] List<User> users);
            // 集合模式 + 自定义处理器
            Task CreateBatchDeltaAsync([ArgumentOut(typeof(DeltaHandler), typeof(UserDelta), Mode = ArgumentMode.Collection)] List<User> users);
        }

        /// <summary>
        /// 服务接口：使用通用 CopyableOutAttribute 处理 ICopyable 参数。
        /// </summary>
        public interface ICopyableUserService
        {
            Task CreateAsync([CopyableOut(typeof(CopyableUser))] CopyableUser user);
        }

        /// <summary>
        /// 服务接口：无回写标记。
        /// </summary>
        public interface ISimpleService
        {
            Task DoAsync(string name);
        }

        /// <summary>
        /// 自定义处理器：服务端返回参数本身，客户端将 Name 转大写并回写 Id。
        /// </summary>
        public class ToUpperNameHandler : IArgumentOutHandler
        {
            public Type ReturnType { get; }
            public ToUpperNameHandler(Type returnType) { ReturnType = returnType; }
            public object GenerateReturnValue(object argument) => argument;
            public void WriteBack(object originalArg, object returnValue)
            {
                var orig = (User)originalArg;
                var server = (User)returnValue;
                orig.Id = server.Id;
                orig.Name = server.Name.ToUpperInvariant();
            }
        }

        /// <summary>
        /// 自定义处理器：服务端返回参数本身，客户端仅回写 Id。
        /// </summary>
        public class IdOnlyHandler : IArgumentOutHandler
        {
            public Type ReturnType { get; }
            public IdOnlyHandler(Type returnType) { ReturnType = returnType; }
            public object GenerateReturnValue(object argument) => argument;
            public void WriteBack(object originalArg, object returnValue)
            {
                var orig = (User)originalArg;
                var server = (User)returnValue;
                orig.Id = server.Id;
            }
        }

        /// <summary>
        /// 自定义处理器：服务端生成精简 DTO（ReturnType 与参数类型不同），客户端按 DTO 回写。
        /// </summary>
        public class DeltaHandler : IArgumentOutHandler
        {
            public Type ReturnType { get; }
            public DeltaHandler(Type returnType) { ReturnType = returnType; }
            public object GenerateReturnValue(object argument)
            {
                var u = (User)argument;
                return new UserDelta { Id = u.Id, CreatedAt = u.CreatedAt };
            }
            public void WriteBack(object originalArg, object returnValue)
            {
                var orig = (User)originalArg;
                var delta = (UserDelta)returnValue;
                orig.Id = delta.Id;
                orig.CreatedAt = delta.CreatedAt;
            }
        }

        // ===== Stub 传输 =====
        private sealed class StubTransport : IRemoteServiceTransport
        {
            public RemoteInvocationRequest LastRequest { get; private set; } = null!;
            public int CallCount { get; private set; }
            private readonly Func<RemoteInvocationRequest, RemoteInvocationResponse> _responder;
            private readonly RemoteServiceDispatcher? _dispatcher;
            private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNameCaseInsensitive = true,
            };

            /// <summary>
            /// 初始化 StubTransport。
            /// </summary>
            /// <param name="responder">响应生成函数。</param>
            /// <param name="dispatcher">
            /// 若非 null，则模拟 HTTP 传输：将请求序列化为 JSON，再通过 <see cref="RemoteServiceDispatcher.ParseRequest"/> 解析，
            /// 确保服务端收到的是参数副本而非客户端原始对象引用。
            /// 若为 null（纯客户端测试），直接将原始请求传给 responder。
            /// </param>
            public StubTransport(Func<RemoteInvocationRequest, RemoteInvocationResponse> responder, RemoteServiceDispatcher? dispatcher = null)
            {
                _responder = responder;
                _dispatcher = dispatcher;
            }

            public Task<RemoteInvocationResponse> InvokeAsync(RemoteInvocationRequest request, CancellationToken cancellationToken = default)
            {
                LastRequest = request;
                CallCount++;

                if (_dispatcher is null)
                    return Task.FromResult(_responder(request));

                // 模拟 HTTP 传输中的 JSON 序列化/反序列化，
                // 通过 dispatcher.ParseRequest 解析 JSON：匹配服务 → 查找方法 → 反序列化参数
                var json = JsonSerializer.Serialize(request, _jsonOptions);
                var parsedRequest = _dispatcher.ParseRequest(json, _jsonOptions);
                return Task.FromResult(_responder(parsedRequest));
            }
        }

        private static T CreateProxy<T>(IServiceProvider provider, IRemoteServiceTransport transport) where T : class
        {
            var interceptor = new RemoteServiceInvokeInterceptor(
                provider.GetRequiredService<ILoggerFactory>(),
                provider,
                transport);
            return new ProxyGenerator().CreateInterfaceProxyWithoutTarget<T>(interceptor.ToInterceptor());
        }

        private static IServiceProvider BuildProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
            return services.BuildServiceProvider();
        }

        /// <summary>
        /// 通过接口类型与方法名构建 <see cref="RemoteInvocationRequest"/>。
        /// 通过反射设置 <see cref="RemoteInvocationRequest.Method"/>（<see cref="MethodInfo"/>），
        /// 模拟经 <see cref="RemoteServiceDispatcher.ParseRequest"/> 解析后的请求。
        /// </summary>
        private static RemoteInvocationRequest Request<TInterface>(string method, params object[] args)
        {
            var methodInfo = typeof(TInterface).GetMethod(method, BindingFlags.Public | BindingFlags.Instance);
            return new RemoteInvocationRequest
            {
                ServiceName = RemoteServiceNameUtil.GetServiceName(typeof(TInterface)),
                Method = methodInfo,
                Arguments = args,
            };
        }

        /// <summary>
        /// 创建测试用 <see cref="TableInfoProvider"/>（基于 <see cref="AttributeTableInfoProvider"/>，依赖已 Mock）。
        /// <see cref="IdentityOutAttribute"/> 通过 <see cref="TableInfoProvider.Default"/> 解析 Identity 列，
        /// 客户端与服务端均需注册。
        /// </summary>
        private static TableInfoProvider CreateTestTableInfoProvider()
        {
            var sqlBuilderFactory = new Mock<ISqlBuilderFactory>();
            sqlBuilderFactory
                .Setup(f => f.GetSqlBuilder(It.IsAny<Type>(), It.IsAny<string>()))
                .Returns(SqlBuilder.Instance);

            var dataSourceProvider = new Mock<IDataSourceProvider>();
            dataSourceProvider.SetupGet(p => p.DefaultDataSourceName).Returns("default");
            dataSourceProvider
                .Setup(p => p.GetDataSource(It.IsAny<string>()))
                .Returns(new DataSourceConfig { Name = "default", Provider = typeof(DbConnection).AssemblyQualifiedName });

            var services = new ServiceCollection();
            services.AddSingleton(sqlBuilderFactory.Object);
            services.AddSingleton(dataSourceProvider.Object);
            return new AttributeTableInfoProvider(services.BuildServiceProvider());
        }

        /// <summary>
        /// 设置 <see cref="TableInfoProvider.Default"/> 的作用域，Dispose 时恢复原值。
        /// </summary>
        private readonly struct TableInfoProviderScope : IDisposable
        {
            private readonly TableInfoProvider _previous;
            public TableInfoProviderScope(TableInfoProvider provider)
            {
                _previous = TableInfoProvider.Default;
                TableInfoProvider.Default = provider;
            }
            public void Dispose() => TableInfoProvider.Default = _previous;
        }

        // ========== 客户端测试：IdentityOutAttribute ==========

        [Fact]
        public async Task Client_IdentityHandler_WriteBack_Identity_Only()
        {
            using var scope = new TableInfoProviderScope(CreateTestTableInfoProvider());
            var user = new User { Name = "alice", Id = 0, CreatedAt = default };
            var stub = new StubTransport(req =>
            {
                // 服务端 IdentityOutAttribute 仅返回 Id 值
                return new RemoteInvocationResponse
                {
                    Success = true,
                    WriteBackArguments = new[]
                    {
                        new OutputArgument
                        {
                            ArgumentIndex = 0,
                            Value = 42L,
                        }
                    }
                };
            });

            var provider = BuildProvider();
            var proxy = CreateProxy<IUserService>(provider, stub);

            await proxy.CreateAsync(user);

            // IdentityOutAttribute 仅回写 Id，其他属性保持不变
            Assert.Equal(42, user.Id);
            Assert.Equal("alice", user.Name);
            Assert.Equal(default, user.CreatedAt);
            Assert.Equal(string.Empty, user.ServerOnly);
        }

        [Fact]
        public async Task Client_IdentityHandler_With_TaskT_ReturnValue()
        {
            using var scope = new TableInfoProviderScope(CreateTestTableInfoProvider());
            var user = new User { Name = "alice", Id = 0, CreatedAt = default, ServerOnly = "client" };
            var stub = new StubTransport(req =>
            {
                return new RemoteInvocationResponse
                {
                    Success = true,
                    WriteBackArguments = new[]
                    {
                        new OutputArgument
                        {
                            ArgumentIndex = 0,
                            Value = 99L,
                        }
                    }
                };
            });

            var provider = BuildProvider();
            var proxy = CreateProxy<IUserService>(provider, stub);

            long result = await proxy.CreateAndReturnIdAsync(user);

            // IdentityOutAttribute 仅回写 Id
            Assert.Equal(99, user.Id);
            Assert.Equal("alice", user.Name);
            Assert.Equal(default, user.CreatedAt);
            Assert.Equal("client", user.ServerOnly);
            // 未设置 Result，返回值退回到 default(long)
            Assert.Equal(0L, result);
        }

        [Fact]
        public async Task Client_NoArgumentOutAttribute_NoWriteBackData_Sent()
        {
            var stub = new StubTransport(_ => new RemoteInvocationResponse { Success = true });
            var provider = BuildProvider();
            var proxy = CreateProxy<ISimpleService>(provider, stub);

            await proxy.DoAsync("x");

            // ISimpleService.DoAsync 无 ArgumentOutAttribute，回写计划为空，调用正常完成
            Assert.Equal(1, stub.CallCount);
        }

        // ========== 客户端测试：自定义处理器 ==========

        [Fact]
        public async Task Client_Custom_Handler_Transforms_WriteBack()
        {
            var user = new User { Name = "alice", Id = 0 };
            var stub = new StubTransport(req =>
            {
                var serverUser = new User { Name = "alice", Id = 42, CreatedAt = new DateTime(2026, 1, 1), ServerOnly = "srv" };
                return new RemoteInvocationResponse
                {
                    Success = true,
                    WriteBackArguments = new[]
                    {
                        new OutputArgument
                        {
                            ArgumentIndex = 0,
                            Value = serverUser,
                        }
                    }
                };
            });

            var provider = BuildProvider();
            var proxy = CreateProxy<IUserService>(provider, stub);

            await proxy.CreateWithHandlerAsync(user);

            // ToUpperNameHandler: Id 回写，Name 转大写
            Assert.Equal(42, user.Id);
            Assert.Equal("ALICE", user.Name);
            Assert.Equal(default, user.CreatedAt);
            Assert.Equal(string.Empty, user.ServerOnly);
        }

        [Fact]
        public async Task Client_Custom_Handler_Writes_Subset_Only()
        {
            var user = new User { Name = "alice", Id = 0, CreatedAt = default, ServerOnly = "client" };
            var stub = new StubTransport(req =>
            {
                var serverUser = new User { Name = "bob", Id = 77, CreatedAt = new DateTime(2026, 2, 2), ServerOnly = "srv" };
                return new RemoteInvocationResponse
                {
                    Success = true,
                    WriteBackArguments = new[]
                    {
                        new OutputArgument
                        {
                            ArgumentIndex = 0,
                            Value = serverUser,
                        }
                    }
                };
            });

            var provider = BuildProvider();
            var proxy = CreateProxy<IUserService>(provider, stub);

            await proxy.CreateIdOnlyAsync(user);

            // IdOnlyHandler 仅回写 Id
            Assert.Equal(77, user.Id);
            Assert.Equal("alice", user.Name);
            Assert.Equal(default, user.CreatedAt);
            Assert.Equal("client", user.ServerOnly);
        }

        [Fact]
        public async Task Client_HandlerType_With_Different_ReturnType_Deserialized_Correctly()
        {
            var user = new User { Name = "alice", Id = 0, CreatedAt = default };
            var stub = new StubTransport(req =>
            {
                // 服务端 DeltaHandler 生成了 UserDelta（非 User），客户端应按 ReturnType 反序列化
                var delta = new UserDelta { Id = 88, CreatedAt = new DateTime(2026, 5, 5) };
                return new RemoteInvocationResponse
                {
                    Success = true,
                    WriteBackArguments = new[]
                    {
                        new OutputArgument
                        {
                            ArgumentIndex = 0,
                            Value = delta,
                        }
                    }
                };
            });

            var provider = BuildProvider();
            var proxy = CreateProxy<IUserService>(provider, stub);

            await proxy.CreateDeltaAsync(user);

            // DeltaHandler.WriteBack 仅回写 Id 和 CreatedAt
            Assert.Equal(88, user.Id);
            Assert.Equal(new DateTime(2026, 5, 5), user.CreatedAt);
            Assert.Equal("alice", user.Name);
        }

        [Fact]
        public async Task Client_CopyableHandler_Copies_Via_ICopyable()
        {
            var user = new CopyableUser { Id = 0, Name = "alice", CreatedAt = default };
            var stub = new StubTransport(req =>
            {
                var serverUser = new CopyableUser { Id = 100, Name = "server", CreatedAt = new DateTime(2026, 7, 7) };
                return new RemoteInvocationResponse
                {
                    Success = true,
                    WriteBackArguments = new[]
                    {
                        new OutputArgument
                        {
                            ArgumentIndex = 0,
                            Value = serverUser,
                        }
                    }
                };
            });

            var provider = BuildProvider();
            var proxy = CreateProxy<ICopyableUserService>(provider, stub);

            await proxy.CreateAsync(user);

            Assert.Equal(100, user.Id);
            Assert.Equal("server", user.Name);
            Assert.Equal(new DateTime(2026, 7, 7), user.CreatedAt);
        }

        // ========== 服务端测试 ==========

        private sealed class UserServiceImpl : IUserService
        {
            public Task CreateAsync(User user)
            {
                user.Id = 123;
                user.CreatedAt = new DateTime(2026, 1, 1);
                user.ServerOnly = "set-by-server";
                return Task.CompletedTask;
            }

            public Task<long> CreateAndReturnIdAsync(User user)
            {
                user.Id = 999;
                return Task.FromResult(user.Id);
            }

            public Task CreateWithHandlerAsync(User user)
            {
                user.Id = 123;
                user.Name = "alice";
                return Task.CompletedTask;
            }

            public Task CreateIdOnlyAsync(User user)
            {
                user.Id = 77;
                user.Name = "bob";
                user.CreatedAt = new DateTime(2026, 2, 2);
                return Task.CompletedTask;
            }

            public Task CreateDeltaAsync(User user)
            {
                user.Id = 88;
                user.CreatedAt = new DateTime(2026, 5, 5);
                return Task.CompletedTask;
            }

            public Task CreateBatchAsync(List<User> users)
            {
                // 模拟批量插入后服务端设置自增 Id
                for (int i = 0; i < users.Count; i++)
                    users[i].Id = 100 + i;
                return Task.CompletedTask;
            }

            public Task CreateBatchDeltaAsync(List<User> users)
            {
                for (int i = 0; i < users.Count; i++)
                {
                    users[i].Id = 200 + i;
                    users[i].CreatedAt = new DateTime(2026, 8, 1 + i);
                }
                return Task.CompletedTask;
            }
        }

        private sealed class CopyableUserServiceImpl : ICopyableUserService
        {
            public Task CreateAsync(CopyableUser user)
            {
                user.Id = 100;
                user.Name = "server";
                user.CreatedAt = new DateTime(2026, 7, 7);
                return Task.CompletedTask;
            }
        }

        private static (RemoteServiceDispatcher dispatcher, IServiceProvider provider) CreateDispatcher<TInterface, TImpl>()
            where TInterface : class
            where TImpl : class, TInterface, new()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
            services.AddScoped<TInterface, TImpl>();
            var provider = services.BuildServiceProvider();

            var resolver = new DelegateRemoteServiceTypeResolver(name =>
                name == RemoteServiceNameUtil.GetServiceName(typeof(TInterface)) ? typeof(TInterface) : null);

            var dispatcher = new RemoteServiceDispatcher(
                provider,
                resolver,
                provider.GetRequiredService<ILoggerFactory>().CreateLogger<RemoteServiceDispatcher>());
            return (dispatcher, provider);
        }

        [Fact]
        public async Task Server_IdentityHandler_Serializes_Identity_Only()
        {
            using var scope = new TableInfoProviderScope(CreateTestTableInfoProvider());
            var (dispatcher, _) = CreateDispatcher<IUserService, UserServiceImpl>();

            var userArg = new User { Name = "bob", Id = 0 };
            var request = Request<IUserService>(nameof(IUserService.CreateAsync), userArg);

            var response = await dispatcher.InvokeAsync(request);

            Assert.True(response.Success);
            Assert.Single(response.WriteBackArguments);
            var wb = response.WriteBackArguments[0];
            Assert.Equal(0, wb.ArgumentIndex);
            // IdentityOutAttribute 仅返回 Id 值（long），而非整个 User 对象
            Assert.Equal(123L, wb.Value);
        }

        [Fact]
        public async Task Server_WriteBack_With_TaskT_ReturnValue_Both_Returned()
        {
            using var scope = new TableInfoProviderScope(CreateTestTableInfoProvider());
            var (dispatcher, _) = CreateDispatcher<IUserService, UserServiceImpl>();

            var request = Request<IUserService>(nameof(IUserService.CreateAndReturnIdAsync), new User { Name = "x" });

            var response = await dispatcher.InvokeAsync(request);

            Assert.True(response.Success);
            Assert.Equal(999L, response.Result);
            Assert.Single(response.WriteBackArguments);
            // IdentityOutAttribute 仅返回 Id 值
            Assert.Equal(999L, response.WriteBackArguments[0].Value);
        }

        [Fact]
        public async Task Server_HandlerType_GenerateReturnValue_Used_For_Serialization()
        {
            var (dispatcher, _) = CreateDispatcher<IUserService, UserServiceImpl>();

            var request = Request<IUserService>(nameof(IUserService.CreateDeltaAsync), new User { Name = "x" });

            var response = await dispatcher.InvokeAsync(request);

            Assert.True(response.Success);
            Assert.Single(response.WriteBackArguments);
            // DeltaHandler 生成了 UserDelta（ReturnType != User）
            var delta = (UserDelta)response.WriteBackArguments[0].Value;
            Assert.Equal(88, delta.Id);
            Assert.Equal(new DateTime(2026, 5, 5), delta.CreatedAt);
        }

        [Fact]
        public async Task Server_CopyableHandler_Returns_Parameter_Itself()
        {
            var (dispatcher, _) = CreateDispatcher<ICopyableUserService, CopyableUserServiceImpl>();

            var request = Request<ICopyableUserService>(nameof(ICopyableUserService.CreateAsync), new CopyableUser { Name = "x" });

            var response = await dispatcher.InvokeAsync(request);

            Assert.True(response.Success);
            Assert.Single(response.WriteBackArguments);
            var written = (CopyableUser)response.WriteBackArguments[0].Value;
            Assert.Equal(100, written.Id);
            Assert.Equal("server", written.Name);
        }

        [Fact]
        public async Task Server_CollectionMode_IdentityHandler_Serializes_Id_List()
        {
            using var scope = new TableInfoProviderScope(CreateTestTableInfoProvider());
            var (dispatcher, _) = CreateDispatcher<IUserService, UserServiceImpl>();

            var users = new List<User>
            {
                new User { Name = "a", Id = 0 },
                new User { Name = "b", Id = 0 },
                new User { Name = "c", Id = 0 },
            };
            var request = Request<IUserService>(nameof(IUserService.CreateBatchAsync), users);

            var response = await dispatcher.InvokeAsync(request);

            Assert.True(response.Success);
            Assert.Single(response.WriteBackArguments);
            var wb = response.WriteBackArguments[0];
            // 集合模式：返回 List<long>
            var ids = (List<long>)wb.Value;
            Assert.Equal(new long[] { 100, 101, 102 }, ids);
        }

        [Fact]
        public async Task Server_CollectionMode_CustomHandler_Serializes_Delta_List()
        {
            var (dispatcher, _) = CreateDispatcher<IUserService, UserServiceImpl>();

            var users = new List<User>
            {
                new User { Name = "a", Id = 0 },
                new User { Name = "b", Id = 0 },
            };
            var request = Request<IUserService>(nameof(IUserService.CreateBatchDeltaAsync), users);

            var response = await dispatcher.InvokeAsync(request);

            Assert.True(response.Success);
            Assert.Single(response.WriteBackArguments);
            var wb = response.WriteBackArguments[0];
            // DeltaHandler.ReturnType = typeof(UserDelta)，集合模式下序列化为 List<UserDelta>
            var deltas = (List<UserDelta>)wb.Value;
            Assert.Equal(2, deltas.Count);
            Assert.Equal(200, deltas[0].Id);
            Assert.Equal(new DateTime(2026, 8, 1), deltas[0].CreatedAt);
            Assert.Equal(201, deltas[1].Id);
            Assert.Equal(new DateTime(2026, 8, 2), deltas[1].CreatedAt);
        }

        // ========== 端到端测试：客户端 → 服务端 ==========

        [Fact]
        public async Task EndToEnd_IdentityHandler_WriteBack_Via_Server()
        {
            using var scope = new TableInfoProviderScope(CreateTestTableInfoProvider());
            var (dispatcher, _) = CreateDispatcher<IUserService, UserServiceImpl>();
            var stubTransport = new StubTransport(req => dispatcher.InvokeAsync(req).GetAwaiter().GetResult(), dispatcher);

            var provider = BuildProvider();
            var proxy = CreateProxy<IUserService>(provider, stubTransport);

            var user = new User { Name = "carol", Id = 0, CreatedAt = default };

            await proxy.CreateAsync(user);

            // IdentityOutAttribute 仅回写 Id，服务端设置的其他属性不会同步到客户端
            Assert.Equal(123, user.Id);
            Assert.Equal("carol", user.Name);
            Assert.Equal(default, user.CreatedAt);
            Assert.Equal(string.Empty, user.ServerOnly);
        }

        [Fact]
        public async Task EndToEnd_Custom_Handler_WriteBack_Via_Server()
        {
            var (dispatcher, _) = CreateDispatcher<IUserService, UserServiceImpl>();
            var stubTransport = new StubTransport(req => dispatcher.InvokeAsync(req).GetAwaiter().GetResult(), dispatcher);

            var provider = BuildProvider();
            var proxy = CreateProxy<IUserService>(provider, stubTransport);

            var user = new User { Name = "carol", Id = 0 };

            await proxy.CreateWithHandlerAsync(user);

            // ToUpperNameHandler: Id 回写，Name 转大写
            Assert.Equal(123, user.Id);
            Assert.Equal("ALICE", user.Name);
        }

        [Fact]
        public async Task EndToEnd_DeltaHandler_Two_Phase_Flow_Via_Server()
        {
            var (dispatcher, _) = CreateDispatcher<IUserService, UserServiceImpl>();
            var stubTransport = new StubTransport(req => dispatcher.InvokeAsync(req).GetAwaiter().GetResult(), dispatcher);

            var provider = BuildProvider();
            var proxy = CreateProxy<IUserService>(provider, stubTransport);

            var user = new User { Name = "carol", Id = 0, CreatedAt = default };

            await proxy.CreateDeltaAsync(user);

            Assert.Equal(88, user.Id);
            Assert.Equal(new DateTime(2026, 5, 5), user.CreatedAt);
            Assert.Equal("carol", user.Name);
        }

        [Fact]
        public async Task EndToEnd_CopyableHandler_WriteBack_Via_ICopyable()
        {
            var (dispatcher, _) = CreateDispatcher<ICopyableUserService, CopyableUserServiceImpl>();
            var stubTransport = new StubTransport(req => dispatcher.InvokeAsync(req).GetAwaiter().GetResult(), dispatcher);

            var provider = BuildProvider();
            var proxy = CreateProxy<ICopyableUserService>(provider, stubTransport);

            var user = new CopyableUser { Id = 0, Name = "carol", CreatedAt = default };

            await proxy.CreateAsync(user);

            Assert.Equal(100, user.Id);
            Assert.Equal("server", user.Name);
            Assert.Equal(new DateTime(2026, 7, 7), user.CreatedAt);
        }

        [Fact]
        public async Task EndToEnd_CollectionMode_IdentityHandler_WriteBack_Each_Item()
        {
            using var scope = new TableInfoProviderScope(CreateTestTableInfoProvider());
            var (dispatcher, _) = CreateDispatcher<IUserService, UserServiceImpl>();
            var stubTransport = new StubTransport(req => dispatcher.InvokeAsync(req).GetAwaiter().GetResult(), dispatcher);

            var provider = BuildProvider();
            var proxy = CreateProxy<IUserService>(provider, stubTransport);

            var users = new List<User>
            {
                new User { Name = "a", Id = 0 },
                new User { Name = "b", Id = 0 },
                new User { Name = "c", Id = 0 },
            };

            await proxy.CreateBatchAsync(users);

            // 集合模式：逐项回写 Identity，其他属性不变
            Assert.Equal(100, users[0].Id);
            Assert.Equal("a", users[0].Name);
            Assert.Equal(101, users[1].Id);
            Assert.Equal("b", users[1].Name);
            Assert.Equal(102, users[2].Id);
            Assert.Equal("c", users[2].Name);
        }

        [Fact]
        public async Task EndToEnd_CollectionMode_DeltaHandler_WriteBack_Each_Item()
        {
            var (dispatcher, _) = CreateDispatcher<IUserService, UserServiceImpl>();
            var stubTransport = new StubTransport(req => dispatcher.InvokeAsync(req).GetAwaiter().GetResult(), dispatcher);

            var provider = BuildProvider();
            var proxy = CreateProxy<IUserService>(provider, stubTransport);

            var users = new List<User>
            {
                new User { Name = "a", Id = 0, CreatedAt = default },
                new User { Name = "b", Id = 0, CreatedAt = default },
            };

            await proxy.CreateBatchDeltaAsync(users);

            // DeltaHandler 回写 Id 和 CreatedAt
            Assert.Equal(200, users[0].Id);
            Assert.Equal(new DateTime(2026, 8, 1), users[0].CreatedAt);
            Assert.Equal("a", users[0].Name);
            Assert.Equal(201, users[1].Id);
            Assert.Equal(new DateTime(2026, 8, 2), users[1].CreatedAt);
            Assert.Equal("b", users[1].Name);
        }
    }
}
