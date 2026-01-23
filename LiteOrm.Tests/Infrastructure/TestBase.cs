using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System;

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
                        ["LiteOrm:ConnectionStrings:0:Name"] = "DefaultConnection",
                        ["LiteOrm:ConnectionStrings:0:ConnectionString"] = $"Data Source={_dbFile}",
                        ["LiteOrm:ConnectionStrings:0:Provider"] = "Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite",
                        ["LiteOrm:ConnectionStrings:0:SyncTable"] = "true"
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
