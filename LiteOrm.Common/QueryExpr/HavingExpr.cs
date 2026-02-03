using System;

namespace LiteOrm.Common
{
    public class HavingExpr : SelectSourceExpr
    {
        public HavingExpr() { }
        public HavingExpr(HavingSourceExpr source, LogicExpr having)
        {
            Source = source;
            Having = having;
        }

        public HavingSourceExpr Source { get; set; }
        public LogicExpr Having { get; set; }
        public override bool Equals(object obj) => obj is HavingExpr other && Equals(Source, other.Source) && Equals(Having, other.Having);
        public override int GetHashCode() => OrderedHashCodes(typeof(HavingExpr).GetHashCode(), Source?.GetHashCode() ?? 0, Having?.GetHashCode() ?? 0);
        public override string ToString() => $"{Source} HAVING {Having}";
    }
}
