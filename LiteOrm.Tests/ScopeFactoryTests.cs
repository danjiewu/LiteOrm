using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using Xunit;

namespace LiteOrm.Tests
{
    public class ScopeFactoryTests
    {
        [Fact]
        public async Task RegisterLiteOrm_ShouldPatchInnerScopeFactory_ForChildScopes()
        {
            using var host = await new HostBuilder()
                .ConfigureAppConfiguration(config =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["LiteOrm:Default"] = "SQLite",
                        ["LiteOrm:DataSources:0:Name"] = "SQLite",
                        ["LiteOrm:DataSources:0:ConnectionString"] = "Data Source=:memory:",
                        ["LiteOrm:DataSources:0:Provider"] = "Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite"
                    });
                })
                .RegisterLiteOrm()
                .StartAsync();

            var innerProviderField = host.Services.GetType().GetField("_innerProvider", BindingFlags.Instance | BindingFlags.NonPublic);
            var innerProvider = Assert.IsAssignableFrom<IServiceProvider>(innerProviderField?.GetValue(host.Services));

            var scopeFactory = innerProvider.GetRequiredService<IServiceScopeFactory>();
            Assert.Equal("LiteOrm.LiteOrmScopeFactory", scopeFactory.GetType().FullName);

            using var scope = scopeFactory.CreateScope();
            var scoped = scope.ServiceProvider.GetRequiredService<SessionManager>();

            Assert.Equal("LiteOrm.LiteOrmServiceProvider", scope.ServiceProvider.GetType().FullName);
            Assert.Same(scoped, SessionManager.Current);
        }
    }
}
