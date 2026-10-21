using LiteOrm.AotDemo.Models;
using LiteOrm.AotDemo.Services;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm.AotDemo.Scenarios
{
    /// <summary>
    /// 场景二：实体关联。
    /// <para>
    /// AOT 下 <c>[TableJoin]</c> / <c>[ForeignType]</c> / <c>[ForeignColumn]</c> 由源生成器转成
    /// <c>TableInfo.JoinedTables</c> 与 <c>ColumnInfo.ForeignColumn</c>，运行期不再读特性，
    /// 业务写法与 JIT 完全一致：视图模型上声明投影列，查询时自动 JOIN。
    /// </para>
    /// </summary>
    public static class AssociationScenario
    {
        /// <summary>运行关联查询场景。</summary>
        /// <param name="services">当前作用域的服务提供者。</param>
        public static async Task RunAsync(IServiceProvider services)
        {
            var orderService = services.GetRequiredService<IAotOrderService>();
            var deptService = services.GetRequiredService<IAotDepartmentService>();

            DemoOutput.Section("2.1 ForeignColumn 投影：订单 → 用户 → 部门");
            var orders = await orderService.SearchAsync();
            foreach (var order in orders)
            {
                DemoOutput.Line(DemoOutput.Row(
                    $"订单{order.Id}",
                    $"金额={order.Amount}",
                    $"状态={order.Status}",
                    $"用户={order.UserName}",
                    $"部门={order.DeptName}",
                    $"含税={order.AmountWithTax:0.##}"));
            }
            DemoOutput.LastSql();

            DemoOutput.Section("2.2 关联列参与过滤、排序与分页");
            var expr = Expr.From(typeof(AotOrderView))
                .Where(Expr.Prop(nameof(AotOrderView.DeptName)) == Expr.Const("研发部"))
                .OrderBy(Expr.Prop(nameof(AotOrder.Amount)).Desc())
                .Section(0, 2);
            var rdOrders = await orderService.SearchAsync(expr);
            DemoOutput.Pair("研发部订单（按金额倒序取前 2）", rdOrders.Count);
            foreach (var order in rdOrders)
            {
                DemoOutput.Line(DemoOutput.Row($"订单{order.Id}", $"金额={order.Amount}", $"用户={order.UserName}", $"部门={order.DeptName}"));
            }
            DemoOutput.LastSql();

            DemoOutput.Section("2.3 Lambda 写法访问关联字段");
            var lambdaResult = await orderService.SearchAsync(q => q.Where(x => x.DeptName == "研发部"));
            DemoOutput.Pair("Lambda Where(DeptName == 研发部)", lambdaResult.Count);
            DemoOutput.LastSql();

            DemoOutput.Section("2.4 TableJoin 自关联：部门 → 上级部门");
            var depts = await deptService.SearchAsync();
            foreach (var dept in depts)
            {
                DemoOutput.Line(DemoOutput.Row($"#{dept.Id}", dept.Name, $"上级={dept.ParentName ?? "(无)"}"));
            }
            DemoOutput.LastSql();
        }
    }
}
