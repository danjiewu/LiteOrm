using System;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 筛选片段，表示 WHERE 语句
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public class WhereExpr : SqlSegment, ISourceAnchor
    {
        /// <summary>
        /// 初始化 WhereExpr 类的新实例
        /// </summary>
        public WhereExpr() { }

        /// <summary>
        /// 使用指定的源片段和筛选条件初始化 WhereExpr 类的新实例
        /// </summary>
        /// <param name="source">源片段（如 TableExpr）</param>
        /// <param name="where">筛选条件表达式</param>
        public WhereExpr(SqlSegment source, LogicExpr where)
        {
            Source = source;
            Where = where;
        }

        /// <summary>
        /// 获取片段类型，返回 Where 类型标识
        /// </summary>
        public override SqlSegmentType SegmentType => SqlSegmentType.Where;

        /// <summary>
        /// 获取或设置筛选条件表达式
        /// </summary>
        public LogicExpr Where { get; set; }

        /// <summary>
        /// 判断两个 WhereExpr 是否相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj) => obj is WhereExpr other && Equals(Source, other.Source) && Equals(Where, other.Where);

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode() => OrderedHashCodes(typeof(WhereExpr).GetHashCode(), Source?.GetHashCode() ?? 0, Where?.GetHashCode() ?? 0);

        /// <summary>
        /// 返回筛选片段的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString() => $"{Source} WHERE {Where}";
    }
}
