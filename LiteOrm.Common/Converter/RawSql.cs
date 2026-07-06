namespace LiteOrm.Common
{
    /// <summary>
    /// 表示原始 SQL 文本的标记类型。在 <see cref="ExprString"/> 插值字符串中插入时，
    /// 其内容会原样拼接到生成的 SQL 中，<strong>不</strong>经过参数化或语法处理。
    /// </summary>
    /// <remarks>
    /// 该类型绕过 LiteOrm 的参数化机制，仅用于在 <see cref="ExprString"/> 中插入数据库特定的静态 SQL 片段
    /// （如方言特有语法、提示、尚未封装的函数调用等）。
    /// <para>
    /// <strong>安全警告：</strong>使用者必须自行保证 <see cref="Sql"/> 文本不包含任何用户可控的输入，
    /// 否则可能引入 SQL 注入风险。该类型不参与 <see cref="ExprValidator"/> 验证体系，亦不支持 Expr JSON 序列化往返，
    /// 仅在受信任的服务端代码中作为 ExprString 的辅助入口使用。
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
