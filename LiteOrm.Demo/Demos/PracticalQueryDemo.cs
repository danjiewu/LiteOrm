using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace LiteOrm.Demo.Demos
{
    public static class PracticalQueryDemo
    {
        public static async Task RunAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("  5. 综合查询实践：从 Lambda 到 SQL");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            // 1. 准备动态条件
            int minAge = 18;
            string searchName = "王";
            var userSvc = factory.UserService;

            // 方式 1: 完整的 Lambda 表达式演示 (Where + OrderBy + Skip/Take)
            // 这种方式最接近 EF/LINQ 习惯，框架会自动转换为 Expr 模型
            Console.WriteLine("[1] 完整 Lambda 链式查询 (推荐)");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("    var results = await userSvc.SearchAsync(\n" +
                              "        q => q.Where(u => u.Age >= minAge && u.UserName.Contains(searchName))\n" +
                              "              .OrderByDescending(u => u.Id)\n" +
                              "              .Skip(0).Take(10)\n" +
                              "    );");
            Console.ResetColor();

            var resultsA = await userSvc.SearchAsync(
                q => q.Where(u => u.Age >= minAge && u.UserName.Contains(searchName))
                      .OrderByDescending(u => u.Id)
                      .Skip(0).Take(10)
            );
            Console.WriteLine($"    → 查询完成，返回 {resultsA.Count} 条记录。");

            resultsA = await userSvc.SearchAsync(
                new SectionExpr(0, 3)
            );

            // 方式 2: 最简单的 Expression 扩展查询
            // 如果只有简单的过滤，可以直接传入 Expression<Func<T, bool>>
            Console.WriteLine("\n[2] 基础 Expression 扩展查询");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("    var results = await userSvc.SearchAsync(u => u.Age >= minAge);");
            Console.ResetColor();

            var resultsC = await userSvc.SearchAsync(u => u.Age >= minAge);
            Console.WriteLine($"    → 查询完成，返回 {resultsC.Count} 条记录。");

            // 3. 构建并输出最终 SQL 模型预览
            var queryModel = Expr.Query<User>(
                    q => q.Where(u => u.Age >= minAge && u.UserName.Contains(searchName))
                          .OrderByDescending(u => u.Id)
                          .Skip(0).Take(10)
            );

            Console.WriteLine("\n[3] 框架生成的逻辑模型 (JSON 序列化后可跨端传递):");
            Console.WriteLine($"> 逻辑模型预览: {queryModel}");

            // 4. 直接构建 SQL 表达式（不推荐，除非你非常熟悉框架内部结构）
            Console.ResetColor();
            Console.WriteLine("\n[4] 直接构建 SQL 表达式 (不推荐，除非你熟悉内部结构)");
            var sqlModel = new SelectExpr(
                new WhereExpr(
                    Expr.From<UserView>(),
                    Expr.Prop("Age") > minAge & Expr.Prop("UserName", LogicOperator.Like, $"%{searchName}%")
                    & Expr.Foreign("Dept",
                        Expr.Prop(nameof(Department.Name), LogicOperator.Like, $"%部%")
                        & Expr.Prop(nameof(Department.ParentId)).In(Expr.From<User>().GroupBy(nameof(User.DeptId)).Having(AggregateFunctionExpr.Count >= Expr.Const(3)).Select(nameof(User.DeptId)))
                    )
                )
             );
            Console.WriteLine("直接构建 SQL 表达式 : new SelectExpr(\r\n" +
    "                new WhereExpr(\r\n" +
    "                    Expr.From<UserView>(),\r\n" +
    "                    Expr.Property(\"Age\") > minAge & Expr.Property(\"UserName\", LogicOperator.Like, $\"%{searchName}%\")\r\n" +
    "                    & Expr.Foreign(\"Dept\",\r\n" +
    "                        Expr.Property(nameof(Department.Name), LogicOperator.Like, $\"%技术部%\")\r\n" +
    "                        & Expr.Property(nameof(Department.ParentId))\r\n" +
    "                        .In(Expr.From<User>().GroupBy(nameof(User.DeptId)).Having(AggregateFunctionExpr.Count > Expr.Const(3)).Select(nameof(User.DeptId)))\r\n" +
    "                    )\r\n" +
    "                )\r\n" +
    "             )");
            Console.WriteLine($"Expr 内容：{sqlModel}\n");
            Console.WriteLine($"序列化后 JSON：{JsonSerializer.Serialize(sqlModel, new JsonSerializerOptions { WriteIndented = true })}\n");

            var resultD = await userSvc.SearchAsync(sqlModel);
            Console.WriteLine($"查询完成，Sql：{SessionManager.Current?.SqlStack?.Last()}\n 返回 {resultD.Count} 条记录。");
        }
    }
}
