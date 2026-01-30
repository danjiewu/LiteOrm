using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class UnaryExpr : ValueTypeExpr
    {
        public UnaryExpr() { }
        public UnaryExpr(UnaryOperator oper, ValueTypeExpr operand)
        {
            Operator = oper;
            Operand = operand;
        }

        public UnaryOperator Operator { get; set; }
        public ValueTypeExpr Operand { get; set; }

        public override bool IsValue => true;

        public override string ToString() => Operator == UnaryOperator.Nagive ? $"-{Operand}" : $"~{Operand}";

        public override bool Equals(object obj) => obj is UnaryExpr p && p.Operator == Operator && Equals(p.Operand, Operand);

        public override int GetHashCode() => OrderedHashCodes(GetType().GetHashCode(), (int)Operator, (Operand?.GetHashCode() ?? 0));
    }
}
