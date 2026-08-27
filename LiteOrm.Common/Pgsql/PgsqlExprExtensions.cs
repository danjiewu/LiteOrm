using LiteOrm.Common;

namespace LiteOrm.Pgsql
{
    /// <summary>
    /// PostgreSQL 专用的 <see cref="ValueTypeExpr"/> 扩展方法。
    /// <para>
    /// 这些扩展仅构造 <see cref="FunctionExpr"/> / <see cref="LogicBinaryExpr"/>，SQL 具体生成
    /// 由 LiteOrm 库 LiteOrmSqlFunctionInitializer 注册的函数处理器完成（针对 PostgreSQL 方言）。
    /// 在非 PostgreSQL 方言下使用这些扩展生成的表达式可能无法正确翻译，请仅用于 PgSQL 场景。
    /// </para>
    /// </summary>
    public static class PgsqlExprExtensions
    {
        /// <summary>
        /// 生成 <c>array_to_string(array, delimiter)</c>。
        /// </summary>
        /// <param name="array">数组表达式。</param>
        /// <param name="delimiter">分隔符。</param>
        public static FunctionExpr ArrayToString(this ValueTypeExpr array, string delimiter)
            => new FunctionExpr("array_to_string", array, new ValueExpr(delimiter));

        /// <summary>
        /// 生成 <c>array_to_string(array, delimiter, null_string)</c>。
        /// </summary>
        /// <param name="array">数组表达式。</param>
        /// <param name="delimiter">分隔符。</param>
        /// <param name="nullString">数组中 NULL 元素的替代文本。</param>
        public static FunctionExpr ArrayToString(this ValueTypeExpr array, string delimiter, string nullString)
            => new FunctionExpr("array_to_string", array, new ValueExpr(delimiter), new ValueExpr(nullString));

        /// <summary>
        /// 生成 <c>array_append(array, element)</c>。
        /// </summary>
        /// <param name="array">数组表达式。</param>
        /// <param name="element">要追加的元素。</param>
        public static FunctionExpr ArrayAppend(this ValueTypeExpr array, ValueTypeExpr element)
            => new FunctionExpr("array_append", array, element);

        /// <summary>
        /// 生成 <c>ANY(array)</c>，通常用于 <c>value == array.Any()</c> 生成 <c>value = ANY(array)</c>。
        /// </summary>
        /// <param name="array">数组表达式。</param>
        public static FunctionExpr Any(this ValueTypeExpr array)
            => new FunctionExpr("ANY", array);

        /// <summary>
        /// 生成 <c>value = ANY(array)</c>（判断 <paramref name="value"/> 是否等于数组中的任一元素）。
        /// </summary>
        /// <param name="value">要匹配的值表达式。</param>
        /// <param name="array">数组表达式。</param>
        public static LogicBinaryExpr Any(this ValueTypeExpr value, ValueTypeExpr array)
            => new LogicBinaryExpr(value, LogicOperator.Equal, new FunctionExpr("ANY", array));

        /// <summary>
        /// 生成 <c>element = ANY(array)</c>（判断数组是否包含指定元素）。
        /// </summary>
        /// <param name="array">数组表达式。</param>
        /// <param name="element">要查找的元素。</param>
        public static LogicBinaryExpr Contains(this ValueTypeExpr array, ValueTypeExpr element)
            => new LogicBinaryExpr(element, LogicOperator.Equal, new FunctionExpr("ANY", array));

        /// <summary>
        /// 生成 <c>jsonb_extract_path(jsonb, path...)</c>。
        /// </summary>
        /// <param name="jsonb">JSONB 表达式。</param>
        /// <param name="path">路径键序列。</param>
        public static FunctionExpr JsonbExtractPath(this ValueTypeExpr jsonb, params ValueTypeExpr[] path)
            => BuildPathFunction("jsonb_extract_path", jsonb, path);

        /// <summary>
        /// 生成 <c>jsonb_extract_path_text(jsonb, path...)</c>。
        /// </summary>
        /// <param name="jsonb">JSONB 表达式。</param>
        /// <param name="path">路径键序列。</param>
        public static FunctionExpr JsonbExtractPathText(this ValueTypeExpr jsonb, params ValueTypeExpr[] path)
            => BuildPathFunction("jsonb_extract_path_text", jsonb, path);

        /// <summary>
        /// 生成 <c>jsonb @&gt; json</c>（判断 JSONB 是否包含另一个 JSON 值）。
        /// </summary>
        /// <param name="jsonb">左侧 JSONB 表达式。</param>
        /// <param name="json">右侧 JSON 表达式。</param>
        public static FunctionExpr JsonbContains(this ValueTypeExpr jsonb, ValueTypeExpr json)
            => new FunctionExpr("jsonb_contains", jsonb, json);

        /// <summary>
        /// 生成 <c>jsonb_build_object(key, value, ...)</c>。
        /// </summary>
        /// <param name="keyValues">键值对序列（键、值交替）。</param>
        public static FunctionExpr JsonbBuildObject(params ValueTypeExpr[] keyValues)
            => new FunctionExpr("jsonb_build_object", keyValues);

        /// <summary>
        /// 生成 <c>jsonb_build_array(element, ...)</c>。
        /// </summary>
        /// <param name="elements">数组元素序列。</param>
        public static FunctionExpr JsonbBuildArray(params ValueTypeExpr[] elements)
            => new FunctionExpr("jsonb_build_array", elements);

        private static FunctionExpr BuildPathFunction(string functionName, ValueTypeExpr jsonb, ValueTypeExpr[] path)
        {
            var args = new ValueTypeExpr[path.Length + 1];
            args[0] = jsonb;
            for (int i = 0; i < path.Length; i++) args[i + 1] = path[i];
            return new FunctionExpr(functionName, args);
        }
    }
}
