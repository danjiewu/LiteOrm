using System;

namespace LiteOrm.Common
{
    public class CommonTableExpr : SourceExpr
    {
        public CommonTableExpr()
        {
        }

        public CommonTableExpr(SelectExpr source)
        {
            Source = source;
        }

        public new SelectExpr Source { get => (SelectExpr)base.Source; set => base.Source = value; }

        public override string Alias { get => Source?.Alias; set => Source?.Alias = value; }

        public override ExprType ExprType => ExprType.CommonTable;

        public override Expr Clone()
        {
            return new CommonTableExpr((SelectExpr)Source?.Clone());
        }

        public override bool Equals(object obj)
        {
            return obj is CommonTableExpr other && Equals(Source, other.Source);
        }

        public override int GetHashCode()
        {
            return OrderedHashCodes(typeof(CommonTableExpr).GetHashCode(), Source?.GetHashCode() ?? 0);
        }

        public override string ToString()
        {
            return $"{Alias} AS ({Source})";
        }
    }
}
