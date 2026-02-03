using System;

namespace LiteOrm.Common
{
    public abstract class SelectSourceExpr : Expr { }
    public abstract class HavingSourceExpr : SelectSourceExpr { }
    public abstract class GroupBySourceExpr : SelectSourceExpr { }
    public abstract class SectionSourceExpr : GroupBySourceExpr { }
    public abstract class OrderBySourceExpr : SectionSourceExpr { }
    public abstract class SourceExpr : OrderBySourceExpr { }
}
