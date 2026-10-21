using LiteOrm.AotDemo.Models;
using LiteOrm.AotDemo.Services;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Nodes;

namespace LiteOrm.AotDemo.Scenarios
{
    /// <summary>
    /// 场景一：单表 CRUD。同步与异步、单条与批量、实体更新与条件更新、三种删除方式。
    /// </summary>
    public static class CrudScenario
    {
        /// <summary>运行 CRUD 场景。</summary>
        /// <param name="services">当前作用域的服务提供者。</param>
        public static async Task RunAsync(IServiceProvider services)
        {
            var userService = services.GetRequiredService<IAotUserService>();

            DemoOutput.Section("1.1 插入（同步 + 异步）");
            var sync = new AotUser
            {
                UserName = "crud_sync",
                Age = 20,
                Role = UserRole.Staff,
                CreateTime = DateTime.Now,
                Guid = Guid.NewGuid(),
                Duration = TimeSpan.FromSeconds(1),
                Info = JsonNode.Parse("""{"Level":"temp"}""")
            };
            userService.Insert(sync);

            var asyncUser = new AotUser
            {
                UserName = "crud_async",
                Age = 22,
                Role = UserRole.Staff,
                CreateTime = DateTime.Now,
                Guid = Guid.NewGuid(),
                Duration = TimeSpan.FromSeconds(2)
            };
            await userService.InsertAsync(asyncUser);
            DemoOutput.Pair("自增主键回填", $"crud_sync.Id={sync.Id}, crud_async.Id={asyncUser.Id}");

            DemoOutput.Section("1.2 查询与计数");
            int total = userService.Count(Expr.Prop(nameof(AotUser.UserName)).Like("crud_%"));
            DemoOutput.Pair("Count(UserName LIKE 'crud_%')", total);

            var found = await userService.SearchAsync(Expr.Prop(nameof(AotUser.Age)) >= Expr.Const(20));
            DemoOutput.Pair("Search(Age >= 20)", found.Count);

            var one = userService.SearchOne(Expr.Prop(nameof(AotUser.UserName)) == Expr.Const("crud_sync"));
            DemoOutput.Line(one is null
                ? "SearchOne(crud_sync) -> null"
                : $"SearchOne(crud_sync) -> Id={one.Id}, Age={one.Age}, Role={one.Role}, Duration={one.Duration}");

            DemoOutput.Section("1.3 更新");
            sync.Age = 21;
            sync.Role = UserRole.Manager;
            userService.Update(sync);
            var reloaded = userService.SearchOne(Expr.Prop(nameof(AotUser.Id)) == Expr.Const(sync.Id));
            DemoOutput.Pair("Update 后回读", $"Age={reloaded?.Age}, Role={reloaded?.Role}（枚举按整型列读写）");

            DemoOutput.Section("1.4 UpdateOrInsert");
            var upsert = new AotUser { UserName = "crud_upsert", Age = 30, CreateTime = DateTime.Now, Guid = Guid.NewGuid() };
            userService.UpdateOrInsert(upsert);
            upsert.Age = 31;
            userService.UpdateOrInsert(upsert);
            var upserted = userService.SearchOne(Expr.Prop(nameof(AotUser.UserName)) == Expr.Const("crud_upsert"));
            DemoOutput.Pair("两次 UpdateOrInsert 后", $"Id={upserted?.Id}, Age={upserted?.Age}");

            DemoOutput.Section("1.5 UpdateAll（条件批量更新）");
            var updateExpr = new UpdateExpr(
                Expr.From(typeof(AotUser)),
                Expr.Prop(nameof(AotUser.UserName)) == Expr.Const("crud_upsert"))
                .Set((nameof(AotUser.Age), Expr.Const(99)));
            int affected = userService.UpdateAll(updateExpr);
            DemoOutput.Pair("UpdateAll 影响行数", affected);
            DemoOutput.LastSql();

            DemoOutput.Section("1.6 删除");
            bool deletedOne = userService.Delete(sync);
            DemoOutput.Pair("Delete(entity)", deletedOne);
            userService.DeleteID(asyncUser.Id);
            DemoOutput.Pair("DeleteID", asyncUser.Id);
            int deletedMany = await userService.DeleteAllAsync(Expr.Prop(nameof(AotUser.UserName)).Like("crud_%"));
            DemoOutput.Pair("DeleteAllAsync(crud_%) 影响行数", deletedMany);
            DemoOutput.Pair("剩余用户数", userService.Count(null));
        }
    }
}
