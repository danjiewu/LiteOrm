using System;
using System.Collections.Generic;
using System.Linq;

namespace LiteOrm.Common
{
    public class OrderByExpr : SectionSourceExpr
    {
        public OrderByExpr() { }

        public OrderByExpr(OrderBySourceExpr source, params (ValueTypeExpr, bool)[] orderBys)
        {
            Source = source;
            OrderBys = orderBys?.ToList() ?? new List<(ValueTypeExpr, bool)>();
        }

        public OrderBySourceExpr Source { get; set; }
        public List<(ValueTypeExpr, bool)> OrderBys { get; set; } = new List<(ValueTypeExpr, bool)>();
        public override bool Equals(object obj) => obj is OrderByExpr other && Equals(Source, other.Source) && OrderBys.SequenceEqual(other.OrderBys);
        public override int GetHashCode() => OrderedHashCodes(typeof(OrderByExpr).GetHashCode(), Source?.GetHashCode() ?? 0, OrderBys.GetHashCode());
        public override string ToString() => $"{Source} ORDER BY {string.Join(", ", OrderBys.Select(ob => $"{ob.Item1} {(ob.Item2 ? "ASC" : "DESC")}"))}";
    }
}
