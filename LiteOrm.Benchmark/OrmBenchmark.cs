using BenchmarkDotNet.Attributes;
using Dapper;
using FreeSql;
using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySqlConnector;
using SqlSugar;


namespace LiteOrm.Benchmark
{
    [MemoryDiagnoser]
    [MediumRunJob]
    public class OrmBenchmark
    {
        private IHost _host;
        private IServiceProvider _serviceProvider => _host.Services;
        private readonly Random _random = new Random();

        private string? _connectionString;

        [Params(100, 1000, 5000)]
        public int BatchCount { get; set; }
        [GlobalSetup]
        public void Setup()
        {
            try
            {
                _host = Host.CreateDefaultBuilder()
                    .ConfigureAppConfiguration((hostingContext, config) =>
                    {
                        config.SetBasePath(AppContext.BaseDirectory);
                        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                        config.AddEnvironmentVariables();
                    })
                    .RegisterLiteOrm()
                    .ConfigureServices((Action<HostBuilderContext, IServiceCollection>)((context, services) =>
                    {
                        _connectionString = context.Configuration.GetConnectionString("DefaultConnection");

                        if (string.IsNullOrEmpty(_connectionString))
                        {
                            throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");
                        }

                        // 1. EF Core 配置
                        services.AddDbContext<BenchmarkDbContext>(options =>
                            options.UseMySql(_connectionString, ServerVersion.AutoDetect(_connectionString))
                                   .LogTo(_ => { }, Microsoft.Extensions.Logging.LogLevel.None));

                        // 2. SqlSugar 配置
                        services.AddScoped<ISqlSugarClient>(s =>
                        {
                            return new SqlSugarClient(new ConnectionConfig()
                            {
                                ConnectionString = _connectionString,
                                DbType = SqlSugar.DbType.MySql,
                                IsAutoCloseConnection = true,
                            });
                        });

                        // 3. FreeSql 配置
                        services.AddSingleton(s =>
                        {
                            return new FreeSqlBuilder()
                                .UseConnectionString(FreeSql.DataType.MySql, _connectionString)
                                .UseAutoSyncStructure(true)
                                .Build();
                        });
                    }))
                    .Build();

                _host.Start();
                Console.WriteLine("Host started.");

                // 初始化数据库结构和种子数据
                using (var scope = _serviceProvider.CreateScope())
                {
                    // 1. 先清空/重建表结构
                    Console.WriteLine("Step 1: Cleaning and rebuilding tables...");
                    var sugar = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
                    sugar.DbMaintenance.DropTable<BenchmarkLog>();
                    sugar.DbMaintenance.DropTable<BenchmarkUser>();
                    sugar.CodeFirst.InitTables<BenchmarkUser, BenchmarkLog>();

                    var fsql = scope.ServiceProvider.GetRequiredService<IFreeSql>();
                    fsql.CodeFirst.SyncStructure(typeof(BenchmarkUser), typeof(BenchmarkLog));

                    var efCtx = scope.ServiceProvider.GetRequiredService<BenchmarkDbContext>();
                    efCtx.Database.EnsureDeleted();
                    efCtx.Database.EnsureCreated();
                    Console.WriteLine("Tables cleaning and rebuilding completed.");

                    // 2. 插入种子数据
                    Console.WriteLine("Step 2: Seeding data...");

                    // EF Core 种子
                    Console.WriteLine("Seeding EF Core...");
                    var efUsers = Enumerable.Range(1, BatchCount).Select(i => new BenchmarkUser { Name = $"User{i}", Age = 20 + (i % 50), Email = $"user{i}@example.com", CreateTime = DateTime.Now }).ToList();
                    efCtx.BenchmarkUsers.AddRange(efUsers);
                    efCtx.SaveChanges();
                    var efLogs = efUsers.Select(u => new BenchmarkLog { UserId = u.Id, Message = $"Log for {u.Name}", LogTime = DateTime.Now }).ToList();
                    efCtx.BenchmarkLogs.AddRange(efLogs);
                    efCtx.SaveChanges();

                    // SqlSugar 种子
                    Console.WriteLine("Seeding SqlSugar...");
                    var sugarUsers = Enumerable.Range(1, BatchCount).Select(i => new BenchmarkUser { Name = $"User{i}", Age = 20 + (i % 50), Email = $"user{i}@example.com", CreateTime = DateTime.Now }).ToList();
                    sugar.Insertable(sugarUsers).ExecuteCommand();
                    var sugarLogs = sugar.Queryable<BenchmarkUser>().ToList().Select(u => new BenchmarkLog { UserId = u.Id, Message = $"Log for {u.Name}", LogTime = DateTime.Now }).ToList();
                    sugar.Insertable(sugarLogs).ExecuteCommand();

                    // FreeSql 种子
                    Console.WriteLine("Seeding FreeSql...");
                    var fsqlUsers = Enumerable.Range(1, BatchCount).Select(i => new BenchmarkUser { Name = $"User{i}", Age = 20 + (i % 50), Email = $"user{i}@example.com", CreateTime = DateTime.Now }).ToList();
                    fsql.Insert(fsqlUsers).ExecuteAffrows();
                    var fsqlLogs = fsql.Select<BenchmarkUser>().ToList().Select(u => new BenchmarkLog { UserId = u.Id, Message = $"Log for {u.Name}", LogTime = DateTime.Now }).ToList();
                    fsql.Insert(fsqlLogs).ExecuteAffrows();

                    // LiteOrm 种子
                    Console.WriteLine("Seeding LiteOrm...");
                    var userService = scope.ServiceProvider.GetRequiredService<IEntityServiceAsync<BenchmarkUser>>();
                    var liteUsers = Enumerable.Range(1, BatchCount).Select(i => new BenchmarkUser { Name = $"User{i}", Age = 20 + (i % 50), Email = $"user{i}@example.com", CreateTime = DateTime.Now }).ToList();
                    userService.BatchInsertAsync(liteUsers).GetAwaiter().GetResult();

                    var userViewService = scope.ServiceProvider.GetRequiredService<IEntityViewServiceAsync<BenchmarkUser>>();
                    var logService = scope.ServiceProvider.GetRequiredService<IEntityServiceAsync<BenchmarkLog>>();
                    var liteLogs = userViewService.SearchAsync(null).GetAwaiter().GetResult().Select(u => new BenchmarkLog { UserId = u.Id, Message = $"Log for {u.Name}", LogTime = DateTime.Now }).ToList();
                    logService.BatchInsertAsync(liteLogs).GetAwaiter().GetResult();

                    Console.WriteLine("Step 2: Seeding data completed.");
                }
                Console.WriteLine("GlobalSetup completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Setup failed: " + ex.ToString());
                throw;
            }
        }



