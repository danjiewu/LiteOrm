using LiteOrm.AotDemo.Models;
using LiteOrm.AotDemo.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Nodes;

namespace LiteOrm.AotDemo.Scenarios
{
    /// <summary>
    /// 演示数据准备：先清空历史数据，再写入一组部门 / 用户 / 订单，
    /// 供后续各场景共用（演示可重复运行）。
    /// </summary>
    public static class DemoData
    {
        /// <summary>清空并重新写入演示数据。</summary>
        /// <param name="services">当前作用域的服务提供者。</param>
        public static void Reset(IServiceProvider services)
        {
            var deptService = services.GetRequiredService<IAotDepartmentService>();
            var userService = services.GetRequiredService<IAotUserService>();
            var orderService = services.GetRequiredService<IAotOrderService>();

            // 清空历史数据（DeleteAll 传 null 表示删除全部）
            orderService.DeleteAll(null);
            userService.DeleteAll(null);
            deptService.DeleteAll(null);

            var hq = new AotDepartment { Name = "总部" };
            deptService.Insert(hq);
            deptService.BatchInsert(new[]
            {
                new AotDepartment { Name = "研发部", ParentId = hq.Id },
                new AotDepartment { Name = "市场部", ParentId = hq.Id }
            });
            var depts = deptService.Search(null);
            var rd = depts.First(d => d.Name == "研发部");
            var mk = depts.First(d => d.Name == "市场部");

            var users = new[]
            {
                new AotUser { UserName = "alice", Age = 30, Role = UserRole.Manager, DeptId = rd.Id,
                    CreateTime = DateTime.Now, Guid = Guid.NewGuid(), Duration = TimeSpan.FromSeconds(10),
                    Info = JsonNode.Parse("""{"Level":"gold","Tags":["vip","beta"]}""") },
                new AotUser { UserName = "bob", Age = 25, Role = UserRole.Staff, DeptId = rd.Id,
                    CreateTime = DateTime.Now, Guid = Guid.NewGuid(), Duration = TimeSpan.FromSeconds(5),
                    Info = JsonNode.Parse("""{"Level":"silver"}""") },
                new AotUser { UserName = "carol", Age = 28, Role = UserRole.Admin, DeptId = mk.Id,
                    CreateTime = DateTime.Now, Guid = Guid.NewGuid(), Duration = TimeSpan.FromSeconds(15),
                    Info = JsonNode.Parse("""{"Level":"free"}""") }
            };
            userService.BatchInsert(users);

            var orders = new[]
            {
                new AotOrder { UserId = users[0].Id, Amount = 120.5, Status = OrderStatus.Paid, CreateTime = DateTime.Now },
                new AotOrder { UserId = users[0].Id, Amount = 88, Status = OrderStatus.Pending, CreateTime = DateTime.Now },
                new AotOrder { UserId = users[1].Id, Amount = 300, Status = OrderStatus.Paid, CreateTime = DateTime.Now },
                new AotOrder { UserId = users[2].Id, Amount = 45.8, Status = OrderStatus.Cancelled, CreateTime = DateTime.Now }
            };
            orderService.BatchInsert(orders);

            DemoOutput.Section("准备演示数据");
            DemoOutput.Pair("部门", depts.Count);
            DemoOutput.Pair("用户", userService.Count(null));
            DemoOutput.Pair("订单", orderService.Count(null));
        }
    }
}
