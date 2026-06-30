using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;


namespace LiteOrm
{
    /// <summary>
    /// GreatDB（万里数据库）SQL 构建器。
    /// </summary>
    /// <remarks>
    /// GreatDB 兼容 MySQL 协议与大部分语法，通过 MySqlConnector / MySql.Data 驱动接入，
    /// 因此直接继承自 <see cref="MySqlBuilder"/>。如使用 GreatDB 分布式集群特性，
    /// 可按需在子类中覆盖分片相关方法。
    ///
    /// 默认匹配关键字：<c>GREATDB</c>。
    /// </remarks>
    public class GreatDBBuilder : MySqlBuilder
    {
        /// <summary>
        /// 获取 <see cref="GreatDBBuilder"/> 的单例实例。
        /// </summary>
        public static readonly new GreatDBBuilder Instance = new GreatDBBuilder();

        // GreatDB MySQL 兼容模式下与 MySQL 行为一致，暂不覆盖任何方法。
    }
}
