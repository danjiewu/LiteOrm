using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;


namespace LiteOrm
{
    /// <summary>
    /// 人大金仓 KingbaseES SQL 构建器。
    /// </summary>
    /// <remarks>
    /// KingbaseES 高度兼容 PostgreSQL 协议和语法（Kdbndp 驱动），因此本构建器直接继承自
    /// <see cref="PostgreSqlBuilder"/>，仅在需要时覆盖差异点。
    ///
    /// 默认匹配关键字：<c>KINGBASE</c>、<c>KDBNDP</c>。
    /// </remarks>
    public class KingbaseESBuilder : PostgreSqlBuilder
    {
        /// <summary>
        /// 获取 <see cref="KingbaseESBuilder"/> 的单例实例。
        /// </summary>
        public static readonly new KingbaseESBuilder Instance = new KingbaseESBuilder();

        // KingbaseES 9+ 与 PostgreSQL 行为高度一致，
        // 暂不需要覆盖任何方法；保留独立类型用于：
        // 1) 在工厂的关键字识别中分流；
        // 2) 后续按版本差异追加专属扩展点（如自定义类型、双引号大小写策略等）。
    }
}
