using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class NotExpr : LogicExpr
    {
        public NotExpr() { }
        public NotExpr(LogicExpr operand)
        {
            Operand = operand;
        }
        public LogicExpr Operand { get; set; }

        public override string ToString() => $"NOT {Operand}";

        public override bool Equals(object obj) => obj is NotExpr p && Equals(p.Operand, Operand);

        public override int GetHashCode() => OrderedHashCodes(GetType().GetHashCode(), (Operand?.GetHashCode() ?? 0));
    }
}
