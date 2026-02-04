using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    [JsonConverter(typeof(SqlSegmentJsonConverterFactory))]
    public class SelectExpr : SqlSegment, ISelectAnchor
    {
        public SelectExpr() { }
        public SelectExpr(SqlSegment source, params ValueTypeExpr[] selects)
        {
            Source = source;
            Selects = selects?.ToList() ?? new List<ValueTypeExpr>();
        }

        public override bool IsValue =>  true;

        public override SqlSegmentType SegmentType => SqlSegmentType.Select;

        public List<ValueTypeExpr> Selects { get; set; } = new List<ValueTypeExpr>();
        public override bool Equals(object obj) => obj is SelectExpr other && Equals(Source, other.Source) && Selects.SequenceEqual(other.Selects);
        public override int GetHashCode() => OrderedHashCodes(typeof(SelectExpr).GetHashCode(), Source?.GetHashCode() ?? 0, SequenceHash(Selects));
        public override string ToString() => $"SELECT {string.Join(", ", Selects)} FROM {Source}";
    }
}
