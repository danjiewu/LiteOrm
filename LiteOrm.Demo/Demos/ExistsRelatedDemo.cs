using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using static LiteOrm.Common.Expr;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 演示 Expr.ExistsRelated 的使用场景。
    /// ExistsRelated 与 Exists 的区别在于：无需手动指定关联条件，
    /// ORM 根据 [ForeignType] 元数据自动推断 EXISTS 子查询中的 JOIN 字段。
    /// </summary>
    public static class ExistsRelatedDemo
    {
        public static async Task RunAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║    7. ExistsRelated 关联过滤演示                           ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

            var userSvc = factory.UserService;
            var deptSvc = factory.DepartmentService;

            await Demo1_ForwardFilterUsersByDeptAsync(userSvc);
            await Demo2_ForwardFilterDeptsByManagerAsync(deptSvc);
            await Demo3_NotExistsRelatedAsync(userSvc);
            await Demo4_CombinedConditionsAsync(userSvc);
        }

        /// <summary>
        /// 演示7.1：正向关联过滤 — 按部门名称筛选用户
        /// 关系：User.DeptId → [ForeignType(typeof(DepartmentView))]
        /// 自动推断条件：EXISTS (SELECT 1 FROM Departments T1 WHERE T1.Id = T0.DeptId AND ...)
        /// </summary>
        private static async Task Demo1_ForwardFilterUsersByDeptAsync(IUserService userSvc)
        {
            Console.WriteLine("\n┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示7.1：正向关联 — 按部门名称筛选用户                    │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "查询属于「研发中心」的所有用户。\n" +
                    "User.DeptId 通过 [ForeignType(typeof(DepartmentView))] 关联部门表，\n" +
                    "ExistsRelated 自动推断关联条件，无需手动写 T1.Id = T0.DeptId。");

                DemoHelper.PrintSection("📝 代码实现",
                    "var expr = ExistsRelated<DepartmentView>(Prop(\"Name\") == \"研发中心\");\n" +
                    "var results = await userSvc.SearchAsync(expr);");

                var expr = ExistsRelated<DepartmentView>(Prop("Name") == "研发中心");
                var results = await userSvc.SearchAsync(expr);

                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL（自动推断关联条件）", sql);

                DemoHelper.PrintSection("✅ 查询结果",
                    $"共 {results.Count} 名研发中心成员\n" +
                    (results.Count > 0
                        ? string.Join("\n", results.ConvertAll(u => $"  • {u.UserName}（年龄: {u.Age}，部门: {u.DeptName}）"))
                        : "  • 无匹配记录"));

                Console.WriteLine("✓ 演示7.1 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示7.1 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示7.2：正向关联过滤 — 按负责人年龄筛选部门
        /// 关系：Department.ManagerId → [ForeignType(typeof(User))]
        /// 自动推断条件：EXISTS (SELECT 1 FROM Users T1 WHERE T1.Id = T0.ManagerId AND ...)
        /// </summary>
        private static async Task Demo2_ForwardFilterDeptsByManagerAsync(IDepartmentService deptSvc)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示7.2：正向关联 — 按负责人年龄筛选部门                  │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "查询负责人年龄大于 35 岁的部门。\n" +
                    "Department.ManagerId 通过 [ForeignType(typeof(User))] 关联用户表，\n" +
                    "ExistsRelated 自动推断 EXISTS 子查询的关联条件 T1.Id = T0.ManagerId。");

                DemoHelper.PrintSection("📝 代码实现",
                    "var expr = ExistsRelated<User>(Prop(\"Age\") > 35);\n" +
                    "var results = await deptSvc.SearchAsync(expr);");

                var expr = ExistsRelated<User>(Prop("Age") > 35);
                var results = await deptSvc.SearchAsync(expr);

                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL（自动推断关联条件）", sql);

                DemoHelper.PrintSection("✅ 查询结果",
                    $"共 {results.Count} 个部门的负责人年龄 > 35\n" +
                    (results.Count > 0
                        ? string.Join("\n", results.ConvertAll(d => $"  • {d.Name}（负责人: {d.ManagerName}）"))
                        : "  • 无匹配记录"));

                Console.WriteLine("✓ 演示7.2 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示7.2 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示7.3：NOT ExistsRelated — 筛选不属于特定部门的用户
        /// </summary>
        private static async Task Demo3_NotExistsRelatedAsync(IUserService userSvc)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示7.3：NOT ExistsRelated — 筛选非研发部门用户            │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "查询不属于任何「研发」相关部门的用户（部门名称不以「研」开头）。\n" +
                    "对 ExistsRelated 取反，生成 NOT EXISTS 子查询。");

                DemoHelper.PrintSection("📝 代码实现",
                    "var expr = Not(ExistsRelated<DepartmentView>(Prop(\"Name\").StartsWith(\"研\")));\n" +
                    "var results = await userSvc.SearchAsync(expr);");

                var expr = Not(ExistsRelated<DepartmentView>(Prop("Name").StartsWith("研")));
                var results = await userSvc.SearchAsync(expr);

                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL（NOT EXISTS）", sql);

                DemoHelper.PrintSection("✅ 查询结果",
                    $"共 {results.Count} 名非研发部门用户\n" +
                    (results.Count > 0
                        ? string.Join("\n", results.Take(5).Select(u => $"  • {u.UserName}（部门: {u.DeptName ?? "无"}）"))
                          + (results.Count > 5 ? $"\n  ...（共 {results.Count} 条）" : "")
                        : "  • 无匹配记录"));

                Console.WriteLine("✓ 演示7.3 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示7.3 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示7.4：ExistsRelated 与其他条件组合
        /// 在 ExistsRelated 基础上叠加年龄过滤
        /// </summary>
        private static async Task Demo4_CombinedConditionsAsync(IUserService userSvc)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示7.4：ExistsRelated 与其他条件组合                      │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "查询属于「市场部」且年龄大于 25 岁的用户。\n" +
                    "将 ExistsRelated 条件与普通字段条件通过 & 运算符组合使用。");

                DemoHelper.PrintSection("📝 代码实现",
                    "var expr = ExistsRelated<DepartmentView>(Prop(\"Name\") == \"市场部\")\n" +
                    "         & (Prop(\"Age\") > 25);\n" +
                    "var results = await userSvc.SearchAsync(expr);");

                var expr = ExistsRelated<DepartmentView>(Prop("Name") == "市场部")
                         & (Prop("Age") > 25);
                var results = await userSvc.SearchAsync(expr);

                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL（ExistsRelated + 普通条件）", sql);

                DemoHelper.PrintSection("✅ 查询结果",
                    $"共 {results.Count} 名市场部且年龄 > 25 的用户\n" +
                    (results.Count > 0
                        ? string.Join("\n", results.ConvertAll(u => $"  • {u.UserName}（年龄: {u.Age}）"))
                        : "  • 无匹配记录"));

                Console.WriteLine("✓ 演示7.4 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示7.4 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }
    }
}
