using System;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
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
}
