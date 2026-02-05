using System;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表片段，表示查询的数据源表
    /// </summary>
    [JsonConverter(typeof(SqlSegmentJsonConverterFactory))]
    public sealed class TableExpr : SqlSegment, ISourceAnchor
    {
        /// <summary>
        /// 初始化 TableExpr 类的新实例
        /// </summary>
        public TableExpr() { }

        /// <summary>
        /// 使用指定的表信息初始化 TableExpr 类的新实例
        /// </summary>
        /// <param name="table">表元数据信息</param>
        public TableExpr(SqlTable table) => Table = table;

        /// <summary>
        /// 获取或设置此片段关联的表信息
        /// </summary>
        public new SqlTable Table { get; set; }

        /// <summary>
        /// 获取片段类型，返回 Table 类型标识
        /// </summary>
        public override SqlSegmentType SegmentType => SqlSegmentType.Table;

        /// <summary>
        /// 判断两个 TableExpr 是否相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj) => obj is TableExpr other && Equals(Table, other.Table);

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode() => OrderedHashCodes(typeof(TableExpr).GetHashCode(), Table?.GetHashCode() ?? 0);

        /// <summary>
        /// 返回表的名称字符串
        /// </summary>
        /// <returns>表名称</returns>
        public override string ToString() => Table?.Name ?? string.Empty;
    }
}
