using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace LiteOrm.Tests.Infrastructure
{
    public abstract class TestBase : IDisposable
    {
        protected IHost Host { get; }
        protected IServiceProvider ServiceProvider => Host.Services;
        private readonly string _dbFile;

        protected TestBase()
        {
            _dbFile = $"{Guid.NewGuid():N}.db";

            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["LiteOrm:Default"] = "DefaultConnection",
                        ["LiteOrm:DataSources:0:Name"] = "DefaultConnection",
                        ["LiteOrm:DataSources:0:ConnectionString"] = $"Data Source={_dbFile}",
                        ["LiteOrm:DataSources:0:Provider"] = "Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite",
                        ["LiteOrm:DataSources:0:SyncTable"] = "true"
                    });
                })
                .RegisterLiteOrm()
                .Build();

            Host.Start();
        }

        public void Dispose()
        {
            Host.StopAsync().Wait();
            Host.Dispose();

            // Best effort cleanup
            try
            {
                if (File.Exists(_dbFile))
                {
                    // SQLite might hold the file for a bit
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    File.Delete(_dbFile);
                }
            }
            catch { }
        }
    }
}
