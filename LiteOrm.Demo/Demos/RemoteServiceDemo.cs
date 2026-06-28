using LiteOrm;
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
    ///    将未注册的接口类型（<c>IUserService</c>、<c>ISalesService</c> 等）自动注册为远程代理；
    /// 4. 从工厂获取远程服务并调用——使用方式与本地 <see cref="ServiceFactory"/> 完全一致。
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
            //    - 切换到 Autofac 容器并执行 AutoRegister 扫描
            //      （RemoteServiceInvokeInterceptor、RemoteServiceGenerateInterceptor 等自动注册）
            //    - 通过 IStartable 设置 TableInfoProvider.Default
            var host = Host.CreateDefaultBuilder()
                .RegisterLiteOrmRemote(opts =>
                {
                    opts.RemoteServiceUri = new Uri(remoteUri);
                    opts.RemoteServicePath = remotePath;
                })
                .ConfigureServices(services =>
                {
                    // 3. 注册远程服务工厂代理
                    //    AddRemoteServiceGenerator 自动扫描 RemoteServiceFactory 的所有属性与方法返回类型
                    //    （IUserService、ISalesService、IBusinessService、IDepartmentService），
                    //    将未注册的接口类型自动注册为远程代理（通过 RemoteServiceInvokeInterceptor 转发）。
                    //    因此无需再手动调用 AddRemoteService<IUserService>() 等逐个注册。
                    services.AddRemoteServiceGenerator<RemoteServiceFactory>();
                })
                .Build();

            try
            {
                using var scope = host.Services.CreateScope();
                var factory = scope.ServiceProvider.GetRequiredService<RemoteServiceFactory>();

                Console.WriteLine($"已创建远程服务工厂代理：{factory.GetType().Name}");
                Console.WriteLine($"  - UserService   => {factory.UserService.GetType().Name}");
                Console.WriteLine($"  - SalesService  => {factory.SalesService.GetType().Name}");
                Console.WriteLine($"  - BusinessService => {factory.BusinessService.GetType().Name}");
                Console.WriteLine($"  - DepartmentService => {factory.DepartmentService.GetType().Name}");

                // 4. 通过工厂调用远程服务（使用方式与本地 ServiceFactory 完全一致）
                //    实际运行需要远程服务端部署在配置指定的地址
                try
                {
                    Console.WriteLine("\n尝试远程调用 UserService.GetByUserNameAsync(\"alice\")...");
                    var user = await factory.UserService.GetByUserNameAsync("alice");
                    Console.WriteLine(user is null
                        ? "远程返回：用户不存在。"
                        : $"远程返回：用户 ID={user.Id}, 用户名={user.UserName}");
                }
                catch (Exception ex)
                {
                    // 远程服务端未运行时，调用会失败——此处仅演示客户端代码结构
                    Console.WriteLine($"远程调用失败（远程服务端未运行时属正常现象）：{ex.Message}");
                }
            }
            finally
            {
                await host.StopAsync();
                host.Dispose();
            }
        }
    }
}
