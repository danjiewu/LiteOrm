using System;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    [JsonConverter(typeof(SqlSegmentJsonConverterFactory))]
    public class SectionExpr : SqlSegment, ISectionAnchor
    {
        public SectionExpr() { }
        public SectionExpr(int skip, int take)
        {
            Skip = skip;
            Take = take;
        }

        public SectionExpr(SqlSegment source, int skip, int take) : this(skip, take)
        {
            Source = source;
        }

        public override SqlSegmentType SegmentType => SqlSegmentType.Section;

        public int Skip { get; set; }
        public int Take { get; set; }

        public override bool Equals(object obj) => obj is SectionExpr other && Equals(Source, other.Source) && Skip == other.Skip && Take == other.Take;
        public override int GetHashCode() => OrderedHashCodes(typeof(SectionExpr).GetHashCode(), Source?.GetHashCode() ?? 0, Skip, Take);
        public override string ToString() => $"{Source} SKIP {Skip} TAKE {Take}";
    }
}
