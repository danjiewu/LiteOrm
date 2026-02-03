using System;
using System.Collections.Generic;
using System.Linq;

namespace LiteOrm.Common
{
    public class GroupByExpr : HavingSourceExpr
    {
        public GroupByExpr() { }
        public GroupByExpr(GroupBySourceExpr source, params ValueTypeExpr[] groupBys)
        {
            Source = source;
            GroupBys = groupBys?.ToList() ?? new List<ValueTypeExpr>();
        }

        public GroupBySourceExpr Source { get; set; }
        public List<ValueTypeExpr> GroupBys { get; set; } = new List<ValueTypeExpr>();
        public override bool Equals(object obj) => obj is GroupByExpr other && Equals(Source, other.Source) && GroupBys.SequenceEqual(other.GroupBys);
        public override int GetHashCode() => OrderedHashCodes(typeof(GroupByExpr).GetHashCode(), Source?.GetHashCode() ?? 0, GroupBys.GetHashCode());
        public override string ToString() => $"{Source} GROUP BY {string.Join(", ", GroupBys)}";
    }
}
