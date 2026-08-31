using BenchmarkDotNet.Attributes;
using Dapper;
using FreeSql;
using LiteOrm.Common;
using LiteOrm.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Data.Common;
using System.Text;
using Microsoft.Data.Sqlite;
using Oracle.ManagedDataAccess.Client;
using SqlSugar;
using LiteOrm.Service;


namespace LiteOrm.Benchmark
{
    /// <summary>
    /// 基准测试共享基类：封装 DI 容器构建、表初始化、种子数据、辅助构建方法等通用逻辑。
    /// 批量评测与单条评测分别继承此类，仅需提供各自的种子数量。
    /// </summary>
    public abstract class OrmBenchmarkBase
    {
        protected IHost? _host;
        protected IServiceProvider _serviceProvider => _host!.Services;
        protected readonly Random _random = new Random();

        protected string? _connectionString;
        protected string? _provider; // e.g. MySql, SQLite, Oracle
        protected string? _providerTypeName; // connection type assembly qualified name from config
        protected record DataSourceConfig
        {
            public string? Name { get; init; }
            public string? ConnectionString { get; init; }
            public string? Provider { get; init; }
            public bool SyncTable { get; init; }
        }

        protected DbConnection CreateDbConnection()
        {
            var p = (_provider ?? "").ToLower();
            if (p.Contains("sqlite"))
            {
                return new SqliteConnection(_connectionString);
            }
            if (p.Contains("oracle"))
            {
                return new OracleConnection(_connectionString);
            }

            // default to MySql
            return new MySqlConnector.MySqlConnection(_connectionString);
        }

        /// <summary>
        /// 统一构造含多种属性类型的 <see cref="BenchmarkUser"/>，供各 ORM 的 Insert/Seed 使用，
        /// 保证各框架映射到相同的数据列集合，公平对比不同类型属性的读写成本。
        /// </summary>
        protected static BenchmarkUser NewBenchmarkUser(string name, int age, string email)
        {
            return new BenchmarkUser
            {
                Name = name,
                Age = age,
                Email = email,
                CreateTime = DateTime.Now,
                Uid = Guid.NewGuid(),
                Salary = age * 100m + 0.50m,
                IsActive = (age & 1) == 0,
                Score = age * 1.25,
                LoginCount = age * 7L,
                Remark = "benchmark-" + name
            };
        }

        /// <summary>
        /// 批量插入：构建单条多行 VALUES INSERT 语句（分块避免 MySQL 参数上限 65535）
        /// </summary>
        protected static async Task ExecuteBatchInsertAsync(DbConnection conn, IReadOnlyList<BenchmarkUser> users, DbTransaction trans)
        {
            const int chunkSize = 2000;
            for (int offset = 0; offset < users.Count; offset += chunkSize)
            {
                var chunk = users.Skip(offset).Take(chunkSize).ToList();
                var sb = new StringBuilder("INSERT INTO BenchmarkUser (Name, Age, Email, CreateTime, Uid, Salary, IsActive, Score, LoginCount, Remark) VALUES ");
                var p = new Dapper.DynamicParameters();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append($"(@N{i},@A{i},@E{i},@T{i},@U{i},@S{i},@B{i},@Sc{i},@L{i},@R{i})");
                    p.Add($"N{i}", chunk[i].Name);
                    p.Add($"A{i}", chunk[i].Age);
                    p.Add($"E{i}", chunk[i].Email);
                    p.Add($"T{i}", chunk[i].CreateTime);
                    p.Add($"U{i}", chunk[i].Uid);
                    p.Add($"S{i}", chunk[i].Salary);
                    p.Add($"B{i}", chunk[i].IsActive);
                    p.Add($"Sc{i}", chunk[i].Score);
                    p.Add($"L{i}", chunk[i].LoginCount);
                    p.Add($"R{i}", chunk[i].Remark);
                }
                await conn.ExecuteAsync(sb.ToString(), p, trans);
            }
        }

