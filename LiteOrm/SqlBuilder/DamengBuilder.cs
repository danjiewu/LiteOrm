namespace LiteOrm
{
    /// <summary>
    /// 达梦（DM）SQL 构建器。
    /// </summary>
    /// <remarks>
    /// 达梦数据库（DM7/DM8）SQL 语法与 Oracle 高度兼容，使用 Dm 驱动（Dm.DmProvider）接入，
    /// 故本构建器继承自 <see cref="OracleBuilder"/>。主要差异点：
    /// <list type="bullet">
    /// <item>标识列采用 <c>IDENTITY(1, 1)</c> 内联语法，而非 Oracle 的 GENERATED AS IDENTITY。</item>
    /// <item>默认大小写策略与 Oracle 一致（双引号包裹、内部转大写）。</item>
    /// <item>EXCEPT 仍需翻译为 MINUS。</item>
    /// <item>布尔类型映射为 NUMBER(1)。</item>
    /// </list>
    ///
    /// 默认匹配关键字：<c>DM.</c>、<c>DAMENG</c>、<c>DMNET</c>。
    /// </remarks>
    public class DamengBuilder : OracleBuilder
    {
        /// <summary>
        /// 获取 <see cref="DamengBuilder"/> 的单例实例。
        /// </summary>
        public static readonly new DamengBuilder Instance = new DamengBuilder();

        /// <summary>
        /// 达梦使用 IDENTITY(1, 1) 内联语法生成标识列，与 Oracle 的 GENERATED AS IDENTITY 不同。
        /// </summary>
        protected override string GetAutoIncrementSql() => "IDENTITY(1, 1)";
    }
}
