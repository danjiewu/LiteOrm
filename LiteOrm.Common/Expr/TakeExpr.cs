using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public abstract class FromExpr : Expr
    {
    }

    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class TableFromExpr : FromExpr
    {
        public TableFromExpr() { }
        public TableFromExpr(SqlTable table) => Table = table;
        public SqlTable Table { get; set; }

        public override bool Equals(object obj) => obj is TableFromExpr other && Equals(Table, other.Table);
        public override int GetHashCode() => OrderedHashCodes(typeof(TableFromExpr).GetHashCode(), Table?.GetHashCode() ?? 0);
        public override string ToString() => Table?.Name ?? string.Empty;
    }

    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class SubQueryFromExpr : FromExpr
    {
        public SubQueryFromExpr() { }
        public SubQueryFromExpr(QueryExpr subQuery, string alias)
        {
            SubQuery = subQuery;
            Alias = alias;
        }
        public QueryExpr SubQuery { get; set; }
        public string Alias { get; set; }

        public override bool Equals(object obj) => obj is SubQueryFromExpr other && Equals(SubQuery, other.SubQuery) && Alias == other.Alias;
        public override int GetHashCode() => OrderedHashCodes(typeof(SubQueryFromExpr).GetHashCode(), SubQuery?.GetHashCode() ?? 0, Alias?.GetHashCode() ?? 0);
        public override string ToString() => $"( {SubQuery} ) AS {Alias}";
    }

    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public class QueryExpr : ValueTypeExpr
    {
        public FromExpr From { get; set; }
        public LogicExpr Where { get; set; }
        public List<OrderByExpr> OrderBys { get; set; } = new List<OrderByExpr>();
        public SectionExpr Section { get; set; }
        public List<ValueTypeExpr> GroupBys { get; set; } = new List<ValueTypeExpr>();
        public LogicExpr Having { get; set; }
        public List<ValueTypeExpr> Selects { get; set; } = new List<ValueTypeExpr>();

        public override string ToString()
        {
            return "SELECT Query";
        }

        public override bool Equals(object obj) => obj is QueryExpr other &&
            Equals(From, other.From) &&
            Equals(Where, other.Where) &&
            Equals(Section, other.Section) &&
            Equals(Having, other.Having) &&
            Selects.SequenceEqual(other.Selects) &&
            OrderBys.SequenceEqual(other.OrderBys) &&
            GroupBys.SequenceEqual(other.GroupBys);

        public override int GetHashCode() => OrderedHashCodes(typeof(QueryExpr).GetHashCode(), From?.GetHashCode() ?? 0, Where?.GetHashCode() ?? 0);
    }

    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class AggregateFunctionExpr : ValueTypeExpr
    {
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
    public class OrderByExpr : Expr
    {
        public OrderByExpr() { }
        public OrderByExpr(ValueTypeExpr expression, bool isAscending = true)
        {
            Expression = expression;
            IsAscending = isAscending;
        }
        public ValueTypeExpr Expression { get; set; }
        public bool IsAscending { get; set; } = true;

        public override bool Equals(object obj) => obj is OrderByExpr other && Equals(Expression, other.Expression) && IsAscending == other.IsAscending;
        public override int GetHashCode() => OrderedHashCodes(typeof(OrderByExpr).GetHashCode(), Expression?.GetHashCode() ?? 0, IsAscending.GetHashCode());
        public override string ToString() => $"{Expression} {(IsAscending ? "ASC" : "DESC")}";
    }

    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public class SectionExpr : Expr
    {
        public SectionExpr() { }
        public SectionExpr(int skip, int take)
        {
            Skip = skip;
            Take = take;
        }
        public int Skip { get; set; }
        public int Take { get; set; }

        public override bool Equals(object obj) => obj is SectionExpr other && Skip == other.Skip && Take == other.Take;
        public override int GetHashCode() => OrderedHashCodes(typeof(SectionExpr).GetHashCode(), Skip, Take);
        public override string ToString() => $"SKIP {Skip} TAKE {Take}";
    }
}
