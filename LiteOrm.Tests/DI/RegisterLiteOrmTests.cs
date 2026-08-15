using Autofac.Core.Registration;
using LiteOrm.Common;
using LiteOrm.DependencyInjection;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// 验证 RegisterLiteOrm（Autofac 集成）选项与自动注册（程序集扫描）行为。
    /// 纯 DI 测试，无需数据库集合。
    /// </summary>
    public class RegisterLiteOrmTests
    {
        private static IHost BuildHost(Action<LiteOrm.DependencyInjection.LiteOrmServiceExtensions.LiteOrmOptions>? configureOptions = null)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LiteOrm:Default"] = "SQLite",
                    ["LiteOrm:DataSources:0:Name"] = "SQLite",
                    ["LiteOrm:DataSources:0:ConnectionString"] = "Data Source=demo.db",
                    ["LiteOrm:DataSources:0:Provider"] = "Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite"
                })
                .Build();

            return Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((_, config) => config.AddConfiguration(configuration))
                .RegisterLiteOrm(configureOptions)
                .Build();
        }

        [Fact]
        public void RegisterLiteOrm_AutoRegisterServices_ShouldResolveCustomServices()
        {
            using var host = BuildHost();

            using var scope = host.Services.CreateScope();
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITestUserService>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITestDepartmentService>());
        }

        [Fact]
        public void RegisterLiteOrm_AutoRegisterDisabled_ShouldNotResolveCustomServices()
        {
            using var host = BuildHost(options => options.AutoRegisterServices = false);

            using var scope = host.Services.CreateScope();
            Assert.Throws<ComponentNotRegisteredException>(() =>
                scope.ServiceProvider.GetRequiredService<ITestUserService>());
            Assert.Throws<ComponentNotRegisteredException>(() =>
                scope.ServiceProvider.GetRequiredService<ITestDepartmentService>());
        }

        [Fact]
        public void RegisterLiteOrm_AutoRegisterDisabled_StillResolvesCoreServices()
        {
            using var host = BuildHost(options => options.AutoRegisterServices = false);

            using var scope = host.Services.CreateScope();
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<ObjectDAO<TestUser>>());
        }
    }
}
