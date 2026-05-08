using LiteOrm.Common;
using LiteOrm.Demo.Models;
using static LiteOrm.Common.Expr;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 演示公共表表达式（CTE / WITH 子句）的使用方式。
    /// CTE 可以将复杂子查询提取为命名的临时结果集，在主查询中多次引用。
    /// </summary>
    public static class CommonTableExprDemo
    {
        public static void RunAll()
        {
            Console.WriteLine("\n===== 9. 公共表表达式（CTE）演示 =====");

            Demo1_BasicCte();
            Demo2_CteWithFiltering();
            Demo3_CteChainPreview();
        }

        /// <summary>
        /// 9.1 基础 CTE：将 SELECT 子查询包装为 CTE，再用主查询引用
        /// </summary>
        private static void Demo1_BasicCte()
        {
            DemoHelper.PrintSection("9.1 基础 CTE 定义与引用", "");

            // 定义 CTE 的 SELECT 查询
            var cteDef = new SelectExpr(
                From(typeof(User)),
                Prop("Id").As("Id"),
                Prop("UserName").As("Name"),
                Prop("Age").As("Age")
            );

            // 使用扩展方法 .With(name) 包装为 CTE
            var query = cteDef.With("ActiveUsers")
                .Select(Prop("Name").As("Name"), Prop("Age").As("Age"));

            var json = System.Text.Json.JsonSerializer.Serialize(query,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            Console.WriteLine("CTE 表达式构建完成：");
            Console.WriteLine($"  - CTE 名称: ActiveUsers");
            Console.WriteLine($"  - CTE 定义: SELECT Id, UserName, Age FROM Users");
            Console.WriteLine("JSON 序列化结果：");
            Console.WriteLine(json);
        }

        /// <summary>
        /// 9.2 带过滤条件的 CTE：CTE 定义中包含 WHERE，主查询进一步过滤
        /// </summary>
        private static void Demo2_CteWithFiltering()
        {
            DemoHelper.PrintSection("9.2 带过滤条件的 CTE 查询", "");

            // CTE：从 Users 表中筛选成年人
            var cteDef = new SelectExpr(
                From(typeof(User)).Where(Prop("Age") > 18),
                Prop("Id").As("Id"),
                Prop("UserName").As("Name"),
                Prop("Age").As("Age")
            );

            // 主查询：从 CTE 中进一步筛选年龄 >= 25 的用户
            var query = cteDef.With("AdultUsers")
                .Where(Prop("Age") >= 25)
                .OrderBy(Prop("Name").Asc());

            Console.WriteLine("表达式结构：");
            Console.WriteLine($"  WITH AdultUsers AS (");
            Console.WriteLine($"    SELECT Id, UserName AS Name, Age");
            Console.WriteLine($"    FROM Users WHERE Age > 18");
            Console.WriteLine($"  )");
            Console.WriteLine($"  SELECT * FROM AdultUsers WHERE Age >= 25 ORDER BY Name ASC");
        }

        /// <summary>
        /// 9.3 CTE 链式预览：展示 Expr.From 配合 SelectExpr 构建 CTE 链
        /// </summary>
        private static void Demo3_CteChainPreview()
        {
            DemoHelper.PrintSection("9.3 CTE 与子查询表达式预览", "");

            // SelectExpr 既可以作为 CTE 定义，也可以直接作为子查询源
            var subquery = new SelectExpr(
                From(typeof(User)).Where(Prop("Age") > 18),
                Prop("Id").As("Id"),
                Prop("UserName").As("Name")
            );

            Console.WriteLine("SelectExpr 可直接序列化为 JSON 用于前后端传输：");
            Console.WriteLine("  - ExprType: CommonTable");
            Console.WriteLine("  - 支持 SqlBuilder.SupportCteExpr 控制是否生成 WITH 子句");
            Console.WriteLine("  - 当 SupportCteExpr = false 时，CTE 将展开为内联子查询");

            // 验证 ExprType
            var cte = new CommonTableExpr(subquery);
            Console.WriteLine($"\nCommonTableExpr.ExprType = {cte.ExprType}");
        }
    }
}
