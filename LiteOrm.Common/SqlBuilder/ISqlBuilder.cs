using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示用于数据库值与 .NET 对象值之间转换的接口。
    /// </summary>
    public interface IDbConverter
    {
        /// <summary>
        /// 获取将数据库值转换为 <paramref name="objectType"/> 类型值的转换委托。
        /// 委托按目标类型缓存，获取后可直接复用，避免每次转换都重新分发。
        /// </summary>
        /// <param name="objectType">目标对象类型。</param>
        /// <returns>转换委托：输入数据库值，输出目标类型的值。</returns>
        Func<object?, object?> GetFromDbValueConverter([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] Type objectType);
        /// <summary>
        /// 获取将 <paramref name="sourceType"/> 类型的 .NET 值转换为数据库可接受值的转换委托。
        /// 委托按 (源类型, 目标取值类型) 缓存，获取后可直接复用，避免每次转换都重新分发。
        /// </summary>
        /// <param name="sourceType">源值类型。</param>
        /// <param name="dbValueType">目标数据库取值类型（可含 <see cref="DbValueType.Array"/> 掩码）。</param>
        /// <returns>转换委托：输入 .NET 值，输出数据库可接受的值。</returns>
        Func<object?, object> GetToDbValueConverter(Type sourceType, DbValueType dbValueType);
        /// <summary>
        /// 尝试获取数据库读取从 <typeparamref name="TSource"/> 到 <typeparamref name="TResult"/> 的转换器函数。
        /// </summary>
        /// <typeparam name="TSource">从数据库读取的值的类型。</typeparam>
        /// <typeparam name="TResult">要转换的目标实体属性类型。</typeparam>
        /// <param name="handler">输出转换器函数。</param>
        /// <returns>如果成功获取转换器函数，则返回 true；否则返回 false。</returns>
        bool TryGetReadConverter<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] TResult>(out Func<TSource, TResult>? handler);
        /// <summary>
        /// 将 .NET 类型映射为数据库对应的 <see cref="DbValueType"/>。
        /// </summary>
        /// <param name="type">要映射的 .NET 类型。</param>
        /// <returns>返回对应的 <see cref="DbValueType"/> 值。</returns>
        DbValueType GetDbValueType(Type type);
        /// <summary>
        /// 获取指定数据库取值类型的默认列长度。
        /// </summary>
        /// <param name="dbValueType">数据库取值类型。</param>
        /// <returns>默认存储长度。</returns>
        int GetDefaultLength(DbValueType dbValueType);
        /// <summary>
        /// 将 <see cref="DbValueType"/> 转换为 <see cref="DbType"/>（数据库操作边界转换）。
        /// 子类可覆盖以提供方言特定的映射（如 Oracle 将 Boolean 映射为 Byte、DateTime 映射为 Date）。
        /// </summary>
        /// <param name="dbValueType">数据库取值类型（可含 <see cref="DbValueType.Array"/> 掩码）。</param>
        /// <returns>对应的 <see cref="DbType"/> 值。</returns>
        DbType ToDbType(DbValueType dbValueType);
    }

    /// <summary>
    /// 表示用于生成数据库相关 SQL 片段的构建器接口。
    /// 不同数据库的实现负责将通用表达转换为目标数据库的原生 SQL 语法和参数格式。
    /// </summary>
    public interface ISqlBuilder: IDbConverter
    {
        /// <summary>
        /// 获取当前数据库是否支持公共表表达式（CTE / WITH 子句）。
        /// 若为 <see langword="false"/>，CTE 将被展开为内联子查询。
        /// </summary>
        bool SupportCteExpr { get; }

        /// <summary>
        /// 获取当前数据库是否要求显式声明递归 CTE（即使用 WITH RECURSIVE 语法）。
        /// 若为 <see langword="true"/>，生成 SQL 时会在 WITH 后直接追加 RECURSIVE 关键字。
        /// SQL Server、Oracle 等不需要显式 RECURSIVE 关键字的数据库可保持 <see langword="false"/>。
        /// </summary>
        bool ExplicitRecursive { get; }

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
        /// 判断字符串是否包含需要转义的 LIKE 特殊字符。
        /// </summary>
        /// <param name="value">要检查的字符串。</param>
        /// <returns>如果包含需要转义的特殊字符则返回 true。</returns>
        bool NeedLikeEscape(string value);

        /// <summary>
        /// 构建一个函数调用的 SQL 片段，直接写入 <paramref name="outSql"/>。
        /// 实现应负责函数名和参数在目标数据库中的兼容性处理。
        /// </summary>
        /// <param name="outSql">接收输出 SQL 片段的字符串构建器。</param>
        /// <param name="expr">函数表达式，包含函数名及参数列表。</param>
        /// <param name="context">SQL 构建上下文。</param>
        /// <param name="outputParams">输出参数集合。</param>
        void BuildFunctionSql(ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ICollection<Param> outputParams);

        /// <summary>
        /// 使用传入的 <see cref="ValueStringBuilder"/> 构建字符串连接 SQL 片段。
        /// </summary>
        /// <param name="sb">接收输出 SQL 片段的字符串构建器。</param>
        /// <param name="strs">要连接的字符串或表达式片段。</param>
        void BuildConcatSql(ref ValueStringBuilder sb, params string[] strs);

        /// <summary>
        /// 将结构化的 SQL 片段组装成最终的 SELECT 语句。
        /// </summary>
        /// <param name="subSelect">包含 SELECT 各个子句片段的结构体。</param>
        /// <param name="result">输出 SQL 语句的缓冲区。</param>
        /// <param name="indent">缩进级别。</param>
        void BuildSelectSql(ref SqlValueStringBuilder subSelect, ref ValueStringBuilder result,int indent);

        /// <summary>
        /// 将集合操作类型转换为 SQL 语句
        /// </summary>
        /// <param name="selectSetType">集合操作类型</param>
        /// <returns>返回对应的 SQL 语句片段。</returns>
        string ToSelectSetTypeSql(SelectSetType selectSetType);
    }
}
