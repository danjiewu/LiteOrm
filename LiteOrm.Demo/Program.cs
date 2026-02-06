using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Demo;
using LiteOrm.Demo.Data;
using LiteOrm.Demo.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text;


// 设置控制台输出编码为UTF-8，确保中文正确显示
Console.OutputEncoding = Encoding.UTF8;

// 使用 RegisterLiteOrm 从 appsettings.json 自动配置
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()
    .ConfigureServices(services =>
    {
        // 注册应用程序服务
        services.AddServiceGenerator<ServiceFactory>();
    })
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
    var serviceFactory = scope.ServiceProvider.GetRequiredService<ServiceFactory>();

    Console.WriteLine("\n[1] 表达式全功能演示展示...");
    // 运行表达式全示例演示
    await ExprDemo.RunAllExamplesAsync(serviceFactory);

    Console.WriteLine("\n[2] 三层架构与事务处理演示展示...");
    await ExprDemo.RunThreeTierDemo(serviceFactory);    
}

