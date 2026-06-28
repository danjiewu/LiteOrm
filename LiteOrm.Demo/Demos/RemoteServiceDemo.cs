using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using LiteOrm.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 远程服务调用演示。
    /// <para>
    /// 演示完整的远程客户端配置流程：
    /// 1. 从 <c>appsettings.json</c> 读取 <c>RemoteService</c> 配置节（远程服务地址与路径）；
    /// 2. 通过 <see cref="LiteOrmRemoteExtensions.RegisterLiteOrmRemote"/> 注册远程调用基础设施
    ///    （传输层、AutoRegister 扫描、<c>RemoteServiceInvokeInterceptor</c> 等）；
    /// 3. 通过 <see cref="LiteOrmRemoteExtensions.AddRemoteServiceGenerator{TService}"/>
    ///    注册 <see cref="RemoteServiceFactory"/> 工厂代理——该方法自动扫描工厂的所有属性与方法返回类型，
    ///    将未注册的接口类型（<c>IDemoUserService</c>、<c>IDemoOrderService</c>、<c>IDemoDepartmentService</c>）自动注册为远程代理；
    /// 4. 从工厂获取远程服务并调用——使用方式与本地 <see cref="ServiceFactory"/> 完全一致。
    /// </para>
    /// <para>
    /// 演示场景覆盖：
    /// <list type="bullet">
    /// <item>新增实体并回写 Identity（<see cref="IDemoUserService.InsertAsync"/>）</item>
    /// <item>按主键查询（<see cref="IEntityViewServiceAsync{T}.GetObjectAsync(object, string[], CancellationToken)"/>）</item>
    /// <item>自定义方法查询（<see cref="IDemoUserService.GetByUserNameAsync"/>）</item>
    /// <item>使用 <see cref="Expr"/> 条件查询集合（<see cref="IEntityViewServiceAsync{T}.SearchAsync(Expr, string[], CancellationToken)"/>）</item>
    /// <item>查询单条记录（<see cref="IEntityViewServiceAsync{T}.SearchOneAsync(Expr, string[], CancellationToken)"/>）</item>
    /// <item>条件存在性检查（<see cref="IEntityViewServiceAsync{T}.ExistsAsync"/>）与计数（<see cref="IEntityViewServiceAsync{T}.CountAsync"/>）</item>
    /// <item>批量新增并逐项回写 Identity（<see cref="IEntityServiceAsync{T}.BatchInsertAsync"/>）</item>
    /// <item>批量更新实体（<see cref="IEntityServiceAsync{T}.BatchUpdateAsync"/>）</item>
    /// <item>更新实体（<see cref="IEntityServiceAsync{T}.UpdateAsync(T, CancellationToken)"/>）</item>
    /// <item>存在则更新、不存在则新增（<see cref="IEntityServiceAsync{T}.UpdateOrInsertAsync"/>）</item>
    /// <item>按条件删除（<see cref="IEntityServiceAsync{T}.DeleteAsync(LogicExpr, string[], CancellationToken)"/>）</item>
    /// <item>按主键删除（<see cref="IEntityServiceAsync{T}.DeleteIDAsync"/>）</item>
    /// <item>遍历查询结果集（<see cref="IEntityViewServiceAsync{T}.ForEachAsync"/>）</item>
    /// </list>
    /// </para>
    /// <para>
    /// 本演示仅完成代码结构，实际运行需要远程服务端（<c>LiteOrm.Remote.Server</c>）部署在配置指定的地址。
    /// </para>
    /// </summary>
    public static class RemoteServiceDemo
    {
        public static async Task RunAsync()
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("  10. 远程服务调用（AddRemoteServiceGenerator 演示）：");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            // 1. 从 appsettings.json 读取 RemoteService 配置节
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var remoteSection = configuration.GetSection("RemoteService");
            var remoteUri = remoteSection["Uri"];
            var remotePath = remoteSection["Path"] ?? "api/remote/invoke";

            if (string.IsNullOrEmpty(remoteUri))
            {
                Console.WriteLine("未配置 RemoteService:Uri，跳过远程服务演示。");
                return;
            }

            Console.WriteLine($"远程服务地址：{remoteUri}（路径：{remotePath}）");

            // 2. 构建远程客户端主机
            //    RegisterLiteOrmRemote 完成：
            //    - 注册 IRemoteServiceTransport（基于 HttpClient 的 HttpRemoteServiceTransport）
            //    - 配置 Autofac 容器（UseServiceProviderFactory + ConfigureContainer）
            //    - 扫描 [AutoRegister] 类型注册到 Autofac：
            //      RemoteServiceInvokeInterceptor、RemoteServiceGenerateInterceptor、
            //      AttributeTableInfoProvider（作为 TableInfoProvider）、
            //      LiteOrmRemoteInitializer（IStartable，自动设置 TableInfoProvider.Default）
            //    - AutoRegisterEntityServices = true 通过 RemoteServiceProxyRegistrationSource
            //      （Autofac IRegistrationSource）按需为 IEntityServiceAsync<T>、IEntityViewServiceAsync<T>
            //      及继承自它们的自定义接口（如 IDemoUserService）创建远程代理，
            //      无需扫描 [Table] 特性逐个注册
            var host = Host.CreateDefaultBuilder()
                .RegisterLiteOrmRemote(opts =>
                {
                    opts.RemoteServiceUri = new Uri(remoteUri);
                    opts.RemoteServicePath = remotePath;
                    // 自动注册所有实体服务为远程代理，包括泛型 IEntityServiceAsync<T> 和 IEntityViewServiceAsync<T>
                    // 通过 Autofac IRegistrationSource 按需创建代理，无需扫描 [Table] 特性
                    opts.AutoRegisterEntityServices = true;
                })
                .ConfigureServices(services =>
                {
                    // 3. 注册远程服务工厂代理
                    //    由于 AutoRegisterEntityServices 已通过 IRegistrationSource 按需注册所有实体服务接口，
                    //    此处注册工厂代理仅是为了演示工厂模式访问方式。
                    //    AddRemoteServiceGenerator 自动扫描 RemoteServiceFactory 的所有属性与方法返回类型，
                    //    将未注册的接口类型自动注册为远程代理（已注册的不会覆盖）。
                    services.AddRemoteServiceGenerator<RemoteServiceFactory>();
                })
                .Build();

            try
            {
                using var scope = host.Services.CreateScope();
                var factory = scope.ServiceProvider.GetRequiredService<RemoteServiceFactory>();

                Console.WriteLine($"已创建远程服务工厂代理：{factory.GetType().Name}");
                Console.WriteLine($"  - DemoUserService       => {factory.DemoUserService.GetType().Name}");
                Console.WriteLine($"  - DemoOrderService      => {factory.DemoOrderService.GetType().Name}");
                Console.WriteLine($"  - DemoDepartmentService => {factory.DemoDepartmentService.GetType().Name}");

                // 4. 各业务服务接口也可直接从 DI 容器解析
                //    AutoRegisterEntityServices 已自动注册所有实体服务接口（含自定义接口和泛型接口）
                var userService = scope.ServiceProvider.GetRequiredService<IDemoUserService>();
                var orderService = scope.ServiceProvider.GetRequiredService<IDemoOrderService>();
                Console.WriteLine($"  - 直接解析 IDemoUserService => {userService.GetType().Name}");
                Console.WriteLine($"  - 直接解析 IDemoOrderService => {orderService.GetType().Name}");

                // 4.1 泛型接口也可直接解析（AutoRegisterEntityServices 注册了 IEntityServiceAsync<T> 和 IEntityViewServiceAsync<T>）
                var genericUserService = scope.ServiceProvider.GetRequiredService<IEntityServiceAsync<DemoUser>>();
                var genericUserViewService = scope.ServiceProvider.GetRequiredService<IEntityViewServiceAsync<DemoUserView>>();
                Console.WriteLine($"  - 直接解析 IEntityServiceAsync<DemoUser> => {genericUserService.GetType().Name}");
                Console.WriteLine($"  - 直接解析 IEntityViewServiceAsync<DemoUserView> => {genericUserViewService.GetType().Name}");

                // 5. 远程调用演示：所有调用通过 RemoteServiceInvokeInterceptor 转发到远程服务端
                //    实际运行需要远程服务端部署在配置指定的地址
                try
                {
                    await DemonstrateUserAsync(factory.DemoUserService);
                    await DemonstrateOrderAsync(factory.DemoOrderService);
                    await DemonstrateDepartmentAsync(factory.DemoDepartmentService);
                }
                catch (Exception ex)
                {
                    // 远程服务端未运行时，调用会失败——此处仅演示客户端代码结构
                    Console.WriteLine($"远程调用失败（远程服务端未运行时属正常现象）：{ex.Message}");
                }
            }
            catch
            {
            }
            finally
            {
                await host.StopAsync();
                host.Dispose();
            }
        }

        /// <summary>
        /// 演示 <see cref="IDemoUserService"/> 的远程调用：新增、查询、存在性检查、计数、更新、删除。
        /// </summary>
        private static async Task DemonstrateUserAsync(IDemoUserService userService)
        {
            Console.WriteLine("\n━━━ 用户服务演示 ━━━");

            // 5.1 新增实体并回写 Identity
            //     [IdentityOut] 标记的参数在服务端执行后，Id 会通过 WriteBackArguments 回写到客户端对象
            var user = new DemoUser
            {
                UserName = "alice",
                DisplayName = "Alice Wonderland",
                Role = "Admin",
                CreatedTime = DateTime.Now,
            };
            await userService.InsertAsync(user);
            Console.WriteLine($"[InsertAsync] 新增用户成功，Identity 回写：Id={user.Id}");

            // 5.2 按主键查询
            var loaded = await userService.GetObjectAsync(user.Id);
            Console.WriteLine($"[GetObjectAsync] 按主键查询：UserName={loaded?.UserName}, DisplayName={loaded?.DisplayName}");

            // 5.3 自定义方法查询
            var byName = await userService.GetByUserNameAsync("alice");
            Console.WriteLine($"[GetByUserNameAsync] 按用户名查询：Id={byName?.Id}, DepartmentName={byName?.DepartmentName}");

            // 5.4 使用 Lambda 条件查询单条记录
            //     SearchOneAsync 在条件匹配多条时返回首条，无匹配返回 null
            var one = await userService.SearchOneAsync(u => u.UserName == "alice");
            Console.WriteLine($"[SearchOneAsync] 按用户名查询单条：Id={one?.Id}, Role={one?.Role}");

            // 5.5 使用 Lambda 条件查询集合（条件会被转换为 Expr 并按 Expr 参数序列化，无需额外类型信息）
            var users = await userService.SearchAsync(u => u.Role == "Admin");
            Console.WriteLine($"[SearchAsync] 角色为 Admin 的用户数量：{users.Count}");
            foreach (var u in users)
                Console.WriteLine($"  - Id={u.Id}, UserName={u.UserName}");

            // 5.6 条件存在性检查与计数
            var exists = await userService.ExistsAsync(u => u.UserName == "alice");
            var adminCount = await userService.CountAsync(u => u.Role == "Admin");
            Console.WriteLine($"[ExistsAsync] 是否存在 UserName=alice 的用户：{exists}");
            Console.WriteLine($"[CountAsync] 角色为 Admin 的用户数量：{adminCount}");

            // 5.7 更新实体
            user.DisplayName = "Alice Updated";
            await userService.UpdateAsync(user);
            Console.WriteLine($"[UpdateAsync] 更新用户：DisplayName={user.DisplayName}");

            // 5.8 按条件删除
            var deletedCount = await userService.DeleteAsync(u => u.UserName == "alice");
            Console.WriteLine($"[DeleteAsync(LogicExpr)] 按条件删除数量：{deletedCount}");
        }

        /// <summary>
        /// 演示 <see cref="IDemoOrderService"/> 的远程调用：批量新增（含集合模式 Identity 回写）、批量更新、查询、遍历、删除。
        /// </summary>
        private static async Task DemonstrateOrderAsync(IDemoOrderService orderService)
        {
            Console.WriteLine("\n━━━ 订单服务演示 ━━━");

            // 6.1 批量新增并逐项回写 Identity
            //     [IdentityOut(Mode = ArgumentMode.Collection)] 标记的集合参数，
            //     服务端会对集合中每个元素逐项回写 Identity
            var orders = new List<DemoOrder>
            {
                new DemoOrder { OrderNo = "ORD-001", CustomerName = "Customer A", ProductName = "Product X", Quantity = 2, UnitPrice = 99.5m, TotalAmount = 199.0m, Status = DemoOrderStatuses.Pending, CreatedTime = DateTime.Now },
                new DemoOrder { OrderNo = "ORD-002", CustomerName = "Customer B", ProductName = "Product Y", Quantity = 1, UnitPrice = 1500m, TotalAmount = 1500m, Status = DemoOrderStatuses.Paid, CreatedTime = DateTime.Now },
                new DemoOrder { OrderNo = "ORD-003", CustomerName = "Customer A", ProductName = "Product Z", Quantity = 5, UnitPrice = 25.0m, TotalAmount = 125.0m, Status = DemoOrderStatuses.Shipped, CreatedTime = DateTime.Now },
            };
            await orderService.BatchInsertAsync(orders);
            Console.WriteLine("[BatchInsertAsync] 批量新增订单成功，逐项回写 Identity：");
            foreach (var o in orders)
                Console.WriteLine($"  - OrderNo={o.OrderNo}, Id={o.Id}");

            // 6.2 使用 Lambda 复合条件查询（And / Equal / NotEqual）
            var customerAOrders = await orderService.SearchAsync(o =>
                o.CustomerName == "Customer A" && o.Status != DemoOrderStatuses.Cancelled);
            Console.WriteLine($"[SearchAsync] Customer A 的未取消订单数量：{customerAOrders.Count}");

            // 6.3 查询单条记录并按条件计数
            var firstOrder = await orderService.SearchOneAsync(o => o.OrderNo == "ORD-001");
            var customerACount = await orderService.CountAsync(o => o.CustomerName == "Customer A");
            Console.WriteLine($"[SearchOneAsync] 按订单号查询单条：Id={firstOrder?.Id}, Status={firstOrder?.Status}");
            Console.WriteLine($"[CountAsync] Customer A 的订单数量：{customerACount}");

            // 6.4 批量更新实体
            //     注意：BatchUpdateAsync 不带 [IdentityOut]，仅按主键更新，不会回写 Id
            foreach (var o in customerAOrders)
                o.Status = DemoOrderStatuses.Completed;
            await orderService.BatchUpdateAsync(customerAOrders);
            Console.WriteLine($"[BatchUpdateAsync] 批量更新 {customerAOrders.Count} 条订单状态为 Completed");

            // 6.5 使用 ForEachAsync 流式遍历查询结果（适合大结果集，避免一次性加载到内存）
            //     注意：ForEachAsync 没有 Lambda 扩展方法，需通过 Expr.Lambda 显式构造条件
            Console.WriteLine("[ForEachAsync] 遍历 Customer A 的订单：");
            var visited = 0;
            await orderService.ForEachAsync(
                Expr.Lambda<DemoOrder>(o => o.CustomerName == "Customer A"),
                async o =>
                {
                    Interlocked.Increment(ref visited);
                    await Task.CompletedTask;
                    Console.WriteLine($"  - 访问：OrderNo={o.OrderNo}, Status={o.Status}");
                });
            Console.WriteLine($"[ForEachAsync] 共访问 {visited} 条订单");

            // 6.6 使用 Lambda 条件删除
            var deletedCount = await orderService.DeleteAsync(o => o.CustomerName == "Customer A");
            Console.WriteLine($"[DeleteAsync(LogicExpr)] 删除 Customer A 的订单数量：{deletedCount}");
        }

        /// <summary>
        /// 演示 <see cref="IDemoDepartmentService"/> 的远程调用：新增、存在则更新否则新增、按主键删除。
        /// </summary>
        private static async Task DemonstrateDepartmentAsync(IDemoDepartmentService departmentService)
        {
            Console.WriteLine("\n━━━ 部门服务演示 ━━━");

            // 7.1 新增部门并回写 Identity
            var dept = new DemoDepartment
            {
                Name = "研发部",
                Code = "R&D",
            };
            await departmentService.InsertAsync(dept);
            Console.WriteLine($"[InsertAsync] 新增部门成功，Identity 回写：Id={dept.Id}");

            // 7.2 UpdateOrInsert 场景1：主键已存在 → 执行 Update
            //     因为 dept.Id 已经被 Identity 回写，此处调用会按主键更新而非新增
            dept.Name = "研发一部";
            await departmentService.UpdateOrInsertAsync(dept);
            Console.WriteLine($"[UpdateOrInsertAsync] 主键已存在，执行更新：Name={dept.Name}");

            // 7.3 UpdateOrInsert 场景2：主键为 0（默认值）→ 执行 Insert 并回写新 Identity
            var newDept = new DemoDepartment { Name = "市场部", Code = "MKT" };
            await departmentService.UpdateOrInsertAsync(newDept);
            Console.WriteLine($"[UpdateOrInsertAsync] 主键为 0，执行新增并回写：Id={newDept.Id}, Name={newDept.Name}");

            // 7.4 按主键查询验证
            var loaded = await departmentService.GetObjectAsync(newDept.Id);
            Console.WriteLine($"[GetObjectAsync] 按主键查询：Name={loaded?.Name}, Code={loaded?.Code}");

            // 7.5 按主键删除（与按条件删除 DeleteAsync(LogicExpr) 形成对比）
            await departmentService.DeleteIDAsync(newDept.Id);
            Console.WriteLine($"[DeleteIDAsync] 按主键删除 Id={newDept.Id}");

            var exists = await departmentService.ExistsAsync(d => d.Id == newDept.Id);
            Console.WriteLine($"[ExistsAsync] 删除后是否仍存在：{exists}");
        }
    }
}
