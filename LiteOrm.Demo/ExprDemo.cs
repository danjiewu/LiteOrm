using LiteOrm.Demo.Demos;
using LiteOrm.Demo.Services;

namespace LiteOrm.Demo
{
    /// <summary>
    /// LiteOrm 演示入口类，按功能模块分发调用
    /// </summary>
    public static class ExprDemo
    {
        /// <summary>
        /// 运行所有演示示例
        /// </summary>
        public static async Task RunAllExamplesAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n=== Expr 表达式全示例展示 ===");

            // 1. 基础表达式
            ExprBasicDemo.ShowBinaryExpr();
            ExprBasicDemo.ShowValueExpr();
            ExprBasicDemo.ShowPropertyExpr();
            ExprBasicDemo.ShowUnaryExpr();
            ExprBasicDemo.ShowExprSet();

            // 2. 高级表达式支持
            ExprAdvancedDemo.ShowForeignExpr();
            ExprAdvancedDemo.ShowLambdaExpr();
            ExprAdvancedDemo.ShowExprConvert();
            ExprAdvancedDemo.ShowSqlGeneration();
            ExprAdvancedDemo.ShowQueryExpr();

            // Lambda 表达式查询演示
            LambdaQueryDemo.ShowLambdaQueryDemo();
            LambdaQueryDemo.ShowLambdaOrderingDemo();

            // Lambda 表达式查询执行演示
            await LambdaQueryDemo.ShowLambdaQueryWithResultsAsync(factory.UserService);

            // 3. 查询场景演示
            await QueryUsageDemo.ShowJoinQueryAsync(factory.DepartmentService);
            await QueryUsageDemo.ShowArgedQueryAsync(factory.SalesService);
            await QueryUsageDemo.ShowQueryResultsAsync(factory.UserService, factory.SalesService, factory.DepartmentService);

            // 4. 性能演示
            await PerformanceDemo.ShowPerformanceComparisonAsync(factory.SalesService);
            await PerformanceDemo.ShowBatchUpdatePerformanceAsync(factory.SalesService);

            // 5. 自定义 DAO 演示
            await CustomDaoDemo.ShowCustomDaoDemoAsync(factory.UserCustomDAO);
        }

        /// <summary>
        /// 运行三层架构与事务演示
        /// </summary>
        public static async Task RunThreeTierDemo(ServiceFactory factory)
        {
            await TransactionDemo.RunThreeTierDemoAsync(factory);
        }
    }
}
