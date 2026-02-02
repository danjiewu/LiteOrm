using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{


    //from - where - order by - section - group by - having - select

    public abstract class SelectableExpr : Expr { }

    public abstract class GroupableExpr : SelectableExpr { }

    public abstract class OrderableExpr : GroupableExpr { }
    public abstract class SectionableExpr : OrderableExpr { }

    public abstract class WhereableExpr : SectionableExpr { }

    public class WhereExpr : SectionableExpr
    {
        public WhereableExpr From { get; set; }
        public LogicExpr Where { get; set; }

        public override bool Equals(object obj) => obj is WhereExpr other && Equals(From, other.From) && Equals(Where, other.Where);
        public override int GetHashCode() => OrderedHashCodes(typeof(WhereExpr).GetHashCode(), From?.GetHashCode() ?? 0, Where?.GetHashCode() ?? 0);
        public override string ToString() => $"{From} WHERE {Where}";
    }
    public class HavingExpr : SelectableExpr
    {
        public GroupByExpr GroupBy { get; set; }
        public LogicExpr Having { get; set; }

        public override bool Equals(object obj) => obj is HavingExpr other && Equals(GroupBy, other.GroupBy) && Equals(Having, other.Having);
        public override int GetHashCode() => OrderedHashCodes(typeof(HavingExpr).GetHashCode(), GroupBy?.GetHashCode() ?? 0, Having?.GetHashCode() ?? 0);
        public override string ToString() => $"{GroupBy} HAVING {Having}";
    }
    public class GroupByExpr : SelectableExpr
    {
        public GroupableExpr From { get; set; }
        public List<ValueTypeExpr> GroupBys { get; set; } = new List<ValueTypeExpr>();

        public override bool Equals(object obj) => obj is GroupByExpr other && Equals(From, other.From) && GroupBys.SequenceEqual(other.GroupBys);
        public override int GetHashCode() => OrderedHashCodes(typeof(GroupByExpr).GetHashCode(), From?.GetHashCode() ?? 0, GroupBys.GetHashCode());
        public override string ToString() => $"{From} GROUP BY {string.Join(", ", GroupBys)}";
    }

    public class OrderByExpr : SectionableExpr
    {
        public OrderByExpr() { }

        public OrderableExpr From { get; set; }
        public List<(ValueTypeExpr, bool)> OrderBys { get; set; } = new List<(ValueTypeExpr, bool)>();
        public override bool Equals(object obj) => obj is OrderByExpr other && Equals(From, other.From) && OrderBys.SequenceEqual(other.OrderBys);
        public override int GetHashCode() => OrderedHashCodes(typeof(OrderByExpr).GetHashCode(), From?.GetHashCode() ?? 0, OrderBys.GetHashCode());
        public override string ToString() => $"{From} ORDER BY {string.Join(", ", OrderBys.Select(ob => $"{ob.Item1} {(ob.Item2 ? "ASC" : "DESC")}"))}";
    }

    public class SelectExpr : WhereableExpr
    {
        public SelectableExpr From { get; set; }
        public List<ValueTypeExpr> Selects { get; set; } = new List<ValueTypeExpr>();

        public override bool Equals(object obj) => obj is SelectExpr other && Equals(From, other.From) && Selects.SequenceEqual(other.Selects);
        public override int GetHashCode() => OrderedHashCodes(typeof(SelectExpr).GetHashCode(), From?.GetHashCode() ?? 0, Selects.GetHashCode());
        public override string ToString() => $"SELECT {string.Join(", ", Selects)} FROM {From}";
    }

    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class TableExpr : WhereableExpr
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
    public class SectionExpr : GroupableExpr
    {
        public SectionExpr() { }
        public SectionExpr(int skip, int take)
        {
            Skip = skip;
            Take = take;
        }

        public SectionableExpr From { get; set; }

        public int Skip { get; set; }
        public int Take { get; set; }

        public override bool Equals(object obj) => obj is SectionExpr other && Equals(From, other.From) && Skip == other.Skip && Take == other.Take;
        public override int GetHashCode() => OrderedHashCodes(typeof(SectionExpr).GetHashCode(), From?.GetHashCode() ?? 0, Skip, Take);
        public override string ToString() => $"{From} SKIP {Skip} TAKE {Take}";
    }
}
