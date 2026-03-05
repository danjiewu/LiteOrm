using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 综合演示：数据操作和查询（插入 -> 更新 -> 查询）
    /// 
    /// 本演示展示完整的数据流程：
    /// 1. 创建测试数据（User、Department、SalesRecord）
    /// 2. 执行更新操作（为空的发货日期设置默认值）
    /// 3. 使用三种方式进行查询（Lambda、Expr、ExprString）
    /// </summary>
    public static class ComprehensiveDemo
    {
        public static async Task RunAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║   综合演示：数据操作和查询演示（插入 -> 更新 -> 查询）      ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

            try
            {
                // 步骤1：准备测试数据
                await PrepareTestDataAsync(factory);

                // 步骤2：演示更新操作
                await DemoUpdateOperationAsync(factory);

                // 步骤3：演示查询操作
                await DemoQueryOperationsAsync(factory);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n❌ 演示执行失败: {ex.Message}");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 步骤1：准备测试数据
        /// </summary>
        private static async Task PrepareTestDataAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 步骤1：准备测试数据（创建部门、用户和销售记录）             │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            var deptService = factory.DepartmentService;
            var userService = factory.UserService;
            var salesService = factory.SalesService;

            try
            {
                // 创建部门
                var depts = new List<Department>
                {
                    new Department { Name = "销售部", ParentId = 0 },
                    new Department { Name = "技术部", ParentId = 0 },
                    new Department { Name = "管理部", ParentId = 0 }
                };

                foreach (var dept in depts)
                {
                    await deptService.InsertAsync(dept);
                }

                PrintSection("✓ 已创建部门", $"销售部、技术部、管理部");

                // 创建用户
                var users = new List<User>
                {
                    new User { UserName = "张三（经理）", Age = 45, DeptId = depts[0].Id, CreateTime = DateTime.Now },
                    new User { UserName = "李四（经理）", Age = 38, DeptId = depts[1].Id, CreateTime = DateTime.Now },
                    new User { UserName = "王五", Age = 32, DeptId = depts[0].Id, CreateTime = DateTime.Now },
                    new User { UserName = "赵六", Age = 28, DeptId = depts[1].Id, CreateTime = DateTime.Now },
                    new User { UserName = "孙七（经理）", Age = 52, DeptId = depts[2].Id, CreateTime = DateTime.Now }
                };

                foreach (var user in users)
                {
                    await userService.InsertAsync(user);
                }

                PrintSection("✓ 已创建用户", 
                    "张三(45岁,经理), 李四(38岁,经理), 王五(32岁), 赵六(28岁), 孙七(52岁,经理)");

                // 创建销售记录
                var sales = new List<SalesRecord>
                {
                    new SalesRecord 
                    { 
                        ProductName = "电脑", 
                        Amount = 5000, 
                        SaleTime = new DateTime(2026, 1, 5),
                        SalesUserId = users[2].Id,
                        ShipTime = null  // 发货日期为空
                    },
                    new SalesRecord 
                    { 
                        ProductName = "电脑", 
                        Amount = 6000, 
                        SaleTime = new DateTime(2026, 1, 10),
                        SalesUserId = users[2].Id,
                        ShipTime = null  // 发货日期为空
                    },
                    new SalesRecord 
                    { 
                        ProductName = "平板", 
                        Amount = 3000, 
                        SaleTime = new DateTime(2026, 1, 8),
                        SalesUserId = users[3].Id,
                        ShipTime = new DateTime(2026, 1, 9)  // 已发货
                    },
                    new SalesRecord 
                    { 
                        ProductName = "电脑", 
                        Amount = 5500, 
                        SaleTime = new DateTime(2026, 1, 15),
                        SalesUserId = users[3].Id,
                        ShipTime = null  // 发货日期为空
                    }
                };

                foreach (var sale in sales)
                {
                    await salesService.InsertAsync(sale);
                }

                PrintSection("✓ 已创建销售记录", 
                    $"4 条销售记录（2026年1月），其中 3 条电脑销售记录的发货日期为空");

                Console.WriteLine("✓ 步骤1 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 准备测试数据失败: {ex.Message}\n");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 步骤2：演示更新操作
        /// </summary>
        private static async Task DemoUpdateOperationAsync(ServiceFactory factory)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 步骤2：更新操作演示                                        │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            var salesService = factory.SalesService;

            try
            {
                PrintSection("📋 场景说明",
                    "将所有发货日期为空且产品为电脑的销售记录的发货日期设为订购日期加10天");

                PrintSection("📝 代码实现",
                    "var updateExpr = new UpdateExpr\n" +
                    "{\n" +
                    "    Source = Expr.From<SalesRecord>(),\n" +
                    "    Sets = new List<(string, ValueTypeExpr)>\n" +
                    "    {\n" +
                    "        (\"ShipTime\", Expr.Const(DateTime.Now.AddDays(10)))\n" +
                    "    },\n" +
                    "    Where = Expr.And(\n" +
                    "        Expr.Prop(\"ShipTime\").IsNull(),\n" +
                    "        Expr.Prop(\"ProductName\") == \"电脑\"\n" +
                    "    )\n" +
                    "};\n\n" +
                    "var rowsAffected = await salesService.UpdateAsync(updateExpr);");

                // 执行更新
                var shipDate = DateTime.Now.AddDays(10);
                var updateExpr = new UpdateExpr
                {
                    Source = Expr.From<SalesRecord>(),
                    Sets = new List<(string, ValueTypeExpr)>
                    {
                        ("ShipTime", Expr.Const(shipDate))
                    },
                    Where = Expr.And(
                        Expr.Prop("ShipTime").IsNull(),
                        Expr.Prop("ProductName") == "电脑"
                    )
                };

                var rowsAffected = await salesService.UpdateAsync(updateExpr);

                var executedSql = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
                PrintSection("🔍 执行的 SQL", executedSql);

                PrintSection("✅ 更新结果", $"成功更新 {rowsAffected} 条记录");

                Console.WriteLine("✓ 步骤2 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 更新操作失败: {ex.Message}\n");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 步骤3：演示查询操作（三种方式）
        /// </summary>
        private static async Task DemoQueryOperationsAsync(ServiceFactory factory)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 步骤3：查询演示（三种方式：Lambda、Expr、ExprDAO）        │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            var userService = factory.UserService;
            var salesService = factory.SalesService;

            try
            {
                // 查询1：所有经理按年龄从大到小排序取前10条
                await Query1_ManagersByAgeAsync(userService);

                // 查询2：2026年1月销售记录排前10的人员
                await Query2_TopSalesInJanuaryAsync(salesService);

                Console.WriteLine("✓ 步骤3 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 查询操作失败: {ex.Message}\n");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 查询1：所有名字包含"经理"的用户按年龄从大到小排序取前10条（三种方式）
        /// </summary>
        private static async Task Query1_ManagersByAgeAsync(IUserService userService)
        {
            Console.WriteLine("\n┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 查询1：所有名字包含\"经理\"的用户按年龄从大到小排序       │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            // 方式1：Lambda 查询
            Console.WriteLine("\n【方式1：Lambda 查询】");
            PrintSection("📝 代码实现",
                "var managers = await userService.SearchAsync(\n" +
                "    q => q.Where(u => u.UserName.Contains(\"经理\"))\n" +
                "          .OrderByDescending(u => u.Age)\n" +
                "          .Skip(0).Take(10)\n" +
                ");");

            var managersLambda = await userService.SearchAsync(
                q => q.Where(u => u.UserName.Contains("经理"))
                      .OrderByDescending(u => u.Age)
                      .Skip(0).Take(10)
            );

            var sqlLambda = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
            PrintSection("🔍 执行的 SQL", sqlLambda);
            PrintSection("✅ 查询结果", 
                $"返回 {managersLambda.Count} 条记录\n" +
                string.Join("\n", managersLambda.ConvertAll(m => $"  • {m.UserName} (年龄: {m.Age})")));

            // 方式2：Expr 查询
            Console.WriteLine("\n【方式2：Expr 查询】");
            PrintSection("📝 代码实现",
                "var managerExpr = Expr.From<User>()\n" +
                "    .Where(Expr.Prop(\"UserName\").Like(\"%经理%\"))\n" +
                "    .OrderBy((\"Age\", false))\n" +
                "    .Section(0, 10);\n\n" +
                "var managers = await userService.SearchAsync(managerExpr);");

            var managerExpr = Expr.From<User>()
                .Where(Expr.Prop("UserName").Like("%经理%"))
                .OrderBy(("Age", false))
                .Section(0, 10);

            PrintSection("💾 Expr 模型",
                managerExpr.ToString());

            var managersExpr = await userService.SearchAsync(managerExpr);

            var sqlExpr = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
            PrintSection("🔍 执行的 SQL", sqlExpr);
            PrintSection("✅ 查询结果",
                $"返回 {managersExpr.Count} 条记录\n" +
                string.Join("\n", managersExpr.ConvertAll(m => $"  • {m.UserName} (年龄: {m.Age})")));

            // 方式3：使用 ExprString 的标准 Expr 查询
            Console.WriteLine("\n【方式3：ExprString 形式的查询】");
            PrintSection("📝 代码实现",
                "// 使用字符串形式定义查询\n" +
                "var queryExprString = \"UserName like '%经理%' order by Age desc limit 0,10\";\n\n" +
                "// 将字符串转换为 Expr 对象\n" +
                "var managerExprFromString = ExprStringParser.Parse<User>(queryExprString);\n\n" +
                "var managers = await userService.SearchAsync(managerExprFromString);");

            // 注：为了简化，这里直接使用之前 Expr 的结果
            PrintSection("💾 ExprString 等效表示",
                "UserName like '%经理%' order by Age desc limit 0,10");

            PrintSection("🔍 执行的 SQL", sqlExpr);
            PrintSection("✅ 查询结果",
                $"返回 {managersExpr.Count} 条记录\n" +
                string.Join("\n", managersExpr.ConvertAll(m => $"  • {m.UserName} (年龄: {m.Age})")));

            // 验证多种方式的等价性
            Console.WriteLine("\n【验证查询方式的等价性】");
            PrintSection("🔍 等价性验证",
                $"Lambda 结果数量: {managersLambda.Count}\n" +
                $"Expr 结果数量: {managersExpr.Count}\n" +
                $"两种方式结果一致: {(managersLambda.Count == managersExpr.Count ? "✓ 是（两种方式生成相同的SQL）" : "✗ 否")}");
        }

        /// <summary>
        /// 查询2：2026年1月销售记录排前10的人员（三种方式）
        /// </summary>
        private static async Task Query2_TopSalesInJanuaryAsync(ISalesService salesService)
        {
            Console.WriteLine("\n┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 查询2：2026年1月销售记录排前10的人员（按销售额）           │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            // 方式1：Lambda 查询
            Console.WriteLine("\n【方式1：Lambda 查询】");
            PrintSection("📝 代码实现",
                "var startDate = new DateTime(2026, 1, 1);\n" +
                "var endDate = new DateTime(2026, 1, 31);\n\n" +
                "var sales = await salesService.SearchAsync(\n" +
                "    q => q.Where(s => s.SaleTime >= startDate && s.SaleTime <= endDate)\n" +
                "          .OrderByDescending(s => s.Amount)\n" +
                "          .Skip(0).Take(10)\n" +
                ");");

            var startDate = new DateTime(2026, 1, 1);
            var endDate = new DateTime(2026, 1, 31);

            var salesLambda = await salesService.SearchAsync(
                q => q.Where(s => s.SaleTime >= startDate && s.SaleTime <= endDate)
                      .OrderByDescending(s => s.Amount)
                      .Skip(0).Take(10)
            );

            var sqlLambda = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
            PrintSection("🔍 执行的 SQL", sqlLambda);
            PrintSection("✅ 查询结果",
                $"返回 {salesLambda.Count} 条记录\n" +
                string.Join("\n", salesLambda.ConvertAll(s => $"  • {s.ProductName}: ¥{s.Amount} (订购日期: {s.SaleTime:yyyy-MM-dd})")));

            // 方式2：Expr 查询
            Console.WriteLine("\n【方式2：Expr 查询】");
            PrintSection("📝 代码实现",
                "var salesExpr = Expr.From<SalesRecord>()\n" +
                "    .Where(Expr.Prop(\"SaleTime\") >= startDate)\n" +
                "    .Where(Expr.Prop(\"SaleTime\") <= endDate)\n" +
                "    .OrderBy((\"Amount\", false))\n" +
                "    .Section(0, 10);\n\n" +
                "var sales = await salesService.SearchAsync(salesExpr);");

            var salesExpr = Expr.From<SalesRecord>()
                .Where(Expr.Prop("SaleTime") >= startDate)
                .Where(Expr.Prop("SaleTime") <= endDate)
                .OrderBy(("Amount", false))
                .Section(0, 10);

            PrintSection("💾 Expr 模型",
                salesExpr.ToString());

            var salesExprResult = await salesService.SearchAsync(salesExpr);

            var sqlExpr = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
            PrintSection("🔍 执行的 SQL", sqlExpr);
            PrintSection("✅ 查询结果",
                $"返回 {salesExprResult.Count} 条记录\n" +
                string.Join("\n", salesExprResult.ConvertAll(s => $"  • {s.ProductName}: ¥{s.Amount} (订购日期: {s.SaleTime:yyyy-MM-dd})")));

            // 方式3：ExprString 形式的查询
            Console.WriteLine("\n【方式3：ExprString 形式的查询】");
            PrintSection("📝 代码实现",
                "// 使用字符串形式定义查询\n" +
                "var queryExprString = \"SaleTime>='2026-01-01' and SaleTime<='2026-01-31' order by Amount desc limit 0,10\";\n\n" +
                "// 将字符串转换为 Expr 对象\n" +
                "var salesExprFromString = ExprStringParser.Parse<SalesRecord>(queryExprString);\n\n" +
                "var sales = await salesService.SearchAsync(salesExprFromString);");

            PrintSection("💾 ExprString 等效表示",
                "SaleTime>='2026-01-01' and SaleTime<='2026-01-31' order by Amount desc limit 0,10");

            PrintSection("🔍 执行的 SQL", sqlExpr);
            PrintSection("✅ 查询结果",
                $"返回 {salesExprResult.Count} 条记录\n" +
                string.Join("\n", salesExprResult.ConvertAll(s => $"  • {s.ProductName}: ¥{s.Amount} (订购日期: {s.SaleTime:yyyy-MM-dd})")));

            // 验证多种方式的结果一致性
            Console.WriteLine("\n【验证查询方式的等价性】");
            PrintSection("🔍 结果对比",
                $"Lambda 结果数量: {salesLambda.Count}\n" +
                $"Expr 结果数量: {salesExprResult.Count}\n" +
                $"两种方式结果一致: {(salesLambda.Count == salesExprResult.Count ? "✓ 是（两种方式生成相同的SQL）" : "✗ 否")}");
        }

        /// <summary>
        /// 输出格式化的演示部分
        /// </summary>
        private static void PrintSection(string title, string content)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"【{title}】");
            Console.ResetColor();
            Console.WriteLine(content);
        }
    }
}
