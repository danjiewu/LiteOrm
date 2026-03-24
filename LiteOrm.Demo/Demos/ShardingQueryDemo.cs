using System;
using System.Collections.Generic;
using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using Microsoft.Extensions.DependencyInjection;
using static LiteOrm.Common.Expr;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 分表查询演示
    /// 
    /// 本演示展示如何使用 LiteOrm 进行分表查询。分表场景中，表名包含参数（如 Sales_{0}），
    /// 通过在 Lambda 表达式中显式指定 TableArgs 来指定要查询的具体分表。
    /// 推荐方式：s => s.TableArgs == new[] { "202412" } && s.Amount > 40
    /// </summary>
    public static class ShardingQueryDemo
    {
        /// <summary>
        /// 运行所有分表演示
        /// </summary>
        public static async Task RunAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║    4. 分表查询演示 (Sharding Query Demo)                   ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

            await Demo1_BasicShardingAsync(factory);
            await Demo2_ExplicitTableArgsAsync(factory);
            await Demo3_ExprArgsAsync(factory);
            await Demo4_ShardingWithOrderAsync(factory);
        }

        /// <summary>
        /// 演示4.1：基础分表查询 - Lambda 内部指定分表参数
        /// </summary>
        private static async Task Demo1_BasicShardingAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示4.1：基础分表查询 - Lambda 内部指定分表参数            │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                var salesService = factory.SalesService;
                var userService = factory.UserService;

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

                DemoHelper.PrintSection("📋 场景说明", "查询金额大于 40 的销售记录，指定查询 Sales_202412 分表");

                DemoHelper.PrintSection("💾 示例数据",
                    $"已插入 2 条销售记录到 Sales_202412 表：\n" +
                    $"  • {sale1.ProductName}: ¥{sale1.Amount} (2024-12-15)\n" +
                    $"  • {sale2.ProductName}: ¥{sale2.Amount} (2024-12-20)");

                DemoHelper.PrintSection("📝 代码实现",
                    "var sales = await salesService.SearchAsync(s =>\n" +
                    "    s.TableArgs == new[] { \"202412\" } && s.Amount > 40\n" +
                    ");");

                // 执行查询并获取 SQL
                var sales = await salesService.SearchAsync(s =>
                    s.TableArgs == new[] { "202412" } && s.Amount > 40
                );

                var executedSql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";

                DemoHelper.PrintSection("🔍 执行的 SQL",
                    executedSql);

                DemoHelper.PrintSection("✅ 查询结果",
                    $"共返回 {sales.Count} 条记录：\n" +
                    string.Join("\n", sales.Select(s => $"  • {s.ProductName}: ¥{s.Amount} (销售员: {s.UserName})")));

                Console.WriteLine("✓ 演示4.1 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示4.1 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示4.2：显式指定参数分表
        /// </summary>
        private static async Task Demo2_ExplicitTableArgsAsync(ServiceFactory factory)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示4.2：显式指定参数分表                                  │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                var salesService = factory.SalesService;
                var userService = factory.UserService;

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

                DemoHelper.PrintSection("📋 场景说明", "查询金额大于 100 的销售记录，指定查询 Sales_202411 分表");

                DemoHelper.PrintSection("💾 示例数据",
                    $"已插入 1 条销售记录到 Sales_202411 表：\n" +
                    $"  • {sale.ProductName}: ¥{sale.Amount} (2024-11-10)");

                DemoHelper.PrintSection("📝 代码实现",
                    "var sales = await salesService.SearchAsync(s => s.Amount > 100,\n" +
                    "    [\"202411\"]\n" +
                    ");");

                var sales = await salesService.SearchAsync(s => s.Amount > 100,
                    ["202411"]
                );

                var executedSql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";

                DemoHelper.PrintSection("🔍 执行的 SQL",
                    executedSql);

                DemoHelper.PrintSection("✅ 查询结果",
                    $"共返回 {sales.Count} 条记录（来自 Sales_202411 表）：\n" +
                    string.Join("\n", sales.Select(s => $"  • {s.ProductName}: ¥{s.Amount} (销售员: {s.UserName})")));

                Console.WriteLine("✓ 演示4.2 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示4.2 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示4.3：Expr 方式分表
        /// </summary>
        private static async Task Demo3_ExprArgsAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示4.3：Expr 方式指定分表参数                             │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                var salesService = factory.SalesService;
                var userService = factory.UserService;

                // 创建用户
                var user = new User { UserName = "Carol Davis", Age = 40, CreateTime = DateTime.Now };
                await userService.InsertAsync(user);

                // 插入 2024 年 11 月的销售记录
                var sale1 = new SalesRecord
                {
                    ProductId = 4,
                    ProductName = "Monitor",
                    Amount = 500,
                    SaleTime = new DateTime(2024, 11, 25),
                    SalesUserId = user.Id
                };
                var sale2 = new SalesRecord
                {
                    ProductId = 5,
                    ProductName = "USB Cable",
                    Amount = 20,
                    SaleTime = new DateTime(2024, 11, 28),
                    SalesUserId = user.Id
                };

                await salesService.InsertAsync(sale1);
                await salesService.InsertAsync(sale2);

                DemoHelper.PrintSection("📋 场景说明", "使用 Expr API 方式指定分表，查询金额大于 100 的销售记录");

                DemoHelper.PrintSection("💾 示例数据",
                    $"已插入 2 条销售记录到 Sales_202411 表：\n" +
                    $"  • {sale1.ProductName}: ¥{sale1.Amount} (2024-11-25)\n" +
                    $"  • {sale2.ProductName}: ¥{sale2.Amount} (2024-11-28)");

                DemoHelper.PrintSection("📝 代码实现",
                    "var sales = await salesService.SearchAsync(\n" +
                    "    From<SalesRecordView>(\"202411\")\n" +
                    "        .Where(Prop(\"Amount\") > 100)\n" +
                    ");");

                var sales = await salesService.SearchAsync(
                    From<SalesRecordView>("202411")
                        .Where(Prop("Amount") > 100)
                );

                var executedSql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";

                DemoHelper.PrintSection("🔍 执行的 SQL",
                    executedSql);

                DemoHelper.PrintSection("✅ 查询结果",
                    $"共返回 {sales.Count} 条记录（来自 Sales_202411 表）：\n" +
                    string.Join("\n", sales.Select(s => $"  • {s.ProductName}: ¥{s.Amount} (销售员: {s.UserName})")));

                Console.WriteLine("✓ 演示4.3 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示4.3 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示4.4：分表查询结合排序和分页
        /// </summary>
        private static async Task Demo4_ShardingWithOrderAsync(ServiceFactory factory)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示4.4：分表查询结合排序和分页                            │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                var salesService = factory.SalesService;
                var userService = factory.UserService;

                // 创建用户
                var user = new User { UserName = "David Wilson", Age = 45, CreateTime = DateTime.Now };
                await userService.InsertAsync(user);

                // 插入多条销售记录到 2024 年 12 月
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

                DemoHelper.PrintSection("📋 场景说明", "使用 Expr API 指定 Sales_202412 分表，按金额降序排列，取前 3 条记录");

                DemoHelper.PrintSection("💾 示例数据",
                    $"已插入 {amounts.Length} 条销售记录到 Sales_202412 表");

                DemoHelper.PrintSection("📝 代码实现",
                    "var topSales = await salesService.SearchAsync(\n" +
                    "    From<SalesRecordView>(\"202412\")\n" +
                    "        .Where(Prop(\"Amount\") > 0)\n" +
                    "        .OrderBy((\"Amount\", false))\n" +
                    "        .Section(0, 3)\n" +
                    ");");

                var topSales = await salesService.SearchAsync(
                    From<SalesRecordView>("202412")
                        .Where(Prop("Amount") > 0)
                        .OrderBy(("Amount", false))
                        .Section(0, 3)
                );

                var executedSql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";

                DemoHelper.PrintSection("🔍 执行的 SQL",
                    executedSql);

                DemoHelper.PrintSection("✅ 查询结果",
                    $"共返回 {topSales.Count} 条记录（按金额降序，来自 Sales_202412 表）：\n" +
                    string.Join("\n", topSales.Select(s => $"  • {s.ProductName}: ¥{s.Amount}")));

                Console.WriteLine("✓ 演示4.4 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示4.4 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }
    }
}