        /// <summary>
        /// 批量更新/插入：INSERT ... ON DUPLICATE KEY UPDATE（MySQL 批量更新模式）
        /// 用单条 SQL 完成全部行的更新，避免逐行 UPDATE
        /// </summary>
        protected static async Task ExecuteBatchUpsertAsync(DbConnection conn, IReadOnlyList<BenchmarkUser> users, DbTransaction trans)
        {
            const int chunkSize = 2000;
            for (int offset = 0; offset < users.Count; offset += chunkSize)
            {
                var chunk = users.Skip(offset).Take(chunkSize).ToList();
                var sb = new StringBuilder("INSERT INTO BenchmarkUser (Id, Name, Age, Email, CreateTime, Uid, Salary, IsActive, Score, LoginCount, Remark) VALUES ");
                var p = new Dapper.DynamicParameters();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append($"(@I{i},@N{i},@A{i},@E{i},@T{i},@U{i},@S{i},@B{i},@Sc{i},@L{i},@R{i})");
                    p.Add($"I{i}", chunk[i].Id);
                    p.Add($"N{i}", chunk[i].Name);
                    p.Add($"A{i}", chunk[i].Age);
                    p.Add($"E{i}", chunk[i].Email);
                    p.Add($"T{i}", chunk[i].CreateTime);
                    p.Add($"U{i}", chunk[i].Uid);
                    p.Add($"S{i}", chunk[i].Salary);
                    p.Add($"B{i}", chunk[i].IsActive);
                    p.Add($"Sc{i}", chunk[i].Score);
                    p.Add($"L{i}", chunk[i].LoginCount);
                    p.Add($"R{i}", chunk[i].Remark);
                }
                sb.Append(" ON DUPLICATE KEY UPDATE Name=VALUES(Name), Age=VALUES(Age), Email=VALUES(Email), Salary=VALUES(Salary), IsActive=VALUES(IsActive), Score=VALUES(Score), LoginCount=VALUES(LoginCount), Remark=VALUES(Remark)");
                await conn.ExecuteAsync(sb.ToString(), p, trans);
            }
        }

        /// <summary>
        /// 构建 DI 容器、初始化表结构、插入种子数据。
        /// </summary>
        /// <param name="seedCount">每个 ORM 要插入的种子记录数。</param>
        protected void SetupCore(int seedCount)
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
                    .ConfigureLogging(logging =>
                    {
                        foreach (var provider in logging.Services.Where(x => x.ServiceType.Name.Contains("LoggerProvider")).ToList())
                        {
                            logging.Services.Remove(provider);
                        }
                    })
                    .RegisterLiteOrm()
                    .ConfigureServices((Action<HostBuilderContext, IServiceCollection>)((context, services) =>
                    {
                        // Support switching provider via LiteOrm section in configuration
                        var liteOrmSection = context.Configuration.GetSection("LiteOrm");
                        _provider = liteOrmSection.GetValue<string>("Default") ?? "MySql";
                        var dataSources = liteOrmSection.GetSection("DataSources").Get<List<DataSourceConfig>>() ?? new List<DataSourceConfig>();
                        var selected = dataSources.FirstOrDefault(d => string.Equals(d.Name, _provider, StringComparison.OrdinalIgnoreCase)) ?? dataSources.FirstOrDefault();

                        if (selected == null)
                        {
                            throw new InvalidOperationException("No data source configured under LiteOrm:DataSources.");
                        }

                        _connectionString = selected.ConnectionString;
                        _providerTypeName = selected.Provider;

                        // 1. EF Core 配置（根据 provider 选择）
                        services.AddDbContext<BenchmarkDbContext>(options =>
                        {
                            var p = (_provider ?? "").ToLower();
                            if (p.Contains("sqlite"))
                            {
                                options.UseSqlite(_connectionString);
                            }
                            else if (p.Contains("oracle"))
                            {
                                options.UseOracle(_connectionString);
                            }
                            else
                            {
                                options.UseMySQL(_connectionString!);
                            }
                            options.LogTo(_ => { }, Microsoft.Extensions.Logging.LogLevel.None);
                        });

                        // 2. SqlSugar 配置
                        services.AddScoped<ISqlSugarClient>(s =>
                        {
                            var dbType = SqlSugar.DbType.MySql;
                            switch ((_provider ?? "").ToLower())
                            {
                                case var p when p.Contains("sqlite"):
                                    dbType = SqlSugar.DbType.Sqlite;
                                    break;
                                case var p when p.Contains("oracle"):
                                    dbType = SqlSugar.DbType.Oracle;
                                    break;
                                default:
                                    dbType = SqlSugar.DbType.MySql;
                                    break;
                            }
                            return new SqlSugarClient(new ConnectionConfig()
                            {
                                ConnectionString = _connectionString,
                                DbType = dbType,
                                IsAutoCloseConnection = true,
                            });
                        });

                        // 3. FreeSql 配置
                        services.AddSingleton(s =>
                        {
                            var dataType = FreeSql.DataType.MySql;
                            switch ((_provider ?? "").ToLower())
                            {
                                case var p when p.Contains("sqlite"):
                                    dataType = FreeSql.DataType.Sqlite;
                                    break;
                                case var p when p.Contains("oracle"):
                                    dataType = FreeSql.DataType.Oracle;
                                    break;
                                default:
                                    dataType = FreeSql.DataType.MySql;
                                    break;
                            }
                            return new FreeSqlBuilder()
                                .UseConnectionString(dataType, _connectionString)
                                .UseAutoSyncStructure(true)
                                .Build();
                        });
                    }))
                    .Build();

                _host!.Start();
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
                    efCtx.Database.EnsureCreated();
                    Console.WriteLine("Tables cleaning and rebuilding completed.");

                    // 2. 插入种子数据
                    Console.WriteLine("Step 2: Seeding data...");

                    // EF Core 种子
                    Console.WriteLine("Seeding EF Core...");
                    var efUsers = Enumerable.Range(1, seedCount).Select(i => NewBenchmarkUser($"User{i}", 20 + (i % 50), $"user{i}@example.com")).ToList();
                    efCtx.BenchmarkUsers.AddRange(efUsers);
                    efCtx.SaveChanges();
                    var efLogs = efUsers.Select(u => new BenchmarkLog { UserId = u.Id, Message = $"Log for {u.Name}", LogTime = DateTime.Now }).ToList();
                    efCtx.BenchmarkLogs.AddRange(efLogs);
                    efCtx.SaveChanges();

                    // SqlSugar 种子
                    Console.WriteLine("Seeding SqlSugar...");
                    var sugarUsers = Enumerable.Range(1, seedCount).Select(i => NewBenchmarkUser($"User{i}", 20 + (i % 50), $"user{i}@example.com")).ToList();
                    sugar.Insertable(sugarUsers).ExecuteCommand();
                    var sugarLogs = sugar.Queryable<BenchmarkUser>().ToList().Select(u => new BenchmarkLog { UserId = u.Id, Message = $"Log for {u.Name}", LogTime = DateTime.Now }).ToList();
                    sugar.Insertable(sugarLogs).ExecuteCommand();

                    // FreeSql 种子
                    Console.WriteLine("Seeding FreeSql...");
                    var fsqlUsers = Enumerable.Range(1, seedCount).Select(i => NewBenchmarkUser($"User{i}", 20 + (i % 50), $"user{i}@example.com")).ToList();
                    fsql.Insert(fsqlUsers).ExecuteAffrows();
                    var fsqlLogs = fsql.Select<BenchmarkUser>().ToList().Select(u => new BenchmarkLog { UserId = u.Id, Message = $"Log for {u.Name}", LogTime = DateTime.Now }).ToList();
                    fsql.Insert(fsqlLogs).ExecuteAffrows();

                    // LiteOrm 种子
                    Console.WriteLine("Seeding LiteOrm...");
                    var userDao = scope.ServiceProvider.GetRequiredService<ObjectDAO<BenchmarkUser>>();
                    var liteUsers = Enumerable.Range(1, seedCount).Select(i => NewBenchmarkUser($"User{i}", 20 + (i % 50), $"user{i}@example.com")).ToList();
                    userDao.BatchInsertAsync(liteUsers).GetAwaiter().GetResult();

                    var userViewDao = scope.ServiceProvider.GetRequiredService<ObjectViewDAO<BenchmarkUser>>();
                    var logDao = scope.ServiceProvider.GetRequiredService<ObjectDAO<BenchmarkLog>>();
                    var liteLogs = userViewDao.Search().ToListAsync().GetAwaiter().GetResult().Select(u => new BenchmarkLog { UserId = u.Id, Message = $"Log for {u.Name}", LogTime = DateTime.Now }).ToList();
                    logDao.BatchInsertAsync(liteLogs).GetAwaiter().GetResult();

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
    }

    /// <summary>
    /// 批量操作与联表查询评测，数据量由 <see cref="BatchCount"/> 参数控制。
    /// </summary>
    [MemoryDiagnoser]
    [MediumRunJob]
    public class OrmBenchmark : OrmBenchmarkBase
    {
        [Params(10, 100, 1000, 10000)]
        public int BatchCount { get; set; }

        [GlobalSetup]
        public void Setup() => SetupCore(BatchCount);

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
                db.ChangeTracker.AutoDetectChangesEnabled = false;
                var users = Enumerable.Range(1, BatchCount).Select(i => NewBenchmarkUser("EF", 25, "ef@test.com")).ToList();
                await db.BenchmarkUsers.AddRangeAsync(users);
                db.ChangeTracker.DetectChanges();
                await db.SaveChangesAsync();
            }
        }

        [Benchmark]
        public async Task SqlSugar_Insert_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var sugar = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
                var users = Enumerable.Range(1, BatchCount).Select(i => NewBenchmarkUser("Sugar", 25, "sugar@test.com")).ToList();
                await sugar.Insertable(users).ExecuteCommandAsync();
            }
        }


        [Benchmark]
        public async Task LiteOrm_Insert_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IEntityServiceAsync<BenchmarkUser>>();
                var users = Enumerable.Range(1, BatchCount).Select(i => NewBenchmarkUser("Lite", 25, "lite@test.com")).ToList();
                await service.BatchInsertAsync(users);
            }
        }

        [Benchmark]
        public async Task Dapper_Insert_Async()
        {
            using (var conn = CreateDbConnection())
            {
                await conn.OpenAsync();
                using (var trans = conn.BeginTransaction())
                {
                    var users = Enumerable.Range(1, BatchCount).Select(i => NewBenchmarkUser("Dapper", 25, "dapper@test.com")).ToList();
                    await ExecuteBatchInsertAsync(conn, users, trans);
                    trans.Commit();
                }
            }
        }

        [Benchmark]
        public async Task FreeSql_Insert_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var fsql = scope.ServiceProvider.GetRequiredService<IFreeSql>();
                var users = Enumerable.Range(1, BatchCount).Select(i => NewBenchmarkUser("FreeSql", 25, "freesql@test.com")).ToList();
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
                db.ChangeTracker.AutoDetectChangesEnabled = false;
                var users = await db.BenchmarkUsers.Take(BatchCount).ToListAsync();
                foreach (var u in users)
                {
                    u.Name = "EFCore" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    u.Age = _random.Next(20, 60);
                    u.Email = Guid.NewGuid().ToString("N").Substring(0, 10) + "@test.com";
                }
                db.ChangeTracker.DetectChanges();
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
                var service = scope.ServiceProvider.GetRequiredService<IEntityServiceAsync<BenchmarkUser>>();
                var users = await viewService.SearchAsync(new SectionExpr(0, BatchCount));
                foreach (var u in users)
                {
                    u.Name = "LiteOrm" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    u.Age = _random.Next(20, 60);
                    u.Email = Guid.NewGuid().ToString("N").Substring(0, 10) + "@test.com";
                }
                await service.BatchUpdateAsync(users);
            }
        }



        [Benchmark]
        public async Task Dapper_Update_Async()
        {
            using (var conn = CreateDbConnection())
            {
                await conn.OpenAsync();
                var selectSql = (_provider ?? "").ToLower().Contains("oracle") ? $"SELECT * FROM BenchmarkUser WHERE ROWNUM <= {BatchCount}" : $"SELECT * FROM BenchmarkUser LIMIT {BatchCount}";
                var users = (await conn.QueryAsync<BenchmarkUser>(selectSql)).ToList();
                using (var trans = conn.BeginTransaction())
                {
                    foreach (var u in users)
                    {
                        u.Name = "Dapper" + Guid.NewGuid().ToString("N").Substring(0, 8);
                        u.Age = _random.Next(20, 60);
                        u.Email = Guid.NewGuid().ToString("N").Substring(0, 10) + "@test.com";
                    }
                    await ExecuteBatchUpsertAsync(conn, users, trans);
                    trans.Commit();
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

        #region Async Upsert
        [Benchmark]
        public async Task EFCore_Upsert_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<BenchmarkDbContext>();
                db.ChangeTracker.AutoDetectChangesEnabled = false;
                var existingUsers = await db.BenchmarkUsers.Take(BatchCount / 2).ToListAsync();
                var localRandom = new Random();
                foreach (var u in existingUsers)
                {
                    u.Name = "EF_Upsert_U";
                    u.Age = localRandom.Next(20, 60);
                }

                string tag = Guid.NewGuid().ToString("N").Substring(0, 6);
                var newUsers = Enumerable.Range(1, BatchCount / 2).Select(i => NewBenchmarkUser("EF_Upsert_I", localRandom.Next(20, 60), $"ef_upsert_{tag}_{i}@test.com")).ToList();

                await db.BenchmarkUsers.AddRangeAsync(newUsers);
                db.ChangeTracker.DetectChanges();
                await db.SaveChangesAsync();
            }
        }

        [Benchmark]
        public async Task SqlSugar_Upsert_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var sugar = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
                var existingUsers = await sugar.Queryable<BenchmarkUser>().Take(BatchCount / 2).ToListAsync();
                foreach (var u in existingUsers) { u.Name = "Sugar_Upsert_U"; u.Age = _random.Next(20, 60); }
                var newUsers = Enumerable.Range(1, BatchCount / 2).Select(i => NewBenchmarkUser("Sugar_Upsert_I", _random.Next(20, 60), $"sugar_upsert{i}@test.com")).ToList();
                var all = existingUsers.Concat(newUsers).ToList();
                await sugar.Storageable(all).ExecuteCommandAsync();
            }
        }



        [Benchmark]
        public async Task LiteOrm_Upsert_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var viewService = scope.ServiceProvider.GetRequiredService<IEntityViewServiceAsync<BenchmarkUser>>();
                var service = scope.ServiceProvider.GetRequiredService<IEntityServiceAsync<BenchmarkUser>>();
                var existingUsers = await viewService.SearchAsync(new SectionExpr(0, BatchCount / 2));
                foreach (var u in existingUsers) { u.Name = "Lite_Upsert_U"; u.Age = _random.Next(20, 60); }
                var newUsers = Enumerable.Range(1, BatchCount / 2).Select(i => NewBenchmarkUser("Lite_Upsert_I", _random.Next(20, 60), $"lite_upsert{i}@test.com")).ToList();
                var all = existingUsers.Concat(newUsers).ToList();
                await service.BatchUpdateOrInsertAsync(all);
            }
        }

        [Benchmark]
        public async Task Dapper_Upsert_Async()
        {
            using (var conn = CreateDbConnection())
            {
                await conn.OpenAsync();
                var selectSql = (_provider ?? "").ToLower().Contains("oracle") ? $"SELECT * FROM BenchmarkUser WHERE ROWNUM <= {BatchCount / 2}" : $"SELECT * FROM BenchmarkUser LIMIT {BatchCount / 2}";
                var existingUsers = (await conn.QueryAsync<BenchmarkUser>(selectSql)).ToList();
                using (var trans = conn.BeginTransaction())
                {
                    foreach (var u in existingUsers) { u.Name = "Dapper_Upsert_U"; u.Age = _random.Next(20, 60); }
                    var newUsers = Enumerable.Range(1, BatchCount / 2).Select(i => NewBenchmarkUser("Dapper_Upsert_I", _random.Next(20, 60), $"dapper_upsert{i}@test.com")).ToList();
                    // 合并已有 + 新增，单条 INSERT ... ON DUPLICATE KEY UPDATE 完成全部
                    var all = existingUsers.Concat(newUsers).ToList();
                    if (all.Count > 0)
                        await ExecuteBatchUpsertAsync(conn, all, trans);
                    trans.Commit();
                }
            }
        }

        [Benchmark]
        public async Task FreeSql_Upsert_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var fsql = scope.ServiceProvider.GetRequiredService<IFreeSql>();
                var existingUsers = await fsql.Select<BenchmarkUser>().Limit(BatchCount / 2).ToListAsync();
                foreach (var u in existingUsers) { u.Name = "FreeSql_Upsert_U"; u.Age = _random.Next(20, 60); }
                var newUsers = Enumerable.Range(1, BatchCount / 2).Select(i => NewBenchmarkUser("FreeSql_Upsert_I", _random.Next(20, 60), $"freesql_upsert{i}@test.com")).ToList();
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
                var list = await db.BenchmarkLogs
                    .Include(l => l.User)
                    .Where(l => l.User.Age < 30)
                    .OrderByDescending(l => l.Id)
                    .Skip(0).Take(BatchCount)
                    .ToListAsync();
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
                    .OrderBy((l, u) => l.Id, OrderByType.Desc)
                    .Select((l, u) => new { l.Id, l.Message, UserName = u.Name })
                    .Skip(0).Take(BatchCount)
                    .ToListAsync();
            }
        }

        [Benchmark]
        public async Task LiteOrm_JoinQuery_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IEntityViewServiceAsync<BenchmarkLogView>>();
                var list = await service.SearchAsync(q => q.Where(l => l.Age < 30)
                          .OrderByDescending(l => l.Id)
                          .Skip(0).Take(BatchCount)
                );
            }
        }


        [Benchmark]
        public async Task Dapper_JoinQuery_Async()
        {
            using (var conn = CreateDbConnection())
            {
                await conn.OpenAsync();
                string sql;
                if ((_provider ?? "").ToLower().Contains("oracle"))
                {
                    sql = $@"SELECT l.*, u.* FROM BenchmarkLog l 
                             INNER JOIN BenchmarkUser u ON l.UserId = u.Id 
                             WHERE u.Age < 30 
                             ORDER BY l.Id DESC 
                             FETCH FIRST {BatchCount} ROWS ONLY";
                }
                else
                {
                    sql = $@"SELECT l.*, u.* FROM BenchmarkLog l 
                             INNER JOIN BenchmarkUser u ON l.UserId = u.Id 
                             WHERE u.Age < 30 
                             ORDER BY l.Id DESC 
                             LIMIT {BatchCount} OFFSET 0";
                }
                var list = await conn.QueryAsync<BenchmarkLog, BenchmarkUser, BenchmarkLog>(sql, (log, user) => { log.User = user; return log; });
            }
        }

        [Benchmark]
        public async Task FreeSql_JoinQuery_Async()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var fsql = scope.ServiceProvider.GetRequiredService<IFreeSql>();
                var list = await fsql.Select<BenchmarkLog>()
                    .InnerJoin(a => a.UserId == a.User.Id)
                    .Where(a => a.User.Age < 30)
                    .OrderByDescending(a => a.Id)
                    .Skip(0).Limit(BatchCount)
                    .ToListAsync();
            }
        }
        #endregion
    }

    /// <summary>
    /// 单条 Insert / Update / Upsert 评测，固定循环 <see cref="SingleLoopCount"/>（1000）次，
    /// 不参与 <c>BatchCount</c> 参数化。
    /// </summary>
    [MemoryDiagnoser]
    [MediumRunJob]
    public class OrmSingleBenchmark : OrmBenchmarkBase
    {
        private const int SingleLoopCount = 1000;

        [GlobalSetup]
        public void Setup() => SetupCore(SingleLoopCount);

        [GlobalCleanup]
        public void Cleanup()
        {
            _host?.Dispose();
        }

        #region Single Insert (1000 iterations)

        [Benchmark]
        public async Task EFCore_SingleInsert_Async()
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BenchmarkDbContext>();
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            for (int i = 0; i < SingleLoopCount; i++)
            {
                var user = NewBenchmarkUser("EF_SI", 25, $"ef_si{i}@test.com");
                await db.BenchmarkUsers.AddAsync(user);
                db.ChangeTracker.DetectChanges();
                await db.SaveChangesAsync();
                db.Entry(user).State = EntityState.Detached;
            }
        }

        [Benchmark]
        public async Task SqlSugar_SingleInsert_Async()
        {
            using var scope = _serviceProvider.CreateScope();
            var sugar = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
            for (int i = 0; i < SingleLoopCount; i++)
            {
                var user = NewBenchmarkUser("S_SI", 25, $"s_si{i}@test.com");
                await sugar.Insertable(user).ExecuteCommandAsync();
            }
        }

        [Benchmark]
        public async Task LiteOrm_SingleInsert_Async()
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IEntityServiceAsync<BenchmarkUser>>();
            for (int i = 0; i < SingleLoopCount; i++)
            {
                var user = NewBenchmarkUser("L_SI", 25, $"l_si{i}@test.com");
                await service.InsertAsync(user);
            }
        }

        [Benchmark]
        public async Task Dapper_SingleInsert_Async()
        {
            using var conn = CreateDbConnection();
            await conn.OpenAsync();
            using var trans = conn.BeginTransaction();
            for (int i = 0; i < SingleLoopCount; i++)
            {
                var user = NewBenchmarkUser("D_SI", 25, $"d_si{i}@test.com");
                await conn.ExecuteAsync(
                    "INSERT INTO BenchmarkUser (Name, Age, Email, CreateTime, Uid, Salary, IsActive, Score, LoginCount, Remark) VALUES (@Name, @Age, @Email, @CreateTime, @Uid, @Salary, @IsActive, @Score, @LoginCount, @Remark)",
                    user, trans);
            }
            trans.Commit();
        }

        [Benchmark]
        public async Task FreeSql_SingleInsert_Async()
        {
            using var scope = _serviceProvider.CreateScope();
            var fsql = scope.ServiceProvider.GetRequiredService<IFreeSql>();
            for (int i = 0; i < SingleLoopCount; i++)
            {
                var user = NewBenchmarkUser("F_SI", 25, $"f_si{i}@test.com");
                await fsql.Insert(user).ExecuteAffrowsAsync();
            }
        }

        #endregion

        #region Single Update (1000 iterations)

        [Benchmark]
        public async Task EFCore_SingleUpdate_Async()
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BenchmarkDbContext>();
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            var users = await db.BenchmarkUsers.AsNoTracking().Take(SingleLoopCount).ToListAsync();
            if (users.Count == 0) return;
            for (int i = 0; i < SingleLoopCount; i++)
            {
                var u = users[i % users.Count];
                u.Name = "EF_SU" + Guid.NewGuid().ToString("N").Substring(0, 8);
                u.Age = _random.Next(20, 60);
                u.Email = Guid.NewGuid().ToString("N").Substring(0, 10) + "@test.com";
                db.BenchmarkUsers.Update(u);
                await db.SaveChangesAsync();
                db.Entry(u).State = EntityState.Detached;
            }
        }

        [Benchmark]
        public async Task SqlSugar_SingleUpdate_Async()
        {
            using var scope = _serviceProvider.CreateScope();
            var sugar = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
            var users = await sugar.Queryable<BenchmarkUser>().Take(SingleLoopCount).ToListAsync();
            if (users.Count == 0) return;
            for (int i = 0; i < SingleLoopCount; i++)
            {
                var u = users[i % users.Count];
                u.Name = "S_SU" + Guid.NewGuid().ToString("N").Substring(0, 8);
                u.Age = _random.Next(20, 60);
                u.Email = Guid.NewGuid().ToString("N").Substring(0, 10) + "@test.com";
                await sugar.Updateable(u).ExecuteCommandAsync();
            }
        }

        [Benchmark]
        public async Task LiteOrm_SingleUpdate_Async()
        {
            using var scope = _serviceProvider.CreateScope();
            var viewService = scope.ServiceProvider.GetRequiredService<IEntityViewServiceAsync<BenchmarkUser>>();
            var service = scope.ServiceProvider.GetRequiredService<IEntityServiceAsync<BenchmarkUser>>();
            var users = (await viewService.SearchAsync(new SectionExpr(0, SingleLoopCount))).ToList();
            if (users.Count == 0) return;
            for (int i = 0; i < SingleLoopCount; i++)
            {
                var u = users[i % users.Count];
                u.Name = "L_SU" + Guid.NewGuid().ToString("N").Substring(0, 8);
                u.Age = _random.Next(20, 60);
                u.Email = Guid.NewGuid().ToString("N").Substring(0, 10) + "@test.com";
                await service.UpdateAsync(u);
            }
        }

        [Benchmark]
        public async Task Dapper_SingleUpdate_Async()
        {
            using var conn = CreateDbConnection();
            await conn.OpenAsync();
            var selectSql = (_provider ?? "").ToLower().Contains("oracle")
                ? $"SELECT * FROM BenchmarkUser WHERE ROWNUM <= {SingleLoopCount}"
                : $"SELECT * FROM BenchmarkUser LIMIT {SingleLoopCount}";
            var users = (await conn.QueryAsync<BenchmarkUser>(selectSql)).ToList();
            if (users.Count == 0) return;
            using var trans = conn.BeginTransaction();
            for (int i = 0; i < SingleLoopCount; i++)
            {
                var u = users[i % users.Count];
                u.Name = "D_SU" + Guid.NewGuid().ToString("N").Substring(0, 8);
                u.Age = _random.Next(20, 60);
                u.Email = Guid.NewGuid().ToString("N").Substring(0, 10) + "@test.com";
                await conn.ExecuteAsync(
                    "UPDATE BenchmarkUser SET Name=@Name, Age=@Age, Email=@Email WHERE Id=@Id",
                    u, trans);
            }
            trans.Commit();
        }

        [Benchmark]
        public async Task FreeSql_SingleUpdate_Async()
        {
            using var scope = _serviceProvider.CreateScope();
            var fsql = scope.ServiceProvider.GetRequiredService<IFreeSql>();
            var users = await fsql.Select<BenchmarkUser>().Limit(SingleLoopCount).ToListAsync();
            if (users.Count == 0) return;
            for (int i = 0; i < SingleLoopCount; i++)
            {
                var u = users[i % users.Count];
                u.Name = "F_SU" + Guid.NewGuid().ToString("N").Substring(0, 8);
                u.Age = _random.Next(20, 60);
                u.Email = Guid.NewGuid().ToString("N").Substring(0, 10) + "@test.com";
                await fsql.Update<BenchmarkUser>().SetSource(u).ExecuteAffrowsAsync();
            }
        }

        #endregion

        #region Single Upsert (1000 iterations)

        [Benchmark]
        public async Task EFCore_SingleUpsert_Async()
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BenchmarkDbContext>();
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            var existing = await db.BenchmarkUsers.AsNoTracking().Take(SingleLoopCount).ToListAsync();
            for (int i = 0; i < SingleLoopCount; i++)
            {
                if (i % 2 == 0 && existing.Count > 0)
                {
                    var u = existing[i % existing.Count];
                    u.Name = "EF_SX_U" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    u.Age = _random.Next(20, 60);
                    db.BenchmarkUsers.Update(u);
                    await db.SaveChangesAsync();
                    db.Entry(u).State = EntityState.Detached;
                }
                else
                {
                    var user = NewBenchmarkUser("EF_SX_I", 25, $"ef_sx{i}@test.com");
                    await db.BenchmarkUsers.AddAsync(user);
                    db.ChangeTracker.DetectChanges();
                    await db.SaveChangesAsync();
                    db.Entry(user).State = EntityState.Detached;
                }
            }
        }

        [Benchmark]
        public async Task SqlSugar_SingleUpsert_Async()
        {
            using var scope = _serviceProvider.CreateScope();
            var sugar = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
            var existing = await sugar.Queryable<BenchmarkUser>().Take(SingleLoopCount).ToListAsync();
            for (int i = 0; i < SingleLoopCount; i++)
            {
                if (i % 2 == 0 && existing.Count > 0)
                {
                    var u = existing[i % existing.Count];
                    u.Name = "S_SX_U" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    u.Age = _random.Next(20, 60);
                    await sugar.Storageable(u).ExecuteCommandAsync();
                }
                else
                {
                    var user = NewBenchmarkUser("S_SX_I", 25, $"s_sx{i}@test.com");
                    await sugar.Storageable(user).ExecuteCommandAsync();
                }
            }
        }

        [Benchmark]
        public async Task LiteOrm_SingleUpsert_Async()
        {
            using var scope = _serviceProvider.CreateScope();
            var viewService = scope.ServiceProvider.GetRequiredService<IEntityViewServiceAsync<BenchmarkUser>>();
            var service = scope.ServiceProvider.GetRequiredService<IEntityServiceAsync<BenchmarkUser>>();
            var existing = (await viewService.SearchAsync(new SectionExpr(0, SingleLoopCount))).ToList();
            for (int i = 0; i < SingleLoopCount; i++)
            {
                if (i % 2 == 0 && existing.Count > 0)
                {
                    var u = existing[i % existing.Count];
                    u.Name = "L_SX_U" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    u.Age = _random.Next(20, 60);
                    await service.UpdateOrInsertAsync(u);
                }
                else
                {
                    var user = NewBenchmarkUser("L_SX_I", 25, $"l_sx{i}@test.com");
                    await service.UpdateOrInsertAsync(user);
                }
            }
        }

        [Benchmark]
        public async Task Dapper_SingleUpsert_Async()
        {
            using var conn = CreateDbConnection();
            await conn.OpenAsync();
            var selectSql = (_provider ?? "").ToLower().Contains("oracle")
                ? $"SELECT * FROM BenchmarkUser WHERE ROWNUM <= {SingleLoopCount}"
                : $"SELECT * FROM BenchmarkUser LIMIT {SingleLoopCount}";
            var existing = (await conn.QueryAsync<BenchmarkUser>(selectSql)).ToList();
            using var trans = conn.BeginTransaction();
            for (int i = 0; i < SingleLoopCount; i++)
            {
                BenchmarkUser u;
                if (i % 2 == 0 && existing.Count > 0)
                {
                    u = existing[i % existing.Count];
                    u.Name = "D_SX_U" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    u.Age = _random.Next(20, 60);
                }
                else
                {
                    u = NewBenchmarkUser("D_SX_I", 25, $"d_sx{i}@test.com");
                }
                await conn.ExecuteAsync(
                    "INSERT INTO BenchmarkUser (Id, Name, Age, Email, CreateTime, Uid, Salary, IsActive, Score, LoginCount, Remark) VALUES (@Id, @Name, @Age, @Email, @CreateTime, @Uid, @Salary, @IsActive, @Score, @LoginCount, @Remark) ON DUPLICATE KEY UPDATE Name=VALUES(Name), Age=VALUES(Age), Email=VALUES(Email)",
                    u, trans);
            }
            trans.Commit();
        }

        [Benchmark]
        public async Task FreeSql_SingleUpsert_Async()
        {
            using var scope = _serviceProvider.CreateScope();
            var fsql = scope.ServiceProvider.GetRequiredService<IFreeSql>();
            var existing = await fsql.Select<BenchmarkUser>().Limit(SingleLoopCount).ToListAsync();
            for (int i = 0; i < SingleLoopCount; i++)
            {
                if (i % 2 == 0 && existing.Count > 0)
                {
                    var u = existing[i % existing.Count];
                    u.Name = "F_SX_U" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    u.Age = _random.Next(20, 60);
                    await fsql.InsertOrUpdate<BenchmarkUser>().SetSource(u).ExecuteAffrowsAsync();
                }
                else
                {
                    var user = NewBenchmarkUser("F_SX_I", 25, $"f_sx{i}@test.com");
                    await fsql.InsertOrUpdate<BenchmarkUser>().SetSource(user).ExecuteAffrowsAsync();
                }
            }
        }

        #endregion

        #region Single Get (1000 iterations)

        [Benchmark]
        public async Task EFCore_SingleGet_Async()
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BenchmarkDbContext>();
            db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            for (int i = 0; i < SingleLoopCount; i++)
            {
                int id = (i % SingleLoopCount) + 1;
                var u = await db.BenchmarkUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            }
        }

        [Benchmark]
        public async Task SqlSugar_SingleGet_Async()
        {
            using var scope = _serviceProvider.CreateScope();
            var sugar = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
            for (int i = 0; i < SingleLoopCount; i++)
            {
                int id = (i % SingleLoopCount) + 1;
                var u = await sugar.Queryable<BenchmarkUser>().Where(x => x.Id == id).FirstAsync();
            }
        }

        [Benchmark]
        public async Task LiteOrm_SingleGet_Async()
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IEntityViewServiceAsync<BenchmarkUser>>();
            for (int i = 0; i < SingleLoopCount; i++)
            {
                int id = (i % SingleLoopCount) + 1;
                var u = await service.GetObjectAsync(id);
            }
        }

        [Benchmark]
        public async Task Dapper_SingleGet_Async()
        {
            using var conn = CreateDbConnection();
            await conn.OpenAsync();
            for (int i = 0; i < SingleLoopCount; i++)
            {
                int id = (i % SingleLoopCount) + 1;
                var u = await conn.QueryFirstOrDefaultAsync<BenchmarkUser>(
                    "SELECT * FROM BenchmarkUser WHERE Id=@Id", new { Id = id });
            }
        }

        [Benchmark]
        public async Task FreeSql_SingleGet_Async()
        {
            using var scope = _serviceProvider.CreateScope();
            var fsql = scope.ServiceProvider.GetRequiredService<IFreeSql>();
            for (int i = 0; i < SingleLoopCount; i++)
            {
                int id = (i % SingleLoopCount) + 1;
                var u = await fsql.Select<BenchmarkUser>().Where(x => x.Id == id).FirstAsync();
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