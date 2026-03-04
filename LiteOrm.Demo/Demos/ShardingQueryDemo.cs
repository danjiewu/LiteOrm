using System;
using System.Collections.Generic;
using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 分表查询演示
    /// 
    /// 本演示展示如何使用 LiteOrm 进行分表查询。分表场景中，表名包含参数（如 Sales_{0}），
    /// 通过向 SearchAsync 等方法传入 tableArgs 参数来指定要查询的具体分表。
    /// </summary>
    public static class ShardingQueryDemo
    {
        /// <summary>
        /// 运行所有分表演示
        /// </summary>
        public static async System.Threading.Tasks.Task RunAsync(IServiceProvider serviceProvider)
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("  分表查询演示 (Sharding Query Demo)");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            await Demo1_BasicShardingAsync(serviceProvider);
            await Demo2_ExplicitTableArgsAsync(serviceProvider);
            await Demo3_MultipleMonthsAsync(serviceProvider);
            await Demo4_ShardingWithOrderAsync(serviceProvider);
        }

        /// <summary>
        /// 演示1：基础分表查询 - 自动提取表参数
        /// </summary>
        private static async System.Threading.Tasks.Task Demo1_BasicShardingAsync(IServiceProvider serviceProvider)
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("演示1：基础分表查询 - 自动提取表参数");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("场景：表名为 Sales_{0}，{0} 由 SaleTime 的 yyyyMM 自动提取");
            Console.WriteLine();

            try
            {
                var salesService = serviceProvider.GetRequiredService<IEntityServiceAsync<SalesRecord>>();
                var salesViewService = serviceProvider.GetRequiredService<IEntityViewServiceAsync<SalesRecordView>>();
                var userService = serviceProvider.GetRequiredService<IEntityServiceAsync<User>>();

                // 创建用户
                var user = new User { UserName = "Alice Smith", Age = 30, CreateTime = DateTime.Now };
                await userService.InsertAsync(user);

                // 插入 2024 年 12 月的销售记录
                var sale1 = new SalesRecord
                {
                    ProductId = 1,
                    ProductName = "Laptop",
                    Amount = 1000,
                    SaleTime = new DateTime(2024, 12, 15),
                    SalesUserId = user.Id
                };
                var sale2 = new SalesRecord
                {
                    ProductId = 2,
                    ProductName = "Mouse",
                    Amount = 50,
                    SaleTime = new DateTime(2024, 12, 20),
                    SalesUserId = user.Id
                };

                await salesService.InsertAsync(sale1);
                await salesService.InsertAsync(sale2);

                Console.WriteLine("✓ 已插入数据到 Sales_202412 表：");
                Console.WriteLine($"  - {sale1.ProductName}: ¥{sale1.Amount} (2024-12-15)");
                Console.WriteLine($"  - {sale2.ProductName}: ¥{sale2.Amount} (2024-12-20)");
                Console.WriteLine();

                // 查询 2024 年 12 月的销售记录
                Console.WriteLine("执行查询：");
                Console.WriteLine("  var sales = await salesViewService.SearchAsync(");
                Console.WriteLine("      Expr.Lambda<SalesRecordView>(s => s.Amount > 40)");
                Console.WriteLine("  );");
                Console.WriteLine();

                var sales = await salesViewService.SearchAsync(
                    Expr.Lambda<SalesRecordView>(s => s.Amount > 40),
                    new string[] { "202412" }
                );

                Console.WriteLine($"✓ 查询完成，共返回 {sales.Count} 条记录：");
                foreach (var sale in sales)
                {
                    Console.WriteLine($"  - {sale.ProductName}: ¥{sale.Amount} (销售员: {sale.UserName})");
                }

                Console.WriteLine();
                Console.WriteLine("✅ 演示1完成");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ 演示1失败: {ex.Message}");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示2：显式指定表参数进行分表查询
        /// </summary>
        private static async System.Threading.Tasks.Task Demo2_ExplicitTableArgsAsync(IServiceProvider serviceProvider)
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("演示2：显式指定表参数");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("场景：通过 tableArgs 显式指定 Sales_202411 表");
            Console.WriteLine();

            try
            {
                var salesService = serviceProvider.GetRequiredService<IEntityServiceAsync<SalesRecord>>();
                var salesViewService = serviceProvider.GetRequiredService<IEntityViewServiceAsync<SalesRecordView>>();
                var userService = serviceProvider.GetRequiredService<IEntityServiceAsync<User>>();

                // 创建用户
                var user = new User { UserName = "Bob Johnson", Age = 35, CreateTime = DateTime.Now };
                await userService.InsertAsync(user);

                // 插入 2024 年 11 月的销售记录
                var sale = new SalesRecord
                {
                    ProductId = 3,
                    ProductName = "Keyboard",
                    Amount = 150,
                    SaleTime = new DateTime(2024, 11, 10),
                    SalesUserId = user.Id
                };

                await salesService.InsertAsync(sale);

                Console.WriteLine("✓ 已插入数据到 Sales_202411 表：");
                Console.WriteLine($"  - {sale.ProductName}: ¥{sale.Amount} (2024-11-10)");
                Console.WriteLine();

                // 显式指定查询 Sales_202411 表
                var tableArgs = new[] { "202411" };
                Console.WriteLine("执行查询（显式指定 tableArgs = [\"202411\"]）：");
                Console.WriteLine("  var sales = await salesViewService.SearchAsync(");
                Console.WriteLine("      Expr.Lambda<SalesRecordView>(s => s.Amount > 100),");
                Console.WriteLine("      new[] { \"202411\" }");
                Console.WriteLine("  );");
                Console.WriteLine();

                var sales = await salesViewService.SearchAsync(
                    Expr.Lambda<SalesRecordView>(s => s.Amount > 100),
                    tableArgs
                );

                Console.WriteLine($"✓ 查询完成，共返回 {sales.Count} 条记录（来自 Sales_202411 表）");
                foreach (var s in sales)
                {
                    Console.WriteLine($"  - {s.ProductName}: ¥{s.Amount} (销售员: {s.UserName})");
                }

                Console.WriteLine();
                Console.WriteLine("✅ 演示2完成");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ 演示2失败: {ex.Message}");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示3：动态表参数
        /// </summary>
        private static async System.Threading.Tasks.Task Demo3_MultipleMonthsAsync(IServiceProvider serviceProvider)
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("演示3：动态表参数");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("场景：使用变量动态指定 Sales_202411 或 Sales_202412");
            Console.WriteLine();

            try
            {
                var salesService = serviceProvider.GetRequiredService<IEntityServiceAsync<SalesRecord>>();
                var salesViewService = serviceProvider.GetRequiredService<IEntityViewServiceAsync<SalesRecordView>>();
                var userService = serviceProvider.GetRequiredService<IEntityServiceAsync<User>>();

                // 创建用户
                var user = new User { UserName = "Carol Davis", Age = 40, CreateTime = DateTime.Now };
                await userService.InsertAsync(user);

                // 插入 2024 年 11 月的数据
                var sale1 = new SalesRecord
                {
                    ProductId = 4,
                    ProductName = "Monitor",
                    Amount = 500,
                    SaleTime = new DateTime(2024, 11, 25),
                    SalesUserId = user.Id
                };

                await salesService.InsertAsync(sale1);

                Console.WriteLine("✓ 已插入数据到 Sales_202411 表：");
                Console.WriteLine($"  - {sale1.ProductName}: ¥{sale1.Amount} (2024-11)");
                Console.WriteLine();

                // 使用变量动态指定表参数
                // 注意：tableArgs 是单个参数数组，对应 Sales_{0} 中的 {0} 占位符
                Console.WriteLine("执行查询（动态指定 tableArgs）：");
                Console.WriteLine("  var targetMonth = \"202411\";\n");
                Console.WriteLine("  var sales = await salesViewService.SearchAsync(");
                Console.WriteLine("      Expr.Lambda<SalesRecordView>(s => s.Amount > 400),");
                Console.WriteLine("      new[] { targetMonth }");
                Console.WriteLine("  );");
                Console.WriteLine();

                var targetMonth = "202411";
                var tableArgs = new[] { targetMonth };
                var sales = await salesViewService.SearchAsync(
                    Expr.Lambda<SalesRecordView>(s => s.Amount > 400),
                    tableArgs
                );

                Console.WriteLine($"✓ 查询完成，共返回 {sales.Count} 条记录（来自 Sales_202411 表）");
                foreach (var s in sales)
                {
                    Console.WriteLine($"  - {s.ProductName}: ¥{s.Amount}");
                }

                Console.WriteLine();
                Console.WriteLine("✅ 演示3完成");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ 演示3失败: {ex.Message}");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示4：分表查询结合排序和分页
        /// </summary>
        private static async System.Threading.Tasks.Task Demo4_ShardingWithOrderAsync(IServiceProvider serviceProvider)
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("演示4：分表查询结合排序和分页");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("场景：按金额降序排列，分页查询前 3 条");
            Console.WriteLine();

            try
            {
                var salesService = serviceProvider.GetRequiredService<IEntityServiceAsync<SalesRecord>>();
                var salesViewService = serviceProvider.GetRequiredService<IEntityViewServiceAsync<SalesRecordView>>();
                var userService = serviceProvider.GetRequiredService<IEntityServiceAsync<User>>();

                // 创建用户
                var user = new User { UserName = "David Wilson", Age = 45, CreateTime = DateTime.Now };
                await userService.InsertAsync(user);

                // 插入多条销售记录
                var amounts = new[] { 100, 500, 250, 750, 300, 600, 1000 };
                for (int i = 0; i < amounts.Length; i++)
                {
                    var sale = new SalesRecord
                    {
                        ProductId = 20 + i,
                        ProductName = $"Product {i + 1}",
                        Amount = amounts[i],
                        SaleTime = new DateTime(2024, 12, 5 + i),
                        SalesUserId = user.Id
                    };
                    await salesService.InsertAsync(sale);
                }

                Console.WriteLine($"✓ 已插入 {amounts.Length} 条数据");
                Console.WriteLine();

                // 按金额降序排列，取前 3 条
                var tableArgs = new[] { "202412" };
                Console.WriteLine("执行查询（按金额降序，取前 3 条）：");
                Console.WriteLine("  var topSales = await salesViewService.SearchAsync(");
                Console.WriteLine("      Expr.Where<SalesRecordView>(s => s.Amount > 0)");
                Console.WriteLine("          .OrderBy((\"Amount\", false))");
                Console.WriteLine("          .Section(0, 3),");
                Console.WriteLine("      new[] { \"202412\" }");
                Console.WriteLine("  );");
                Console.WriteLine();

                var topSales = await salesViewService.SearchAsync(
                    Expr.Where<SalesRecordView>(s => s.Amount > 0)
                        .OrderBy(("Amount", false))
                        .Section(0, 3),
                    tableArgs
                );

                Console.WriteLine($"✓ 查询完成，返回前 3 条（按金额降序）：");
                foreach (var sale in topSales)
                {
                    Console.WriteLine($"  - {sale.ProductName}: ¥{sale.Amount}");
                }

                Console.WriteLine();
                Console.WriteLine("✅ 演示4完成");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ 演示4失败: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}
