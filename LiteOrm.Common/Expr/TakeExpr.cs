using System;
using System.Collections.Generic;
using System.Text;

namespace LiteOrm.Common
{

    public abstract class ResultExpr : Expr
    {
        public abstract LogicExpr Where { get; }
    }
    public class TakeExpr : ResultExpr
    {
        public override LogicExpr Where => throw new NotImplementedException();

        public int Count { get; set; }
    }

    public class SkipExpr : ResultExpr
    {
        public int Count { get; set; }
    }

    public class OrderByExpr : ResultExpr
    {
        public ValueTypeExpr Expression { get; set; }
        public bool IsAscending { get; set; } = true;
    }

    public class SelectExpr : ResultExpr
    {
        public List<ValueTypeExpr> Expressions { get; set; } = new List<ValueTypeExpr>();
    }
}
