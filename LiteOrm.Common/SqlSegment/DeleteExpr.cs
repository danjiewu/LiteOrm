using System;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    [JsonConverter(typeof(SqlSegmentJsonConverterFactory))]
    public class DeleteExpr : SqlSegment
    {
        public DeleteExpr() { }
        public DeleteExpr(TableExpr source, LogicExpr where = null)
        {
            Source = source;
            Where = where;
        }

        public override SqlSegmentType SegmentType => SqlSegmentType.Delete;

        public LogicExpr Where { get; set; }

        public override bool Equals(object obj) => obj is DeleteExpr other && Equals(Source, other.Source) && Equals(Where, other.Where);
        public override int GetHashCode() => OrderedHashCodes(typeof(DeleteExpr).GetHashCode(), Source?.GetHashCode() ?? 0, Where?.GetHashCode() ?? 0);
        public override string ToString() => $"DELETE FROM {Source}{(Where != null ? $" WHERE {Where}" : "")}";
    }
}
