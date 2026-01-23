using Autofac;
using LiteOrm.Common;
using LiteOrm.SqlBuilder;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm SQL 函数初始化器，负责注册各数据库方言的 SQL 函数生成逻辑。
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Singleton)]
    public class LiteOrmSqlFunctionInitializer : IComponentInitializer
    {
        /// <summary>
        /// 使用指定的组件上下文初始化 SQL 函数映射。
        /// </summary>
        /// <param name="componentContext">用于解析服务的组件上下文。</param>
        public void Initialize(IComponentContext componentContext)
        {
            RegisterSqlFunctions();
        }

        /// <summary>
        /// 注册各数据库方言的 SQL 函数生成逻辑。
        /// </summary>
        private void RegisterSqlFunctions()
        {
            // 注册跨库通用的 SQL 映射
            BaseSqlBuilder.Instance.RegisterFunctionSqlHandler("Now", (functionName, args) => "CURRENT_TIMESTAMP");
            BaseSqlBuilder.Instance.RegisterFunctionSqlHandler("Today", (functionName, args) => "CURRENT_DATE");
            // 特殊处理 IndexOf 和 Substring，支持 C# 到 SQL 的索引转换 (0-based -> 1-based)
            BaseSqlBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (functionName, args) => args.Count > 2 ?
                        $"INSTR({args[0].Key}, {args[1].Key}, {args[2].Key}+1)-1" : $"INSTR({args[0].Key}, {args[1].Key})-1");
            BaseSqlBuilder.Instance.RegisterFunctionSqlHandler("Substring", (name, args) => args.Count > 2 ?
                        $"SUBSTR({args[0].Key}, {args[1].Key}+1, {args[2].Key})" : $"SUBSTR({args[0].Key}, {args[1].Key}+1)");

            // SQLite 方言配置
            SQLiteBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
                (functionName, args) => $"DATE({args[0].Key}, CAST({args[1].Key} AS TEXT)||' {functionName.Substring(3).ToLower()}')");

            // MySQL 方言配置
            MySqlBuilder.Instance.RegisterFunctionSqlHandler("LENGTH", (functionName, args) => $"CHAR_LENGTH({args[0].Key})");
            MySqlBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
                (functionName, args) => $"DATE_ADD({args[0].Key}, INTERVAL {args[1].Key} {functionName.Substring(3).ToUpper().TrimEnd('S')})");

            // Oracle 方言配置
            OracleBuilder.Instance.RegisterFunctionSqlHandler("IfNull", (name, args) => $"NVL({args[0].Key}, {args[1].Key})");
            OracleBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays"], (functionName, args) => $"({args[0].Key} + NUMTODSINTERVAL({args[1].Key}, '{functionName.Substring(3).ToUpper().TrimEnd('S')}'))");
            OracleBuilder.Instance.RegisterFunctionSqlHandler(["AddMonths", "AddYears"], (functionName, args) => $"({args[0].Key} + NUMTOYMINTERVAL({args[1].Key}, '{functionName.Substring(3).ToUpper().TrimEnd('S')}'))");

            // PostgreSQL 方言配置
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (functionName, args) => $"POSITION({args[1].Key} IN {args[0].Key})-1");
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler("Substring", (name, args) => args.Count > 2 ?
                        $"SUBSTRING({args[0].Key} FROM {args[1].Key}+1 FOR {args[2].Key})" : $"SUBSTRING({args[0].Key} FROM {args[1].Key}+1)");
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
                (functionName, args) => $"({args[0].Key} + ({args[1].Key} || ' {functionName.Substring(3).ToLower()}')::interval)");

            // SQL Server 方言配置
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("IfNull", (name, args) => $"ISNULL({args[0].Key}, {args[1].Key})");
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("Length", (functionName, args) => $"LEN({args[0].Key})");
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (functionName, args) => args.Count > 2 ?
                        $"CHARINDEX({args[1].Key}, {args[0].Key}, {args[2].Key}+1)-1" : $"CHARINDEX({args[1].Key}, {args[0].Key})-1");
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("Substring", (name, args) => args.Count > 2 ?
                        $"SUBSTRING({args[0].Key}, {args[1].Key}+1, {args[2].Key})" : $"SUBSTRING({args[0].Key}, {args[1].Key}+1, LEN({args[0].Key}))");
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
                (functionName, args) => $"DATEADD({functionName.Substring(3).ToLower().TrimEnd('s')}, {args[1].Key}, {args[0].Key})");
        }
    }
}
