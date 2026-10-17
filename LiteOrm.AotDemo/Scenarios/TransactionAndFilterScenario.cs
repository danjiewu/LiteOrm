using LiteOrm.AotDemo.Models;
using LiteOrm.AotDemo.Services;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm.AotDemo.Scenarios
{
    /// <summary>
    /// 场景五：事务与固定过滤。
    /// <para>
    /// AOT 下没有 Castle 动态代理，<c>[Transaction]</c> 拦截失效，
    /// 事务改用 <see cref="SessionManagerExtensions.ExecuteInTransaction"/> 显式包起来。
    /// 固定筛选（<c>Column.Constant</c>）在 AOT 下同样由源生成器写进 <c>ColumnInfo.Constant</c>，
    /// 运行期装配成 <c>TableDefinition.ConstFilter</c>；运行期也可以直接改这个属性来临时收紧可见范围。
    /// </para>
    /// </summary>
    public static class TransactionAndFilterScenario
    {
        /// <summary>运行事务与固定过滤场景。</summary>
        /// <param name="services">当前作用域的服务提供者。</param>
        public static async Task RunAsync(IServiceProvider services)
        {
            var sessionManager = services.GetRequiredService<SessionManager>();
            var userService = services.GetRequiredService<IAotUserService>();
            var orderService = services.GetRequiredService<IAotOrderService>();

            DemoOutput.Section("5.1 显式事务：异常回滚");
            int before = userService.Count(null);
            try
            {
                sessionManager.ExecuteInTransaction(_ =>
                {
                    userService.Insert(new AotUser { UserName = "tx_rollback_1", Age = 40, CreateTime = DateTime.Now, Guid = Guid.NewGuid() });
                    userService.Insert(new AotUser { UserName = "tx_rollback_2", Age = 41, CreateTime = DateTime.Now, Guid = Guid.NewGuid() });
                    throw new InvalidOperationException("模拟业务校验失败");
                });
            }
            catch (InvalidOperationException ex)
            {
                DemoOutput.Pair("事务内抛出", ex.Message);
            }
            int after = userService.Count(null);
            DemoOutput.Pair("用户数（before → after）", $"{before} → {after}");

            DemoOutput.Section("5.2 显式事务：正常提交");
            sessionManager.ExecuteInTransaction(_ =>
            {
                userService.Insert(new AotUser { UserName = "tx_commit", Age = 45, CreateTime = DateTime.Now, Guid = Guid.NewGuid() });
            });
            var committed = userService.SearchOne(Expr.Prop(nameof(AotUser.UserName)) == Expr.Const("tx_commit"));
            DemoOutput.Pair("提交后可见", committed is not null);
            userService.DeleteAll(Expr.Prop(nameof(AotUser.UserName)).Like("tx_%"), null);

            DemoOutput.Section("5.3 固定过滤：ConstFilter 限定可见范围");
            var orderTable = TableInfoProvider.Instance.GetTableDefinition(typeof(AotOrder))
                ?? throw new InvalidOperationException("未找到 AotOrder 的表定义。");
            var target = await orderService.SearchOneAsync(Expr.Prop(nameof(AotOrder.Id)) > Expr.Const(0));
            if (target is null)
            {
                DemoOutput.Line("没有订单可演示");
                return;
            }

            // 只允许看到未删除的订单：所有基于实体 DAO 的查询/更新/删除都会带上这个条件
            orderTable.ConstFilter = Expr.Prop(nameof(AotOrder.IsDeleted)) == Expr.Const(false);

            target.IsDeleted = true;
            bool softDeleted = orderService.Update(target);
            DemoOutput.Pair("标记订单为已删除", softDeleted);

            bool deletedAfterFilter = orderService.Delete(target);
            DemoOutput.Pair("ConstFilter 生效后 Delete(entity) 是否命中", deletedAfterFilter);

            orderTable.ConstFilter = null;
            bool deletedAfterReset = orderService.Delete(target);
            DemoOutput.Pair("清除 ConstFilter 后 Delete(entity) 是否命中", deletedAfterReset);

            DemoOutput.Section("5.4 元数据固定过滤：Column.Constant → ConstFilter");
            var ticketService = services.GetRequiredService<IAotTicketService>();
            var ticketTable = TableInfoProvider.Instance.GetTableDefinition(typeof(AotTicket))
                ?? throw new InvalidOperationException("未找到 AotTicket 的表定义。");
            DemoOutput.Pair("元数据已生成 ConstFilter", ticketTable.ConstFilter is not null);

            // 每次运行用独立批次前缀，避免历史数据影响计数
            string batch = $"const-{Guid.NewGuid():N}-";
            ticketService.BatchInsert(new[]
            {
                new AotTicket { Title = batch + "open-a", Status = TicketStatus.Open },
                new AotTicket { Title = batch + "open-b", Status = TicketStatus.Open },
                new AotTicket { Title = batch + "closed", Status = TicketStatus.Closed },
                new AotTicket { Title = batch + "removed", Status = TicketStatus.Open, IsDeleted = true }
            });

            var batchFilter = Expr.Prop(nameof(AotTicket.Title)).Like(batch + "%");
            DemoOutput.Pair("写入 4 条（1 条已关闭、1 条已删除）", 4);
            DemoOutput.Pair("固定过滤后可见数量", ticketService.Count(batchFilter));
            foreach (var ticket in ticketService.Search(batchFilter))
                DemoOutput.Line($"{ticket.Title} / Status={ticket.Status} / IsDeleted={ticket.IsDeleted}");
        }
    }
}
