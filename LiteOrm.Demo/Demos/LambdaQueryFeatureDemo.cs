using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Linq.Expressions;
using static LiteOrm.Common.Expr;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 演示 8.1.13 新增的查询表达能力：
    /// Lambda 内构造查询源（From / FromRelated）、根级聚合函数、表连接（Join / JoinRelated）。
    /// </summary>
    public static class LambdaQueryFeatureDemo
    {
        public static async Task RunAsync(IServiceProvider services)
        {
            Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║    12. Lambda 查询能力：From / 聚合 / Join                 ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

            var factory = services.GetRequiredService<ServiceFactory>();
            var userDataViewDAO = services.GetRequiredService<DataViewDAO<User>>();

            await Demo1_FromOverloadsAsync(factory.UserService);
            await Demo2_FromRelatedAsync(factory.UserService);
            await Demo3_RootAggregateAsync(userDataViewDAO);
            await Demo4_JoinAsync(userDataViewDAO);
            await Demo5_JoinRelatedAsync(userDataViewDAO);
            await Demo6_ScalarSubqueryAsync(factory.UserService);
        }

        /// <summary>
        /// 演示12.1：Expr.From 的两个重载
        /// From(Type) 是唯一可真实调用的工厂方法，返回 TableExpr；
        /// From&lt;T&gt;() 只用于 Lambda 内，直接调用会抛异常。
        /// </summary>
        private static async Task Demo1_FromOverloadsAsync(IUserService userSvc)
        {
            Console.WriteLine("\n┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示12.1：Expr.From 的两个重载                             │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            DemoHelper.PrintSection("📋 场景说明",
                "From(Type) 返回 TableExpr，是链式查询的起点；\n" +
                "From<T>() 返回 IEnumerable<T>，只在 Lambda 表达式内有效，直接调用抛异常。");

            DemoHelper.PrintSection("📝 代码实现",
                "var table = Expr.From(typeof(User));              // TableExpr\n" +
                "var expr  = table.Where(Prop(\"Age\") >= 18);      // 链式过滤\n" +
                "var results = await userSvc.SearchAsync(expr);");

            var table = Expr.From(typeof(User));
            DemoHelper.PrintSection("✅ From(typeof(User))",
                $"返回类型 {table.GetType().Name}，实体 {table.Type?.Name}");

            try
            {
                _ = Expr.From<User>();
                Console.WriteLine("✗ 未抛出预期异常");
            }
            catch (InvalidOperationException ex)
            {
                DemoHelper.PrintSection("🔍 From<T>() 直接调用", $"抛出 {ex.GetType().Name}：{ex.Message}");
            }

            var expr = table.Where(Prop("Age") >= 18);
            var results = await userSvc.SearchAsync(expr);
            var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
            DemoHelper.PrintSection("🔍 执行的 SQL", sql);
            DemoHelper.PrintSection("✅ 查询结果", $"共 {results.Count} 名成年用户");
        }

        /// <summary>
        /// 演示12.2：Expr.FromRelated 在 Lambda 内构造带关联条件的查询源
        /// </summary>
        private static async Task Demo2_FromRelatedAsync(IUserService userSvc)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示12.2：Expr.FromRelated — 自动补关联条件的子查询         │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "FromRelated<T>() 写法与 From<T>() 一致，额外按 [ForeignType] 元数据推断关联条件。\n" +
                    "User.DeptId 关联 Department，解析结果是一层带关联过滤的 WhereExpr。");

                DemoHelper.PrintSection("📝 代码实现",
                    "var expr = Expr.Lambda<User>(u =>\n" +
                    "    Expr.FromRelated<Department>()\n" +
                    "        .Where(d => d.Name == \"研发中心\")\n" +
                    "        .Select(d => d.Id)\n" +
                    "        .Contains((long)u.DeptId!.Value));");

                var expr = Expr.Lambda<User>(u =>
                    Expr.FromRelated<Department>()
                        .Where(d => d.Name == "研发中心")
                        .Select(d => d.Id)
                        .Contains((long)u.DeptId!.Value));

                var results = await userSvc.SearchAsync(expr);
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL（FromRelated 自动补关联）", sql);
                DemoHelper.PrintSection("✅ 查询结果",
                    $"共 {results.Count} 名研发中心成员\n" +
                    (results.Count > 0
                        ? string.Join("\n", results.Take(5).Select(u => $"  • {u.UserName}"))
                        : "  • 无匹配记录"));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示12.2 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示12.3：根级聚合函数
        /// Count / Sum / Average / Max / Min 可直接在 Lambda 内解析。
        /// </summary>
        private static async Task Demo3_RootAggregateAsync(DataViewDAO<User> dataViewDAO)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示12.3：Lambda 根级聚合函数                              │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "Count/Sum/Average/Max/Min 在 Expr.Query 内直接解析成聚合查询，\n" +
                    "根级查询与标量子查询共用同一套解析逻辑。");

                DemoHelper.PrintSection("📝 代码实现",
                    "var count  = Expr.Query<User, int>(q => q.Count());\n" +
                    "var maxAge = Expr.Query<User, int>(q => q.Max(u => u.Age));\n" +
                    "var avgAge = Expr.Query<User, double>(q => q.Average(u => (double)u.Age));");

                var count = Expr.Query<User, int>(q => q.Count());
                var maxAge = Expr.Query<User, int>(q => q.Max(u => u.Age));
                var avgAge = Expr.Query<User, double>(q => q.Average(u => (double)u.Age));
                var minAge = Expr.Query<User, int>(q => q.Min(u => u.Age));
                var sumAge = Expr.Query<User, int>(q => q.Sum(u => u.Age));

                DemoHelper.PrintSection("✅ 生成的聚合列",
                    $"Count   → {DescribeAggregate(count)}\n" +
                    $"Max     → {DescribeAggregate(maxAge)}\n" +
                    $"Average → {DescribeAggregate(avgAge)}\n" +
                    $"Min     → {DescribeAggregate(minAge)}\n" +
                    $"Sum     → {DescribeAggregate(sumAge)}");

                var context = dataViewDAO.CreateSqlBuildContext();
                DemoHelper.PrintSection("🔍 生成的 SQL（Count）", count.ToPreparedSql(context).Sql);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示12.3 失败: {ex.Message}\n");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示12.4：Join 显式连接
        /// Join 构造 FromExpr，TableJoinExpr 只承载 JOIN 段。
        /// </summary>
        private static async Task Demo4_JoinAsync(DataViewDAO<User> dataViewDAO)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示12.4：Join — 显式连接条件                              │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "用户表按 DeptId 连接部门表，查询用户及其部门名称。\n" +
                    "Join 接受显式条件 (u, d) => ...，返回元素元组，可继续 Where/Select。");

                DemoHelper.PrintSection("📝 代码实现",
                    "var query = Expr.Query<User, IEnumerable<(User, Department)>>(u =>\n" +
                    "    From<User>()\n" +
                    "        .Join<User, Department>((x, d) => x.DeptId == d.Id)\n" +
                    "        .Where(t => t.Item1.Age >= 20 && t.Item2.Name != null));");

                var where = (WhereExpr)Expr.Query<User, IEnumerable<(User, Department)>>(u =>
                    Expr.From<User>()
                        .Join<User, Department>((x, d) => x.DeptId == d.Id)
                        .Where(t => t.Item1.Age >= 20 && t.Item2.Name != null));
                var from = (FromExpr)where.Source!;
                string mainAlias = from.Source!.Alias!;
                string joinAlias = from.Joins[0].Source!.Alias!;

                DemoHelper.PrintSection("✅ 表达式结构",
                    $"主表　　：{((TableExpr)from.Source!).Type!.Name}（别名 {mainAlias}）\n" +
                    $"连接表数：{from.Joins.Count}\n" +
                    $"连接类型：{from.Joins[0].JoinType}\n" +
                    $"ON 条件 ：{from.Joins[0].On}\n" +
                    $"WHERE 　 ：{where.Where}");

                var select = where.Select(
                    Prop(mainAlias, "UserName").As("UserName"),
                    Prop(joinAlias, "Name").As("DeptName"),
                    Prop(mainAlias, "Age").As("Age"));

                var dt = await dataViewDAO.Search(select).GetResultAsync();
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                DemoHelper.PrintSection("✅ 查询结果（前 5 行）", FormatDataTable(dt, 5, "UserName", "DeptName", "Age"));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示12.4 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示12.5：JoinRelated 自动推断连接条件
        /// </summary>
        private static async Task Demo5_JoinRelatedAsync(DataViewDAO<User> dataViewDAO)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示12.5：JoinRelated — 自动推断连接条件                   │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "JoinRelated 不写 ON，ORM 按外键元数据自动推断。\n" +
                    "带条件的重载会把自动条件与显式条件按 AND 合并。");

                DemoHelper.PrintSection("📝 代码实现",
                    "var from = Expr.Query<User, IEnumerable<(User, Department)>>(u =>\n" +
                    "    From<User>().JoinRelated<User, Department>());\n" +
                    "var merged = Expr.Query<User, IEnumerable<(User, Department)>>(u =>\n" +
                    "    From<User>().JoinRelated<User, Department>((x, d) => d.Name != null));");

                var from = (FromExpr)Expr.Query<User, IEnumerable<(User, Department)>>(u =>
                    Expr.From<User>().JoinRelated<User, Department>());
                DemoHelper.PrintSection("✅ 自动推断的 ON 条件", from.Joins[0].On?.ToString() ?? "未推断出关联关系");

                var merged = (FromExpr)Expr.Query<User, IEnumerable<(User, Department)>>(u =>
                    Expr.From<User>().JoinRelated<User, Department>((x, d) => d.Name != null));
                DemoHelper.PrintSection("✅ 合并后的 ON 条件（自动 + 显式，AND）",
                    merged.Joins[0].On?.ToString() ?? "未推断出关联关系");

                var select = merged.Select(
                    Prop(merged.Source!.Alias!, "UserName").As("UserName"),
                    Prop(merged.Joins[0].Source!.Alias!, "Name").As("DeptName"));

                var dt = await dataViewDAO.Search(select).GetResultAsync();
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                DemoHelper.PrintSection("✅ 查询结果（前 5 行）", FormatDataTable(dt, 5, "UserName", "DeptName"));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示12.5 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示12.6：标量子查询
        /// 聚合查询作为子表达式参与比较，自动加括号。
        /// </summary>
        private static async Task Demo6_ScalarSubqueryAsync(IUserService userSvc)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示12.6：标量子查询 — 年龄高于全员平均                     │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "聚合查询作为标量子查询嵌入比较条件，\n" +
                    "生成 WHERE Age > (SELECT AVG(Age) FROM Users)。");

                DemoHelper.PrintSection("📝 代码实现",
                    "var expr = Expr.Lambda<User>(u =>\n" +
                    "    u.Age > Expr.From<User>().Average(x => x.Age));\n" +
                    "var results = await userSvc.SearchAsync(expr);");

                var expr = Expr.Lambda<User>(u =>
                    u.Age > Expr.From<User>().Average(x => x.Age));

                var results = await userSvc.SearchAsync(expr);
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL（子查询自动加括号）", sql);
                DemoHelper.PrintSection("✅ 查询结果",
                    $"共 {results.Count} 名用户年龄高于全员平均\n" +
                    (results.Count > 0
                        ? string.Join("\n", results.Take(5).Select(u => $"  • {u.UserName}（年龄: {u.Age}）"))
                        : "  • 无匹配记录"));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示12.6 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        private static string DescribeAggregate(Expr expr)
        {
            if (expr is not SelectExpr select || select.Selects.Count == 0) return "非聚合表达式";
            if (select.Selects[0].Value is not FunctionExpr func) return "非聚合表达式";
            string arg = func.Args.Count > 0 ? func.Args[0].ToString() ?? "-" : "-";
            return $"{func.FunctionName}({arg})，IsAggregate={func.IsAggregate}";
        }

        private static string FormatDataTable(DataTable dt, int maxRows, params string[] columns)
        {
            if (dt.Rows.Count == 0) return "  • 无匹配记录";

            var sb = new System.Text.StringBuilder();
            int count = Math.Min(dt.Rows.Count, maxRows);
            for (int i = 0; i < count; i++)
            {
                var values = columns
                    .Where(dt.Columns.Contains)
                    .Select(c => $"{c}={dt.Rows[i][c]}");
                sb.AppendLine($"  • {string.Join("，", values)}");
            }
            if (dt.Rows.Count > maxRows) sb.Append($"  ...（共 {dt.Rows.Count} 条）");
            return sb.ToString().TrimEnd();
        }
    }
}