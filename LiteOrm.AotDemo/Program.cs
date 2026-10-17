using LiteOrm;
using LiteOrm.AotDemo.Scenarios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ──────────────────────────────────────────────────────────────────
// LiteOrm.AotDemo
//
// 本工程用于验证 LiteOrm 在 NativeAOT 编译下的端到端可用性：
//   - 数据源配置通过 appsettings.json 加载（IConfiguration → DataSourceProvider）
//   - 实体元数据由 TableInfoGenerator 在编译期生成（无运行时反射）
//   - 关联元数据（TableJoin / ForeignType / ForeignColumn）同样在编译期落进
//     TableInfo，运行期由 CommonTableInfoProvider 装配出完整的 TableView
//   - DataReader 映射委托、属性访问器由源生成器在编译期注册
//   - SqlBuilder / DbConnection 派生类型由源生成器预注册到 TypeResolverHelper
//   - 用户 Service 由 [AutoRegister] 在编译期生成 MS DI 注册代码
//
// 场景划分：
//   1. 单表 CRUD（同步/异步、单条/批量、条件更新、三种删除）
//   2. 实体关联（ForeignColumn 投影、关联列过滤/排序/分页、TableJoin 自关联）
//   3. 自定义投影与聚合（SearchAs / GroupBy / SearchOneAs）
//   4. JSON 列与计算列（JSON 路径过滤、计算列参与过滤）
//   5. 事务与固定过滤（显式事务回滚/提交、TableDefinition.ConstFilter）
//
// 注意：本工程不引用 LiteOrm.DependencyInjection（基于 Autofac/Castle DynamicProxy，
// 依赖运行时反射与动态程序集生成，不支持 NativeAOT）。改用基础库 AddLiteOrm()
// 进行纯 MS DI 注册；因此 [Transaction] 拦截特性也不可用，事务改为显式 API。
// ──────────────────────────────────────────────────────────────────

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
var sp = scope.ServiceProvider;

Console.WriteLine("LiteOrm AOT 演示（元数据全部在编译期生成，运行期不读特性）");

try
{
    DemoData.Reset(sp);

    await CrudScenario.RunAsync(sp);
    await AssociationScenario.RunAsync(sp);
    await ProjectionScenario.RunAsync(sp);
    await JsonAndComputedScenario.RunAsync(sp);
    await TransactionAndFilterScenario.RunAsync(sp);
}
catch (Exception ex)
{
    DemoOutput.Section("演示中断");
    DemoOutput.Error(ex);
    Console.WriteLine(ex.StackTrace);
    return 1;
}

Console.WriteLine();
Console.WriteLine("--- LiteOrm.AotDemo finished ---");
return 0;
