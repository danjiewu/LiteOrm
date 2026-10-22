using LiteOrm.AotDemo.Models;
using LiteOrm.AotDemo.Services;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm.AotDemo.Scenarios
{
    /// <summary>
    /// 场景四：JSON 列与计算列。
    /// <para>
    /// JSON 列在 AOT 下由源生成器生成 <c>JsonNode.Parse(reader.GetString(i))</c> 的读取代码；
    /// 计算列不落库，只在 SELECT 时按表达式渲染，因此也能参与过滤。
    /// </para>
    /// </summary>
    public static class JsonAndComputedScenario
    {
        /// <summary>运行 JSON 与计算列场景。</summary>
        /// <param name="services">当前作用域的服务提供者。</param>
        public static async Task RunAsync(IServiceProvider services)
        {
            var userService = services.GetRequiredService<IAotUserService>();
            var orderService = services.GetRequiredService<IAotOrderService>();

            DemoOutput.Section("4.1 JSON 列读写");
            var all = await userService.SearchAsync();
            foreach (var user in all)
            {
                DemoOutput.Line(DemoOutput.Row(user.UserName, user.Info?.ToJsonString() ?? "(null)", $"Duration={user.Duration}"));
            }
            DemoOutput.LastSql();

            DemoOutput.Section("4.2 JSON 路径过滤（Lambda 写法）");
            var goldUsers = await userService.SearchAsync(Expr.From<AotUserView>().Where(x => x.Info!["Level"]!.GetValue<string>() == "gold"));
            DemoOutput.Pair("Info['Level'] == 'gold'", goldUsers.Count);
            foreach (var user in goldUsers)
            {
                DemoOutput.Line($"{user.UserName}: {user.Info?.ToJsonString()}");
            }
            DemoOutput.LastSql();

            DemoOutput.Section("4.3 计算列参与过滤");
            var taxedExpr = Expr.Prop(nameof(AotOrder.AmountWithTax)) > Expr.Const(200d);
            var taxedOrders = await orderService.SearchAsync(taxedExpr);
            DemoOutput.Pair("AmountWithTax > 200", taxedOrders.Count);
            foreach (var order in taxedOrders)
            {
                DemoOutput.Line(DemoOutput.Row($"订单{order.Id}", $"金额={order.Amount}", $"含税={order.AmountWithTax:0.##}"));
            }
            DemoOutput.LastSql();
        }
    }
}
