using LiteOrm.Common;
using LiteOrm.Demo.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Linq;
using System.Text;
using static LiteOrm.Common.Expr;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 演示公共表表达式（CTE / WITH 子句）的使用方式。
    /// CTE 可以将复杂子查询提取为命名的临时结果集，在主查询中多次引用。
    /// </summary>
    public static class CommonTableExprDemo
    {
        public static async Task RunAllAsync(IServiceProvider services)
        {
            Console.WriteLine("\n===== 9. 公共表表达式（CTE）演示 =====");

            var userDataViewDAO = services.GetRequiredService<DataViewDAO<User>>();

            await Demo1_BasicCteAsync(userDataViewDAO);
            await Demo2_CteWithFilteringAsync(userDataViewDAO);
            await Demo3_CteAggregateAsync(userDataViewDAO);
            await Demo4_CteReuseInUnionAsync(userDataViewDAO);
        }

        /// <summary>
        /// 9.1 基础 CTE：将 SELECT 子查询包装为 CTE，再用主查询引用并实际执行
        /// </summary>
        private static async Task Demo1_BasicCteAsync(DataViewDAO<User> userDataViewDAO)
        {
            DemoHelper.PrintSection("9.1 基础 CTE 定义与引用", "");

            try
            {
                var cteDef = new SelectExpr(
                    From(typeof(User)).Where(Prop("Age") >= 18),
                    Prop("Id").As("Id"),
                    Prop("UserName").As("Name"),
                    Prop("Age").As("Age"),
                    Prop("DeptId").As("DeptId")
                );

                var query = cteDef.With("ActiveUsers")
                    .OrderBy(Prop("Age").Desc())
                    .Section(0, 5)
                    .Select(Prop("Name"), Prop("Age"), Prop("DeptId"));

                var dt = await userDataViewDAO.Search(query).GetResultAsync();
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";

                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                DemoHelper.PrintSection("✅ 查询结果（年龄前 5 位）", FormatDataTable(dt, "Name", "Age", "DeptId"));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示9.1 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 9.2 带过滤条件的 CTE：CTE 定义中包含 WHERE，主查询进一步过滤并实际执行
        /// </summary>
        private static async Task Demo2_CteWithFilteringAsync(DataViewDAO<User> userDataViewDAO)
        {
            DemoHelper.PrintSection("9.2 带过滤条件的 CTE 查询", "");

            try
            {
                var cteDef = new SelectExpr(
                    From(typeof(User)).Where(Prop("Age") >= 18),
                    Prop("Id").As("Id"),
                    Prop("UserName").As("Name"),
                    Prop("Age").As("Age")
                );

                var query = cteDef.With("AdultUsers")
                    .Where(Prop("Age") >= 30)
                    .OrderBy(Prop("Name").Asc())
                    .Select(Prop("Name"), Prop("Age"));

                var dt = await userDataViewDAO.Search(query).GetResultAsync();
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";

                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                DemoHelper.PrintSection("✅ 查询结果（30 岁及以上）", FormatDataTable(dt, "Name", "Age"));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示9.2 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 9.3 聚合 CTE：先统计部门成年人数，再在主查询中过滤并排序
        /// </summary>
        private static async Task Demo3_CteAggregateAsync(DataViewDAO<User> userDataViewDAO)
        {
            DemoHelper.PrintSection("9.3 聚合 CTE 实际查询", "");

            try
            {
                var cteDef = From<User>()
                    .Where(Prop("Age") >= 25)
                    .GroupBy(Prop("DeptId"))
                    .Select(
                        Prop("DeptId"),
                        Prop("Id").Count().As("UserCount"),
                        Prop("Age").Avg().As("AvgAge")
                    );

                var query = cteDef.With("DeptAdultStats")
                    .Where(Prop("UserCount") >= 2)
                    .OrderBy(Prop("UserCount").Desc())
                    .Select(Prop("DeptId"), Prop("UserCount"), Prop("AvgAge"));

                var dt = await userDataViewDAO.Search(query).GetResultAsync();
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";

                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                DemoHelper.PrintSection("✅ 查询结果（成年人数 >= 2 的部门）", FormatDataTable(dt, "DeptId", "UserCount", "AvgAge"));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示9.3 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 9.4 在 UNION 查询中复用同一个 CTE 表达式。
        /// </summary>
        private static async Task Demo4_CteReuseInUnionAsync(DataViewDAO<User> userDataViewDAO)
        {
            DemoHelper.PrintSection("9.4 UNION 中复用同一个 CTE", "");

            try
            {
                var adultUsers = From<User>()
                    .Where(Prop("Age") >= 18)
                    .Select(
                        Prop("UserName").As("Name"),
                        Prop("Age").As("Age"),
                        Prop("DeptId").As("DeptId"))
                    .With("AdultUsers");

                var youngerAdults = adultUsers
                    .Where(Prop("Age") < 30)
                    .Select(
                        Prop("Name"),
                        Prop("Age"),
                        Prop("DeptId"),
                        Const("18-29").As("AgeGroup"));

                var olderAdults = adultUsers
                    .Where(Prop("Age") >= 30)
                    .Select(
                        Prop("Name"),
                        Prop("Age"),
                        Prop("DeptId"),
                        Const("30+").As("AgeGroup"));

                var query = youngerAdults.UnionAll(olderAdults);

                var dt = await userDataViewDAO.Search(query).GetResultAsync();
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";

                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                DemoHelper.PrintSection("✅ 查询结果（同一个 CTE 在 UNION 两侧复用）", FormatDataTable(dt, "Name", "Age", "DeptId", "AgeGroup"));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示9.4 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.LastOrDefault() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        private static string FormatDataTable(DataTable dt, params string[] columns)
        {
            if (dt.Rows.Count == 0)
            {
                return "  • 无匹配记录";
            }

            var sb = new StringBuilder();
            foreach (var row in dt.Rows.Cast<DataRow>())
            {
                sb.Append("  • ");
                sb.Append(string.Join(" | ", columns.Select(column => $"{column}: {row[column]}")));
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }
    }
}
