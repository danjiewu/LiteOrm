using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// 验证 AddLiteOrm 选项与自动注册（LiteOrm.Generators 源生成器驱动）行为。
    /// 纯 DI 测试，无需数据库集合。
    /// </summary>
    public class AddLiteOrmTests
    {
        private static IServiceCollection CreateServices()
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LiteOrm:Default"] = "SQLite",
                    ["LiteOrm:DataSources:0:Name"] = "SQLite",
                    ["LiteOrm:DataSources:0:ConnectionString"] = "Data Source=demo.db",
                    ["LiteOrm:DataSources:0:Provider"] = "Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite"
                })
                .Build();
            services.AddSingleton<IConfiguration>(configuration);
            return services;
        }

        [Fact]
        public void AddLiteOrm_ShouldRegisterCoreServices()
        {
            var services = CreateServices().AddLiteOrm();
            using var provider = services.BuildServiceProvider();

            using var scope = provider.CreateScope();
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<ObjectDAO<TestUser>>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IObjectDAO<TestUser>>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IObjectViewDAO<TestUser>>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<EntityService<TestUser>>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEntityService<TestUser>>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEntityViewService<TestUser>>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>());
        }

        [Fact]
        public void AddLiteOrm_AutoRegisterServices_ShouldResolveCustomServices()
        {
            var services = CreateServices().AddLiteOrm();
            using var provider = services.BuildServiceProvider();

            using var scope = provider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<ITestUserService>();
            Assert.IsType<TestUserService>(userService);
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<TestUserService>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITestDepartmentService>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<TestDepartmentService>());
        }

        [Fact]
        public void AddLiteOrm_AutoRegister_InheritedFromBaseClass_ShouldResolve()
        {
            var services = CreateServices().AddLiteOrm();
            using var provider = services.BuildServiceProvider();

            var inherited = provider.GetRequiredService<InheritedAutoRegisteredService>();
            Assert.IsType<InheritedAutoRegisteredService>(inherited);
            Assert.Same(inherited, provider.GetRequiredService<InheritedAutoRegisteredService>());
        }

        [Fact]
        public void AddLiteOrm_AutoRegisterDisabled_ShouldNotResolveCustomServices()
        {
            var services = CreateServices()
                .AddLiteOrm(options => options.AutoRegisterServices = false);
            using var provider = services.BuildServiceProvider();

            using var scope = provider.CreateScope();
            Assert.Throws<InvalidOperationException>(() =>
                scope.ServiceProvider.GetRequiredService<ITestUserService>());
        }

        private interface ICustomMarkerService { }

        [Fact]
        public void AddLiteOrm_ConfigureServicesHook_ShouldRun()
        {
            var services = CreateServices()
                .AddLiteOrm(options =>
                {
                    options.ConfigureServices = sc => sc.AddScoped<ICustomMarkerService>(_ => new CustomMarkerService());
                });
            using var provider = services.BuildServiceProvider();

            using var scope = provider.CreateScope();
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICustomMarkerService>());
        }

        private sealed class CustomMarkerService : ICustomMarkerService { }

        [Fact]
        public void AddLiteOrm_OptionsCallback_ShouldThrowOnFailure()
        {
            var services = CreateServices();
            Assert.Throws<InvalidOperationException>(() =>
                services.AddLiteOrm((Action<LiteOrmOptions>?)(_ => throw new InvalidOperationException("boom"))));
        }

        [Fact]
        public void AddLiteOrm_Factory_Overrides_Parameters_From_DI()
        {
            // 不通过 AddLiteOrm(configure) 配置，而是用注入工厂从 DI（含 IConfiguration）构造选项。
            var services = CreateServices()
                .AddLiteOrm(sp =>
                {
                    Assert.NotNull(sp.GetRequiredService<IConfiguration>());
                    return new LiteOrmOptions { AutoRegisterServices = false };
                })
                .AddLiteOrm();
            using var provider = services.BuildServiceProvider();

            // 工厂构造的选项可通过 DI 解析到。
            var resolved = provider.GetRequiredService<LiteOrmOptions>();
            Assert.False(resolved.AutoRegisterServices);
        }
    }
}
