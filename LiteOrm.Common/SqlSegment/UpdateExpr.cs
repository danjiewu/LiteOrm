using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    [JsonConverter(typeof(SqlSegmentJsonConverterFactory))]
    public class UpdateExpr : SqlSegment
    {
        public UpdateExpr() { }
        public UpdateExpr(SqlSegment source, LogicExpr where = null)
        {
            Source = source;
            Where = where;
        }

        public override SqlSegmentType SegmentType => SqlSegmentType.Update;

        public List<(string, ValueTypeExpr)> Sets { get; set; } = new List<(string, ValueTypeExpr)>();
        public LogicExpr Where { get; set; }

        public override bool Equals(object obj) => obj is UpdateExpr other 
            && Equals(Source, other.Source) 
            && Sets.SequenceEqual(other.Sets)
            && Equals(Where, other.Where);
            
        public override int GetHashCode() => OrderedHashCodes(typeof(UpdateExpr).GetHashCode(), Source?.GetHashCode() ?? 0, SequenceHash(Sets), Where?.GetHashCode() ?? 0);
        
        public override string ToString()
        {
            string setStr = string.Join(", ", Sets.Select(s => $"[{s.Item1}] = {s.Item2}"));
            return $"UPDATE {Source} SET {setStr}{(Where != null ? $" WHERE {Where}" : "")}";
        }
    }
}
