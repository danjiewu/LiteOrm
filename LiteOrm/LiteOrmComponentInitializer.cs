using Autofac;
using LiteOrm.Common;
using LiteOrm.SqlBuilder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;


namespace LiteOrm
{
    /// <summary>
    /// 在应用程序的依赖注入容器中为 LiteOrm 组件提供初始化逻辑。
    /// </summary>
    /// <remarks>此类通常注册为单例，负责在应用程序启动期间配置核心 LiteOrm 服务，例如会话管理和表信息提供程序。它应被用于确保 LiteOrm 组件在使用之前已正确设置。</remarks>
    [AutoRegister(Lifetime =ServiceLifetime.Singleton)]
    public class LiteOrmComponentInitializer : IComponentInitializer
    {
        
        /// <summary>
        /// 使用指定的组件上下文初始化应用程序范围的服务并注册自定义 SQL 函数。
        /// </summary>
        /// <remarks>此方法设置当前会话管理器和表信息提供程序，并注册用于 SQLite 查询的 'AddHours' SQL 函数。在应用程序启动期间调用此方法以确保所需服务可用。</remarks>
        /// <param name="componentContext">用于解析和配置所需服务的组件上下文。不能为 null。</param>
        public void Initialize(IComponentContext componentContext)
        {
            SessionManager.Current = componentContext.Resolve<SessionManager>();
            TableInfoProvider.Default = componentContext.Resolve<TableInfoProvider>();
            RegisterSqlFunctions();
        }

        private void RegisterSqlFunctions()
        {
            // 注册自定义 SQL 函数
            BaseSqlBuilder.Instance.RegisterFunctionSqlHandler("Now", (functionName, args) => "CURRENT_TIMESTAMP");
            BaseSqlBuilder.Instance.RegisterFunctionSqlHandler("Today", (functionName, args) => "CURRENT_DATE");
            // C# 0-indexed -> SQL 1-indexed 偏移处理
            BaseSqlBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (functionName, args) => args.Count > 2 ?
                        $"INSTR({args[0].Key}, {args[1].Key}, {args[2].Key}+1)-1" : $"INSTR({args[0].Key}, {args[1].Key})-1");
            BaseSqlBuilder.Instance.RegisterFunctionSqlHandler("Substring", (name, args) => args.Count > 2 ?
                        $"SUBSTR({args[0].Key}, {args[1].Key}+1, {args[2].Key})" : $"SUBSTR({args[0].Key}, {args[1].Key}+1)");

            // SQLite
            SQLiteBuilder.Instance.RegisterFunctionSqlHandler("AddHours", (functionName, args) => $"DATETIME({args[0].Key}, '+' || {args[1].Key} || ' hours')");

            // MySQL
            MySqlBuilder.Instance.RegisterFunctionSqlHandler("LENGTH", (functionName, args) => $"CHAR_LENGTH({args[0].Key})");
            MySqlBuilder.Instance.RegisterFunctionSqlHandler("DateAdd", (functionName, args) => $"DATE_ADD({args[0].Key}, INTERVAL {args[2].Key} {args[1].Key.ToUpper()})");
            MySqlBuilder.Instance.RegisterFunctionSqlHandler("DateSub", (functionName, args) => $"DATE_SUB({args[0].Key}, INTERVAL {args[2].Key} {args[1].Key.ToUpper()})");
            MySqlBuilder.Instance.RegisterFunctionSqlHandler("DateDiff", (functionName, args) => $"TIMESTAMPDIFF({args[0].Key}, {args[1].Key}, {args[2].Key})");

            // Oracle
            OracleBuilder.Instance.RegisterFunctionSqlHandler("IfNull", (name, args) => $"NVL({args[0].Key}, {args[1].Key})");

            // PostgreSQL
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (functionName, args) => $"POSITION({args[1].Key} IN {args[0].Key})-1");
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler("Substring", (name, args) => args.Count > 2 ?
                        $"SUBSTRING({args[0].Key} FROM {args[1].Key}+1 FOR {args[2].Key})" : $"SUBSTRING({args[0].Key} FROM {args[1].Key}+1)");

            // SQL Server
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("Length", (functionName, args) => $"LEN({args[0].Key})");
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (functionName, args) => args.Count > 2 ?
                        $"CHARINDEX({args[1].Key}, {args[0].Key}, {args[2].Key}+1)-1" : $"CHARINDEX({args[1].Key}, {args[0].Key})-1");
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("Substring", (name, args) => args.Count > 2 ?
                        $"SUBSTRING({args[0].Key}, {args[1].Key}+1, {args[2].Key})" : $"SUBSTRING({args[0].Key}, {args[1].Key}+1, LEN({args[0].Key}))");
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("Now", (name, args) => "GETDATE()");
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("IfNull", (name, args) => $"ISNULL({args[0].Key}, {args[1].Key})");
        }
    }
}
