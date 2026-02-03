using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    //from - where - order by - section - group by - having - select
    public abstract class SelectSourceExpr : Expr { }
    public abstract class HavingSourceExpr : SelectSourceExpr { }
    public abstract class GroupBySourceExpr : SelectSourceExpr { }
    public abstract class SectionSourceExpr : GroupBySourceExpr { }
    public abstract class OrderBySourceExpr : SectionSourceExpr { }
    public abstract class SourceExpr : OrderBySourceExpr { }

    public class WhereExpr : OrderBySourceExpr
    {
        public SourceExpr Source { get; set; }
        public LogicExpr Where { get; set; }

        public override bool Equals(object obj) => obj is WhereExpr other && Equals(Source, other.Source) && Equals(Where, other.Where);
        public override int GetHashCode() => OrderedHashCodes(typeof(WhereExpr).GetHashCode(), Source?.GetHashCode() ?? 0, Where?.GetHashCode() ?? 0);
        public override string ToString() => $"{Source} WHERE {Where}";
    }
    public class GroupByExpr : HavingSourceExpr
    {
        public GroupBySourceExpr Source { get; set; }
        public List<ValueTypeExpr> GroupBys { get; set; } = new List<ValueTypeExpr>();
        public override bool Equals(object obj) => obj is GroupByExpr other && Equals(Source, other.Source) && GroupBys.SequenceEqual(other.GroupBys);
        public override int GetHashCode() => OrderedHashCodes(typeof(GroupByExpr).GetHashCode(), Source?.GetHashCode() ?? 0, GroupBys.GetHashCode());
        public override string ToString() => $"{Source} GROUP BY {string.Join(", ", GroupBys)}";
    }

    public class HavingExpr : SelectSourceExpr
    {
        public HavingSourceExpr Source { get; set; }
        public LogicExpr Having { get; set; }
        public override bool Equals(object obj) => obj is HavingExpr other && Equals(Source, other.Source) && Equals(Having, other.Having);
        public override int GetHashCode() => OrderedHashCodes(typeof(HavingExpr).GetHashCode(), Source?.GetHashCode() ?? 0, Having?.GetHashCode() ?? 0);
        public override string ToString() => $"{Source} HAVING {Having}";
    }

    public class OrderByExpr : SectionSourceExpr
    {
        public OrderByExpr() { }

        public OrderBySourceExpr Source { get; set; }
        public List<(ValueTypeExpr, bool)> OrderBys { get; set; } = new List<(ValueTypeExpr, bool)>();
        public override bool Equals(object obj) => obj is OrderByExpr other && Equals(Source, other.Source) && OrderBys.SequenceEqual(other.OrderBys);
        public override int GetHashCode() => OrderedHashCodes(typeof(OrderByExpr).GetHashCode(), Source?.GetHashCode() ?? 0, OrderBys.GetHashCode());
        public override string ToString() => $"{Source} ORDER BY {string.Join(", ", OrderBys.Select(ob => $"{ob.Item1} {(ob.Item2 ? "ASC" : "DESC")}"))}";
    }

    public class SelectExpr : SourceExpr
    {
        public SelectSourceExpr Source { get; set; }
        public List<ValueTypeExpr> Selects { get; set; } = new List<ValueTypeExpr>();
        public override bool Equals(object obj) => obj is SelectExpr other && Equals(Source, other.Source) && Selects.SequenceEqual(other.Selects);
        public override int GetHashCode() => OrderedHashCodes(typeof(SelectExpr).GetHashCode(), Source?.GetHashCode() ?? 0, Selects.GetHashCode());
        public override string ToString() => $"SELECT {string.Join(", ", Selects)} FROM {Source}";
    }

    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class TableExpr : SourceExpr
    {
        public TableExpr() { }
        public TableExpr(SqlTable table) => Table = table;
        public SqlTable Table { get; set; }

        public override bool Equals(object obj) => obj is TableExpr other && Equals(Table, other.Table);
        public override int GetHashCode() => OrderedHashCodes(typeof(TableExpr).GetHashCode(), Table?.GetHashCode() ?? 0);
        public override string ToString() => Table?.Name ?? string.Empty;
    }

    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class AggregateFunctionExpr : ValueTypeExpr
    {
        public readonly static AggregateFunctionExpr Count = new AggregateFunctionExpr("Count", Expr.Const(1));
        public AggregateFunctionExpr() { }
        public AggregateFunctionExpr(string functionName, ValueTypeExpr expression, bool isDistinct = false)
        {
            FunctionName = functionName;
            Expression = expression;
            IsDistinct = isDistinct;
        }
        public ValueTypeExpr Expression { get; set; }
        public string FunctionName { get; set; }
        public bool IsDistinct { get; set; }

        public override bool Equals(object obj) => obj is AggregateFunctionExpr other && FunctionName == other.FunctionName && Equals(Expression, other.Expression) && IsDistinct == other.IsDistinct;
        public override int GetHashCode() => OrderedHashCodes(typeof(AggregateFunctionExpr).GetHashCode(), FunctionName?.GetHashCode() ?? 0, Expression?.GetHashCode() ?? 0, IsDistinct.GetHashCode());
        public override string ToString() => $"{FunctionName}({(IsDistinct ? "DISTINCT " : "")}{Expression})";
    }


    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public class SectionExpr : GroupBySourceExpr
    {
        public SectionExpr() { }
        public SectionExpr(int skip, int take)
        {
            Skip = skip;
            Take = take;
        }

        public SectionSourceExpr Source { get; set; }

        public int Skip { get; set; }
        public int Take { get; set; }

        public override bool Equals(object obj) => obj is SectionExpr other && Equals(Source, other.Source) && Skip == other.Skip && Take == other.Take;
        public override int GetHashCode() => OrderedHashCodes(typeof(SectionExpr).GetHashCode(), Source?.GetHashCode() ?? 0, Skip, Take);
        public override string ToString() => $"{Source} SKIP {Skip} TAKE {Take}";
    }
}