        [GlobalCleanup]
        public void Cleanup()
        {
            _host?.Dispose();
        }

        #region Async Insert
        [Benchmark]
        public async Task EFCore_Insert_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<BenchmarkDbContext>();
                var users = Enumerable.Range(1, BatchCount).Select(i => new BenchmarkUser { Name = "EF", Age = 25, Email = "ef@test.com", CreateTime = DateTime.Now }).ToList();
                await db.BenchmarkUsers.AddRangeAsync(users);
                await db.SaveChangesAsync();
            }
        }

        [Benchmark]
        public async Task SqlSugar_Insert_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var sugar = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
                var users = Enumerable.Range(1, BatchCount).Select(i => new BenchmarkUser { Name = "Sugar", Age = 25, Email = "sugar@test.com", CreateTime = DateTime.Now }).ToList();
                await sugar.Insertable(users).ExecuteCommandAsync();
            }
        }

        [Benchmark]
        public async Task LiteOrm_Insert_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IEntityServiceAsync<BenchmarkUser>>();
                var users = Enumerable.Range(1, BatchCount).Select(i => new BenchmarkUser { Name = "Lite", Age = 25, Email = "lite@test.com", CreateTime = DateTime.Now }).ToList();
                await service.BatchInsertAsync(users);
            }
        }

        [Benchmark]
        public async Task Dapper_Insert_Async()
        {
            using (var conn = new MySqlConnection(_connectionString!))
            {
                await conn.OpenAsync();
                using (var trans = await conn.BeginTransactionAsync())
                {
                    var users = Enumerable.Range(1, BatchCount).Select(i => new BenchmarkUser { Name = "Dapper", Age = 25, Email = "dapper@test.com", CreateTime = DateTime.Now }).ToList();
                    await conn.ExecuteAsync("INSERT INTO BenchmarkUser (Name, Age, Email, CreateTime) VALUES (@Name, @Age, @Email, @CreateTime)", users, trans);
                    await trans.CommitAsync();
                }
            }
        }

        [Benchmark]
        public async Task FreeSql_Insert_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var fsql = scope.ServiceProvider.GetRequiredService<IFreeSql>();
                var users = Enumerable.Range(1, BatchCount).Select(i => new BenchmarkUser { Name = "FreeSql", Age = 25, Email = "freesql@test.com", CreateTime = DateTime.Now }).ToList();
                await fsql.Insert(users).ExecuteAffrowsAsync();
            }
        }


        #endregion

        #region Async Update
        [Benchmark]
        public async Task EFCore_Update_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<BenchmarkDbContext>();
                var users = await db.BenchmarkUsers.Take(BatchCount).ToListAsync();
                foreach (var u in users)
                {
                    u.Name = "EFCore" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    u.Age = _random.Next(20, 60);
                    u.Email = Guid.NewGuid().ToString("N").Substring(0, 10) + "@test.com";
                }
                await db.SaveChangesAsync();
            }
        }

        [Benchmark]
        public async Task SqlSugar_Update_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var sugar = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
                var users = await sugar.Queryable<BenchmarkUser>().Take(BatchCount).ToListAsync();
                foreach (var u in users)
                {
                    u.Name = "SqlSugar" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    u.Age = _random.Next(20, 60);
                    u.Email = Guid.NewGuid().ToString("N").Substring(0, 10) + "@test.com";
                }
                await sugar.Updateable(users).ExecuteCommandAsync();
            }
        }

        [Benchmark]
        public async Task LiteOrm_Update_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var viewService = scope.ServiceProvider.GetRequiredService<IEntityViewServiceAsync<BenchmarkUser>>();
                var updateService = scope.ServiceProvider.GetRequiredService<IEntityServiceAsync<BenchmarkUser>>();
                var users = await viewService.SearchAsync(new SectionExpr(0, BatchCount));
                foreach (var u in users)
                {
                    u.Name = "LiteOrm" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    u.Age = _random.Next(20, 60);
                    u.Email = Guid.NewGuid().ToString("N").Substring(0, 10) + "@test.com";
                }
                await updateService.BatchUpdateAsync(users);
            }
        }



        [Benchmark]
        public async Task Dapper_Update_Async()
        {
            using (var conn = new MySqlConnection(_connectionString!))
            {
                await conn.OpenAsync();
                var users = (await conn.QueryAsync<BenchmarkUser>($"SELECT * FROM BenchmarkUser LIMIT {BatchCount}")).ToList();
                using (var trans = await conn.BeginTransactionAsync())
                {
                    foreach (var u in users)
                    {
                        u.Name = "Dapper" + Guid.NewGuid().ToString("N").Substring(0, 8);
                        u.Age = _random.Next(20, 60);
                        u.Email = Guid.NewGuid().ToString("N").Substring(0, 10) + "@test.com";
                    }
                    await conn.ExecuteAsync("UPDATE BenchmarkUser SET Name = @Name, Age = @Age, Email = @Email WHERE Id = @Id", users, trans);
                    await trans.CommitAsync();
                }
            }
        }

        [Benchmark]
        public async Task FreeSql_Update_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var fsql = scope.ServiceProvider.GetRequiredService<IFreeSql>();
                var users = await fsql.Select<BenchmarkUser>().Limit(BatchCount).ToListAsync();
                foreach (var u in users)
                {
                    u.Name = "FreeSql" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    u.Age = _random.Next(20, 60);
                    u.Email = Guid.NewGuid().ToString("N").Substring(0, 10) + "@test.com";
                }
                await fsql.Update<BenchmarkUser>().SetSource(users).ExecuteAffrowsAsync();
            }
        }
        #endregion

        #region Async UpdateOrInsert
        [Benchmark]
        public async Task EFCore_UpdateOrInsert_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<BenchmarkDbContext>();
                var existingUsers = await db.BenchmarkUsers.Take(BatchCount / 2).ToListAsync();
                var localRandom = new Random();
                foreach (var u in existingUsers)
                {
                    u.Name = "EF_Upsert_U";
                    u.Age = localRandom.Next(20, 60);
                }

                string tag = Guid.NewGuid().ToString("N").Substring(0, 6);
                var newUsers = Enumerable.Range(1, BatchCount / 2).Select(i => new BenchmarkUser
                {
                    Name = "EF_Upsert_I",
                    Age = localRandom.Next(20, 60),
                    Email = $"ef_upsert_{tag}_{i}@test.com",
                    CreateTime = DateTime.Now
                }).ToList();

                await db.BenchmarkUsers.AddRangeAsync(newUsers);
                await db.SaveChangesAsync();
            }
        }

        [Benchmark]
        public async Task SqlSugar_UpdateOrInsert_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var sugar = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
                var existingUsers = await sugar.Queryable<BenchmarkUser>().Take(BatchCount / 2).ToListAsync();
                foreach (var u in existingUsers) { u.Name = "Sugar_Upsert_U"; u.Age = _random.Next(20, 60); }
                var newUsers = Enumerable.Range(1, BatchCount / 2).Select(i => new BenchmarkUser { Name = "Sugar_Upsert_I", Age = _random.Next(20, 60), Email = $"sugar_upsert{i}@test.com", CreateTime = DateTime.Now }).ToList();
                var all = existingUsers.Concat(newUsers).ToList();
                await sugar.Storageable(all).ExecuteCommandAsync();
            }
        }

        [Benchmark]
        public async Task LiteOrm_UpdateOrInsert_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var viewService = scope.ServiceProvider.GetRequiredService<IEntityViewServiceAsync<BenchmarkUser>>();
                var service = scope.ServiceProvider.GetRequiredService<IEntityServiceAsync<BenchmarkUser>>();
                var existingUsers = await viewService.SearchAsync(new SectionExpr(0, BatchCount / 2));
                foreach (var u in existingUsers) { u.Name = "Lite_Upsert_U"; u.Age = _random.Next(20, 60); }
                var newUsers = Enumerable.Range(1, BatchCount / 2).Select(i => new BenchmarkUser { Name = "Lite_Upsert_I", Age = _random.Next(20, 60), Email = $"lite_upsert{i}@test.com", CreateTime = DateTime.Now }).ToList();
                var all = existingUsers.Concat(newUsers).ToList();
                await service.BatchUpdateOrInsertAsync(all);
            }
        }

        [Benchmark]
        public async Task Dapper_UpdateOrInsert_Async()
        {
            using (var conn = new MySqlConnection(_connectionString!))
            {
                await conn.OpenAsync();
                var sql = "SELECT * FROM BenchmarkUser LIMIT " + (BatchCount / 2);
                var existingUsers = (await conn.QueryAsync<BenchmarkUser>(sql)).ToList();
                using (var trans = await conn.BeginTransactionAsync())
                {
                    foreach (var u in existingUsers) { u.Name = "Dapper_Upsert_U"; u.Age = _random.Next(20, 60); }
                    var newUsers = Enumerable.Range(1, BatchCount / 2).Select(i => new BenchmarkUser { Id = 0, Name = "Dapper_Upsert_I", Age = _random.Next(20, 60), Email = $"dapper_upsert{i}@test.com", CreateTime = DateTime.Now }).ToList();
                    var all = existingUsers.Concat(newUsers).ToList();
                    var upsertSql = @"
                    INSERT INTO BenchmarkUser (Id, Name, Age, Email, CreateTime) 
                    VALUES (NULLIF(@Id, 0), @Name, @Age, @Email, @CreateTime) 
                    ON DUPLICATE KEY UPDATE Name = VALUES(Name), Age = VALUES(Age), Email = VALUES(Email)";
                    await conn.ExecuteAsync(upsertSql, all, trans);
                    await trans.CommitAsync();
                }
            }
        }

        [Benchmark]
        public async Task FreeSql_UpdateOrInsert_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var fsql = scope.ServiceProvider.GetRequiredService<IFreeSql>();
                var existingUsers = await fsql.Select<BenchmarkUser>().Limit(BatchCount / 2).ToListAsync();
                foreach (var u in existingUsers) { u.Name = "FreeSql_Upsert_U"; u.Age = _random.Next(20, 60); }
                var newUsers = Enumerable.Range(1, BatchCount / 2).Select(i => new BenchmarkUser { Name = "FreeSql_Upsert_I", Age = _random.Next(20, 60), Email = $"freesql_upsert{i}@test.com", CreateTime = DateTime.Now }).ToList();
                var all = existingUsers.Concat(newUsers).ToList();
                await fsql.InsertOrUpdate<BenchmarkUser>().SetSource(all).ExecuteAffrowsAsync();
            }
        }
        #endregion

        #region Async Join Query
        [Benchmark]
        public async Task EFCore_JoinQuery_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<BenchmarkDbContext>();
                var list = await db.BenchmarkLogs.Include(l => l.User).Where(l => l.User.Age < 30).ToListAsync();
            }
        }

        [Benchmark]
        public async Task SqlSugar_JoinQuery_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var sugar = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
                var list = await sugar.Queryable<BenchmarkLog, BenchmarkUser>((l, u) => l.UserId == u.Id)
                    .Where((l, u) => u.Age < 30)
                    .Select((l, u) => new { l.Id, l.Message, UserName = u.Name })
                    .ToListAsync();
            }
        }

        [Benchmark]
        public async Task LiteOrm_JoinQuery_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IEntityViewServiceAsync<BenchmarkLogView>>();
                var list = await service.SearchAsync(Expr.Exp<BenchmarkLogView>(l => l.Age < 30));
            }
        }


        [Benchmark]
        public async Task Dapper_JoinQuery_Async()
        {
            using (var conn = new MySqlConnection(_connectionString!))
            {
                var sql = @"SELECT l.*, u.* FROM BenchmarkLog l INNER JOIN BenchmarkUser u ON l.UserId = u.Id WHERE u.Age < 30";
                var list = await conn.QueryAsync<BenchmarkLog, BenchmarkUser, BenchmarkLog>(sql, (log, user) => { log.User = user; return log; });
            }
        }

        [Benchmark]
        public async Task FreeSql_JoinQuery_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var fsql = scope.ServiceProvider.GetRequiredService<IFreeSql>();
                var list = await fsql.Select<BenchmarkLog>().InnerJoin(a => a.UserId == a.User.Id).Where(a => a.User.Age < 30).ToListAsync();
            }
        }
        #endregion
    }

    public class BenchmarkDbContext : DbContext
    {
        public BenchmarkDbContext(DbContextOptions<BenchmarkDbContext> options) : base(options) { }
        public DbSet<BenchmarkUser> BenchmarkUsers { get; set; }
        public DbSet<BenchmarkLog> BenchmarkLogs { get; set; }
    }

}
