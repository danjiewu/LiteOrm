namespace LiteOrm
{
    /// <summary>
    /// 华为 GaussDB SQL 构建器。
    /// </summary>
    /// <remarks>
    /// GaussDB（含原 openGauss 内核版本）兼容 PostgreSQL 协议与大部分语法，
    /// 通过 Npgsql / Kdbndp 兼容驱动接入。本构建器继承自 <see cref="PostgreSqlBuilder"/>，
    /// 仅在出现内核差异时按需覆盖。
    ///
    /// 默认匹配关键字：<c>GAUSSDB</c>、<c>OPENGAUSS</c>。
    /// </remarks>
    public class GaussDBBuilder : PostgreSqlBuilder
    {
        /// <summary>
        /// 获取 <see cref="GaussDBBuilder"/> 的单例实例。
        /// </summary>
        public static readonly new GaussDBBuilder Instance = new GaussDBBuilder();

        // openGauss / GaussDB 在常用 SQL 与 PostgreSQL 一致，
        // 暂无需覆盖；保留独立类型以便后续扩展分布式 / 分布表相关语法。
    }
}
