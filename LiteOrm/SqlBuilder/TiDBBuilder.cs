namespace LiteOrm
{
    /// <summary>
    /// TiDB SQL 构建器。
    /// </summary>
    /// <remarks>
    /// TiDB 与 MySQL 协议高度兼容（使用 MySqlConnector / MySql.Data 驱动），
    /// 分页、参数前缀、自增等行为与 MySQL 一致，故直接继承自 <see cref="MySqlBuilder"/>。
    /// 注：TiDB 的自增列在分布式场景下仅保证唯一、不保证连续，
    /// <see cref="MySqlBuilder.BuildBatchIdentityInsertSql"/> 返回的 LAST_INSERT_ID() 仅作为首行参考值。
    ///
    /// 默认匹配关键字：<c>TIDB</c>。
    /// </remarks>
    public class TiDBBuilder : MySqlBuilder
    {
        /// <summary>
        /// 获取 <see cref="TiDBBuilder"/> 的单例实例。
        /// </summary>
        public static readonly new TiDBBuilder Instance = new TiDBBuilder();

        // TiDB 与 MySQL 行为基本一致，暂不覆盖任何方法；
        // 保留独立类型便于后续按需扩展 TiDB 特有特性（如 AUTO_RANDOM）。
    }
}
