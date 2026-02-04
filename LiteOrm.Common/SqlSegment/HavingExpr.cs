using System;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    [JsonConverter(typeof(SqlSegmentJsonConverterFactory))]
    public class HavingExpr : SqlSegment, IHavingAnchor
    {
        public HavingExpr() { }
        public HavingExpr(SqlSegment source, LogicExpr having)
        {
            Source = source;
            Having = having;
        }

        public override SqlSegmentType SegmentType => SqlSegmentType.Having;

        public LogicExpr Having { get; set; }
        public override bool Equals(object obj) => obj is HavingExpr other && Equals(Source, other.Source) && Equals(Having, other.Having);
        public override int GetHashCode() => OrderedHashCodes(typeof(HavingExpr).GetHashCode(), Source?.GetHashCode() ?? 0, Having?.GetHashCode() ?? 0);
        public override string ToString() => $"{Source} HAVING {Having}";
    }
}
