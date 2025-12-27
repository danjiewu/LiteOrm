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
using System.Threading.Tasks;


namespace MyOrm.Test
{
    class Program
    {
        static void Main(string[] args)
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
            using var sessionScope = scope.ServiceProvider.GetRequiredService<SessionManager>().EnterContext();
            var service = scope.ServiceProvider.GetRequiredService<IAccountintLogService>();
            var logs = service.SearchSection(null, new SectionSet() { SectionSize = 1000 }, "202512");
            for (int i = 0; i < 100; i++)
            {
                Console.WriteLine($"第{i}轮：");
                Task.WaitAny(Task.Delay(500),
                Task.Run(() => { service.BatchInsert(logs); }));
            }

            Console.ReadKey();
        }
    }

}
