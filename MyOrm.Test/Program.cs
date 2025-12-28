using DAL;
using DAL.Data;
using LogRecord;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using MyOrm.Common;
using MyOrm.Oracle;
using MyOrm.Service;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;


namespace MyOrm.Test
{
    class Program
    {
        private static readonly SemaphoreSlim _batchSemaphore = new SemaphoreSlim(1); // 并发度 1
        private static Task currentTask = Task.CompletedTask;
        static async Task Main(string[] args)
        {
            OracleConfiguration.BindByName = true;
            OracleBuilder.Instance.IdentitySource = OracleIdentitySourceType.Sequence;
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory()) // 设置配置文件目录
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true) // 加载JSON配置
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true) // 环境配置
                .AddEnvironmentVariables() // 读取环境变量
                .Build();

            var host = Host.CreateDefaultBuilder(args)

            .UseServiceProviderFactory(new MyServiceProviderFactory())
            .ConfigureServices(services =>
            {
                services.AddSingleton<IConfiguration>(configuration)
                        .AddLogging(builder =>
                            builder.AddConsole());
            })
            .Build();

            // 使用ServiceProvider
            var serviceProvider = host.Services;
            var scope = serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IAccountintLogService>();
            var logs = service.SearchSection(null, new SectionSet() { SectionSize = 1000 }, "202501");


            for (int i = 0; i < 1000; i++)
            {
                int cur = i;
                Console.WriteLine($"第{cur}轮开始.");
                _batchSemaphore.Wait();
                Console.WriteLine($"第{cur}轮任务创建.");                

                // 创建新任务
                currentTask = Task.Run(async () =>
                {
                    try
                    {                        
                        using (var session = SessionManager.Current.EnterContext())
                        {                            
                            Console.WriteLine($"第{cur}轮BatchInsert");
                            var service = scope.ServiceProvider.GetRequiredService<IAccountintLogService>();
                            service.BatchInsert(logs);
                        }
                    }
                    finally
                    {
                        _batchSemaphore.Release();
                        Console.WriteLine($"第{cur}轮完成.");
                    }
                });

                await Task.Delay(100);
            }

            Console.ReadKey();
        }
    }

}
