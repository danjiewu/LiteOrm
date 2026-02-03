using System;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
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
}
