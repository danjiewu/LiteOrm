using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    [JsonConverter(typeof(SqlSegmentJsonConverterFactory))]
    public class GroupByExpr : SqlSegment, IGroupByAnchor
    {
        public GroupByExpr() { }
        public GroupByExpr(SqlSegment source, params ValueTypeExpr[] groupBys)
        {
            Source = source;
            GroupBys = groupBys?.ToList() ?? new List<ValueTypeExpr>();
        }

        public override SqlSegmentType SegmentType => SqlSegmentType.GroupBy;

        public List<ValueTypeExpr> GroupBys { get; set; } = new List<ValueTypeExpr>();
        public override bool Equals(object obj) => obj is GroupByExpr other && Equals(Source, other.Source) && GroupBys.SequenceEqual(other.GroupBys);
        public override int GetHashCode() => OrderedHashCodes(typeof(GroupByExpr).GetHashCode(), Source?.GetHashCode() ?? 0, SequenceHash(GroupBys));
        public override string ToString() => $"{Source} GROUP BY {string.Join(", ", GroupBys)}";
    }
}
