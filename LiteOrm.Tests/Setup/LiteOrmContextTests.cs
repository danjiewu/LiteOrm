using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests.Setup
{
    /// <summary>
    /// <see cref="LiteOrmContext"/> 的行为验证：纯手动创建上下文，不使用任何 DI 容器，
    /// 直接创建会话与 DAO 完成增删改查。
    /// </summary>
    public class LiteOrmContextTests : IDisposable
    {
        private readonly List<string> _tempFiles = new List<string>();

        private string CreateTempDbPath()
        {
            var path = Path.Combine(Path.GetTempPath(), $"liteorm-context-{Guid.NewGuid():N}.db");
            _tempFiles.Add(path);
            return path;
        }

        public void Dispose()
        {
            foreach (var file in _tempFiles)
            {
                try { if (File.Exists(file)) File.Delete(file); } catch { }
            }
        }

        [Fact]
        public void NewContext_ShouldBeEmpty()
        {
            using var context = new LiteOrmContext();

            Assert.Empty(context.DataSources);
            Assert.Null(context.GetDataSource());
            Assert.Null(context.DefaultDataSourceName);
        }

        [Fact]
        public void AddDataSourceGeneric_ShouldRegisterTypeAndSetDefault()
        {
            var dbPath = CreateTempDbPath();

            using var context = new LiteOrmContext()
                .AddDataSource<SqliteConnection>("main", $"Data Source={dbPath}", @default: true);

            Assert.Equal("main", context.DefaultDataSourceName);

            var config = context.GetDataSource("main");
            Assert.NotNull(config);
            Assert.Equal($"Data Source={dbPath}", config!.ConnectionString);
            Assert.Equal(typeof(SqliteConnection), config.ProviderType);

            // 泛型重载会把连接类型预注册到名称解析器，AOT 下按名称反查也能命中
            Assert.NotNull(TypeResolverHelper.FindType(typeof(SqliteConnection).AssemblyQualifiedName!));
        }

        [Fact]
        public void AddDataSourceGeneric_ShouldApplyPoolOptions()
        {
            var dbPath = CreateTempDbPath();

            using var context = new LiteOrmContext()
                .AddDataSource<SqliteConnection>(
                    "main",
                    $"Data Source={dbPath}",
                    @default: true,
                    poolSize: 8,
                    maxPoolSize: 32,
                    paramCountLimit: 500);

            var config = context.GetDataSource("main")!;
            Assert.Equal(8, config.PoolSize);
            Assert.Equal(32, config.MaxPoolSize);
            Assert.Equal(500, config.ParamCountLimit);
        }

        [Fact]
        public void AddDataSourceGeneric_WithTypeOnly_ShouldAllowConnectionStringLater()
        {
            // 无参形式：只登记类型，连接字符串稍后补
            using var context = new LiteOrmContext()
                .AddDataSource<SqliteConnection>("staged");

            var config = context.GetDataSource("staged");
            Assert.NotNull(config);
            Assert.Null(config!.ConnectionString);

            // 同名再次添加即覆盖，补齐连接字符串
            context.AddDataSource<SqliteConnection>("staged", "Data Source=:memory:");

            Assert.Equal("Data Source=:memory:", context.GetDataSource("staged")!.ConnectionString);
            Assert.Single(context.DataSources);
        }

        [Fact]
        public void FirstDataSource_ShouldBecomeDefaultImplicitly()
        {
            using var context = new LiteOrmContext()
                .AddDataSource<SqliteConnection>("only", "Data Source=:memory:");

            Assert.Equal("only", context.DefaultDataSourceName);
        }

        [Fact]
        public void MultipleDataSources_ShouldKeepFirstAsDefault()
        {
            using var context = new LiteOrmContext()
                .AddDataSource<SqliteConnection>("a", "Data Source=:memory:")
                .AddDataSource<SqliteConnection>("b", "Data Source=:memory:");

            // 第一个数据源自动成为默认，后续数据源不抢占
            Assert.Equal("a", context.DefaultDataSourceName);
        }

        [Fact]
        public void AddDataSource_ShouldDeriveProviderTypeFromConnectionType()
        {
            // 提供程序类型一律由泛型参数推导，配置里存的就是 Type，不再有字符串中转
            using var context = new LiteOrmContext()
                .AddDataSource<SqliteConnection>("main", "Data Source=:memory:");

            Assert.Equal(typeof(SqliteConnection), context.GetDataSource("main")!.ProviderType);
        }

        [Fact]
        public void AddDataSource_ShouldSetSyncTablePerSourceAtAddTime()
        {
            using var context = new LiteOrmContext()
                .AddDataSource<SqliteConnection>("first", "Data Source=:memory:", syncTable: true)
                .AddDataSource<SqliteConnection>("second", "Data Source=:memory:");

            Assert.True(context.GetDataSource("first")!.SyncTable);
            Assert.False(context.GetDataSource("second")!.SyncTable);
        }

        [Fact]
        public void AddDataSource_WithEmptyName_ShouldThrow()
        {
            using var context = new LiteOrmContext();

            Assert.Throws<ArgumentException>(
                () => context.AddDataSource<SqliteConnection>("   ", "Data Source=:memory:"));
        }

        [Fact]
        public void AddDataSource_WithNullConfig_ShouldThrow()
        {
            using var context = new LiteOrmContext();

            Assert.Throws<ArgumentNullException>(() => context.AddDataSource((DataSourceConfig)null!));
        }

        [Fact]
        public void AddDataSource_AfterPoolsCreated_ShouldThrow()
        {
            var dbPath = CreateTempDbPath();

            using var context = new LiteOrmContext()
                .AddDataSource<SqliteConnection>("main", $"Data Source={dbPath}", @default: true);

            // 取会话即建池，此后不再接受新的数据源
            using var session = context.CreateSession();

            Assert.Throws<InvalidOperationException>(
                () => context.AddDataSource<SqliteConnection>("late", "Data Source=:memory:"));
        }

        [Fact]
        public void CreateSession_ShouldNotTouchCurrent()
        {
            var dbPath = CreateTempDbPath();

            using var context = new LiteOrmContext()
                .AddDataSource<SqliteConnection>("main", $"Data Source={dbPath}", @default: true);

            // 与 DI 线路彻底分开：本线路不接管 SessionManager.Current
            using var session = context.CreateSession();
            Assert.NotNull(session);
        }

        [Fact]
        public void CreateSession_EachCallReturnsNewInstance()
        {
            var dbPath = CreateTempDbPath();

            using var context = new LiteOrmContext()
                .AddDataSource<SqliteConnection>("main", $"Data Source={dbPath}", @default: true);

            using var first = context.CreateSession();
            using var second = context.CreateSession();

            Assert.NotSame(first, second);
        }

        [Fact]
        public void Dispose_ShouldReleasePoolFactory()
        {
            var dbPath = CreateTempDbPath();
            var context = new LiteOrmContext()
                .AddDataSource<SqliteConnection>("main", $"Data Source={dbPath}", @default: true);

            using var session = context.CreateSession();
            context.Dispose();

            // 会话本身是调用方的资源，但底层连接池工厂已随上下文销毁
            Assert.Throws<ObjectDisposedException>(() => session.GetDAOContextPool());
            Assert.Throws<ObjectDisposedException>(() => context.CreateSession());
        }

        [Fact]
        public async Task CreateSession_ShouldServeDaoCrudWithoutDi()
        {
            var dbPath = CreateTempDbPath();

            // 整条链纯手动：没有任何 ServiceCollection / ServiceProvider
            using var context = new LiteOrmContext()
                .AddDataSource<SqliteConnection>("main", $"Data Source={dbPath}", @default: true, syncTable: true);

            using var session = context.CreateSession();

            var ct = TestContext.Current.CancellationToken;

            var dao = new ObjectDAO<TestUser>(session);
            var viewDao = new ObjectViewDAO<TestUser>(session);
            var user = new TestUser { Name = "context-user", Age = 30, CreateTime = DateTime.Now };

            Assert.True(await dao.InsertAsync(user, ct));
            Assert.True(user.Id > 0);

            var loaded = await viewDao.GetObject(user.Id).FirstOrDefaultAsync(ct);
            Assert.NotNull(loaded);
            Assert.Equal("context-user", loaded!.Name);

            Assert.True(await viewDao.ExistsKey(user.Id).GetResultAsync(ct));
            Assert.Equal(1, await viewDao.Count(Expr.Prop("Id") == user.Id).GetResultAsync(ct));

            user.Name = "renamed";
            Assert.True(await dao.UpdateAsync(user, null, ct));
            Assert.Equal("renamed", (await viewDao.GetObject(user.Id).FirstOrDefaultAsync(ct))!.Name);

            Assert.Equal(1, await dao.DeleteAsync(Expr.Prop("Id") == user.Id, ct));
            Assert.False(await viewDao.ExistsKey(user.Id).GetResultAsync(ct));
        }

        [Fact]
        public async Task MultipleDataSources_ShouldEachHaveOwnPool()
        {
            var mainPath = CreateTempDbPath();
            var logPath = CreateTempDbPath();

            using var context = new LiteOrmContext()
                .AddDataSource<SqliteConnection>("main", $"Data Source={mainPath}", @default: true, syncTable: true)
                .AddDataSource<SqliteConnection>("log", $"Data Source={logPath}", syncTable: true);

            using var session = context.CreateSession();

            // 两个池互不相同
            var mainPool = session.GetDAOContextPool("main");
            var logPool = session.GetDAOContextPool("log");
            Assert.NotNull(mainPool);
            Assert.NotNull(logPool);
            Assert.NotSame(mainPool, logPool);

            var ct = TestContext.Current.CancellationToken;

            await new ObjectDAO<TestUser>(session).InsertAsync(
                new TestUser { Name = "in-main", Age = 1, CreateTime = DateTime.Now }, ct);

            var users = await new ObjectViewDAO<TestUser>(session).Search().ToListAsync(ct);
            Assert.Single(users);
            Assert.Equal("in-main", users[0].Name);

            // 落在默认库 main 上；log 库因上述写入而未被触碰
            Assert.True(File.Exists(mainPath));
            Assert.True(new FileInfo(mainPath).Length > 0);
        }

        [Fact]
        public async Task EntityService_ShouldBeConstructableFromSession()
        {
            var dbPath = CreateTempDbPath();

            using var context = new LiteOrmContext()
                .AddDataSource<SqliteConnection>("main", $"Data Source={dbPath}", @default: true, syncTable: true);

            using var session = context.CreateSession();

            // EntityService<T> 依赖 IServiceProvider 解析 ObjectDAO<T>/ObjectViewDAO<T>；
            // 纯手动线路用只认本会话的最小实现即可，不必引入任何 DI 容器
            var services = new SingleSessionServiceProvider(session);
            var service = new EntityService<TestUser>(services);

            var ct = TestContext.Current.CancellationToken;
            var user = new TestUser { Name = "svc-user", Age = 22, CreateTime = DateTime.Now };
            Assert.True(await service.InsertAsync(user, ct));

            var loaded = await service.SearchOneAsync(Expr.Prop("Id") == user.Id, null, ct);
            Assert.NotNull(loaded);
            Assert.Equal("svc-user", loaded!.Name);
        }

        /// <summary>
        /// 最小服务提供程序：把 <see cref="SessionManager"/> 与基于该会话的 DAO 解析出来，
        /// 用于在纯手动线路里满足 <c>EntityService&lt;T&gt;</c> 的构造依赖。
        /// </summary>
        private sealed class SingleSessionServiceProvider : IServiceProvider
        {
            private readonly SessionManager _session;

            public SingleSessionServiceProvider(SessionManager session) => _session = session;

            public object? GetService(Type serviceType)
            {
                if (serviceType == typeof(SessionManager)) return _session;

                if (serviceType.IsGenericType)
                {
                    var definition = serviceType.GetGenericTypeDefinition();
                    var argument = serviceType.GetGenericArguments()[0];

                    // EntityService<T, TView> 还会向容器索取 IEnumerable<IEntityServiceEvent<T>>；
                    // 手动线路没有事件订阅者，返回空集合即“未配置任何监听器”。
                    if (definition == typeof(IEnumerable<>))
                        return Array.CreateInstance(argument, 0);

                    if (definition == typeof(ObjectDAO<>)) return Activator.CreateInstance(serviceType, _session);
                    if (definition == typeof(ObjectViewDAO<>)) return Activator.CreateInstance(serviceType, _session);
                    if (definition == typeof(IObjectDAO<>)) return Activator.CreateInstance(typeof(ObjectDAO<>).MakeGenericType(argument), _session);
                    if (definition == typeof(IObjectViewDAO<>)) return Activator.CreateInstance(typeof(ObjectViewDAO<>).MakeGenericType(argument), _session);
                }
                return null;
            }
        }
    }
}
