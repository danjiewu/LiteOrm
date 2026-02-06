using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 排序片段，表示 ORDER BY 子句
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public class OrderByExpr : SqlSegment, IOrderByAnchor
    {
        /// <summary>
        /// 初始化 OrderByExpr 类的新实例
        /// </summary>
        public OrderByExpr() { }

        /// <summary>
        /// 使用指定的源片段和排序字段列表初始化 OrderByExpr 类的新实例
        /// </summary>
        /// <param name="source">源片段</param>
        /// <param name="orderBys">排序字段元组列表，每个元组包含字段表达式和升序/降序标识</param>
        public OrderByExpr(SqlSegment source, params (ValueTypeExpr, bool)[] orderBys)
        {
            Source = source;
            OrderBys = orderBys?.ToList() ?? new List<(ValueTypeExpr, bool)>();
        }

        /// <summary>
        /// 获取片段类型，返回 OrderBy 类型标识
        /// </summary>
        public override SqlSegmentType SegmentType => SqlSegmentType.OrderBy;

        /// <summary>
        /// 获取或设置排序字段元组列表
        /// </summary>
        public List<(ValueTypeExpr, bool)> OrderBys { get; set; } = new List<(ValueTypeExpr, bool)>();

        /// <summary>
        /// 判断两个 OrderByExpr 是否相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj) => obj is OrderByExpr other && Equals(Source, other.Source) && OrderBys.SequenceEqual(other.OrderBys);

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode() => OrderedHashCodes(typeof(OrderByExpr).GetHashCode(), Source?.GetHashCode() ?? 0, SequenceHash(OrderBys));

        /// <summary>
        /// 返回排序片段的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString() => $"{Source} ORDER BY {string.Join(", ", OrderBys.Select(ob => $"{ob.Item1} {(ob.Item2 ? "ASC" : "DESC")}"))}";
    }
}
