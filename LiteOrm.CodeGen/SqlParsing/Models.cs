using System.Collections.Generic;

namespace LiteOrm.CodeGen;

internal enum SqlJoinType
{
    Inner,
    Left,
    Right
}

internal enum ProjectionKind
{
    Column,
    Function,
    Wildcard
}

internal enum FilterLogicalOperator
{
    And,
    Or
}

internal enum FilterComparisonOperator
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Like,
    NotLike,
    In,
    NotIn,
    IsNull,
    IsNotNull
}

internal sealed class ParsedSelectQuery
{
    public List<ParsedProjection> Projections { get; } = new();

    public ParsedTableSource? MainSource { get; set; }

    public List<ParsedJoin> Joins { get; } = new();

    public FilterNode? Where { get; set; }

    public List<ParsedColumnReference> GroupBy { get; } = new();

    public List<ParsedOrderBy> OrderBy { get; } = new();
}

internal sealed class ParsedTableSource
{
    public string TableName { get; set; } = string.Empty;

    public string Alias { get; set; } = string.Empty;
}

internal sealed class ParsedJoin
{
    public SqlJoinType JoinType { get; set; }

    public ParsedTableSource Table { get; set; } = new();

    public List<ParsedJoinCondition> Conditions { get; } = new();
}

internal sealed class ParsedJoinCondition
{
    public ParsedColumnReference Left { get; set; } = new();

    public ParsedColumnReference Right { get; set; } = new();
}

internal sealed class ParsedProjection
{
    public ProjectionKind Kind { get; set; }

    public ParsedColumnReference? Column { get; set; }

    public ParsedFunctionCall? Function { get; set; }

    public string? Alias { get; set; }

    public string? WildcardAlias { get; set; }
}

internal sealed class ParsedFunctionCall
{
    public string Name { get; set; } = string.Empty;

    public bool IsDistinct { get; set; }

    public ParsedColumnReference? Argument { get; set; }

    public bool IsStarArgument { get; set; }
}

internal sealed class ParsedColumnReference
{
    public string? Alias { get; set; }

    public string ColumnName { get; set; } = string.Empty;
}

internal sealed class ParsedOrderBy
{
    public ParsedColumnReference Column { get; set; } = new();

    public bool Ascending { get; set; } = true;
}

internal abstract class FilterNode
{
}

internal sealed class FilterLogicalNode : FilterNode
{
    public FilterLogicalOperator Operator { get; set; }

    public FilterNode Left { get; set; } = null!;

    public FilterNode Right { get; set; } = null!;
}

internal sealed class FilterComparisonNode : FilterNode
{
    public ParsedColumnReference Left { get; set; } = new();

    public FilterComparisonOperator Operator { get; set; }

    public object? RightLiteral { get; set; }

    public ParsedColumnReference? RightColumn { get; set; }

    public List<object?>? RightValues { get; set; }
}
