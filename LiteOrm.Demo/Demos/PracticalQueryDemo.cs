using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 综合查询实践演示
    /// 展示 Lambda 查询、Expr 模型、序列化和等价性验证
    /// </summary>
    public static class PracticalQueryDemo
    {
        public static async Task RunAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║    2. 综合查询实践：从 Lambda 到 SQL                       ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

            var userSvc = factory.UserService;

            await Demo1_LambdaChainQueryAsync(userSvc);
            await Demo2_ExprSerializationAsync(userSvc);
            await Demo3_ExprEquivalenceAsync(userSvc);
            await Demo4_ComplexFilterAsync(userSvc);
        }

        /// <summary>
        /// 演示2.1：Lambda 链式查询（推荐方式）
        /// </summary>
        private static async Task Demo1_LambdaChainQueryAsync(IUserService userSvc)
        {
            Console.WriteLine("\n┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示2.1：Lambda 链式查询（推荐方式）                       │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                var minAge = 18;
                var searchName = "王";

                DemoHelper.PrintSection("📋 场景说明",
                    "使用 Lambda 表达式进行链式查询，包括 WHERE、ORDER BY 和分页");

                DemoHelper.PrintSection("📝 代码实现",
                    "var results = await userSvc.SearchAsync(\n" +
                    "    q => q.Where(u => u.Age >= minAge && u.UserName.Contains(searchName))\n" +
                    "          .OrderByDescending(u => u.Id)\n" +
                    "          .Skip(0).Take(10)\n" +
                    ");");

                var results = await userSvc.SearchAsync(
                    q => q.Where(u => u.Age >= minAge && u.UserName.Contains(searchName))
                          .OrderByDescending(u => u.Id)
                          .Skip(0).Take(10)
                );

                var executedSql = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", executedSql);

                DemoHelper.PrintSection("✅ 查询结果",
                    $"共返回 {results.Count} 条记录\n" +
                    (results.Count > 0 ? string.Join("\n", results.ConvertAll(r => $"  • {r.UserName} (年龄: {r.Age})")) : "  • 无匹配记录"));

                Console.WriteLine("✓ 演示2.1 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示2.1 失败: {ex.Message}\n");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示2.2：Expr 模型序列化和反序列化
        /// </summary>
        private static async Task Demo2_ExprSerializationAsync(IUserService userSvc)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示2.2：Expr 模型序列化和反序列化                         │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "将查询表达式序列化为 JSON，可用于跨服务传递或存储");

                var minAge = 20;
                var searchName = "李";

                DemoHelper.PrintSection("📝 代码实现",
                    "// 构建 Expr 模型\n" +
                    "var expr = Expr.From<User>()\n" +
                    "    .Where(Expr.Prop(\"Age\") >= minAge)\n" +
                    "    .Where(Expr.Prop(\"UserName\").Like($\"%{searchName}%\"))\n" +
                    "    .OrderBy((\"Id\", false))\n" +
                    "    .Section(0, 5);\n\n" +
                    "// 序列化为 JSON\n" +
                    "var json = JsonSerializer.Serialize(expr);\n\n" +
                    "// 反序列化\n" +
                    "var deserializedExpr = JsonSerializer.Deserialize<SqlSegmentExpr>(json);");

                // 构建 Expr 模型
                var expr = Expr.From<User>()
                    .Where(Expr.Prop("Age") >= minAge)
                    .Where(Expr.Prop("UserName").Like($"%{searchName}%"))
                    .OrderBy(("Id", false))
                    .Section(0, 5);

                DemoHelper.PrintSection("💾 序列化前的 Expr",
                    expr.ToString());

                // 序列化为 JSON
                var json = JsonSerializer.Serialize(expr, new JsonSerializerOptions { WriteIndented = false });
                DemoHelper.PrintSection("📄 序列化后的 JSON（示意）",
                    json.Length > 200 ? json.Substring(0, 200) + "..." : json);

                // 执行查询
                var results = await userSvc.SearchAsync(expr);

                var executedSql = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", executedSql);

                DemoHelper.PrintSection("✅ 查询结果",
                    $"共返回 {results.Count} 条记录");

                Console.WriteLine("✓ 演示2.2 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示2.2 失败: {ex.Message}\n");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示2.3：Lambda 和 Expr 的等价性验证
        /// </summary>
        private static async Task Demo3_ExprEquivalenceAsync(IUserService userSvc)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示2.3：Lambda 和 Expr 的等价性验证                       │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "验证两种方式构建的查询是否生成相同的 SQL");

                DemoHelper.PrintSection("📝 代码实现",
                    "// 方式1：Lambda 表达式\n" +
                    "var lambdaExpr = q => q.Where(u => u.Age > 25);\n\n" +
                    "// 方式2：Expr 模型\n" +
                    "var exprModel = Expr.From<User>()\n" +
                    "    .Where(Expr.Prop(\"Age\") > 25);\n\n" +
                    "// 验证等价性\n" +
                    "var lambdaExprConverted = LambdaExprConverter.ToSqlSegment(lambdaExpr);\n" +
                    "bool isEquivalent = lambdaExprConverted.Equals(exprModel);");

                // 方式1：Lambda 表达式
                System.Linq.Expressions.Expression<System.Func<System.Linq.IQueryable<User>,
                    System.Linq.IQueryable<User>>> lambdaExpr = q => q.Where(u => u.Age > 25);

                // 方式2：Expr 模型
                var exprModel = Expr.From<User>().As("User")
                    .Where(Expr.Prop("Age") > 25);

                // 验证等价性
                var lambdaExprConverted = LambdaExprConverter.ToSqlSegment(lambdaExpr);
                bool isEquivalent = lambdaExprConverted.Equals(exprModel);

                DemoHelper.PrintSection("🔍 等价性验证结果",
                    $"Lambda 转换后的 Expr：{lambdaExprConverted}\n\n" +
                    $"手动构建的 Expr：{exprModel}\n\n" +
                    $"是否等价：{(isEquivalent ? "✓ 是（两者生成相同的 SQL）" : "✗ 否（结构不完全相同）")}");

                Console.WriteLine("✓ 演示2.3 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示2.3 失败: {ex.Message}\n");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示2.4：复杂过滤条件组合
        /// </summary>
        private static async Task Demo4_ComplexFilterAsync(IUserService userSvc)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示2.4：复杂过滤条件组合                                  │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                var minAge = 25;
                var maxAge = 50;
                var searchName = "张";

                DemoHelper.PrintSection("📋 场景说明",
                    "组合多个过滤条件：年龄范围、名字包含、排序和分页");

                DemoHelper.PrintSection("📝 代码实现",
                    "var results = await userSvc.SearchAsync(\n" +
                    "    q => q.Where(u => u.Age >= minAge && u.Age <= maxAge)\n" +
                    "          .Where(u => u.UserName.Contains(searchName))\n" +
                    "          .OrderBy(u => u.Age)\n" +
                    "          .ThenBy(u => u.UserName)\n" +
                    "          .Skip(0).Take(5)\n" +
                    ");");

                var results = await userSvc.SearchAsync(
                    q => q.Where(u => u.Age >= minAge && u.Age <= maxAge)
                          .Where(u => u.UserName.Contains(searchName))
                          .OrderBy(u => u.Age)
                          .ThenBy(u => u.UserName)
                          .Skip(0).Take(5)
                );

                var executedSql = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", executedSql);

                DemoHelper.PrintSection("✅ 查询结果",
                    $"共返回 {results.Count} 条记录\n" +
                    (results.Count > 0 ? string.Join("\n", results.ConvertAll(r =>
                        $"  • {r.UserName} (年龄: {r.Age})")) : "  • 无匹配记录"));

                Console.WriteLine("✓ 演示2.4 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示2.4 失败: {ex.Message}\n");
                Console.ResetColor();
            }
        }
    }
}
