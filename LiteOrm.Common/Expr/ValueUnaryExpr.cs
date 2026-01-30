using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class ValueUnaryExpr : ValueTypeExpr
    {
        public ValueUnaryExpr() { }
        public ValueUnaryExpr(ValueUnaryOperator oper, ValueTypeExpr operand)
        {
            Operator = oper;
            Operand = operand;
        }

        public ValueUnaryOperator Operator { get; set; }
        public ValueTypeExpr Operand { get; set; }

        public override bool IsValue => true;

        public override string ToString() => Operator == ValueUnaryOperator.Nagive ? $"-{Operand}" : $"~{Operand}";

        public override bool Equals(object obj) => obj is ValueUnaryExpr p && p.Operator == Operator && Equals(p.Operand, Operand);

        public override int GetHashCode() => OrderedHashCodes(GetType().GetHashCode(), (int)Operator, (Operand?.GetHashCode() ?? 0));
    }
}
