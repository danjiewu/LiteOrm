namespace LiteOrm
{
    /// <summary>
    /// OceanBase SQL 构建器（MySQL 兼容模式）。
    /// </summary>
    /// <remarks>
    /// OceanBase 默认提供 MySQL 兼容模式，使用 MySqlConnector / MySql.Data 驱动接入，
    /// 其分页、参数前缀、自增、批量更新等语法与 MySQL 一致，故直接继承自 <see cref="MySqlBuilder"/>。
    ///
    /// 默认匹配关键字：<c>OCEANBASE</c>。
    /// </remarks>
    public class OceanBaseBuilder : MySqlBuilder
    {
        /// <summary>
        /// 获取 <see cref="OceanBaseBuilder"/> 的单例实例。
        /// </summary>
        public static readonly new OceanBaseBuilder Instance = new OceanBaseBuilder();

        // OceanBase MySQL 模式与 MySQL 行为一致，暂不覆盖任何方法。
        // 保留独立类型用于 Oracle 模式分流或后续分布式特性扩展。
    }
}
