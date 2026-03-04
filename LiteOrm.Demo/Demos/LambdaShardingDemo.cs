using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// Lambda 方式分表查询演示
    /// 
    /// 本演示展示如何使用 Lambda 表达式的方式设置分表参数（TableArgs），
    /// 这使得分表查询的编写更加直观和类型安全。
    /// </summary>
    public static class LambdaShardingDemo
    {
        /// <summary>
        /// 运行所有演示
        /// </summary>
        public static async Task RunAsync(IServiceProvider serviceProvider)
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("  Lambda 方式分表查询演示");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            await Demo1_BasicShardingAsync();
            await Demo2_MultipleTableArgsAsync();
            await Demo3_ComplexConditionsAsync();
            await Demo4_ExistsSubqueryAsync();
            await Demo5_VariableReferencesAsync();
            await Demo6_ComparisonWithExprAPIAsync();
        }

        /// <summary>
        /// 演示1：基础分表查询
        /// 
        /// 场景：查询2024年1月的用户数据，ID大于100的记录
        /// </summary>
        private static async Task Demo1_BasicShardingAsync()
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("演示1：基础分表查询");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("场景：查询2024年1月的用户数据，ID大于100的记录");
            Console.WriteLine();

            try
            {
                // 使用 Lambda 表达式的方式指定分表参数
                Expression<Func<SimpleUser, bool>> lambda = u =>
                    ((IArged)u).TableArgs == new[] { "202401" } &&
                    u.Id > 100;

                Console.WriteLine($"Lambda 表达式：");
                Console.WriteLine($"  u => ((IArged)u).TableArgs == new[] {{ \"202401\" }} && u.Id > 100");
                Console.WriteLine();

                // 转换为逻辑表达式
                var logicExpr = LambdaExprConverter.ToLogicExpr(lambda);
                Console.WriteLine($"转换后的逻辑表达式：");
                Console.WriteLine($"  {logicExpr}");
                Console.WriteLine();

                Console.WriteLine("✅ 演示1完成");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ 演示1失败: {ex.Message}");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示2：多月份分表查询
        /// 
        /// 场景：查询2024年1-3月的用户数据
        /// </summary>
        private static async Task Demo2_MultipleTableArgsAsync()
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("演示2：多月份分表查询");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("场景：查询2024年1-3月的用户数据，用户名包含'admin'");
            Console.WriteLine();

            try
            {
                // 指定多个分表参数
                Expression<Func<SimpleUser, bool>> lambda = u =>
                    ((IArged)u).TableArgs == new[] { "202401", "202402", "202403" } &&
                    u.Name.Contains("admin");

                Console.WriteLine($"Lambda 表达式：");
                Console.WriteLine($"  u => ((IArged)u).TableArgs == new[] {{ \"202401\", \"202402\", \"202403\" }} &&");
                Console.WriteLine($"       u.Name.Contains(\"admin\")");
                Console.WriteLine();

                // 转换为逻辑表达式
                var logicExpr = LambdaExprConverter.ToLogicExpr(lambda);
                Console.WriteLine($"转换后的逻辑表达式：");
                Console.WriteLine($"  {logicExpr}");
                Console.WriteLine();

                Console.WriteLine("✅ 演示2完成");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ 演示2失败: {ex.Message}");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示3：复杂条件组合
        /// 
        /// 场景：分表条件与多个业务条件组合
        /// </summary>
        private static async Task Demo3_ComplexConditionsAsync()
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("演示3：复杂条件组合");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("场景：分表条件与多个业务条件组合查询");
            Console.WriteLine();

            try
            {
                // 分表条件与多个业务逻辑条件组合
                Expression<Func<SimpleUser, bool>> lambda = u =>
                    ((IArged)u).TableArgs == new[] { "202401" } &&
                    u.Name.Contains("user") &&
                    u.Age > 18;

                Console.WriteLine($"Lambda 表达式：");
                Console.WriteLine($"  u => ((IArged)u).TableArgs == new[] {{ \"202401\" }} &&");
                Console.WriteLine($"       u.Name.Contains(\"user\") &&");
                Console.WriteLine($"       u.Age > 18");
                Console.WriteLine();

                // 转换为逻辑表达式
                var logicExpr = LambdaExprConverter.ToLogicExpr(lambda);
                Console.WriteLine($"转换后的逻辑表达式：");
                Console.WriteLine($"  {logicExpr}");
                Console.WriteLine();

                Console.WriteLine("✅ 演示3完成");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ 演示3失败: {ex.Message}");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示4：Exists 子查询中的分表
        /// 
        /// 场景：在 Exists 子查询中使用分表参数
        /// </summary>
        private static async Task Demo4_ExistsSubqueryAsync()
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("演示4：Exists 子查询中的分表");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("场景：查询2024年1月有订单的用户");
            Console.WriteLine();

            try
            {
                // 在 Exists 子查询中使用分表
                Expression<Func<SimpleUser, bool>> lambda = u =>
                    Expr.Exists<SimpleOrder>(o =>
                        ((IArged)o).TableArgs == new[] { "202401" } &&
                        o.UserId == u.Id);

                Console.WriteLine($"Lambda 表达式：");
                Console.WriteLine($"  u => Expr.Exists<SimpleOrder>(o =>");
                Console.WriteLine($"          ((IArged)o).TableArgs == new[] {{ \"202401\" }} &&");
                Console.WriteLine($"          o.UserId == u.Id)");
                Console.WriteLine();

                // 转换为逻辑表达式
                var logicExpr = LambdaExprConverter.ToLogicExpr(lambda);
                Console.WriteLine($"转换后的逻辑表达式：");
                Console.WriteLine($"  {logicExpr}");
                Console.WriteLine();

                Console.WriteLine("✅ 演示4完成");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ 演示4失败: {ex.Message}");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示5：使用变量引用分表参数
        /// 
        /// 场景：动态使用变量或方法返回值作为分表参数
        /// </summary>
        private static async Task Demo5_VariableReferencesAsync()
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("演示5：使用变量引用分表参数");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("场景：动态使用变量作为分表参数");
            Console.WriteLine();

            try
            {
                // 使用变量引用
                var currentMonth = "202401";
                var nextMonth = "202402";
                var tableArgs = new[] { currentMonth, nextMonth };

                Expression<Func<SimpleUser, bool>> lambda = u =>
                    ((IArged)u).TableArgs == tableArgs &&
                    u.Id > 0;

                Console.WriteLine($"变量定义：");
                Console.WriteLine($"  var currentMonth = \"{currentMonth}\";");
                Console.WriteLine($"  var nextMonth = \"{nextMonth}\";");
                Console.WriteLine($"  var tableArgs = new[] {{ currentMonth, nextMonth }};");
                Console.WriteLine();

                Console.WriteLine($"Lambda 表达式：");
                Console.WriteLine($"  u => ((IArged)u).TableArgs == tableArgs && u.Id > 0");
                Console.WriteLine();

                // 转换为逻辑表达式
                var logicExpr = LambdaExprConverter.ToLogicExpr(lambda);
                Console.WriteLine($"转换后的逻辑表达式：");
                Console.WriteLine($"  {logicExpr}");
                Console.WriteLine();

                Console.WriteLine("✅ 演示5完成");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ 演示5失败: {ex.Message}");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示6：Lambda 方式优势总结
        /// 
        /// 场景：展示 Lambda 分表方式的关键优势
        /// </summary>
        private static async Task Demo6_ComparisonWithExprAPIAsync()
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("演示6：Lambda 方式优势总结");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("场景：展示 Lambda 分表方式的优势");
            Console.WriteLine();

            try
            {
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("✨ Lambda 方式的核心优势");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine();

                Console.WriteLine($"1️⃣  更直观 - 分表条件与业务条件统一表达");
                Console.WriteLine($"   代码示例：");
                Console.WriteLine($"   u => ((IArged)u).TableArgs == [\"202401\"] && u.Age > 18");
                Console.WriteLine();

                Console.WriteLine($"2️⃣  类型安全 - 编译时检查");
                Console.WriteLine($"   • 属性名拼写错误会在编译时被发现");
                Console.WriteLine($"   • 条件类型不匹配会被编译器检出");
                Console.WriteLine();

                Console.WriteLine($"3️⃣  易于维护 - 条件与分表参数在一起");
                Console.WriteLine($"   • 不容易忘记设置分表参数");
                Console.WriteLine($"   • 修改查询条件时一目了然");
                Console.WriteLine();

                Console.WriteLine($"4️⃣  支持复杂组合 - 可与 AND/OR 自由组合");
                Console.WriteLine($"   • 支持多个 && 连接");
                Console.WriteLine($"   • 支持在 Exists 子查询中使用");
                Console.WriteLine();

                Console.WriteLine($"5️⃣  动态灵活 - 支持变量和方法返回值");
                Expression<Func<SimpleUser, bool>> demoLambda = u =>
                    ((IArged)u).TableArgs == new[] { "202401" } &&
                    u.Id > 0;

                var demoLogicExpr = LambdaExprConverter.ToLogicExpr(demoLambda);
                Console.WriteLine($"   示例转换结果：{demoLogicExpr}");
                Console.WriteLine();

                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("✅ 演示6完成 - 了解更多请查看文档：");
                Console.WriteLine("   📖 docs/LAMBDA_SHARDING.md");
                Console.WriteLine("   📖 docs/LITEORM_API_REFERENCE.md");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ 演示6失败: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    /// <summary>
    /// 模拟的用户模型（用于演示）
    /// </summary>
    public class SimpleUser
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
    }

    /// <summary>
    /// 模拟的订单模型（用于演示）
    /// </summary>
    public class SimpleOrder
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal Amount { get; set; }
    }
}
