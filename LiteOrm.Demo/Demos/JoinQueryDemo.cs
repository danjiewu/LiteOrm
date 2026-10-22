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
    /// 可以一路链式追加，结果元组始终平铺，用 <c>Item1</c>…<c>Item7</c> 访问。
    /// </para>
    /// </summary>
    public static class JoinQueryDemo
    {
        public static async Task RunAsync(IServiceProvider services)
        {
            Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║    11. Join 多表连接查询演示                               ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

            var userDAO = services.GetRequiredService<ObjectViewDAO<User>>();

            await Demo1_ExplicitJoinAsync(userDAO);
            await Demo2_JoinRelatedAsync(userDAO);
            await Demo3_ChainedJoinAsync(userDAO);
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
                    "Join<T2> 只写右表类型，连接条件由 (左表, 右表) 两个参数给出。");

                DemoHelper.PrintSection("📝 代码实现",
                    "var query = Expr.From<User>()\n" +
                    "    .Join<Department>((u, d) => u.DeptId == d.Id)\n" +
                    "    .Where(x => x.Item1.Age >= 30)\n" +
                    "    .Select(x => new JoinResultRow\n" +
                    "    {\n" +
                    "        UserName = x.Item1.UserName,\n" +
                    "        Age = x.Item1.Age,\n" +
                    "        DepartmentName = x.Item2.Name\n" +
                    "    });\n" +
                    "var rows = await userDAO.SearchAs(query).GetResultAsync();");

                var query = Expr.From<User>()
                    .Join<Department>((u, d) => u.DeptId == d.Id)
                    .Where(x => x.Item1.Age >= 30)
                    .Select(x => new JoinResultRow
                    {
                        UserName = x.Item1.UserName,
                        Age = x.Item1.Age,
                        DepartmentName = x.Item2.Name
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
                    "    .Where(x => x.Item2.Name.Contains(\"研发\"))\n" +
                    "    .Select(x => new JoinResultRow\n" +
                    "    {\n" +
                    "        UserName = x.Item1.UserName,\n" +
                    "        Age = x.Item1.Age,\n" +
                    "        DepartmentName = x.Item2.Name\n" +
                    "    });\n" +
                    "var rows = await userDAO.SearchAs(query).GetResultAsync();");

                var query = Expr.From<User>()
                    .JoinRelated<Department>(TableJoinType.Inner)
                    .Where(x => x.Item2.Name.Contains("研发"))
                    .Select(x => new JoinResultRow
                    {
                        UserName = x.Item1.UserName,
                        Age = x.Item1.Age,
                        DepartmentName = x.Item2.Name
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
        /// 演示11.3：链式追加连接，一次连接三张表
        /// </summary>
        private static async Task Demo3_ChainedJoinAsync(ObjectViewDAO<User> userDAO)
        {
            Console.WriteLine("\n┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示11.3：链式 Join — 用户 + 部门 + 上级部门              │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "每次 Join<TNext> 只追加一张右表，返回下一元数的查询源：\n" +
                    "QuerySource<User> → Join<Department> → QuerySource<User, Department>\n" +
                    "                 → Join<Department> → QuerySource<User, Department, Department>\n" +
                    "连接条件的参数一表一个：Join<T3> 的 lambda 参数依次为 T1、T2、T3；\n" +
                    "结果元组平铺，x.Item1 / x.Item2 / x.Item3 分别对应三张表。");

                DemoHelper.PrintSection("📝 代码实现",
                    "var query = Expr.From<User>()\n" +
                    "    .Join<Department>((u, d) => u.DeptId == d.Id)\n" +
                    "    .Join<Department>((u, d, parent) => d.ParentId == parent.Id)\n" +
                    "    .Where(x => x.Item1.Age >= 25)\n" +
                    "    .Select(x => new JoinResultRow\n" +
                    "    {\n" +
                    "        UserName = x.Item1.UserName,\n" +
                    "        Age = x.Item1.Age,\n" +
                    "        DepartmentName = x.Item2.Name,\n" +
                    "        ParentDepartmentName = x.Item3.Name\n" +
                    "    });\n" +
                    "var rows = await userDAO.SearchAs(query).GetResultAsync();");

                var query = Expr.From<User>()
                    .Join<Department>((u, d) => u.DeptId == d.Id)
                    .Join<Department>((u, d, parent) => d.ParentId == parent.Id)
                    .Where(x => x.Item1.Age >= 25)
                    .Select(x => new JoinResultRow
                    {
                        UserName = x.Item1.UserName,
                        Age = x.Item1.Age,
                        DepartmentName = x.Item2.Name,
                        ParentDepartmentName = x.Item3.Name
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
    }
}
