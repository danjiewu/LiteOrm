using System;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 聚合筛选片段，表示 HAVING 语句
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public class HavingExpr : SqlSegment, IHavingAnchor
    {
        /// <summary>
        /// 初始化 HavingExpr 类的新实例
        /// </summary>
        public HavingExpr() { }

        /// <summary>
        /// 使用指定的源片段和 Having 条件初始化 HavingExpr 类的新实例
        /// </summary>
        /// <param name="source">源片段</param>
        /// <param name="having">Having 条件表达式</param>
        public HavingExpr(SqlSegment source, LogicExpr having)
        {
            Source = source;
            Having = having;
        }

        /// <summary>
        /// 获取片段类型，返回 Having 类型标识
        /// </summary>
        public override SqlSegmentType SegmentType => SqlSegmentType.Having;

        /// <summary>
        /// 获取或设置 Having 条件表达式
        /// </summary>
        public LogicExpr Having { get; set; }

        /// <summary>
        /// 判断两个 HavingExpr 是否相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj) => obj is HavingExpr other && Equals(Source, other.Source) && Equals(Having, other.Having);

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode() => OrderedHashCodes(typeof(HavingExpr).GetHashCode(), Source?.GetHashCode() ?? 0, Having?.GetHashCode() ?? 0);

        /// <summary>
        /// 返回 Having 片段的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString() => $"{Source} HAVING {Having}";
    }
}
