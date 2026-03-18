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
    [AutoRegister(Lifetime = ServiceLifetime.Singleton)]
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
            // 注册 SQL 危险关键字为不可用，预防潜在风险，直接抛出异常提示用户。如确定需要使用，请自行重新注册自定义函数映射。
            SqlBuilder.Instance.RegisterFunctionSqlHandler(Constants.ExcludedSqlNames, (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) => throw new NotSupportedException($"Function '{expr.FunctionName}' is not supported. You must register it manually if it is absolutely necessary."));
            // 注册通用的 SQL 映射
            SqlBuilder.Instance.RegisterFunctionSqlHandler("Now", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) => outSql.Append("CURRENT_TIMESTAMP"));
            SqlBuilder.Instance.RegisterFunctionSqlHandler("Today", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) => outSql.Append("CURRENT_DATE"));
            SqlBuilder.Instance.RegisterFunctionSqlHandler("CASE", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
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
            SqlBuilder.Instance.RegisterFunctionSqlHandler("Over", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
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
                            outSql.Append(" ");
                        begin = outSql.Length;
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
                            if (outSql.Length > begin)
                                outSql.Append(" ");
                            outSql.Append(expr.Args[3].ToSql(context, sqlBuilder, outputParams));
                        }
                    }
                    outSql.Append(')');
                }
            });

            SqlBuilder.Instance.RegisterFunctionSqlHandler(["RowsBetween", "RangeBetween"], (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
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
                    else if (pos<0)
                        outSql.Append($"{-pos} PRECEDING");
                    else if(pos>0)
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
                    if(end == null)
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
            SqlBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
            {
                if (expr.Args.Count > 2)
                    outSql.Append($"INSTR({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[2].ToSql(context, sqlBuilder, outputParams)}+1)-1");
                else
                    outSql.Append($"INSTR({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)})-1");
            });
            SqlBuilder.Instance.RegisterFunctionSqlHandler("Substring", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
            {
                if (expr.Args.Count > 2)
                    outSql.Append($"SUBSTR({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}+1, {expr.Args[2].ToSql(context, sqlBuilder, outputParams)})");
                else
                    outSql.Append($"SUBSTR({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}+1)");
            });

            // SQLite 函数注册
            SQLiteBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
                (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
                    outSql.Append($"DATE({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, CAST({expr.Args[1].ToSql(context, sqlBuilder, outputParams)} AS TEXT)||' {expr.FunctionName.Substring(3).ToLower()}')"));
            SQLiteBuilder.Instance.RegisterFunctionSqlHandler("Concat", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
            {
                for (int i = 0; i < expr.Args.Count; i++)
                {
                    if (i > 0) outSql.Append("||");
                    expr.Args[i].ToSql(ref outSql, context, sqlBuilder, outputParams);
                }
            });

            // MySQL 函数注册
            MySqlBuilder.Instance.RegisterFunctionSqlHandler("LENGTH", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
                outSql.Append($"CHAR_LENGTH({expr.Args[0].ToSql(context, sqlBuilder, outputParams)})"));
            MySqlBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
                (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
                    outSql.Append($"DATE_ADD({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, INTERVAL {expr.Args[1].ToSql(context, sqlBuilder, outputParams)} {expr.FunctionName.Substring(3).ToUpper().TrimEnd('S')})"));

            // Oracle 函数注册
            OracleBuilder.Instance.RegisterFunctionSqlHandler("IfNull", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
                outSql.Append($"NVL({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)})"));
            OracleBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays"],
                (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
                    outSql.Append($"({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} + NUMTODSINTERVAL({expr.Args[1].ToSql(context, sqlBuilder, outputParams)}, '{expr.FunctionName.Substring(3).ToUpper().TrimEnd('S')}'))"));
            OracleBuilder.Instance.RegisterFunctionSqlHandler(["AddMonths", "AddYears"],
                (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
                    outSql.Append($"({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} + NUMTOYMINTERVAL({expr.Args[1].ToSql(context, sqlBuilder, outputParams)}, '{expr.FunctionName.Substring(3).ToUpper().TrimEnd('S')}'))"));

            // PostgreSQL 函数注册
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
                outSql.Append($"POSITION({expr.Args[1].ToSql(context, sqlBuilder, outputParams)} IN {expr.Args[0].ToSql(context, sqlBuilder, outputParams)})-1"));
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler("Substring", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
            {
                if (expr.Args.Count > 2)
                    outSql.Append($"SUBSTRING({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} FROM {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}+1 FOR {expr.Args[2].ToSql(context, sqlBuilder, outputParams)})");
                else
                    outSql.Append($"SUBSTRING({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} FROM {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}+1)");
            });
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
                (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
                    outSql.Append($"({expr.Args[0].ToSql(context, sqlBuilder, outputParams)} + ({expr.Args[1].ToSql(context, sqlBuilder, outputParams)} || ' {expr.FunctionName.Substring(3).ToLower()}')::interval)"));

            // SQL Server 函数注册
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("IfNull", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
                outSql.Append($"ISNULL({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)})"));
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("Length", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
                outSql.Append($"LEN({expr.Args[0].ToSql(context, sqlBuilder, outputParams)})"));
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
            {
                if (expr.Args.Count > 2)
                    outSql.Append($"CHARINDEX({expr.Args[1].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[2].ToSql(context, sqlBuilder, outputParams)}+1)-1");
                else
                    outSql.Append($"CHARINDEX({expr.Args[1].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[0].ToSql(context, sqlBuilder, outputParams)})-1");
            });
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("Substring", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
            {
                if (expr.Args.Count > 2)
                    outSql.Append($"SUBSTRING({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}+1, {expr.Args[2].ToSql(context, sqlBuilder, outputParams)})");
                else
                    outSql.Append($"SUBSTRING({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}+1, LEN({expr.Args[0].ToSql(context, sqlBuilder, outputParams)}))");
            });
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
                (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
                    outSql.Append($"DATEADD({expr.FunctionName.Substring(3).ToLower().TrimEnd('s')}, {expr.Args[1].ToSql(context, sqlBuilder, outputParams)}, {expr.Args[0].ToSql(context, sqlBuilder, outputParams)})"));
        }
    }
}
