using System;
using System.Collections.Generic;
using System.Linq;

namespace LiteOrm.Common
{
    public class SelectExpr : SourceExpr
    {
        public SelectExpr() { }
        public SelectExpr(SelectSourceExpr source, params ValueTypeExpr[] selects)
        {
            Source = source;
            Selects = selects?.ToList() ?? new List<ValueTypeExpr>();
        }

        public SelectSourceExpr Source { get; set; }
        public List<ValueTypeExpr> Selects { get; set; } = new List<ValueTypeExpr>();
        public override bool Equals(object obj) => obj is SelectExpr other && Equals(Source, other.Source) && Selects.SequenceEqual(other.Selects);
        public override int GetHashCode() => OrderedHashCodes(typeof(SelectExpr).GetHashCode(), Source?.GetHashCode() ?? 0, Selects.GetHashCode());
        public override string ToString() => $"SELECT {string.Join(", ", Selects)} FROM {Source}";
    }
}
