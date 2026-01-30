using System;
using System.Collections.Generic;
using System.Data;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示用于生成数据库相关 SQL 片段的构建器接口。
    /// 不同数据库的实现负责将通用表达转换为目标数据库的原生 SQL 语法和参数格式。
    /// </summary>
    public interface ISqlBuilder
    {
        /// <summary>
        /// 替换 SQL 中的命名占位符或标识符为目标数据库的命名格式。
        /// </summary>
        /// <param name="sql">原始 SQL 字符串。</param>
        /// <returns>返回替换后的 SQL 字符串。</returns>
        string ReplaceSqlName(string sql);

        /// <summary>
        /// 将参数名或占位符转换为本地（native）命名格式。
        /// 例如将 "@p0" 转换为具体驱动使用的参数名。
        /// </summary>
        /// <param name="paramName">通用参数名。</param>
        /// <returns>返回本地参数名。</returns>
        string ToNativeName(string paramName);

        /// <summary>
        /// 将本地参数名转换为通用参数名格式。
        /// </summary>
        /// <param name="nativeName">数据库驱动的本地参数名。</param>
        /// <returns>返回通用参数名。</returns>
        string ToParamName(string nativeName);

        /// <summary>
        /// 将代码中的名称（如列名、表名）转换为目标数据库的 SQL 名称（包含必要的转义）。
        /// </summary>
        /// <param name="name">源名称。</param>
        /// <returns>返回适用于 SQL 的名称。</returns>
        string ToSqlName(string name);

        /// <summary>
        /// 根据 SQL 对象类型生成对应的 SQL 表达式片段。
        /// </summary>
        /// <param name="sqlObject">SQL 对象（如表、列、引用等）。</param>
        /// <param name="tableNameArgs">用于格式化表名的参数。</param>
        /// <returns>生成的 SQL 字符串。</returns>
        string BuildExpression(SqlObject sqlObject, params string[] tableNameArgs);

        /// <summary>
        /// 将本地参数名或变量名格式化为 SQL 中使用的参数占位符。
        /// 例如将参数名转换为 "@param" 或 ":param" 等形式。
        /// </summary>
        /// <param name="nativeName">本地参数名。</param>
        /// <returns>返回 SQL 参数占位符字符串。</returns>
        string ToSqlParam(string nativeName);

        /// <summary>
        /// 将一个值格式化为用于 LIKE 查询的 SQL 表达式（包含必要的转义或通配符处理）。
        /// </summary>
        /// <param name="value">原始匹配值。</param>
        /// <returns>返回适合放入 LIKE 的值字符串。</returns>
        string ToSqlLikeValue(string value);

        /// <summary>
        /// 构建一个函数调用的 SQL 片段，例如 <c>FUNCTION(arg1, arg2)</c>。
        /// 实现应负责函数名和参数在目标数据库中的兼容性处理。
        /// </summary>
        /// <param name="functionName">函数名。</param>
        /// <param name="args">函数参数列表。</param>
        /// <returns>返回构建好的函数调用 SQL 片段。</returns>
        string BuildFunctionSql(string functionName, IList<KeyValuePair<string, Expr>> args);

        /// <summary>
        /// 构建多个字符串连接的 SQL 表达式，兼容目标数据库的连接语法。
        /// </summary>
        /// <param name="strs">要连接的字符串或表达式片段。</param>
        /// <returns>返回用于连接的 SQL 片段。</returns>
        string BuildConcatSql(params string[] strs);

        /// <summary>
        /// 从数据库值转换为 .NET 对象值。
        /// </summary>
        /// <param name="dbValue">数据库值。</param>
        /// <param name="objectType">目标对象类型（可选）。</param>
        /// <returns>返回转换后的 .NET 对象值。</returns>
        object ConvertFromDbValue(object dbValue, Type objectType = null);
        /// <summary>
        /// 转换 .NET 对象值为数据库可接受的值。
        /// </summary>
        /// <param name="value">要转换的 .NET 对象值。</param>
        /// <param name="dbType">目标数据库类型（可选）。</param>
        /// <returns>返回转换后的数据库值。</returns>
        object ConvertToDbValue(object value, DbType dbType = DbType.Object);

        /// <summary>
        /// 将 .NET 类型映射为数据库对应的 <see cref="DbType"/>。
        /// </summary>
        /// <param name="type">要映射的 .NET 类型。</param>
        /// <returns>返回对应的 <see cref="DbType"/> 值。</returns>
        DbType GetDbType(Type type);
    }
}
