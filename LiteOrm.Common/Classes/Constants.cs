using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LiteOrm.Common
{
    /// <summary>
    /// 常量定义类。
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// 需排除的 SQL 关键字列表，在生成 SQL 语句时使用，避免潜在风险。
        /// </summary>
        public static HashSet<string> ExcludedSqlNames = new(["DROP", "TRUNCATE", "DELETE", "INSERT", "UPDATE", "UNION", "SET"], StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// 默认的表别名，在生成 SQL 语句时使用，避免与用户定义的别名冲突。
        /// </summary>
        public const string DefaultTableAlias = "T0";
        /// <summary>
        /// SQL语句中like条件中的转义符
        /// </summary>
        public const char LikeEscapeChar = '/';

        /// <summary>
        /// 日期时间格式
        /// </summary>
        public const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss.fff";

        /// <summary>
        /// 日期格式
        /// </summary>
        public const string DateFormat = "yyyy-MM-dd";

        /// <summary>
        /// 有效的名称模式（只允许字母、数字和下划线）
        /// </summary>
        public const string ValidNamePattern = "^[a-zA-Z0-9_]*$";

        /// <summary>
        /// 有效的名称正则表达式
        /// </summary>
        public static readonly Regex ValidNameRegex = new Regex(ValidNamePattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }
}
