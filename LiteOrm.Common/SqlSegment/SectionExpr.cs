using System;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 分页片段，表示 LIMIT/OFFSET 语句
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public class SectionExpr : Expr, ISectionAnchor
    {
        /// <summary>
        /// 初始化 SectionExpr 类的新实例
        /// </summary>
        public SectionExpr() { }

        /// <summary>
        /// 使用指定的跳过数和获取数初始化 SectionExpr 类的新实例
        /// </summary>
        /// <param name="skip">要跳过的记录数</param>
        /// <param name="take">要获取的记录数</param>
        public SectionExpr(int skip, int take)
        {
            Skip = skip;
            Take = take;
        }

        /// <summary>
        /// 使用指定的源片段、跳过数和获取数初始化 SectionExpr 类的新实例
        /// </summary>
        /// <param name="source">源片段</param>
        /// <param name="skip">要跳过的记录数</param>
        /// <param name="take">要获取的记录数</param>
        public SectionExpr(ISqlSegment source, int skip, int take) : this(skip, take)
        {
            Source = source;
        }

        /// <summary>
        /// 获取或设置分页片段的源片段（From表达式）
        /// </summary>
        public ISqlSegment Source { get; set; }

        /// <summary>
        /// 获取片段类型，返回 Section 类型标识
        /// </summary>
        public SqlSegmentType SegmentType => SqlSegmentType.Section;

        /// <summary>
        /// 获取或设置要跳过的记录数
        /// </summary>
        public int Skip { get; set; }

        /// <summary>
        /// 获取或设置要获取的记录数
        /// </summary>
        public int Take { get; set; }

        /// <summary>
        /// 判断两个 SectionExpr 是否相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj) => obj is SectionExpr other && Equals(Source, other.Source) && Skip == other.Skip && Take == other.Take;

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode() => OrderedHashCodes(typeof(SectionExpr).GetHashCode(), Source?.GetHashCode() ?? 0, Skip, Take);

        /// <summary>
        /// 返回分页片段的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString() => $"{Source} SKIP {Skip} TAKE {Take}";
    }
}
