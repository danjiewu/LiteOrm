using LogRecord;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using LiteOrm.Common;
using LiteOrm.Oracle;
using LiteOrm.Service;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Text.Json;


namespace LiteOrm.Test
{
    public class Program
    {
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
            .RegisterLiteOrm()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IConfiguration>(configuration)
                        .AddLogging(builder =>
                            builder.AddConsole()
                            .SetMinimumLevel(LogLevel.Debug));
            })
            .Build();

            // 使用ServiceProvider
            var serviceProvider = host.Services;
            var dao = serviceProvider.GetRequiredService<IObjectDAO<Session>>();
            var service = serviceProvider.GetRequiredService<IAccountingLogService>();
            var ids = new[] { 1, 2, 3 };
            var inCondition = Expr.Exp<AccountingLog>(l => ids.Contains(l.AcctStatusType.Value) && l.AcctInputOctets > 0 && l.Id.ToString().Length == 5);
            var json = JsonSerializer.Serialize(inCondition, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
            var expr = JsonSerializer.Deserialize<Expr>(json);
            Console.WriteLine(expr);
            var logs = service.SearchSection(expr, new PageSection().Take(1000), ["202501"]);

            service.BatchInsert(logs);

            foreach (var log in logs)
            {
                Console.WriteLine($"{log.Id}, {log.UserName}, {log.AcctInputOctets}, {log.AcctOutputOctets}");
            }
            Console.WriteLine("Finished. Press any key to exit.");

        }
    }

}
