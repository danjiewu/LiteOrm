using System;
using System.Collections.Generic;
using System.Text;

namespace LiteOrm.Common
{
    public class TakeExpr : Expr
    {
        public int Count { get; set; }
    }

    public class SkipExpr : Expr
    {
        public int Count { get; set; }
    }

    public class OrderByExpr : Expr
    {
        public ValueTypeExpr Expression { get; set; }
        public bool IsAscending { get; set; } = true;
    }

    public class SelectExpr : Expr
    {
        public List<ValueTypeExpr> Expressions { get; set; } = new List<ValueTypeExpr>();
    }
}
