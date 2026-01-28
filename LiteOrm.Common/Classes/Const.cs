using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LiteOrm.Common
{
    /// <summary>
    /// 常量定义类。
    /// </summary>
    public static class Const
    {
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
    }
}
