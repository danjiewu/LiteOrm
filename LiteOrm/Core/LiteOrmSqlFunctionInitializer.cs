using Autofac;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm SQL ??????? SQL ?
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Singleton)]
    public class LiteOrmSqlFunctionInitializer : IStartable
    {
        /// <summary>
        /// Æô¶¯Ê±³õÊ¼»¯ SQL º¯ÊýÓ³Éä¡£
        /// </summary>
        public void Start()
        {
            RegisterSqlFunctions();
        }

        /// <summary>
        /// ?????? SQL ?
        /// </summary>
        private void RegisterSqlFunctions()
        {
            // ×¢²áÒ»Ð©Í¨ÓÃµÄ SQL Ó³Éä
            SqlBuilder.Instance.RegisterFunctionSqlHandler("Now", (functionName, args) => "CURRENT_TIMESTAMP");
            SqlBuilder.Instance.RegisterFunctionSqlHandler("Today", (functionName, args) => "CURRENT_DATE");
            // ¶îÍâ´¦Àí IndexOf ºÍ Substring£¬Ö§³Ö C# µ½ SQL µÄË÷Òý×ª»» (0-based -> 1-based)
            SqlBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (functionName, args) => args.Count > 2 ?
                        $"INSTR({args[0].Key}, {args[1].Key}, {args[2].Key}+1)-1" : $"INSTR({args[0].Key}, {args[1].Key})-1");
            SqlBuilder.Instance.RegisterFunctionSqlHandler("Substring", (name, args) => args.Count > 2 ?
                        $"SUBSTR({args[0].Key}, {args[1].Key}+1, {args[2].Key})" : $"SUBSTR({args[0].Key}, {args[1].Key}+1)");

            // SQLite º¯Êý×¢²á
            SQLiteBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
                (functionName, args) => $"DATE({args[0].Key}, CAST({args[1].Key} AS TEXT)||' {functionName.Substring(3).ToLower()}')");

            // MySQL º¯Êý×¢²á
            MySqlBuilder.Instance.RegisterFunctionSqlHandler("LENGTH", (functionName, args) => $"CHAR_LENGTH({args[0].Key})");
            MySqlBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
                (functionName, args) => $"DATE_ADD({args[0].Key}, INTERVAL {args[1].Key} {functionName.Substring(3).ToUpper().TrimEnd('S')})");

            // Oracle º¯Êý×¢²á
            OracleBuilder.Instance.RegisterFunctionSqlHandler("IfNull", (name, args) => $"NVL({args[0].Key}, {args[1].Key})");
            OracleBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays"], (functionName, args) => $"({args[0].Key} + NUMTODSINTERVAL({args[1].Key}, '{functionName.Substring(3).ToUpper().TrimEnd('S')}'))");
            OracleBuilder.Instance.RegisterFunctionSqlHandler(["AddMonths", "AddYears"], (functionName, args) => $"({args[0].Key} + NUMTOYMINTERVAL({args[1].Key}, '{functionName.Substring(3).ToUpper().TrimEnd('S')}'))");

            // PostgreSQL º¯Êý×¢²á
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (functionName, args) => $"POSITION({args[1].Key} IN {args[0].Key})-1");
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler("Substring", (name, args) => args.Count > 2 ?
                        $"SUBSTRING({args[0].Key} FROM {args[1].Key}+1 FOR {args[2].Key})" : $"SUBSTRING({args[0].Key} FROM {args[1].Key}+1)");
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
                (functionName, args) => $"({args[0].Key} + ({args[1].Key} || ' {functionName.Substring(3).ToLower()}')::interval)");

            // SQL Server º¯Êý×¢²á
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
