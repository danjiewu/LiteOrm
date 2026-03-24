using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using System.Linq.Expressions;
using static LiteOrm.Common.Expr;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 演示 ToString(format) 扩展支持：
    /// Lambda 中的 DateTime.ToString("format") 会被自动转换为数据库原生的日期格式函数。
    /// MySQL  → DATE_FORMAT(col, '%Y-%m-%d')
    /// SQLite → strftime('%Y-%m-%d', col)
    /// Oracle / PostgreSQL → TO_CHAR(col, 'YYYY-MM-DD')
    /// SQL Server → FORMAT(col, 'yyyy-MM-dd')
    /// </summary>
    public static class DateFormatDemo
    {
        public static async Task RunAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║    8. ToString(format) 日期格式化演示                      ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

            var userSvc = factory.UserService;

            await PrepareTestDataAsync(userSvc);

            await Demo1_DirectFunctionExprAsync(userSvc);
            await Demo2_LambdaToStringInWhereAsync(userSvc);

            await userSvc.DeleteAsync(u => u.UserName != null && u.UserName.StartsWith("DateFormatDemo_"));
        }

        private static async Task PrepareTestDataAsync(IUserService userSvc)
        {
            await userSvc.DeleteAsync(u => u.UserName != null && u.UserName.StartsWith("DateFormatDemo_"));
            await userSvc.InsertAsync(new User { UserName = "DateFormatDemo_Alice", Age = 25, CreateTime = new DateTime(2024, 6, 15, 10, 30, 0) });
            await userSvc.InsertAsync(new User { UserName = "DateFormatDemo_Bob",   Age = 30, CreateTime = new DateTime(2024, 12, 25, 8, 0, 0) });
            Console.WriteLine("  测试数据已就绪（Alice=2024-06-15, Bob=2024-12-25）");
        }

        /// <summary>
        /// 演示8.1：使用 FunctionExpr 直接构造格式化条件，等价于 WHERE DATE_FORMAT(CreateTime, '%Y-%m-%d') = '2024-06-15'
        /// </summary>
        private static async Task Demo1_DirectFunctionExprAsync(IUserService userSvc)
        {
            Console.WriteLine("\n┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示8.1：FunctionExpr 直接构造 Format 过滤条件             │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "用 FunctionExpr(\"Format\", Prop(\"CreateTime\"), new ValueExpr(\"yyyy-MM-dd\"))\n" +
                    "构造日期格式化条件，按「仅日期部分」精确匹配，忽略时间差异。");

                DemoHelper.PrintSection("📝 代码实现",
                    "var formatExpr = new FunctionExpr(\"Format\", Prop(\"CreateTime\"), new ValueExpr(\"yyyy-MM-dd\"));\n" +
                    "var results = await userSvc.SearchAsync(formatExpr == \"2024-06-15\");");

                var formatExpr = new FunctionExpr("Format", Prop("CreateTime"), new ValueExpr("yyyy-MM-dd"));
                var results = await userSvc.SearchAsync(formatExpr == "2024-06-15");

                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                DemoHelper.PrintSection("✅ 查询结果",
                    $"共 {results.Count} 条记录\n" +
                    (results.Count > 0
                        ? string.Join("\n", results.ConvertAll(u => $"  • {u.UserName}（CreateTime: {u.CreateTime:yyyy-MM-dd HH:mm:ss}）"))
                        : "  • 无匹配记录"));

                Console.WriteLine("✓ 演示8.1 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示8.1 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示8.2：Lambda 中直接使用 DateTime.ToString("format")，ORM 自动识别并转换为对应数据库函数。
        /// </summary>
        private static async Task Demo2_LambdaToStringInWhereAsync(IUserService userSvc)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示8.2：Lambda u.CreateTime.ToString(\"format\") 自动转换  │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "在 Lambda WHERE 条件中直接写 u.CreateTime.ToString(\"yyyy-MM-dd\")，\n" +
                    "ORM 自动将其转换为当前数据库的原生格式函数，无需手工构造 FunctionExpr。");

                DemoHelper.PrintSection("📝 代码实现",
                    "Expression<Func<UserView, bool>> where =\n" +
                    "    u => u.CreateTime.ToString(\"yyyy-MM-dd\") == \"2024-12-25\";\n" +
                    "var results = await userSvc.SearchAsync(where);");

                Expression<Func<UserView, bool>> where =
                    u => u.CreateTime.ToString("yyyy-MM-dd") == "2024-12-25";
                var results = await userSvc.SearchAsync(where);

                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL（Lambda 自动转换）", sql);
                DemoHelper.PrintSection("✅ 查询结果",
                    $"共 {results.Count} 条记录\n" +
                    (results.Count > 0
                        ? string.Join("\n", results.ConvertAll(u => $"  • {u.UserName}（CreateTime: {u.CreateTime:yyyy-MM-dd HH:mm:ss}）"))
                        : "  • 无匹配记录"));

                Console.WriteLine("✓ 演示8.2 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示8.2 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }
    }
}
