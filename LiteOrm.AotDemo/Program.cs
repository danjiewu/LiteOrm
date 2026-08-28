using LiteOrm;
using LiteOrm.AotDemo.Models;
using LiteOrm.AotDemo.Services;
using LiteOrm.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

// ──────────────────────────────────────────────────────────────────
// LiteOrm.AotDemo
//
// 本工程用于验证 LiteOrm 在 NativeAOT 编译下的端到端可用性：
//   - 数据源配置通过 appsettings.json 加载（IConfiguration → DataSourceProvider）
//   - 实体元数据由 TableInfoGenerator 在编译期生成（无运行时反射）
//   - DataReader 映射委托、属性访问器由源生成器在编译期注册
//   - SqlBuilder / DbConnection 派生类型由源生成器预注册到 TypeResolverHelper
//   - 用户 Service 由 [AutoRegister] 在编译期生成 MS DI 注册代码
//
// 本文件演示完整的 CRUD 与 SearchAs/SearchOneAs（同步 + 异步）调用，
// 覆盖 BatchInsert / BatchUpdate / UpdateOrInsert / UpdateAll / Delete / DeleteAll 等。
//
// 注意：本工程不引用 LiteOrm.DependencyInjection（基于 Autofac/Castle DynamicProxy，
// 依赖运行时反射与动态程序集生成，不支持 NativeAOT）。改用基础库 AddLiteOrm()
// 进行纯 MS DI 注册。
// ──────────────────────────────────────────────────────────────────

// 从 appsettings.json 加载配置
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var services = new ServiceCollection();
services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
services.AddSingleton<IConfiguration>(configuration);
services.AddLiteOrm();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var userService = scope.ServiceProvider.GetRequiredService<IAotUserService>();

Console.WriteLine("=== 1. Insert（单条） ===");
userService.Insert(new AotUser { UserName = "alice", Age = 30, CreateTime = DateTime.Now, Guid = Guid.NewGuid(), Info = JsonNode.Parse("""{"Level":"gold","Tags":["vip","beta"]}"""), Duration = TimeSpan.FromSeconds(10) });
userService.Insert(new AotUser { UserName = "bob", Age = 25, CreateTime = DateTime.Now, Guid = Guid.NewGuid(), Info = JsonNode.Parse("""{"Level":"silver"}"""), Duration = TimeSpan.FromSeconds(5) });
await userService.InsertAsync(new AotUser { UserName = "carol", Age = 28, CreateTime = DateTime.Now, Guid = Guid.NewGuid(), Info = JsonNode.Parse("""{"Level":"free"}"""), Duration = TimeSpan.FromSeconds(15) });
Console.WriteLine("Inserted alice / bob (sync, with JsonNode Info), carol (async).");

Console.WriteLine("\n=== 2. BatchInsert（批量插入） ===");
userService.BatchInsert(new[]
{
    new AotUser { UserName = "dave", Age = 22, CreateTime = DateTime.Now, Guid = Guid.NewGuid(), Duration = TimeSpan.FromSeconds(10) },
    new AotUser { UserName = "eve", Age = 35, CreateTime = DateTime.Now, Guid = Guid.NewGuid(), Duration = TimeSpan.FromSeconds(5) },
});
Console.WriteLine("BatchInsert dave / eve.");

Console.WriteLine("\n=== 3. Count / Search ===");
var total = userService.Count(null);
Console.WriteLine($"Count(all) = {total}");
var count = userService.Count(Expr.Prop("Age") > 20);
Console.WriteLine($"Count(Age > 20) = {count}");

var users = userService.Search(Expr.Prop("Age") > 20);
Console.WriteLine($"Search(Age > 20) found {users.Count} users:");
foreach (var u in users)
{
    Console.WriteLine($"  Id={u.Id}, UserName={u.UserName}, Age={u.Age}, Info={u.Info?.ToJsonString()}, Duration={u.Duration}");
}

Console.WriteLine("\n=== 4. SearchOne ===");
var alice = userService.SearchOne(Expr.Prop("UserName") == "alice");
Console.WriteLine(alice is null ? "alice not found" : $"alice: Id={alice.Id}, Age={alice.Age}, Info={alice.Info?.ToJsonString()}");

