using System;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    [JsonConverter(typeof(SqlSegmentJsonConverterFactory))]
    public abstract class SqlSegment : Expr
    {
        public SqlSegment Source;
        public abstract SqlSegmentType SegmentType { get; }
    }

    public enum SqlSegmentType
    {
        Table,
        Select,
        Update,
        Delete,
        Where,
        GroupBy,
        OrderBy,
        Having,
        Section
    }

    // Anchor interfaces for precise control of fluent API
    public interface ISqlAnchor { }
    public interface ISectionAnchor : ISqlAnchor { }
    public interface IOrderByAnchor : ISectionAnchor { }
    public interface ISelectAnchor : IOrderByAnchor { }
    public interface IHavingAnchor : ISelectAnchor { }
    public interface IGroupByAnchor : IHavingAnchor { }
    public interface ISourceAnchor : IGroupByAnchor { }
}
