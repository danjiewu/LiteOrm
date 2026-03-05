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
    /// 通过在 Lambda 表达式中显式指定 TableArgs 来指定要查询的具体分表。
    /// 推荐方式：s => ((IArged)s).TableArgs == new[] { "202412" } && s.Amount > 40
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
            await Demo3_DynamicTableArgsAsync(serviceProvider);
            await Demo4_ShardingWithOrderAsync(serviceProvider);
        }

        /// <summary>
        /// 演示1：基础分表查询 - Lambda 内部指定分表参数
        /// </summary>
        private static async System.Threading.Tasks.Task Demo1_BasicShardingAsync(IServiceProvider serviceProvider)
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("演示1：基础分表查询 - Lambda 内部指定分表参数");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("场景：在 Lambda 表达式中明确指定查询的分表");
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

                // 查询方式：Lambda 内部指定分表参数
                Console.WriteLine("执行查询（Lambda 内部指定 TableArgs）：");
                Console.WriteLine("  var sales = await salesViewService.SearchAsync(s =>");
                Console.WriteLine("      ((IArged)s).TableArgs == new[] { \"202412\" } && s.Amount > 40");
                Console.WriteLine("  );");
                Console.WriteLine();

                var sales = await salesViewService.SearchAsync(s =>
                    ((IArged)s).TableArgs == new[] { "202412" } && s.Amount > 40
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
        /// 演示2：Lambda 内部显式指定不同月份的分表
        /// </summary>
        private static async System.Threading.Tasks.Task Demo2_ExplicitTableArgsAsync(IServiceProvider serviceProvider)
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("演示2：Lambda 内部显式指定不同月份的分表");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("场景：在 Lambda 中显式指定查询 Sales_202411 表的数据");
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

                // 在 Lambda 中指定查询 Sales_202411 表
                Console.WriteLine("执行查询（Lambda 内部指定分表）：");
                Console.WriteLine("  var sales = await salesViewService.SearchAsync(s =>");
                Console.WriteLine("      ((IArged)s).TableArgs == new[] { \"202411\" } && s.Amount > 100");
                Console.WriteLine("  );");
                Console.WriteLine();

                var sales = await salesViewService.SearchAsync(s =>
                    ((IArged)s).TableArgs == new[] { "202411" } && s.Amount > 100
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
        /// 演示3：Lambda 内部动态指定分表参数
        /// </summary>
        private static async System.Threading.Tasks.Task Demo3_DynamicTableArgsAsync(IServiceProvider serviceProvider)
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("演示3：Lambda 内部动态指定分表参数");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("场景：使用变量在 Lambda 中动态指定分表");
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

                // 使用变量动态指定分表（Lambda 内部方式）
                Console.WriteLine("执行查询（动态指定分表）：");
                Console.WriteLine("  var targetMonth = \"202411\";\n");
                Console.WriteLine("  var sales = await salesViewService.SearchAsync(s =>");
                Console.WriteLine("      ((IArged)s).TableArgs == new[] { targetMonth } && s.Amount > 400");
                Console.WriteLine("  );");
                Console.WriteLine();

                var targetMonth = "202411";
                var sales = await salesViewService.SearchAsync(s =>
                    ((IArged)s).TableArgs == new[] { targetMonth } && s.Amount > 400
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
        /// 演示4：分表查询结合排序和分页（Lambda 内部分表）
        /// </summary>
        private static async System.Threading.Tasks.Task Demo4_ShardingWithOrderAsync(IServiceProvider serviceProvider)
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("演示4：分表查询结合排序和分页（Lambda 内部分表）");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("场景：在 Lambda 中指定分表，并结合排序和分页");
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

                // 在 Lambda 中指定分表，结合排序和分页
                Console.WriteLine("执行查询（Lambda 内指定分表，按金额降序，取前 3 条）：");
                Console.WriteLine("  var topSales = await salesViewService.SearchAsync(");
                Console.WriteLine("      Expr.From<SalesRecordView>([\"202412\"])");
                Console.WriteLine("          .Where(Expr.Prop(\"Amount\") > 0)");
                Console.WriteLine("          .OrderBy((\"Amount\", false))");
                Console.WriteLine("          .Section(0, 3)");
                Console.WriteLine("  );");
                Console.WriteLine();

                var topSales = await salesViewService.SearchAsync(
                    Expr.From<SalesRecordView>(["202412"])
                        .Where(Expr.Prop("Amount") > 0)
                        .OrderBy(("Amount", false))
                        .Section(0, 3)
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
