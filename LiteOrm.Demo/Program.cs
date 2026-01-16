using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using LiteOrm;
using LiteOrm.Demo;
using LiteOrm.Demo.Services;
using LiteOrm.Demo.Data;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.DAO;


// 使用 RegisterLiteOrm 从 appsettings.json 自动配置
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()
    .Build();

Console.WriteLine("--- LiteOrm 示例程序 (DI & Configuration) ---");

// 执行数据库初始化
using (var initScope = host.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(initScope.ServiceProvider);
}

using (var scope = host.Services.CreateScope())
{
    // 从容器中获取服务
    var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
    var deptService = scope.ServiceProvider.GetRequiredService<IDepartmentService>();
    var salesService = scope.ServiceProvider.GetRequiredService<ISalesService>();
    var businessService = scope.ServiceProvider.GetRequiredService<IBusinessService>();
    var userDao = scope.ServiceProvider.GetRequiredService<IUserCustomDAO>();

    Console.WriteLine("\n[1] 表达式全功能演示展示...");
    // 运行表达式全示例演示
    await ExprDemo.RunAllExamplesAsync(userService, salesService, deptService, userDao);

    Console.WriteLine("\n[2] 三层架构与事务处理演示展示...");
    await ExprDemo.RunThreeTierDemo(businessService, userService, salesService);
}

await host.RunAsync();
