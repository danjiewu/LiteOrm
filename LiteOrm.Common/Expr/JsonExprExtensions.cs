namespace LiteOrm.Common
{
    /// <summary>
    /// 跨数据库的 JSON 函数表达式扩展方法。
    /// <para>
    /// 这些方法构造统一的 <see cref="FunctionExpr"/>（函数名如 <c>JsonExtract</c>、<c>JsonValue</c>、
    /// <c>JsonQuery</c>、<c>JsonContains</c>、<c>JsonObject</c>、<c>JsonArray</c>、<c>IsJson</c>），
    /// 由各数据库方言（通过 LiteOrmSqlFunctionInitializer 注册）翻译为原生 SQL 函数。
    /// </para>
    /// </summary>
    public static class JsonExprExtensions
    {
        /// <summary>
        /// 从 JSON 文档中提取指定路径的值（返回 JSON 值）。
        /// </summary>
        /// <param name="expr">JSON 表达式。</param>
        /// <param name="path">JSON 路径（如 <c>$.name</c>）。</param>
        public static FunctionExpr JsonExtract(this ValueTypeExpr expr, string path)
            => new FunctionExpr("JsonExtract", expr, new ValueExpr(path));

        /// <summary>
        /// 从 JSON 文档中提取指定路径的标量文本值。
        /// </summary>
        /// <param name="expr">JSON 表达式。</param>
        /// <param name="path">JSON 路径。</param>
        public static FunctionExpr JsonValue(this ValueTypeExpr expr, string path)
            => new FunctionExpr("JsonValue", expr, new ValueExpr(path));

        /// <summary>
        /// 从 JSON 文档中提取指定路径的 JSON 片段。
        /// </summary>
        /// <param name="expr">JSON 表达式。</param>
        /// <param name="path">JSON 路径。</param>
        public static FunctionExpr JsonQuery(this ValueTypeExpr expr, string path)
            => new FunctionExpr("JsonQuery", expr, new ValueExpr(path));

        /// <summary>
        /// 判断 JSON 文档是否包含指定 JSON 值（返回布尔值 0/1）。
        /// </summary>
        /// <param name="expr">JSON 表达式。</param>
        /// <param name="candidate">要判断是否被包含的 JSON 值。</param>
        public static FunctionExpr JsonContains(this ValueTypeExpr expr, ValueTypeExpr candidate)
            => new FunctionExpr("JsonContains", expr, candidate);

        /// <summary>
        /// 构建 JSON 对象（键值对交替传入）。
        /// </summary>
        /// <param name="keyValues">键值对序列（键、值交替）。</param>
        public static FunctionExpr JsonObject(params ValueTypeExpr[] keyValues)
            => new FunctionExpr("JsonObject", keyValues);

        /// <summary>
        /// 构建 JSON 数组。
        /// </summary>
        /// <param name="elements">数组元素序列。</param>
        public static FunctionExpr JsonArray(params ValueTypeExpr[] elements)
            => new FunctionExpr("JsonArray", elements);

        /// <summary>
        /// 判断表达式是否为合法 JSON（返回布尔值 0/1）。
        /// </summary>
        /// <param name="expr">要校验的表达式。</param>
        public static FunctionExpr IsJson(this ValueTypeExpr expr)
            => new FunctionExpr("IsJson", expr);
    }
}
