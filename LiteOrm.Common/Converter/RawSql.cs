namespace LiteOrm.Common
{
    /// <summary>
    /// 表示原始 SQL 文本的标记类型。在 <see cref="ExprString"/> 插值字符串中插入时，
    /// 其内容会原样拼接到生成的 SQL 中，<strong>不</strong>经过参数化或语法处理。
    /// </summary>
    /// <remarks>
    /// 该类型绕过 LiteOrm 的参数化机制，专用于在 <see cref="ExprString"/> 中插入
    /// <strong>动态但不适合使用参数</strong>的 SQL 片段，典型场景包括：
    /// <list type="bullet">
    /// <item><c>LIMIT</c>/<c>OFFSET</c> 的整数值、分页行数、<c>TOP n</c> 等数值类动态值；</item>
    /// <item><c>ORDER BY</c> 的排序方向（<c>ASC</c>/<c>DESC</c>）——SQL 关键字无法参数化；</item>
    /// <item>动态列名/排序字段——若不适合用 <see cref="Expr"/> 表达时（如复杂表达式或绕过名称校验场景）。</item>
    /// </list>
    /// 这些片段的值是运行时计算的，但若以参数形式传入部分数据库会拒绝或改变执行计划，或本身即为 SQL 关键字/标识符不能参数化，
    /// 因此需要直接内联到 SQL 文本中。
    /// <para>
    /// <strong>纯静态的 SQL 文本不需要使用 <c>RawSql</c></strong>，直接写在 <see cref="ExprString"/> 的字面量部分即可。
    /// </para>
    /// <para>
    /// <strong>安全警告：</strong>该类型不参与 <see cref="ExprValidator"/> 验证体系，亦不支持 Expr JSON 序列化往返，
    /// 仅在受信任的服务端代码中作为 ExprString 的辅助入口使用。调用方必须自行保证 <see cref="Sql"/> 文本安全：
    /// 数值类动态值需校验范围（如非负整数、上限）；字符串/token 类动态值（如列名、<c>ASC</c>/<c>DESC</c>）需用白名单校验，
    /// 绝不可拼入未经验证的用户输入而引入 SQL 注入风险。
    /// </para>
    /// </remarks>
    public readonly struct RawSql
    {
        /// <summary>
        /// 初始化 <see cref="RawSql"/> 实例。
        /// </summary>
        /// <param name="sql">原始 SQL 文本。</param>
        public RawSql(string sql)
        {
            Sql = sql;
        }

        /// <summary>
        /// 获取原始 SQL 文本。
        /// </summary>
        public string Sql { get; }

        /// <summary>
        /// 创建 <see cref="RawSql"/> 实例的便捷工厂方法。
        /// </summary>
        /// <param name="sql">原始 SQL 文本。</param>
        /// <returns>封装了指定 SQL 文本的 <see cref="RawSql"/> 实例。</returns>
        public static RawSql From(string sql) => new RawSql(sql);

        /// <summary>
        /// 返回表示当前对象的字符串，即原始 SQL 文本本身。
        /// </summary>
        public override string ToString() => Sql ?? string.Empty;
    }
}
