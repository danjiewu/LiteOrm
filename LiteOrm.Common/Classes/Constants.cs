using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        /// 标识参数的内部名称，通常用于存储主键值或其他唯一标识符，以便在生成 SQL 语句时使用。
        /// </summary>
        public const string IdentityParamName = "IDENTITY_";
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

        /// <summary>
        /// AOT模式下注册的成员类型，包含公开构造函数、公开无参构造函数、公开属性和非公开属性。
        /// <para>
        /// <see cref="DynamicallyAccessedMemberTypes"/> 用于向 AOT/裁剪器声明：本类型出于反射（实体映射、DAO 表信息解析等）
        /// 需要在运行时访问哪些成员，从而保证这些成员在发布裁剪/原生 AOT 时被保留。
        /// 泛型参数标注此标记后，调用方传入的实际类型必须满足同样的成员访问要求，否则会触发 IL2091 警告。
        /// </para>
        /// </summary>
        public const DynamicallyAccessedMemberTypes RegistedMemberTypes = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties;
    }
}
