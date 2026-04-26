using LiteOrm;
using LiteOrm.CodeGen;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

OracleConfiguration.BindByName = true;

var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()
    .ConfigureServices(services =>
    {
        services.AddSingleton<DatabaseSchemaReader>();
        services.AddSingleton<EntityCodeGenerator>();
        services.AddSingleton<SelectArtifactsGenerator>();
        services.AddSingleton<CodeGenService>();
    })
    .Build();

var command = CommandLineArguments.Parse(args);
if (command == null)
{
    Console.WriteLine(CommandLineArguments.HelpText);
    return;
}

using var scope = host.Services.CreateScope();
var service = scope.ServiceProvider.GetRequiredService<CodeGenService>();

switch (command.Name)
{
    case "entity":
        {
            var options = new EntityGenerationOptions
            {
                Namespace = command.GetValue("namespace") ?? "Generated.Models",
                Tables = command.GetCsv("table")
            };
            var result = service.GenerateEntities(command.GetValue("data-source"), options.Tables, options);
            WriteOutput(command.GetValue("output"), result.CombinedCode);
            break;
        }
    case "select":
        {
            var sql = command.GetValue("sql");
            if (string.IsNullOrWhiteSpace(sql))
                throw new InvalidOperationException("select 命令必须提供 --sql。");

            var result = service.GenerateSelectArtifacts(command.GetValue("data-source"), sql, new SelectGenerationOptions
            {
                Namespace = command.GetValue("namespace") ?? "Generated.Models",
                ViewName = command.GetValue("view-name") ?? "GeneratedView"
            });

            var output = new StringBuilder();
            foreach (var diagnostic in result.Diagnostics)
            {
                output.AppendLine($"[{diagnostic.Severity}] {diagnostic.Message}");
                if (!string.IsNullOrWhiteSpace(diagnostic.SqlFragment))
                    output.AppendLine($"  SQL: {diagnostic.SqlFragment}");
                if (!string.IsNullOrWhiteSpace(diagnostic.Hint))
                    output.AppendLine($"  Hint: {diagnostic.Hint}");
            }

            if (result.Succeeded)
            {
                output.AppendLine("=== Related Entities ===");
                output.AppendLine(result.RelatedEntityCode);
                output.AppendLine();
                output.AppendLine("=== View Definition ===");
                output.AppendLine(result.ViewCode);
                output.AppendLine();
                output.AppendLine("=== Expr Builder Code ===");
                output.AppendLine(result.QueryCode);
            }

            WriteOutput(command.GetValue("output"), output.ToString().TrimEnd());
            break;
        }
    default:
        Console.WriteLine(CommandLineArguments.HelpText);
        break;
}

static void WriteOutput(string? outputPath, string content)
{
    if (string.IsNullOrWhiteSpace(outputPath))
    {
        Console.WriteLine(content);
        return;
    }

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
    File.WriteAllText(outputPath, content, Encoding.UTF8);
    Console.WriteLine($"Written to {outputPath}");
}

file sealed class CommandLineArguments
{
    private readonly Dictionary<string, string> _values;

    private CommandLineArguments(string name, Dictionary<string, string> values)
    {
        Name = name;
        _values = values;
    }

    public string Name { get; }

    public static string HelpText =>
        """
        LiteOrm.CodeGen

        Commands:
          entity --data-source SQLite --namespace Demo.Models --table Users,Departments
          select --data-source SQLite --view-name UserReportView --sql "SELECT u.Id, d.Name AS DeptName FROM Users u LEFT JOIN Departments d ON u.DeptId = d.Id"

        Options:
          --data-source <name>   LiteOrm data source name
          --namespace <name>     Generated C# namespace
          --table <csv>          Tables for entity generation
          --view-name <name>     View class name for select generation
          --sql <text>           Input SELECT SQL
          --output <path>        Optional file output path
        """;

    public static CommandLineArguments? Parse(string[] args)
    {
        if (args.Length == 0)
            return null;

        string name = args[0].Trim().ToLowerInvariant();
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
                continue;

            string key = args[i][2..];
            string value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i]
                : "true";
            values[key] = value;
        }

        return new CommandLineArguments(name, values);
    }

    public string? GetValue(string key)
    {
        return _values.TryGetValue(key, out var value) ? value : null;
    }

    public List<string>? GetCsv(string key)
    {
        var raw = GetValue(key);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}
