using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SQLitePCL;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace LiteOrm.Tests.Infrastructure
{
    /// <summary>
    /// 定义数据库测试集合，确保所有数据库测试类串行执行，避免并发清空数据导致测试间干扰。
    /// </summary>
    [CollectionDefinition("Database")]
    public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
    {
    }

    /// <summary>
    /// 数据库测试集合的共享固件，负责初始化 Host 和首次清空测试表。
    /// 同一集合内所有测试类共享此实例。
    /// </summary>
    public class DatabaseFixture : IDisposable
    {
        public IHost Host { get; }
        public IServiceProvider ServiceProvider => Host.Services;

        public DatabaseFixture()
        {
            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                })
                .RegisterLiteOrm()
                .Build();
            var pool = ServiceProvider.GetRequiredService<DAOContextPoolFactory>().GetPool("SQLite");
            pool.OnContextCreated += DatabaseFixture_OnContextCreated;
            pool.ClearPool(); // 确保使用新的连接，触发 OnContextCreated 事件注册函数

            Host.Start();
        }

        private void DatabaseFixture_OnContextCreated(DAOContext context)
        {
            ((SqliteConnection)context.DbConnection).CreateFunction("REGEXP_LIKE", (string input, string pattern) =>
            {
                if (input == null || pattern == null)
                    return false;
                try
                {
                    return System.Text.RegularExpressions.Regex.IsMatch(input, pattern);
                }
                catch
                {
                    return false;
                }
            });
        }
        /// <summary>
        /// 清空所有测试表数据，由每个测试类在构造时调用。
        /// </summary>
        public void CleanupTestTables()
        {
            var poolFactory = ServiceProvider.GetRequiredService<DAOContextPoolFactory>();
            var sqlBuilderFactory = ServiceProvider.GetRequiredService<SqlBuilderFactory>();
            var pool = poolFactory.GetPool();
            var sqlBuilder = sqlBuilderFactory.GetSqlBuilder(pool.ProviderType);
            var context = pool.PeekContext();
            try
            {
                // 先删除有外键引用的子表数据，再删除被引用的父表数据
                string[] tables = ["TestUsers", "TestDepartments"];
                using var cmd = context.CreateCommand();
                foreach (var table in tables)
                {                   
                    cmd.CommandText = $"DELETE FROM {sqlBuilder.ToSqlName(table)}";
                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                pool.ReturnContext(context);
            }
        }

        public void Dispose()
        {
            Host.StopAsync().Wait();
            Host.Dispose();
        }
    }

    /// <summary>
    /// 数据库测试基类。使用 [Collection("Database")] 标记的测试类共享同一个 DatabaseFixture，
    /// 保证串行执行，并在每个测试类开始前清空数据。
    /// </summary>
    public abstract class TestBase : IDisposable
    {
        protected IHost Host => Fixture.Host;
        protected IServiceProvider ServiceProvider { get; }
        protected DatabaseFixture Fixture { get; }
        private readonly IServiceScope _scope;

        protected TestBase(DatabaseFixture fixture)
        {            
            _scope = fixture.ServiceProvider.CreateScope();
            ServiceProvider = _scope.ServiceProvider;
            Fixture = fixture;
            Fixture.CleanupTestTables();
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }
}
