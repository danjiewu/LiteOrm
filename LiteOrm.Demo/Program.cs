using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Demo.Data;
using LiteOrm.Demo.Demos;
using LiteOrm.Demo.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq.Expressions;
using System.Text;

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

// 注册窗口函数扩展处理器（必须在任何查询执行前完成）
WindowFunctionDemo.RegisterHandlers();

// 执行数据库初始化
using (var initScope = host.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(initScope.ServiceProvider);
}

using (var scope = host.Services.CreateScope())
{
    var serviceFactory = scope.ServiceProvider.GetRequiredService<ServiceFactory>();

    // 1. 表达式全方案演示 (1.1-1.5: 基础、比较、结构化、Lambda转换、删除)
    ExprTypeDemo.RunAll();

    // 2. 综合查询实践与 SQL 输出 (2.1-2.5: Lambda链式、序列化、等价性、复杂过滤、ExprString)
    await PracticalQueryDemo.RunAsync(serviceFactory);

    // 3. 业务流程示例 (事务处理)
    await TransactionDemo.RunThreeTierDemoAsync(serviceFactory);

    // 4. 分表查询演示 (4.1-4.4: 基础、显式参数、Expr参数、排序)
    await ShardingQueryDemo.RunAsync(serviceFactory);

    // 5. 窗口函数演示 (5.1-5.2: 仅分区、分区+排序)
    await WindowFunctionDemo.RunAsync(serviceFactory);

    // 6. UpdateExpr 更新表达式演示 (6.1-6.5: 列表初始化、链式Set、算术运算、FunctionExpr、Lambda条件)
    await UpdateExprDemo.RunAsync(serviceFactory);

    // 7. ExistsRelated 关联过滤演示 (7.1-7.4: 正向用户、正向部门、NOT EXISTS、多条件组合)
    await ExistsRelatedDemo.RunAsync(serviceFactory);
}


