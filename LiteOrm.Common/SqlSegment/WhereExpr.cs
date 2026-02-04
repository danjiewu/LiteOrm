using System;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    [JsonConverter(typeof(SqlSegmentJsonConverterFactory))]
    public class WhereExpr : SqlSegment, ISourceAnchor
    {
        public WhereExpr() { }
        public WhereExpr(SqlSegment source, LogicExpr where)
        {
            Source = source;
            Where = where;
        }

        public override SqlSegmentType SegmentType => SqlSegmentType.Where;

        public LogicExpr Where { get; set; }

        public override bool Equals(object obj) => obj is WhereExpr other && Equals(Source, other.Source) && Equals(Where, other.Where);
        public override int GetHashCode() => OrderedHashCodes(typeof(WhereExpr).GetHashCode(), Source?.GetHashCode() ?? 0, Where?.GetHashCode() ?? 0);
        public override string ToString() => $"{Source} WHERE {Where}";
    }
}
