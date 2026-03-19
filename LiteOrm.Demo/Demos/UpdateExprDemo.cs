using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using static LiteOrm.Common.Expr;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 演示 UpdateExpr 的多种构建方式与应用场景
    /// </summary>
    public static class UpdateExprDemo
    {
        public static async Task RunAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║    6. UpdateExpr 更新表达式演示                            ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

            var userSvc = factory.UserService;

            await PrepareTestDataAsync(userSvc);

            await Demo1_SetsListInitAsync(userSvc);
            await Demo2_ConstructorAndSetChainAsync(userSvc);
            await Demo3_ArithmeticExprAsync(userSvc);
            await Demo4_FunctionExprAsync(userSvc);
            await Demo5_LambdaWhereMultiFieldAsync(userSvc);
            await Demo6_SubQuerySetAsync(userSvc);

            await userSvc.DeleteAsync(u => u.UserName != null && u.UserName.StartsWith("UpdateDemo_"));
        }

        private static async Task PrepareTestDataAsync(IUserService userSvc)
        {
            await userSvc.DeleteAsync(u => u.UserName != null && u.UserName.StartsWith("UpdateDemo_"));
            await userSvc.InsertAsync(new User { UserName = "UpdateDemo_Alice", Age = 20, CreateTime = DateTime.Now });
            await userSvc.InsertAsync(new User { UserName = "UpdateDemo_Bob",   Age = 30, CreateTime = DateTime.Now });
            await userSvc.InsertAsync(new User { UserName = "UpdateDemo_Carol", Age = 25, CreateTime = DateTime.Now });
            Console.WriteLine("  测试数据已就绪（Alice=20, Bob=30, Carol=25）");
        }

        /// <summary>
        /// 演示6.1：Sets 列表直接初始化方式
        /// </summary>
        private static async Task Demo1_SetsListInitAsync(IUserService userSvc)
        {
            Console.WriteLine("\n┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示6.1：Sets 列表直接初始化                               │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "使用对象初始化器显式设置 Source、Where 和 Sets 列表，适合动态构建更新字段的场景");

                DemoHelper.PrintSection("📝 代码实现",
                    "var update = new UpdateExpr\n" +
                    "{\n" +
                    "    Source = From<User>(),\n" +
                    "    Where  = Prop(\"UserName\") == \"UpdateDemo_Alice\",\n" +
                    "    Sets   = new List<(PropertyExpr, ValueTypeExpr)>\n" +
                    "    {\n" +
                    "        (Prop(\"Age\"), Const(28))\n" +
                    "    }\n" +
                    "};");

                var update = new UpdateExpr
                {
                    Source = From<User>(),
                    Where  = Prop("UserName") == "UpdateDemo_Alice",
                    Sets   = new List<(PropertyExpr, ValueTypeExpr)>
                    {
                        (Prop("Age"), Const(28))
                    }
                };

                int affected = await userSvc.UpdateAsync(update);

                var sql = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                DemoHelper.PrintSection("✅ 结果", $"受影响行数: {affected}（Alice.Age 20 → 28）");

                Console.WriteLine("✓ 演示6.1 完成\n");
            }
            catch (Exception ex)
            {                
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示6.1 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示6.2：构造函数 + Set 扩展方法链式调用
        /// </summary>
        private static async Task Demo2_ConstructorAndSetChainAsync(IUserService userSvc)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示6.2：构造函数 + Set 扩展方法链式调用                   │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "通过构造函数传入 Source 和 Where，再用 .Set() 扩展方法附加 SET 子句，语法简洁流畅");

                DemoHelper.PrintSection("📝 代码实现",
                    "var update = new UpdateExpr(From<User>(), Prop(\"UserName\") == \"UpdateDemo_Bob\")\n" +
                    "    .Set((\"Age\", Const(35)));");

                var update = new UpdateExpr(From<User>(), Prop("UserName") == "UpdateDemo_Bob")
                    .Set(("Age", Const(35)));

                int affected = await userSvc.UpdateAsync(update);

                var sql = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                DemoHelper.PrintSection("✅ 结果", $"受影响行数: {affected}（Bob.Age 30 → 35）");

                Console.WriteLine("✓ 演示6.2 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示6.2 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示6.3：算术运算表达式（Age = Age + 5）
        /// </summary>
        private static async Task Demo3_ArithmeticExprAsync(IUserService userSvc)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示6.3：算术运算表达式（Age = Age + 5）                   │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "利用运算符重载将 PropertyExpr 与 Const 组合，生成 SET Age = Age + 5 的 SQL，无需手写 SQL");

                DemoHelper.PrintSection("📝 代码实现",
                    "var update = new UpdateExpr(From<User>(), Prop(\"UserName\") == \"UpdateDemo_Carol\")\n" +
                    "    .Set((\"Age\", Prop(\"Age\") + Const(5)));");

                var update = new UpdateExpr(From<User>(), Prop("UserName") == "UpdateDemo_Carol")
                    .Set(("Age", Prop("Age") + Const(5)));

                int affected = await userSvc.UpdateAsync(update);

                var sql = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                DemoHelper.PrintSection("✅ 结果", $"受影响行数: {affected}（Carol.Age 25 → 30）");

                Console.WriteLine("✓ 演示6.3 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示6.3 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示6.4：在 SET 子句中嵌入数据库函数（FunctionExpr）
        /// </summary>
        private static async Task Demo4_FunctionExprAsync(IUserService userSvc)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示6.4：在 SET 子句中嵌入数据库函数（FunctionExpr）       │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "在 SET 子句中使用 FunctionExpr 调用数据库内置函数，展示 UpdateExpr 与函数表达式的集成能力");

                DemoHelper.PrintSection("📝 代码实现",
                    "var update = new UpdateExpr(From<User>(), Prop(\"UserName\") == \"UpdateDemo_Bob\")\n" +
                    "    .Set((\"UserName\", Func(\"CONCAT\", Prop(\"UserName\"), Const(\"_v2\"))));");

                var update = new UpdateExpr(From<User>(), Prop("UserName") == "UpdateDemo_Bob")
                    .Set(("UserName", Func("CONCAT", Prop("UserName"), Const("_v2"))));

                int affected = await userSvc.UpdateAsync(update);

                var sql = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                DemoHelper.PrintSection("✅ 结果", $"受影响行数: {affected}（Bob.UserName → UpdateDemo_Bob_v2）");

                Console.WriteLine("✓ 演示6.4 完成\n");
            }
            catch (Exception ex)
            {                
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示6.4 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示6.6：SELECT 子查询作为 SET 值
        /// </summary>
        private static async Task Demo6_SubQuerySetAsync(IUserService userSvc)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示6.6：SELECT 子查询作为 SET 值                          │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                // 场景1：聚合子查询 ─ SET Age = (SELECT AVG(Age) FROM Users WHERE UserName LIKE 'UpdateDemo_%')
                DemoHelper.PrintSection("📋 场景1 说明",
                    "聚合子查询作为 SET 值：将 Alice 的 Age 更新为所有 UpdateDemo 用户的平均年龄");

                DemoHelper.PrintSection("📝 场景1 代码",
                    "// MySQL 禁止子查询的 FROM 与被更新目标表相同，用派生表包裹规避\n" +
                    "// 生成：SET Age = (SELECT T2.avg_age FROM (SELECT AVG(T1.Age) AS avg_age FROM Users T1 WHERE ...) T2)\n" +
                    "var update1 = Update<User>()\r\n" +
                    "   .Set((\"Age\", From<User>()\r\n" +
                    "       .Where(Prop(\"UserName\").StartsWith(\"UpdateDemo_\"))\r\n" +
                    "       .Select(Aggregate(\"AVG\", Prop(\"Age\")).As(\"avg_age\"))\r\n" +
                    "       .Select(\"avg_age\")\r\n" +
                    "   ))\r\n" +
                    "   .Where(Prop(\"UserName\") == \"UpdateDemo_Alice\");");

                var update1 = 
                    Update<User>()
                    .Set(("Age", From<User>()
                        .Where(Prop("UserName").StartsWith("UpdateDemo_"))
                        .Select(Aggregate("AVG", Prop("Age")).As("avg_age"))
                        .Select("avg_age")//必须加一层嵌套，令 MySQL 将其视为独立数据源，MySQL 禁止子查询的 FROM 与被更新目标表相同
                    ))
                    .Where(Prop("UserName") == "UpdateDemo_Alice");

                int affected1 = await userSvc.UpdateAsync(update1);

                var sql1 = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 场景1 执行的 SQL", sql1);
                DemoHelper.PrintSection("✅ 场景1 结果", $"受影响行数: {affected1}（Alice.Age → UpdateDemo 用户的平均年龄）");

                // 场景2：跨表子查询 ─ SET DeptId = (SELECT Id FROM Departments WHERE Name = '研发中心')
                DemoHelper.PrintSection("📋 场景2 说明",
                    "跨表子查询作为 SET 值：将 Carol 的 DeptId 更新为从 Departments 表查询到的部门 Id");

                DemoHelper.PrintSection("📝 场景2 代码",
                    "var subDept = From<Department>()\n" +
                    "    .Where(Prop(\"Name\") == \"研发中心\")\n" +
                    "    .Select(Prop(\"Id\"));\n" +
                    "\n" +
                    "var update = new UpdateExpr(From<User>(), Prop(\"UserName\") == \"UpdateDemo_Carol\")\n" +
                    "    .Set((\"DeptId\", subDept));");

                var subDept = From<Department>()
                    .Where(Prop("Name") == "研发中心")
                    .Select(Prop("Id"));

                var update2 = new UpdateExpr(From<User>(), Prop("UserName") == "UpdateDemo_Carol")
                    .Set(("DeptId", subDept));

                int affected2 = await userSvc.UpdateAsync(update2);

                var sql2 = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 场景2 执行的 SQL", sql2);
                DemoHelper.PrintSection("✅ 场景2 结果", $"受影响行数: {affected2}（Carol.DeptId → 研发中心的 Id）");

                Console.WriteLine("✓ 演示6.6 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示6.6 失败: {ex.Message}\n");
                var sql = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示6.5：Lambda 条件 + 多字段链式更新
        /// </summary>
        private static async Task Demo5_LambdaWhereMultiFieldAsync(IUserService userSvc)
        {
            Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示6.5：Lambda 条件 + 多字段链式更新                      │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                DemoHelper.PrintSection("📋 场景说明",
                    "使用 Lambda<T>() 将 Lambda 表达式转换为 WHERE 条件，同时通过 .Set() 一次性更新多个字段");

                DemoHelper.PrintSection("📝 代码实现",
                    "var update = new UpdateExpr(\n" +
                    "    From<User>(),\n" +
                    "    Lambda<User>(u => u.Age >= 28))\n" +
                    "    .Set(\n" +
                    "        (\"Age\",        Prop(\"Age\") + Const(1)),\n" +
                    "        (\"CreateTime\", Const(DateTime.Now))\n" +
                    "    );");

                var update = new UpdateExpr(
                        From<User>(),
                        Lambda<User>(u => u.Age >= 28))
                    .Set(
                        ("Age",        Prop("Age") + Const(1)),
                        ("CreateTime", Const(DateTime.Now))
                    );

                int affected = await userSvc.UpdateAsync(update);

                var sql = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                DemoHelper.PrintSection("✅ 结果", $"受影响行数: {affected}（Age >= 28 的记录：Age+1 且 CreateTime 刷新）");

                Console.WriteLine("✓ 演示6.5 完成\n");
            }
            catch (Exception ex)
            {
                var sql = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
                DemoHelper.PrintSection("🔍 执行的 SQL", sql);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示6.5 失败: {ex.Message}\n");
                Console.ResetColor();
            }
        }
    }
}
