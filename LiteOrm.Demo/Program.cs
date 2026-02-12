using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Demo.Data;
using LiteOrm.Demo.Demos;
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

Console.WriteLine("--- LiteOrm 表达式演示程序 ---");

// 执行数据库初始化
using (var initScope = host.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(initScope.ServiceProvider);
}

using (var scope = host.Services.CreateScope())
{
    var serviceFactory = scope.ServiceProvider.GetRequiredService<ServiceFactory>();

    // [1] 表达式全方案演示 (构造、序列化、Lambda转换)
    ExprTypeDemo.RunAll();
    
    // [2] 综合查询实践与 SQL 输出 (业务查询、数据库交互)
    await PracticalQueryDemo.RunAsync(serviceFactory);

    // [3] 业务流程示例 (事务处理)
    await TransactionDemo.RunThreeTierDemoAsync(serviceFactory);

    // [4] DataViewDAO 演示 (直接返回 DataTable)
    await DataViewDemo.RunAsync(scope.ServiceProvider);

    // [5] UpdateExpr 演示 (复杂更新操作)
    await UpdateExprDemo.RunAsync(serviceFactory);
}

