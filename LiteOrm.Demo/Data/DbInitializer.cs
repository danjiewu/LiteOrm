using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using LiteOrm.Common;

namespace LiteOrm.Demo.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider services)
        {
            var configuration = services.GetRequiredService<IConfiguration>();
            
            var connectionString = configuration.GetSection("LiteOrm:ConnectionStrings:0:ConnectionString").Value 
                                   ?? "Data Source=demo.db";

            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            Console.WriteLine("--- 数据库结构检查 ---");

            await EnsureTablesCreatedAsync(connection);

            // 检查是否有数据，若没有则同步初始演示数据
            var userService = services.GetRequiredService<IUserService>();
            var deptService = services.GetRequiredService<IDepartmentService>();
            var salesService = services.GetRequiredService<ISalesService>();

            if (await userService.CountAsync() == 0)
            {
                await SeedDataWithServicesAsync(userService, deptService, salesService);
            }
        }

        private static async Task EnsureTablesCreatedAsync(SqliteConnection connection)
        {
            string currentMonth = DateTime.Now.ToString("yyyyMM");
            // 此处使用原生 SQL 来初始化结构，因为 DDL 通常超出 ORM 职责范围
            string[] createTableSqls = {
                @"CREATE TABLE IF NOT EXISTS Departments (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT, ParentId INTEGER, ManagerId INTEGER);",
                @"CREATE TABLE IF NOT EXISTS Users (Id INTEGER PRIMARY KEY AUTOINCREMENT, UserName TEXT, Age INTEGER, CreateTime DATETIME, DeptId INTEGER);",
                // 支持分表显示 (当前月份)
                $@"CREATE TABLE IF NOT EXISTS Sales_{currentMonth} (Id INTEGER PRIMARY KEY AUTOINCREMENT, ProductId INTEGER, ProductName TEXT, Amount INTEGER NOT NULL, SaleTime DATETIME NOT NULL, ShipTime DATETIME, SalesUserId INTEGER);"
            };

            foreach (var sql in createTableSqls)
            {
                using var cmd = new SqliteCommand(sql, connection);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private static async Task SeedDataWithServicesAsync(IUserService userService, IDepartmentService deptService, ISalesService salesService)
        {
            // 此处演示从服务中获取依赖，虽然可以直接构造
            // 但实际项目中建议通过构造函数注入
            
            Console.WriteLine("--- 使用 Service 接口进行数据初始化 ---");

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
                new() { Id = 9, Name = "广州分公司", ParentId = 4 }
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
                new() { Id = 12, UserName = "小李", Age = 22, CreateTime = DateTime.Now, DeptId = 9 }
            };

            await userService.BatchInsertAsync(users);

            // 3. 设置部门负责人 (演示 UpdateAsync)
            // 重新获取部门进行更新
            var updateDepts = new List<Department>();
            
            async Task MarkManager(int deptId, int managerId) {
                var d = await deptService.GetObjectAsync(deptId);
                if(d != null) { d.ManagerId = managerId; updateDepts.Add(d); }
            }

            await MarkManager(1, 1);  // 总部负责人: Admin
            await MarkManager(2, 2);  // 研发中心负责人: 研发负责人
            await MarkManager(4, 6);  // 销售部负责人: 项目经理
            await MarkManager(6, 8);  // 财务部负责人: 财务负责人
            await MarkManager(7, 10); // 人事负责人: Lucy
            await MarkManager(8, 11); // 上海负责人
            await MarkManager(9, 12); // 广州负责人

            // 演示 BatchUpdate (批量更新对象已存在的实例)
            await deptService.BatchUpdateAsync(updateDepts);

            // 4. 准备销售记录 (原 SeedSalesDataAsync 内容，此处改为服务调用)
            string currentMonth = DateTime.Now.ToString("yyyyMM");
            int count = 50;
            Console.WriteLine($"--- 演示 IArged 分表数据插入 (Sales_{currentMonth})，生成 {count} 条随机销售记录 ---");

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

            var userIds = new[] { 3, 4, 6, 7, 11, 12 };
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

            Console.WriteLine("全量初始化数据填充完成。");
        }
    }
}