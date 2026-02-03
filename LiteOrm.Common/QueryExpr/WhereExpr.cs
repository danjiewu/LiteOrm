using System;

namespace LiteOrm.Common
{
    public class WhereExpr : OrderBySourceExpr
    {
        public WhereExpr() { }
        public WhereExpr(SourceExpr source, LogicExpr where)
        {
            Source = source;
            Where = where;
        }

        public SourceExpr Source { get; set; }
        public LogicExpr Where { get; set; }

        public override bool Equals(object obj) => obj is WhereExpr other && Equals(Source, other.Source) && Equals(Where, other.Where);
        public override int GetHashCode() => OrderedHashCodes(typeof(WhereExpr).GetHashCode(), Source?.GetHashCode() ?? 0, Where?.GetHashCode() ?? 0);
        public override string ToString() => $"{Source} WHERE {Where}";
    }
}
