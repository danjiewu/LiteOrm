using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 分组片段，表示 GROUP BY 子句
    /// </summary>
    [JsonConverter(typeof(SqlSegmentJsonConverterFactory))]
    public class GroupByExpr : SqlSegment, IGroupByAnchor
    {
        /// <summary>
        /// 初始化 GroupByExpr 类的新实例
        /// </summary>
        public GroupByExpr() { }

        /// <summary>
        /// 使用指定的源片段和分组字段列表初始化 GroupByExpr 类的新实例
        /// </summary>
        /// <param name="source">源片段</param>
        /// <param name="groupBys">分组字段表达式列表</param>
        public GroupByExpr(SqlSegment source, params ValueTypeExpr[] groupBys)
        {
            Source = source;
            GroupBys = groupBys?.ToList() ?? new List<ValueTypeExpr>();
        }

        /// <summary>
        /// 获取片段类型，返回 GroupBy 类型标识
        /// </summary>
        public override SqlSegmentType SegmentType => SqlSegmentType.GroupBy;

        /// <summary>
        /// 获取或设置分组字段表达式列表
        /// </summary>
        public List<ValueTypeExpr> GroupBys { get; set; } = new List<ValueTypeExpr>();

        /// <summary>
        /// 判断两个 GroupByExpr 是否相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj) => obj is GroupByExpr other && Equals(Source, other.Source) && GroupBys.SequenceEqual(other.GroupBys);

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode() => OrderedHashCodes(typeof(GroupByExpr).GetHashCode(), Source?.GetHashCode() ?? 0, SequenceHash(GroupBys));

        /// <summary>
        /// 返回分组片段的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString() => $"{Source} GROUP BY {string.Join(", ", GroupBys)}";
    }
}
