using LiteOrm.AotDemo.Models;
using LiteOrm.AotDemo.Services;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm.AotDemo.Scenarios
{
    /// <summary>
    /// 场景三：自定义投影与聚合。
    /// <para>
    /// 结果类型不是 <c>[Table]</c> 实体时，AOT 下没有源生成的读取映射，
    /// 由 <c>AotMapper</c> 按列名匹配属性/构造参数。因此 SELECT 别名要与属性名一致。
    /// </para>
    /// </summary>
    public static class ProjectionScenario
    {
        /// <summary>运行投影与聚合场景。</summary>
        /// <param name="services">当前作用域的服务提供者。</param>
        public static async Task RunAsync(IServiceProvider services)
        {
            var orderService = services.GetRequiredService<IAotOrderService>();

            DemoOutput.Section("3.1 SearchAs：只取需要的列并投影到自定义类型");
            var selectExpr = new SelectExpr(
                Expr.From(typeof(AotOrderView))
                    .OrderBy(Expr.Prop(nameof(AotOrder.Amount)).Desc())
                    .Section(0, 3),
                Expr.Prop(nameof(AotOrder.Id)).As(nameof(AotOrderRow.OrderId)),
                Expr.Prop(nameof(AotOrder.Amount)).As(nameof(AotOrderRow.Amount)),
                Expr.Prop(nameof(AotOrder.Status)).As(nameof(AotOrderRow.Status)),
                Expr.Prop(nameof(AotOrderView.UserName)).As(nameof(AotOrderRow.UserName)),
                Expr.Prop(nameof(AotOrderView.DeptName)).As(nameof(AotOrderRow.DeptName)));

            var rows = await orderService.SearchAsAsync<AotOrderRow>(selectExpr);
            foreach (var row in rows)
            {
                DemoOutput.Line(DemoOutput.Row(
                    $"订单{row.OrderId}", $"金额={row.Amount}", $"状态={row.Status}", $"用户={row.UserName}", $"部门={row.DeptName}"));
            }
            DemoOutput.LastSql();

            DemoOutput.Section("3.2 分组聚合：按用户统计订单数与金额合计");
            var statExpr = Expr.From(typeof(AotOrderView))
                .GroupBy(
                    Expr.Prop(nameof(AotOrderView.UserId)),
                    Expr.Prop(nameof(AotOrderView.UserName)))
                .Select(
                    Expr.Prop(nameof(AotOrderView.UserId)).As(nameof(AotUserOrderStat.UserId)),
                    Expr.Prop(nameof(AotOrderView.UserName)).As(nameof(AotUserOrderStat.UserName)),
                    Expr.Const(1).Count().As(nameof(AotUserOrderStat.OrderCount)),
                    Expr.Prop(nameof(AotOrder.Amount)).Sum().As(nameof(AotUserOrderStat.TotalAmount)));

            var stats = await orderService.SearchAsAsync<AotUserOrderStat>(statExpr);
            foreach (var stat in stats)
            {
                DemoOutput.Line(DemoOutput.Row(stat.UserName, $"订单数={stat.OrderCount}", $"合计={stat.TotalAmount:0.##}"));
            }
            DemoOutput.LastSql();

            DemoOutput.Section("3.3 SearchOneAs：单条投影");
            var single = await orderService.SearchOneAsAsync<AotOrderRow>(selectExpr);
            DemoOutput.Line(single is null
                ? "无结果"
                : $"金额最大的订单：{single.OrderId} / {single.Amount} / {single.UserName}");
        }
    }
}
