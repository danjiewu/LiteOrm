using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// 外部表信息，用于描述关联的外部表
    /// </summary>
    public class ForeignTable
    {
        /// <summary>
        /// 外部表对应的实体类型
        /// </summary>
        public Type ForeignType { get; set; }
        /// <summary>
        /// 联合查询连接类型（如 Left Join）。
        /// </summary>
        public TableJoinType JoinType { get; set; }
        /// <summary>
        /// 是否自动扩展连接的外表。当AutoExpand为true并且作为外表被引用时，自动将本表关联的外表引入连接。默认为false，即不自动扩展连接的外表。
        /// </summary>
        public bool AutoExpand { get; set; } = false;

        /// <summary>
        /// 获取或设置外部表的别名。别名用于在 SQL 查询中引用该外部表，特别是在存在多个关联表时，可以通过别名区分不同的表。
        /// </summary>
        public string Alias { get; set; }
    }
}