Console.WriteLine("\n=== 4.5 SearchAs：JsonNode Lambda 过滤（JSON 字段查询） ===");
var goldUsers = userService.SearchAs(u => u.Where(x => x.Info!["Level"]!.GetValue<string>() == "gold"));
Console.WriteLine($"SearchAs (Info['Level']=='gold') found {goldUsers.Count} users:");
foreach (var v in goldUsers)
    Console.WriteLine($"  {v.UserName}: {v.Info?.ToJsonString()}");

Console.WriteLine("\n=== 5. SearchAs / SearchOneAs（Lambda 投影到 AotUserView） ===");
var projected = userService.SearchAs(u => u.Where(x => x.Age > 20));
Console.WriteLine($"SearchAs<AotUserView>(Age > 20) found {projected.Count}:");
foreach (var v in projected)
{
    Console.WriteLine($"  Id={v.Id}, UserName={v.UserName}, Age={v.Age}, Duration={v.Duration}");
}

var oneAs = userService.SearchOneAs(u => u.Where(x => x.UserName == "bob").Select(x => new { Id = x.Id, Name = x.UserName, Age = x.Age, Duration = x.Duration }));
Console.WriteLine(oneAs is null ? "bob (as view) not found" : $"SearchOneAs<AotUserView>(UserName == bob): Id={oneAs.Id}, Name={oneAs.Name}, Age={oneAs.Age}, Duration={oneAs.Duration}");

var projectedAsync = await userService.SearchAsAsync(u => u.Where(x => x.Age > 20));
Console.WriteLine($"SearchAsAsync<AotUserView>(Age > 20) found {projectedAsync.Count}.");

var oneAsAsync = await userService.SearchOneAsAsync(u => u.Where(x => x.UserName == "carol"));
Console.WriteLine(oneAsAsync is null ? "carol (as view, async) not found" : $"SearchOneAsAsync<AotUserView>(UserName == carol): Id={oneAsAsync.Id}, Age={oneAsAsync.Age}, Duration={oneAsAsync.Duration}");

var anon = userService.SearchAs(u => u.Where(x => x.Age > 20).Select(x => new { x.Id, x.UserName }));
Console.WriteLine($"SearchAs<anonymous>(Age > 20) found {anon.Count}: {string.Join(", ", anon.Select(a => $"{a.Id}:{a.UserName}"))}");

Console.WriteLine("\n=== 6. Update / BatchUpdate / UpdateOrInsert ===");
if (alice is not null)
{
    alice.Age = 31;
    userService.Update(alice);
    Console.WriteLine($"Update alice Age -> 31");
}
userService.BatchUpdate(projected.Take(2).Cast<AotUser>().ToArray()); // 同一条记录批量更新（幂等演示）
Console.WriteLine("BatchUpdate executed (top 2 viewed users, no-op data).");
userService.UpdateOrInsert(new AotUser { UserName = "frank", Age = 40, CreateTime = DateTime.Now, Guid = Guid.NewGuid(), Duration = TimeSpan.FromSeconds(15) });
Console.WriteLine("UpdateOrInsert frank (auto-inserted).");

Console.WriteLine("\n=== 7. UpdateAll（批量条件更新） ===");
var updateExpr = new UpdateExpr(new TableExpr(typeof(AotUser)), Expr.Prop("UserName") == "bob").Set(("Age", Expr.Const(26)));
int updatedAll = userService.UpdateAll(updateExpr);
Console.WriteLine($"UpdateAll set bob.Age=26 -> affected {updatedAll}.");

Console.WriteLine("\n=== 8. Delete / DeleteID / DeleteAll ===");
int beforeDelete = userService.Count(null);
var dave = userService.SearchOne(Expr.Prop("UserName") == "dave");
if (dave is not null)
{
    userService.Delete(dave);
    Console.WriteLine($"Delete(entity) dave removed.");
}
userService.DeleteID(alice?.Id ?? 0);
Console.WriteLine($"DeleteID(alice.Id = {alice?.Id}) executed.");
var deletedAll = userService.DeleteAllAsync(Expr.Prop("UserName") == "frank").GetAwaiter().GetResult();
Console.WriteLine($"DeleteAllAsync(UserName == frank) -> affected {deletedAll}.");
int afterDelete = userService.Count(null);
Console.WriteLine($"Count(all) change: {beforeDelete} -> {afterDelete}.");

Console.WriteLine("\n--- LiteOrm.AotDemo finished ---");