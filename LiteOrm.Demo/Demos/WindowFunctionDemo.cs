using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using System.Linq.Expressions;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 窗口函数演示 (Window Function Demo)
    ///
    /// 演示通过 RegisterMethodHandler + RegisterFunctionSqlHandler 扩展
    /// LiteOrm 表达式处理能力，将 SumOver 方法调用转换为数据库窗口函数 SQL。
    /// </summary>
    public static class WindowFunctionDemo
    {
        /// <summary>
        /// 注册 SumOver Lambda 处理器和 SUM_OVER SQL 处理器。
        /// 必须在首次查询前调用（通常在应用启动时）。
        /// </summary>
        public static void RegisterHandlers()
        {
            // 步骤1：注册 SumOver 方法处理器，将 C# 方法调用转换为 FunctionExpr
            LambdaExprConverter.RegisterMethodHandler("SumOver", (node, converter) =>
            {
                // node.Arguments[0] = this（扩展方法的 amount 参数）
                var amountExpr = converter.Convert(node.Arguments[0]) as ValueTypeExpr;

                var partitionExprs = new List<ValueTypeExpr>();
                var orderExprs = new List<ValueTypeExpr>();

                // node.Arguments[1] = partitionBy（NewArrayExpression，元素为 Quote(Lambda)）
                if (node.Arguments.Count > 1 && node.Arguments[1] is NewArrayExpression partArray)
                {
                    foreach (var elem in partArray.Expressions)
                    {
                        if (converter.Convert(elem) is ValueTypeExpr vte)
                            partitionExprs.Add(vte);
                    }
                }

                // node.Arguments[2] = orderBy（NewArrayExpression，元素为 SumOverOrderBy<T> 构造表达式）
                if (node.Arguments.Count > 2 && node.Arguments[2] is NewArrayExpression orderArray)
                {
                    foreach (var elem in orderArray.Expressions)
                    {
                        if (elem is NewExpression ctorNew && ctorNew.Arguments.Count == 2)
                        {
                            var field = converter.Convert(ctorNew.Arguments[0]) as ValueTypeExpr;
                            bool isAsc = ctorNew.Arguments[1] is ConstantExpression { Value: bool b } && b;
                            if (field is not null)
                                orderExprs.Add(new OrderByItemExpr(field, isAsc));
                        }
                    }
                }

                return new FunctionExpr("SUM_OVER",
                    amountExpr,
                    new ValueSet(partitionExprs),
                    new ValueSet(orderExprs));
            });

            // 步骤2：注册 SUM_OVER SQL 处理器（所有数据库通用）
            // args[0].Key = 金额列 SQL（如 "Amount"）
            // args[1].Key = 分区 ValueSet SQL，格式为 "(ProductId)"，去除首尾括号即可
            // args[2].Key = 排序 ValueSet SQL，格式为 "(SaleTime)"，去除首尾括号即可
            SqlBuilder.Instance.RegisterFunctionSqlHandler("SUM_OVER", (_, args) =>
            {
                string amount = args[0].Key;

                string partitionSql = args.Count > 1 && args[1].Key.Length > 2
                    ? args[1].Key.Substring(1, args[1].Key.Length - 2)
                    : string.Empty;

                string orderSql = args.Count > 2 && args[2].Key.Length > 2
                    ? args[2].Key.Substring(1, args[2].Key.Length - 2)
                    : string.Empty;

                var clauses = new List<string>();
                if (!string.IsNullOrEmpty(partitionSql)) clauses.Add($"PARTITION BY {partitionSql}");
                if (!string.IsNullOrEmpty(orderSql)) clauses.Add($"ORDER BY {orderSql}");

                return $"SUM({amount}) OVER ({string.Join(" ", clauses)})";
            });
        }

        /// <summary>
        /// 运行所有窗口函数演示
        /// </summary>
        public static async Task RunAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║    5. 自定义注册函数及SQL构造器实现窗口函数演示 (Window Function Demo)                  ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

            await Demo1_PartitionOnlyAsync(factory);
            await Demo2_PartitionAndOrderAsync(factory);
        }

        /// <summary>
        /// 演示5.1：仅分区的窗口函数 — PARTITION BY ProductId
        /// </summary>
        private static async Task Demo1_PartitionOnlyAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示5.1：按产品分区的总销售额（仅 PARTITION BY）           │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                string tableMonth = DateTime.Now.ToString("yyyyMM");

                PrintSection("📋 场景说明",
                    $"查询 Sales_{tableMonth} 分表，按产品分区计算每个产品的总销售额。\n" +
                    "使用 SumOver<SalesRecord>(p => p.ProductId) params 重载。");

                PrintSection("📝 代码实现",
                    "ProductTotal = s.Amount.SumOver<SalesRecord>(p => p.ProductId)");

                var results = await factory.SalesDAO
                    .WithArgs([tableMonth])
                    .SearchAs(q => q
                        .OrderBy(s => s.ProductId)
                        .Select(s => new SalesWindowView
                        {
                            Id = s.Id,
                            ProductId = s.ProductId,
                            ProductName = s.ProductName,
                            Amount = s.Amount,
                            SaleTime = s.SaleTime,
                            ProductTotal = s.Amount.SumOver<SalesRecord>(p => p.ProductId)
                        })
                    ).ToListAsync();

                var executedSql = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
                PrintSection("🔍 执行的 SQL", executedSql);

                PrintSection("✅ 查询结果（前 5 条）",
                    FormatPartitionResults(results.Take(5).ToList()));

                Console.WriteLine("✓ 演示5.1 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示5.1 失败: {ex.Message}\n");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 演示5.2：分区 + 排序的窗口函数 — PARTITION BY ProductId ORDER BY SaleTime
        /// </summary>
        private static async Task Demo2_PartitionAndOrderAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ 演示5.2：按产品分区、按时间排序的累计销售额                │");
            Console.WriteLine("└────────────────────────────────────────────────────────────┘");

            try
            {
                string tableMonth = DateTime.Now.ToString("yyyyMM");

                PrintSection("📋 场景说明",
                    $"查询 Sales_{tableMonth} 分表，按产品分区、按销售时间升序排列，\n" +
                    "计算每条记录在同产品内的累计销售额（Running Total）。\n" +
                    "使用显式数组重载：SumOver<SalesRecord>(partitionBy, orderBy)。");

                PrintSection("📝 代码实现",
                    "RunningTotal = s.Amount.SumOver<SalesRecord>(\n" +
                    "    new Expression<Func<SalesRecord, object>>[] { p => p.ProductId },\n" +
                    "    new SumOverOrderBy<SalesRecord>[] { new SumOverOrderBy<SalesRecord>(p => p.SaleTime, true) }\n" +
                    ")");

                var results = await factory.SalesDAO
                    .WithArgs([tableMonth])
                    .SearchAs(q => q
                        .OrderBy(s => s.ProductId)
                        .Select(s => new SalesWindowView
                        {
                            Id = s.Id,
                            ProductId = s.ProductId,
                            ProductName = s.ProductName,
                            Amount = s.Amount,
                            SaleTime = s.SaleTime,
                            RunningTotal = s.Amount.SumOver<SalesRecord>(
                                new Expression<Func<SalesRecord, object>>[] { p => p.ProductId },
                                new SumOverOrderBy<SalesRecord>[] { new SumOverOrderBy<SalesRecord>(p => p.SaleTime, true) }
                            )
                        })
                    ).ToListAsync();

                var executedSql = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
                PrintSection("🔍 执行的 SQL", executedSql);

                var grouped = results
                    .GroupBy(r => r.ProductName)
                    .OrderBy(g => g.Key)
                    .Take(3);

                var sb = new System.Text.StringBuilder();
                foreach (var g in grouped)
                {
                    sb.AppendLine($"  【{g.Key}】");
                    foreach (var r in g.Take(3))
                        sb.AppendLine($"    {r.SaleTime:MM-dd HH:mm}  金额: ¥{r.Amount,6}  累计: ¥{r.RunningTotal,8}");
                }

                PrintSection("✅ 查询结果（按产品分组，每组前 3 条）", sb.ToString().TrimEnd());

                Console.WriteLine("✓ 演示5.2 完成\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 演示5.2 失败: {ex.Message}\n");
                Console.ResetColor();
            }
        }

        private static string FormatPartitionResults(List<SalesWindowView> rows)
        {
            if (rows.Count == 0) return "（无数据）";
            var sb = new System.Text.StringBuilder();
            foreach (var r in rows)
                sb.AppendLine($"  Id={r.Id,-4} {r.ProductName,-12} ¥{r.Amount,6}  产品合计: ¥{r.ProductTotal,8}");
            return sb.ToString().TrimEnd();
        }

        private static void PrintSection(string title, string content) => DemoHelper.PrintSection(title, content);
    }
}
