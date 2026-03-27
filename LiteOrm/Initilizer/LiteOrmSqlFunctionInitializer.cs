using Autofac;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm SQL 函数初始化器，用于注册各个数据库的 SQL 函数处理器。
    /// 在应用启动时注册函数映射，支持 SqlBuilder 的动态 SQL 生成。
    /// </summary>
    [AutoRegister(Lifetime = Lifetime.Singleton)]
    public class LiteOrmSqlFunctionInitializer : IStartable
    {
        /// <summary>
        /// 启动时初始化 SQL 函数映射。
        /// </summary>
        public void Start()
        {
            RegisterSqlFunctions();
        }

        /// <summary>
        /// 注册各数据库的 SQL 函数处理器映射。
        /// 动态注册 Function 和 Handler 一般需要结合 SqlBuilder.RegisterFunctionSqlHandler 使用。
        /// </summary>
        private void RegisterSqlFunctions()
        {
            RegisterBaseSqlFunctions();
            RegisterSQLiteFunctions();
            RegisterMySqlFunctions();
            RegisterOracleFunctions();
            RegisterPostgreSqlFunctions();
            RegisterSqlServerFunctions();
        }

        private void RegisterBaseSqlFunctions()
        {
            // 注册 SQL 危险关键字为不可用，预防潜在风险，直接抛出异常提示用户。如确定需要使用，请自行重新注册自定义函数映射。
            SqlBuilder.Instance.RegisterFunctionSqlHandler(Constants.ExcludedSqlNames, (ref outSql, expr, context, sqlBuilder, outputParams) => throw new NotSupportedException($"Function '{expr.FunctionName}' is not supported. You must register it manually if it is absolutely necessary."));
            // 注册通用的 SQL 映射
            SqlBuilder.Instance.RegisterFunctionSqlHandler("Now", (ref outSql, expr, context, sqlBuilder, outputParams) => outSql.Append("CURRENT_TIMESTAMP"));
            SqlBuilder.Instance.RegisterFunctionSqlHandler("Today", (ref outSql, expr, context, sqlBuilder, outputParams) => outSql.Append("CURRENT_DATE"));
            SqlBuilder.Instance.RegisterFunctionSqlHandler("CASE", (ref outSql, expr, context, sqlBuilder, outputParams) =>
            {
                outSql.Append("CASE");
                for (int i = 0; i < expr.Args.Count - 1; i += 2)
                {
                    outSql.Append(" WHEN ");
                    expr.Args[i].ToSql(ref outSql, context, sqlBuilder, outputParams);
                    outSql.Append(" THEN ");
                    expr.Args[i + 1].ToSql(ref outSql, context, sqlBuilder, outputParams);
                }
                if (expr.Args.Count % 2 == 1)
                {
                    outSql.Append(" ELSE ");
                    expr.Args.Last().ToSql(ref outSql, context, sqlBuilder, outputParams);
                }
                outSql.Append(" END");
            });
            SqlBuilder.Instance.RegisterFunctionSqlHandler("Over", (ref outSql, expr, context, sqlBuilder, outputParams) =>
            {
                // 处理 OVER 函数，支持窗口函数的 SQL 生成
                // 窗口函数格式：FunctionName(args) OVER (partition by ... order by ...)
                expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
                if (expr.Args.Count > 1)
                {
                    outSql.Append(" OVER (");
                    int begin = outSql.Length;
                    outSql.Append("PARTITION BY ");
                    int cur = outSql.Length;
                    expr.Args[1].ToSql(ref outSql, context, sqlBuilder, outputParams);
                    if (outSql.Length == cur)
                    {
                        // 如果没有生成任何内容，说明没有 PARTITION BY
                        outSql.Length = begin;
                    }
                    if (expr.Args.Count > 2)
                    {
                        if (outSql.Length > begin)
                        {
                            begin = outSql.Length;
                            outSql.Append(" ");
                        }
                        outSql.Append("ORDER BY ");
                        cur = outSql.Length;
                        expr.Args[2].ToSql(ref outSql, context, sqlBuilder, outputParams);
                        if (outSql.Length == cur)
                        {
                            // 如果没有生成任何内容，说明没有 ORDER BY
                            outSql.Length = begin;
                        }
                        if (expr.Args.Count > 3)
                        {
                            if (outSql.Length == begin) throw new InvalidOperationException("Cannot have frame_clause arguments for OVER function when there is no ORDER BY clause.");
                            outSql.Append(" ");
                            outSql.Append(expr.Args[3].ToSql(context, sqlBuilder, outputParams));
                        }
                    }
                    outSql.Append(')');
                }
            });
            SqlBuilder.Instance.RegisterFunctionSqlHandler(["RowsBetween", "RangeBetween"], (ref outSql, expr, context, sqlBuilder, outputParams) =>
            {
                if (expr.Args.Count == 0) throw new ArgumentException("At least one argument is required for RowsBetween/RangeBetween function.");
                if ("RowsBetween".Equals(expr.FunctionName, StringComparison.OrdinalIgnoreCase))
                    outSql.Append("ROWS ");
                else
                    outSql.Append("RANGE ");
                if (expr.Args.Count == 1)
                {
                    int? pos = expr.Args[0] switch
                    {
                        ValueExpr vte when vte.Value is int intValue => intValue,
                        _ => null
                    };
                    if (pos == null)
                        outSql.Append(expr.Args[0].ToSql(context, sqlBuilder, outputParams));
                    else if (pos < 0)
                        outSql.Append($"{-pos} PRECEDING");
                    else if (pos > 0)
                        outSql.Append($"{pos} FOLLOWING");
                    else
                        outSql.Append("CURRENT ROW");
                }
                else
                {
                    outSql.Append("BETWEEN ");
                    int? begin = expr.Args[0] switch
                    {
                        ValueExpr vte when vte.Value is int intValue => intValue,
                        _ => null
                    };
                    int? end = expr.Args[1] switch
                    {
                        ValueExpr vte when vte.Value is int intValue => intValue,
                        _ => null
                    };
                    if (begin == null)
                        outSql.Append("UNBOUNDED PRECEDING");
                    else if (begin < 0)
                        outSql.Append($"{-begin} PRECEDING");
                    else if (begin > 0)
                        outSql.Append($"{begin} FOLLOWING");
                    else
                        outSql.Append("CURRENT ROW");
                    outSql.Append(" AND ");
                    if (end == null)
                        outSql.Append("UNBOUNDED FOLLOWING");
                    else if (end < 0)
                        outSql.Append($"{-end} PRECEDING");
                    else if (end > 0)
                        outSql.Append($"{end} FOLLOWING");
                    else
                        outSql.Append("CURRENT ROW");
                }
            });
            // 额外处理 IndexOf 和 Substring，支持 C# 到 SQL 的索引转换 (0-based -> 1-based)
            SqlBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (ref outSql, expr, context, sqlBuilder, outputParams) =>
            {
                if (expr.Args.Count > 2)
                    outSql.Append($"INSTR({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[2].ToSql(context, sqlBuilder, outputParams)}+1)-1");
                else
                    outSql.Append($"INSTR({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)})-1");
            });
            SqlBuilder.Instance.RegisterFunctionSqlHandler("Substring", (ref outSql, expr, context, sqlBuilder, outputParams) =>
            {
                if (expr.Args.Count > 2)
                    outSql.Append($"SUBSTR({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}+1, {expr.Args[2].ToSql(context, sqlBuilder, outputParams)})");
                else
                    outSql.Append($"SUBSTR({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}+1)");
            });
        }
        private void RegisterSQLiteFunctions()
        {
            SQLiteBuilder.Instance.RegisterFunctionSqlHandler("Now", (ref outSql, expr, context, sqlBuilder, outputParams) => outSql.Append("datetime('now', 'localtime')"));
            SQLiteBuilder.Instance.RegisterFunctionSqlHandler("Today", (ref outSql, expr, context, sqlBuilder, outputParams) => outSql.Append("date('now', 'localtime')"));

            SQLiteBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
                (ref outSql, expr, context, sqlBuilder, outputParams) =>
                    outSql.Append($"DATE({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, CAST({expr.Args[1].ToSql(context, sqlBuilder, outputParams)} AS TEXT)||' {expr.FunctionName.Substring(3).ToLower()}')"));
            SQLiteBuilder.Instance.RegisterFunctionSqlHandler("DateDiffSeconds", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"((julianday({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}) - julianday({expr.Args[1].ToSql(context, sqlBuilder, outputParams)})) * 86400.0)"));
            SQLiteBuilder.Instance.RegisterFunctionSqlHandler("DateDiffDays", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"(julianday({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}) - julianday({expr.Args[1].ToSql(context, sqlBuilder, outputParams)}))"));
            SQLiteBuilder.Instance.RegisterFunctionSqlHandler("DateDiffHours", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"((julianday({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}) - julianday({expr.Args[1].ToSql(context, sqlBuilder, outputParams)})) * 24.0)"));
            SQLiteBuilder.Instance.RegisterFunctionSqlHandler("DateDiffMinutes", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"((julianday({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}) - julianday({expr.Args[1].ToSql(context, sqlBuilder, outputParams)})) * 1440.0)"));
            SQLiteBuilder.Instance.RegisterFunctionSqlHandler("DateDiffMilliseconds", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"((julianday({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}) - julianday({expr.Args[1].ToSql(context, sqlBuilder, outputParams)})) * 86400000.0)"));
            SQLiteBuilder.Instance.RegisterFunctionSqlHandler(["TotalSeconds", "TotalDays", "TotalHours", "TotalMinutes", "TotalMilliseconds"],
                (ref outSql, expr, context, sqlBuilder, outputParams) =>
                {
                    var e = expr.Args[0].ToSql(context, sqlBuilder, outputParams);
                    outSql.Append(expr.FunctionName switch
                    {
                        "TotalDays"         => $"(julianday('2000-01-01 ' || {e}) - julianday('2000-01-01'))",
                        "TotalHours"        => $"((julianday('2000-01-01 ' || {e}) - julianday('2000-01-01')) * 24.0)",
                        "TotalMinutes"      => $"((julianday('2000-01-01 ' || {e}) - julianday('2000-01-01')) * 1440.0)",
                        "TotalSeconds"      => $"((julianday('2000-01-01 ' || {e}) - julianday('2000-01-01')) * 86400.0)",
                        "TotalMilliseconds" => $"((julianday('2000-01-01 ' || {e}) - julianday('2000-01-01')) * 86400000.0)",
                        _ => e
                    });
                });
            SQLiteBuilder.Instance.RegisterFunctionSqlHandler("Format", (ref outSql, expr, context, sqlBuilder, outputParams) =>
            {
                outSql.Append("strftime(");
                if (expr.Args[1] is ValueExpr ve && ve.Value is string s)
                {
                    outSql.Append('\'');
                    ConvertFormat(s, ref outSql, static (c, count) => c switch
                    {
                        'y' => count >= 4 ? "%Y" : "%y",
                        'M' => count >= 4 ? "%B" : count == 3 ? "%b" : "%m",
                        'd' => count >= 4 ? "%A" : count == 3 ? "%a" : "%d",
                        'H' => "%H",
                        'h' => "%I",
                        'm' => "%M",
                        's' => "%S",
                        'f' => "%f",
                        't' => "%p",
                        _ => null
                    });
                    outSql.Append('\'');
                }
                else
                    expr.Args[1].ToSql(ref outSql, context, sqlBuilder, outputParams);
                outSql.Append(", ");
                expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
                outSql.Append(')');
            });
            SQLiteBuilder.Instance.RegisterFunctionSqlHandler("Concat", (ref outSql, expr, context, sqlBuilder, outputParams) =>
            {
                for (int i = 0; i < expr.Args.Count; i++)
                {
                    if (i > 0) outSql.Append("||");
                    expr.Args[i].ToSql(ref outSql, context, sqlBuilder, outputParams);
                }
            });
        }

        private void RegisterMySqlFunctions()
        {
            MySqlBuilder.Instance.RegisterFunctionSqlHandler("LENGTH", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"CHAR_LENGTH({expr.Args[0].ToSql(context, sqlBuilder, outputParams)})"));
            MySqlBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
                (ref outSql, expr, context, sqlBuilder, outputParams) =>
                    outSql.Append($"DATE_ADD({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, INTERVAL {expr.Args[1].ToSql(context, sqlBuilder, outputParams)} {expr.FunctionName.Substring(3).ToUpper().TrimEnd('S')})"));
            MySqlBuilder.Instance.RegisterFunctionSqlHandler("DateDiffSeconds", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"TIMESTAMPDIFF(SECOND, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[0].ToSql(context, sqlBuilder, outputParams)})"));
            MySqlBuilder.Instance.RegisterFunctionSqlHandler(["DateDiffDays", "DateDiffHours", "DateDiffMinutes"], (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"TIMESTAMPDIFF({expr.FunctionName.Substring(8).ToUpper().TrimEnd('S')}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[0].ToSql(context, sqlBuilder, outputParams)})"));
            MySqlBuilder.Instance.RegisterFunctionSqlHandler("DateDiffMilliseconds", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"(TIMESTAMPDIFF(MICROSECOND, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[0].ToSql(context, sqlBuilder, outputParams)}) / 1000.0)"));
            MySqlBuilder.Instance.RegisterFunctionSqlHandler(["TotalSeconds", "TotalDays", "TotalHours", "TotalMinutes", "TotalMilliseconds"],
                (ref outSql, expr, context, sqlBuilder, outputParams) =>
                {
                    var e = expr.Args[0].ToSql(context, sqlBuilder, outputParams);
                    outSql.Append(expr.FunctionName switch
                    {
                        "TotalDays"         => $"(TIME_TO_SEC({e}) / 86400.0)",
                        "TotalHours"        => $"(TIME_TO_SEC({e}) / 3600.0)",
                        "TotalMinutes"      => $"(TIME_TO_SEC({e}) / 60.0)",
                        "TotalSeconds"      => $"(TIME_TO_SEC({e}) * 1.0)",
                        "TotalMilliseconds" => $"(TIME_TO_SEC({e}) * 1000.0)",
                        _ => e
                    });
                });
            MySqlBuilder.Instance.RegisterFunctionSqlHandler("Format", (ref outSql, expr, context, sqlBuilder, outputParams) =>
            {
                outSql.Append("DATE_FORMAT(");
                expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
                outSql.Append(", ");
                if (expr.Args[1] is ValueExpr ve && ve.Value is string s)
                {
                    outSql.Append('\'');
                    ConvertFormat(s, ref outSql, static (c, count) => c switch
                    {
                        'y' => count >= 4 ? "%Y" : "%y",
                        'M' => count >= 4 ? "%M" : count == 3 ? "%b" : count == 2 ? "%m" : "%c",
                        'd' => count >= 4 ? "%W" : count == 3 ? "%a" : count == 2 ? "%d" : "%e",
                        'H' => count >= 2 ? "%H" : "%k",
                        'h' => count >= 2 ? "%h" : "%l",
                        'm' => "%i",
                        's' => "%S",
                        'f' => "%f",
                        't' => "%p",
                        _ => null
                    });
                    outSql.Append('\'');
                }
                else
                    expr.Args[1].ToSql(ref outSql, context, sqlBuilder, outputParams);
                outSql.Append(')');
            });
        }

        private void RegisterOracleFunctions()
        {
            OracleBuilder.Instance.RegisterFunctionSqlHandler("IfNull", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"NVL({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)})"));
            OracleBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays"],
                (ref outSql, expr, context, sqlBuilder, outputParams) =>
                    outSql.Append($"({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} + NUMTODSINTERVAL({expr.Args[1].ToSql(context, sqlBuilder, outputParams)}, '{expr.FunctionName.Substring(3).ToUpper().TrimEnd('S')}'))"));
            OracleBuilder.Instance.RegisterFunctionSqlHandler(["AddMonths", "AddYears"],
                (ref outSql, expr, context, sqlBuilder, outputParams) =>
                    outSql.Append($"({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} + NUMTOYMINTERVAL({expr.Args[1].ToSql(context, sqlBuilder, outputParams)}, '{expr.FunctionName.Substring(3).ToUpper().TrimEnd('S')}'))"));
            OracleBuilder.Instance.RegisterFunctionSqlHandler("DateDiffSeconds", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"(({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} - {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}) * 86400)"));
            OracleBuilder.Instance.RegisterFunctionSqlHandler("DateDiffDays", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} - {expr.Args[1].ToSql(context, sqlBuilder, outputParams)})"));
            OracleBuilder.Instance.RegisterFunctionSqlHandler("DateDiffHours", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"(({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} - {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}) * 24)"));
            OracleBuilder.Instance.RegisterFunctionSqlHandler("DateDiffMinutes", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"(({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} - {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}) * 1440)"));
            OracleBuilder.Instance.RegisterFunctionSqlHandler("DateDiffMilliseconds", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"(({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} - {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}) * 86400000)"));
            OracleBuilder.Instance.RegisterFunctionSqlHandler(["TotalSeconds", "TotalDays", "TotalHours", "TotalMinutes", "TotalMilliseconds"],
                (ref outSql, expr, context, sqlBuilder, outputParams) =>
                {
                    var e = expr.Args[0].ToSql(context, sqlBuilder, outputParams);
                    var totalSec = $"(EXTRACT(DAY FROM {e}) * 86400 + EXTRACT(HOUR FROM {e}) * 3600 + EXTRACT(MINUTE FROM {e}) * 60 + EXTRACT(SECOND FROM {e}))";
                    outSql.Append(expr.FunctionName switch
                    {
                        "TotalDays"         => $"({totalSec} / 86400.0)",
                        "TotalHours"        => $"({totalSec} / 3600.0)",
                        "TotalMinutes"      => $"({totalSec} / 60.0)",
                        "TotalSeconds"      => totalSec,
                        "TotalMilliseconds" => $"({totalSec} * 1000.0)",
                        _ => e
                    });
                });
            OracleBuilder.Instance.RegisterFunctionSqlHandler("Format", (ref outSql, expr, context, sqlBuilder, outputParams) =>
            {
                outSql.Append("TO_CHAR(");
                expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
                outSql.Append(", ");
                if (expr.Args[1] is ValueExpr ve && ve.Value is string s)
                {
                    outSql.Append('\'');
                    ConvertFormat(s, ref outSql, static (c, count) => c switch
                    {
                        'y' => count >= 4 ? "YYYY" : "YY",
                        'M' => count >= 4 ? "MONTH" : count == 3 ? "MON" : "MM",
                        'd' => count >= 4 ? "DAY" : count == 3 ? "DY" : "DD",
                        'H' => "HH24",
                        'h' => "HH12",
                        'm' => "MI",
                        's' => "SS",
                        'f' => "FF3",
                        't' => "AM",
                        _ => null
                    }, '"');
                    outSql.Append('\'');
                }
                else
                    expr.Args[1].ToSql(ref outSql, context, sqlBuilder, outputParams);
                outSql.Append(')');
            });
        }

        private void RegisterPostgreSqlFunctions()
        {
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"POSITION({expr.Args[1].ToSql(context, sqlBuilder, outputParams)} IN {expr.Args[0].ToSql(context, sqlBuilder, outputParams)})-1"));
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler("Substring", (ref outSql, expr, context, sqlBuilder, outputParams) =>
            {
                if (expr.Args.Count > 2)
                    outSql.Append($"SUBSTRING({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} FROM {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}+1 FOR {expr.Args[2].ToSql(context, sqlBuilder, outputParams)})");
                else
                    outSql.Append($"SUBSTRING({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} FROM {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}+1)");
            });
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
                (ref outSql, expr, context, sqlBuilder, outputParams) =>
                    outSql.Append($"({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} + ({expr.Args[1].ToSql(context, sqlBuilder, outputParams)} || ' {expr.FunctionName.Substring(3).ToLower()}')::interval)"));
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler("DateDiffSeconds", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"EXTRACT(EPOCH FROM ({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} - {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}))"));
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler("DateDiffDays", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"(EXTRACT(EPOCH FROM ({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} - {expr.Args[1].ToSql(context, sqlBuilder, outputParams)})) / 86400.0)"));
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler("DateDiffHours", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"(EXTRACT(EPOCH FROM ({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} - {expr.Args[1].ToSql(context, sqlBuilder, outputParams)})) / 3600.0)"));
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler("DateDiffMinutes", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"(EXTRACT(EPOCH FROM ({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} - {expr.Args[1].ToSql(context, sqlBuilder, outputParams)})) / 60.0)"));
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler("DateDiffMilliseconds", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"(EXTRACT(EPOCH FROM ({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} - {expr.Args[1].ToSql(context, sqlBuilder, outputParams)})) * 1000.0)"));
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler(["TotalSeconds", "TotalDays", "TotalHours", "TotalMinutes", "TotalMilliseconds"],
                (ref outSql, expr, context, sqlBuilder, outputParams) =>
                {
                    var e = expr.Args[0].ToSql(context, sqlBuilder, outputParams);
                    outSql.Append(expr.FunctionName switch
                    {
                        "TotalDays"         => $"(EXTRACT(EPOCH FROM {e}) / 86400.0)",
                        "TotalHours"        => $"(EXTRACT(EPOCH FROM {e}) / 3600.0)",
                        "TotalMinutes"      => $"(EXTRACT(EPOCH FROM {e}) / 60.0)",
                        "TotalSeconds"      => $"EXTRACT(EPOCH FROM {e})",
                        "TotalMilliseconds" => $"(EXTRACT(EPOCH FROM {e}) * 1000.0)",
                        _ => e
                    });
                });
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler("Format", (ref outSql, expr, context, sqlBuilder, outputParams) =>
            {
                outSql.Append("TO_CHAR(");
                expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
                outSql.Append(", ");
                if (expr.Args[1] is ValueExpr ve && ve.Value is string s)
                {
                    outSql.Append('\'');
                    ConvertFormat(s, ref outSql, static (c, count) => c switch
                    {
                        'y' => count >= 4 ? "YYYY" : "YY",
                        'M' => count >= 4 ? "Month" : count == 3 ? "Mon" : "MM",
                        'd' => count >= 4 ? "Day" : count == 3 ? "Dy" : "DD",
                        'H' => "HH24",
                        'h' => "HH12",
                        'm' => "MI",
                        's' => "SS",
                        'f' => "MS",
                        't' => "AM",
                        _ => null
                    }, '"');
                    outSql.Append('\'');
                }
                else
                    expr.Args[1].ToSql(ref outSql, context, sqlBuilder, outputParams);
                outSql.Append(')');
            });
        }

        private void RegisterSqlServerFunctions()
        {
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("IfNull", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"ISNULL({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)})"));
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("Length", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"LEN({expr.Args[0].ToSql(context, sqlBuilder, outputParams)})"));
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (ref outSql, expr, context, sqlBuilder, outputParams) =>
            {
                if (expr.Args.Count > 2)
                    outSql.Append($"CHARINDEX({expr.Args[1].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[2].ToSql(context, sqlBuilder, outputParams)}+1)-1");
                else
                    outSql.Append($"CHARINDEX({expr.Args[1].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[0].ToSql(context, sqlBuilder, outputParams)})-1");
            });
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("Substring", (ref outSql, expr, context, sqlBuilder, outputParams) =>
            {
                if (expr.Args.Count > 2)
                    outSql.Append($"SUBSTRING({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}+1, {expr.Args[2].ToSql(context, sqlBuilder, outputParams)})");
                else
                    outSql.Append($"SUBSTRING({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}+1, LEN({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}))");
            });
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
                (ref outSql, expr, context, sqlBuilder, outputParams) =>
                    outSql.Append($"DATEADD({expr.FunctionName.Substring(3).ToLower().TrimEnd('s')}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[0].ToSql(context, sqlBuilder, outputParams)})"));
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("DateDiffSeconds", (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"DATEDIFF(SECOND, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[0].ToSql(context, sqlBuilder, outputParams)})"));
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler(["DateDiffDays", "DateDiffHours", "DateDiffMinutes", "DateDiffMilliseconds"], (ref outSql, expr, context, sqlBuilder, outputParams) =>
                outSql.Append($"DATEDIFF({expr.FunctionName.Substring(8).ToUpper().TrimEnd('S')}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[0].ToSql(context, sqlBuilder, outputParams)})"));
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler(["TotalSeconds", "TotalDays", "TotalHours", "TotalMinutes", "TotalMilliseconds"],
                (ref outSql, expr, context, sqlBuilder, outputParams) =>
                {
                    var e = expr.Args[0].ToSql(context, sqlBuilder, outputParams);
                    outSql.Append(expr.FunctionName switch
                    {
                        "TotalDays"         => $"(DATEDIFF(MILLISECOND, '00:00:00', {e}) / 86400000.0)",
                        "TotalHours"        => $"(DATEDIFF(MILLISECOND, '00:00:00', {e}) / 3600000.0)",
                        "TotalMinutes"      => $"(DATEDIFF(MILLISECOND, '00:00:00', {e}) / 60000.0)",
                        "TotalSeconds"      => $"(DATEDIFF(MILLISECOND, '00:00:00', {e}) / 1000.0)",
                        "TotalMilliseconds" => $"(DATEDIFF(MILLISECOND, '00:00:00', {e}) * 1.0)",
                        _ => e
                    });
                });
            // SQL Server FORMAT() 原生支持 .NET 格式字符串，无需转换
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("Format", (ref outSql, expr, context, sqlBuilder, outputParams) =>
            {
                outSql.Append("FORMAT(");
                expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
                outSql.Append(", ");
                if (expr.Args[1] is ValueExpr ve && ve.Value is string s)
                    outSql.Append($"'{s}'");
                else
                    expr.Args[1].ToSql(ref outSql, context, sqlBuilder, outputParams);
                outSql.Append(')');
            });
        }

        /// <summary>
        /// 将 C# 日期格式字符串转换为目标数据库格式，直接写入目标构建器。
        /// </summary>
        /// <param name="csFormat">C# 日期格式字符串。</param>
        /// <param name="outSql">目标构建器，用于输出转换后的格式字符串。</param>
        /// <param name="tokenMap">格式化字符映射委托，接收字符和连续出现次数，返回目标格式字符串；返回 null 时保留原字符。</param>
        /// <param name="literalWrapper">单引号括起的字面量在输出中使用的包裹字符，默认 '\0' 表示不包裹。</param>
        private static void ConvertFormat(string csFormat, ref ValueStringBuilder outSql,
            Func<char, int, string> tokenMap, char literalWrapper = '\0')
        {
            int i = 0;
            while (i < csFormat.Length)
            {
                if (csFormat[i] == '\'')
                {
                    if (literalWrapper != '\0') outSql.Append(literalWrapper);
                    i++;
                    while (i < csFormat.Length && csFormat[i] != '\'') outSql.Append(csFormat[i++]);
                    if (literalWrapper != '\0') outSql.Append(literalWrapper);
                    if (i < csFormat.Length) i++;
                    continue;
                }
                char c = csFormat[i];
                int count = 0;
                while (i < csFormat.Length && csFormat[i] == c) { count++; i++; }
                outSql.Append(tokenMap(c, count) ?? new string(c, count));
            }
        }
    }
}
