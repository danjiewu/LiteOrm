using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;

namespace LiteOrm.Demo.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider services)
        {
            var configuration = services.GetRequiredService<IConfiguration>();

            // 获取已注册的数据源名称
            var dataSourceProvider = services.GetRequiredService<DataSourceProvider>();
            var dataSourceName = dataSourceProvider.DefaultDataSourceName ?? "SQLite";

            var contextPoolFactory = services.GetRequiredService<DAOContextPoolFactory>();
            var context = contextPoolFactory.PeekContext(dataSourceName); // 确保初始化连接池

            await EnsureTablesCreatedAsync(services, context.DbConnection);

            // 检查是否有数据，若没有则同步初始演示数据
            var userService = services.GetRequiredService<IUserService>();
            var deptService = services.GetRequiredService<IDepartmentService>();
            var salesService = services.GetRequiredService<ISalesService>();

            int userCount = await userService.CountAsync();
            int deptCount = await deptService.CountAsync();

            if (userCount == 0 || deptCount == 0)
            {
                await SeedDataWithServicesAsync(userService, deptService, salesService);
            }
            contextPoolFactory.ReturnContext(context);
        }

        private static async Task EnsureTablesCreatedAsync(IServiceProvider services, DbConnection connection)
        {
            // Departments 和 Users 表由 LiteOrm 的 SyncTable 功能在 LiteOrmComponentInitializer 中自动同步。
            // 此处仅对动态分表初始化（SyncTable 目前仅同步固定表名定义）。

            var tableInfoProvider = services.GetRequiredService<TableInfoProvider>();
            var sqlBuilderFactory = services.GetRequiredService<SqlBuilderFactory>();
            var sqlBuilder = sqlBuilderFactory.GetSqlBuilder(connection.GetType());

            string currentMonth = DateTime.Now.ToString("yyyyMM");
            var tableDef = tableInfoProvider.GetTableDefinition(typeof(SalesRecord));
            string tableName = string.Format(tableDef.Name, currentMonth);

            // 检查表是否存在
            bool exists = false;
            try
            {
                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = sqlBuilder.BuildTableExistsSql(tableName);
                await checkCmd.ExecuteScalarAsync();
                exists = true;
            }
            catch
            {
                exists = false;
            }

            if (!exists)
            {
                Console.WriteLine($"正在创建分表: {tableName}");
                string createSql = sqlBuilder.BuildCreateTableSql(tableName, tableDef.Columns);
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = createSql;
                    await cmd.ExecuteNonQueryAsync();
                }

                // 创建辅助索引
                foreach (var col in tableDef.Columns.Where(c => c.IsIndex || c.IsUnique))
                {
                    try
                    {
                        string indexSql = sqlBuilder.BuildCreateIndexSql(tableName, col);
                        using var idxCmd = connection.CreateCommand();
                        idxCmd.CommandText = indexSql;
                        await idxCmd.ExecuteNonQueryAsync();
                    }
                    catch { }
                }
            }
        }


        private static async Task SeedDataWithServicesAsync(IUserService userService, IDepartmentService deptService, ISalesService salesService)
        {
            // 1. 准备部门数据
            var depts = new List<Department>
            {
                new() { Id = 1, Name = "集团总部" },
                new() { Id = 2, Name = "研发中心", ParentId = 1 },
                new() { Id = 3, Name = "市场部", ParentId = 1 },
                new() { Id = 4, Name = "销售部", ParentId = 1 },
                new() { Id = 5, Name = "人工智能实验室", ParentId = 2 },
                new() { Id = 6, Name = "财务部", ParentId = 1 },
                new() { Id = 7, Name = "人力资源部", ParentId = 1 },
                new() { Id = 8, Name = "上海研发部", ParentId = 2 },
                new() { Id = 9, Name = "广州分公司", ParentId = 4 },
                new() { Id = 10, Name = "研发一部", ParentId = 2 },
                new() { Id = 11, Name = "研发二部", ParentId = 2 }
            };

            // 使用 BatchInsert 提高效率
            await deptService.BatchInsertAsync(depts);

            // 2. 准备用户数据
            var users = new List<User>
            {
                new() { Id = 1, UserName = "Admin", Age = 35, CreateTime = DateTime.Now, DeptId = 1 },
                new() { Id = 2, UserName = "研发负责人", Age = 32, CreateTime = DateTime.Now, DeptId = 2 },
                new() { Id = 3, UserName = "张三", Age = 25, CreateTime = DateTime.Now, DeptId = 2 },
                new() { Id = 4, UserName = "李四", Age = 28, CreateTime = DateTime.Now, DeptId = 2 },
                new() { Id = 5, UserName = "王五", Age = 30, CreateTime = DateTime.Now, DeptId = 3 },
                new() { Id = 6, UserName = "项目经理", Age = 33, CreateTime = DateTime.Now, DeptId = 4 },
                new() { Id = 7, UserName = "钱七", Age = 26, CreateTime = DateTime.Now, DeptId = 4 },
                new() { Id = 8, UserName = "财务负责人", Age = 38, CreateTime = DateTime.Now, DeptId = 6 },
                new() { Id = 9, UserName = "财务助理", Age = 24, CreateTime = DateTime.Now, DeptId = 6 },
                new() { Id = 10, UserName = "HRBP-Lucy", Age = 29, CreateTime = DateTime.Now, DeptId = 7 },
                new() { Id = 11, UserName = "老王", Age = 40, CreateTime = DateTime.Now, DeptId = 8 },
                new() { Id = 12, UserName = "小李", Age = 22, CreateTime = DateTime.Now, DeptId = 9 },
                new() { Id = 13, UserName = "赵六", Age = 27, CreateTime = DateTime.Now, DeptId = 2 },
                new() { Id = 14, UserName = "孙八", Age = 31, CreateTime = DateTime.Now, DeptId = 3 },
                new() { Id = 15, UserName = "周九", Age = 23, CreateTime = DateTime.Now, DeptId = 4 },
                new() { Id = 16, UserName = "吴十", Age = 34, CreateTime = DateTime.Now, DeptId = 5 },
                new() { Id = 17, UserName = "郑一", Age = 28, CreateTime = DateTime.Now, DeptId = 5 },
                new() { Id = 18, UserName = "王二", Age = 36, CreateTime = DateTime.Now, DeptId = 8 },
                new() { Id = 19, UserName = "陈三", Age = 25, CreateTime = DateTime.Now, DeptId = 9 },
                new() { Id = 20, UserName = "林四", Age = 30, CreateTime = DateTime.Now, DeptId = 9 }
            };

            await userService.BatchInsertAsync(users);

            // 3. 设置部门负责人
            var updateDepts = new List<Department>();

            async Task MarkManager(int deptId, int managerId)
            {
                var d = await deptService.GetObjectAsync(deptId);
                if (d != null) { d.ManagerId = managerId; updateDepts.Add(d); }
            }

            await MarkManager(1, 1);  // 总部负责人: Admin
            await MarkManager(2, 2);  // 研发中心负责人: 研发负责人
            await MarkManager(4, 6);  // 销售部负责人: 项目经理
            await MarkManager(6, 8);  // 财务部负责人: 财务负责人
            await MarkManager(7, 10); // 人事负责人: Lucy
            await MarkManager(8, 11); // 上海负责人
            await MarkManager(9, 12); // 广州负责人

            // 批量更新部门负责人
            await deptService.BatchUpdateAsync(updateDepts);

            // 4. 准备销售记录
            int count = 50;
            var productPool = new[]
            {
                (Id: 101, Name: "笔记本电脑", Price: 5999),
                (Id: 102, Name: "显示器", Price: 1299),
                (Id: 103, Name: "机械键盘", Price: 499),
                (Id: 201, Name: "无线鼠标", Price: 199),
                (Id: 202, Name: "人体工学椅", Price: 1599),
                (Id: 301, Name: "Type-C 扩展坞", Price: 259),
                (Id: 302, Name: "4K 摄像头", Price: 699),
                (Id: 401, Name: "空气净化器", Price: 2499),
                (Id: 402, Name: "加湿器", Price: 3299),
                (Id: 501, Name: "电竞笔记本", Price: 8999),
            };

            var userIds = new[] { 3, 4, 6, 7, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
            var records = new List<SalesRecord>();
            var random = new Random();
            var saleDateBase = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

            for (int i = 0; i < count; i++)
            {
                var p = productPool[random.Next(productPool.Length)];
                var saleTime = saleDateBase.AddDays(random.Next(25)).AddHours(random.Next(8, 20)).AddMinutes(random.Next(60));
                // 随机一部分订单已发货
                DateTime? shipTime = random.NextDouble() > 0.2 ? saleTime.AddHours(random.Next(2, 72)) : null;

                records.Add(new SalesRecord
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    Amount = p.Price,
                    SaleTime = saleTime,
                    ShipTime = shipTime,
                    SalesUserId = userIds[random.Next(userIds.Length)]
                });
            }

            await salesService.BatchInsertAsync(records);
        }
    }
}