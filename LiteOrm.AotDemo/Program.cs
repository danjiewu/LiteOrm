using LiteOrm;
using LiteOrm.AotDemo.Models;
using LiteOrm.AotDemo.Services;
using LiteOrm.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

// 插入
await userService.InsertAsync(new AotUser { UserName = "alice", Age = 30, CreateTime = DateTime.Now });
await userService.InsertAsync(new AotUser { UserName = "bob", Age = 25, CreateTime = DateTime.Now });
Console.WriteLine("Inserted 2 users.");

// 查询（Expr 路径，返回 AotUserView 列表）
var users = userService.Search(Expr.Prop("Age") > 20);
Console.WriteLine($"Found {users.Count} users with Age > 20:");
foreach (var u in users)
{
    Console.WriteLine($"  Id={u.Id}, UserName={u.UserName}, Age={u.Age}");
}

// 计数
var count = userService.Count(Expr.Prop("Age") > 20);
Console.WriteLine($"Count(Age > 20) = {count}");

// 查询单个
var alice = userService.SearchOne(Expr.Prop("UserName") == "alice");
if (alice is null)
{
    Console.WriteLine("alice not found");
    return;
}
Console.WriteLine($"Found alice: Id={alice.Id}, Age={alice.Age}");

// 更新
alice.Age = 31;
await userService.UpdateAsync(alice);
Console.WriteLine($"Updated alice's Age to {alice.Age}");

// 按条件删除
var deleted = await userService.DeleteAllAsync(Expr.Prop("Id") == alice.Id);
Console.WriteLine($"DeleteAll(Id == alice.Id) affected rows: {deleted}");

// 剩余计数
var remaining = userService.Count(null);
Console.WriteLine($"Remaining count: {remaining}");

Console.WriteLine("--- LiteOrm.AotDemo finished ---");
