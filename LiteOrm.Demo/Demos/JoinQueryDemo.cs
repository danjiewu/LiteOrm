using LiteOrm.Common;
using LiteOrm.Demo.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using static LiteOrm.Common.Expr;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// Join 多表连接查询演示。
    /// <para>
    /// <c>Expr.From&lt;T1&gt;()</c> 返回 <c>QuerySource&lt;T1&gt;</c>，其上的 <c>Join&lt;T2&gt;</c> / <c>JoinRelated&lt;T2&gt;</c>
    /// 是实例方法，只写右表类型；每次连接返回下一元数的查询源（<c>QuerySource&lt;T1, T2&gt;</c> …），
    /// 可以一路链式追加。连接条件的参数一表一个，结果元组始终平铺。
    /// </para>
    /// <para>
    /// 元组查询源上的 <c>Where</c> / <c>Select</c> / <c>OrderBy</c> / <c>ThenBy</c> / <c>GroupBy</c>
    /// 支持多参数 Lambda（如 <c>(u, d) =&gt; ...</c>），无需再写 <c>x.Item1</c> / <c>x.Item2</c>。
    /// </para>
    /// </summary>
    public static class JoinQueryDemo
    {
        public static async Task RunAsync(IServiceProvider services)
        {
            Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║    11. Join 多表连接查询演示（多参数 Lambda）              ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

            var userDAO = services.GetRequiredService<ObjectViewDAO<User>>();

            await Demo1_ExplicitJoinAsync(userDAO);
            await Demo2_JoinRelatedAsync(userDAO);
            await Demo3_ChainedJoinAsync(userDAO);
            await Demo4_OrderByAsync(userDAO);
            await Demo5_GroupByAsync(userDAO);
        }

        /// <summary>
        /// 演示11.1：显式条件的 Join（用户 + 部门）
        /// </summary>
        private static async Task Demo1_ExplicitJoinAsync(ObjectViewDAO<User> userDAO)
        {
            Console.WriteLine("\n┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示11.1：显式条件 Join — 用户 + 部门                      │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "连接 Users 与 Departments，列出年龄不小于 30 的用户及其部门名称。\n" +
                    "Join<T2> 只写右表类型，连接条件由 (左表, 右表) 两个参数给出。\n" +
                    "Where / Select 直接使用 (u, d) 多参数 Lambda，无需再访问 Item1 / Item2。");

                DemoHelper.PrintSection("📝 代码实现",
                    "var query = Expr.From<User>()\n" +
                    "    .Join<Department>((u, d) => u.DeptId == d.Id)\n" +
                    "    .Where((u, d) => u.Age >= 30)\n" +
                    "    .Select((u, d) => new JoinResultRow\n" +
                    "    {\n" +
                    "        UserName = u.UserName,\n" +
                    "        Age = u.Age,\n" +
                    "        DepartmentName = d.Name\n" +
                    "    });\n" +
                    "var rows = await userDAO.SearchAs(query).GetResultAsync();");

                var query = Expr.From<User>()
                    .Join<Department>((u, d) => u.DeptId == d.Id)
                    .Where((u, d) => u.Age >= 30)
                    .Select((u, d) => new JoinResultRow
                    {
                        UserName = u.UserName,
                        Age = u.Age,
                        DepartmentName = d.Name
                    });

                var rows = await userDAO.SearchAs(query).GetResultAsync();

                DemoHelper.PrintSection("🔍 执行的 SQL",
                    SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用");

                DemoHelper.PrintSection("✅ 查询结果",
                    $"共返回 {rows.Count} 条记录\n" +
                    (rows.Count > 0
                        ? string.Join("\n", rows.ConvertAll(r => $"  • {r.UserName}（年龄: {r.Age}，部门: {r.DepartmentName}）"))
                        : "  • 无匹配记录"));

                Console.WriteLine("✓ 演示11.1 完成\n");
            }
            catch (Exception ex)
            {
                PrintError("11.1", ex);
            }
        }

        /// <summary>
        /// 演示11.2：JoinRelated 按外键元数据自动推断条件，并指定内连接
        /// </summary>
        private static async Task Demo2_JoinRelatedAsync(ObjectViewDAO<User> userDAO)
        {
            Console.WriteLine("\n┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示11.2：JoinRelated 自动推断关联 + 指定连接类型          │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "User.DeptId 已用 [ForeignType(typeof(Department))] 声明关联，\n" +
                    "JoinRelated<Department>() 无需写 ON 条件即可自动推断；\n" +
                    "第二个参数可指定 Inner / Left / Right / Full 连接类型。");

                DemoHelper.PrintSection("📝 代码实现",
                    "var query = Expr.From<User>()\n" +
                    "    .JoinRelated<Department>(TableJoinType.Inner)\n" +
                    "    .Where((u, d) => d.Name.Contains(\"研发\"))\n" +
                    "    .Select((u, d) => new JoinResultRow\n" +
                    "    {\n" +
                    "        UserName = u.UserName,\n" +
                    "        Age = u.Age,\n" +
                    "        DepartmentName = d.Name\n" +
                    "    });\n" +
                    "var rows = await userDAO.SearchAs(query).GetResultAsync();");

                var query = Expr.From<User>()
                    .JoinRelated<Department>(TableJoinType.Inner)
                    .Where((u, d) => d.Name.Contains("研发"))
                    .Select((u, d) => new JoinResultRow
                    {
                        UserName = u.UserName,
                        Age = u.Age,
                        DepartmentName = d.Name
                    });

                var rows = await userDAO.SearchAs(query).GetResultAsync();

                DemoHelper.PrintSection("🔍 执行的 SQL",
                    SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用");

                DemoHelper.PrintSection("✅ 查询结果（研发相关部门成员）",
                    $"共返回 {rows.Count} 条记录\n" +
                    (rows.Count > 0
                        ? string.Join("\n", rows.ConvertAll(r => $"  • {r.UserName}（年龄: {r.Age}，部门: {r.DepartmentName}）"))
                        : "  • 无匹配记录"));

                Console.WriteLine("✓ 演示11.2 完成\n");
            }
            catch (Exception ex)
            {
                PrintError("11.2", ex);
            }
        }

        /// <summary>
        /// 演示11.3：链式 JoinRelated 自动推断，一次连接三张表
        /// </summary>
        private static async Task Demo3_ChainedJoinAsync(ObjectViewDAO<User> userDAO)
        {
            Console.WriteLine("\n┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示11.3：链式 JoinRelated — 用户 + 部门 + 上级部门        │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "JoinRelated 会按外键元数据自动推断每一段的 ON 条件，并优先从最近连接的表继续匹配。\n" +
                    "第一段：User.DeptId → Department.Id；\n" +
                    "第二段：Department.ParentId → Department.Id（上级部门）。\n" +
                    "Where / Select 仍可直接写 (u, d, parent) 三个参数。");

                DemoHelper.PrintSection("📝 代码实现",
                    "var query = Expr.From<User>()\n" +
                    "    .JoinRelated<Department>()\n" +
                    "    .JoinRelated<Department>()\n" +
                    "    .Where((u, d, parent) => u.Age >= 25)\n" +
                    "    .Select((u, d, parent) => new JoinResultRow\n" +
                    "    {\n" +
                    "        UserName = u.UserName,\n" +
                    "        Age = u.Age,\n" +
                    "        DepartmentName = d.Name,\n" +
                    "        ParentDepartmentName = parent.Name\n" +
                    "    });\n" +
                    "var rows = await userDAO.SearchAs(query).GetResultAsync();");

                var query = Expr.From<User>()
                    .JoinRelated<Department>()
                    .JoinRelated<Department>()
                    .Where((u, d, parent) => u.Age >= 25)
                    .Select((u, d, parent) => new JoinResultRow
                    {
                        UserName = u.UserName,
                        Age = u.Age,
                        DepartmentName = d.Name,
                        ParentDepartmentName = parent.Name
                    });

                var rows = await userDAO.SearchAs(query).GetResultAsync();

                DemoHelper.PrintSection("🔍 执行的 SQL",
                    SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用");

                DemoHelper.PrintSection("✅ 查询结果",
                    $"共返回 {rows.Count} 条记录\n" +
                    (rows.Count > 0
                        ? string.Join("\n", rows.ConvertAll(r =>
                            $"  • {r.UserName}（年龄: {r.Age}，部门: {r.DepartmentName}，上级部门: {r.ParentDepartmentName ?? "—"}）"))
                        : "  • 无匹配记录"));

                Console.WriteLine("✓ 演示11.3 完成\n");
            }
            catch (Exception ex)
            {
                PrintError("11.3", ex);
            }
        }

        /// <summary>
        /// 演示11.4：多参数排序（OrderBy / ThenByDescending）
        /// </summary>
        private static async Task Demo4_OrderByAsync(ObjectViewDAO<User> userDAO)
        {
            Console.WriteLine("\n┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示11.4：多参数排序 — OrderBy + ThenByDescending          │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "OrderBy / ThenBy / ThenByDescending 支持多参数 Lambda，\n" +
                    "排序键直接引用各表字段：先按部门名称升序，再按年龄降序。\n" +
                    "排序完成后仍可用多参数 Select 投影。");

                DemoHelper.PrintSection("📝 代码实现",
                    "var query = Expr.From<User>()\n" +
                    "    .Join<Department>((u, d) => u.DeptId == d.Id, TableJoinType.Inner)\n" +
                    "    .Where((u, d) => u.Age >= 25)\n" +
                    "    .OrderBy((u, d) => d.Name)\n" +
                    "    .ThenByDescending((u, d) => u.Age)\n" +
                    "    .Select((u, d) => new JoinResultRow\n" +
                    "    {\n" +
                    "        UserName = u.UserName,\n" +
                    "        Age = u.Age,\n" +
                    "        DepartmentName = d.Name\n" +
                    "    });\n" +
                    "var rows = await userDAO.SearchAs(query).GetResultAsync();");

                var query = Expr.From<User>()
                    .Join<Department>((u, d) => u.DeptId == d.Id, TableJoinType.Inner)
                    .Where((u, d) => u.Age >= 25)
                    .OrderBy((u, d) => d.Name)
                    .ThenByDescending((u, d) => u.Age)
                    .Select((u, d) => new JoinResultRow
                    {
                        UserName = u.UserName,
                        Age = u.Age,
                        DepartmentName = d.Name
                    });

                var rows = await userDAO.SearchAs(query).GetResultAsync();

                DemoHelper.PrintSection("🔍 执行的 SQL",
                    SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用");

                DemoHelper.PrintSection("✅ 查询结果（先部门升序，再年龄降序）",
                    $"共返回 {rows.Count} 条记录\n" +
                    (rows.Count > 0
                        ? string.Join("\n", rows.ConvertAll(r => $"  • {r.DepartmentName} / {r.UserName}（年龄: {r.Age}）"))
                        : "  • 无匹配记录"));

                Console.WriteLine("✓ 演示11.4 完成\n");
            }
            catch (Exception ex)
            {
                PrintError("11.4", ex);
            }
        }

        /// <summary>
        /// 演示11.5：多参数分组（GroupBy + OrderBy(g => g.Key) + 聚合）
        /// </summary>
        private static async Task Demo5_GroupByAsync(ObjectViewDAO<User> userDAO)
        {
            Console.WriteLine("\n┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示11.5：多参数分组 — GroupBy + 聚合统计                  │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "GroupBy 支持多参数 Lambda，这里按部门名称分组；\n" +
                    "分组结果的 Key 可继续参与 OrderBy，聚合函数在 Select 中统计每组用户数。");

                DemoHelper.PrintSection("📝 代码实现",
                    "var query = Expr.From<User>()\n" +
                    "    .Join<Department>((u, d) => u.DeptId == d.Id, TableJoinType.Inner)\n" +
                    "    .Where((u, d) => u.Age >= 25)\n" +
                    "    .GroupBy((u, d) => d.Name)\n" +
                    "    .OrderBy(g => g.Key)\n" +
                    "    .Select(g => new JoinGroupRow\n" +
                    "    {\n" +
                    "        DepartmentName = g.Key,\n" +
                    "        UserCount = g.Count()\n" +
                    "    });\n" +
                    "var rows = await userDAO.SearchAs(query).GetResultAsync();");

                var query = Expr.From<User>()
                    .Join<Department>((u, d) => u.DeptId == d.Id, TableJoinType.Inner)
                    .Where((u, d) => u.Age >= 25)
                    .GroupBy((u, d) => d.Name)
                    .OrderBy(g => g.Key)
                    .Select(g => new JoinGroupRow
                    {
                        DepartmentName = g.Key,
                        UserCount = g.Count()
                    });

                var rows = await userDAO.SearchAs(query).GetResultAsync();

                DemoHelper.PrintSection("🔍 执行的 SQL",
                    SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用");

                DemoHelper.PrintSection("✅ 查询结果（按部门分组统计）",
                    $"共 {rows.Count} 个分组\n" +
                    (rows.Count > 0
                        ? string.Join("\n", rows.ConvertAll(r => $"  • {r.DepartmentName}: {r.UserCount} 人"))
                        : "  • 无匹配记录"));

                Console.WriteLine("✓ 演示11.5 完成\n");
            }
            catch (Exception ex)
            {
                PrintError("11.5", ex);
            }
        }

        private static void PrintError(string demoName, Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ 演示{demoName} 失败: {ex.Message}\n");
            var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
            DemoHelper.PrintSection("🔍 执行的 SQL", sql);
            Console.ResetColor();
        }

        /// <summary>
        /// Join 投影结果。投影按列名映射，所以属性名要与 Select 中给出的别名一致。
        /// </summary>
        private sealed class JoinResultRow
        {
            public string? UserName { get; set; }

            public int Age { get; set; }

            public string? DepartmentName { get; set; }

            public string? ParentDepartmentName { get; set; }
        }

        /// <summary>
        /// Join 分组统计结果。属性名与 Select 中的别名一致。
        /// </summary>
        private sealed class JoinGroupRow
        {
            public string? DepartmentName { get; set; }

            public int UserCount { get; set; }
        }
    }
}
