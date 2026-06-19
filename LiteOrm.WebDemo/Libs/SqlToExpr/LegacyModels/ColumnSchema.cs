using System;

namespace LiteOrm.SqlToExpr;

public sealed class ColumnSchema
{
    public string Name { get; set; } = string.Empty;

    public string PropertyName { get; set; } = string.Empty;

    public Type ClrType { get; set; } = typeof(string);

    public bool IsNullable { get; set; }

    public bool IsPrimaryKey { get; set; }

    public bool IsAutoIncrement { get; set; }

    public int? Length { get; set; }

    public int Ordinal { get; set; }

    public string? DefaultValue { get; set; }

    public bool IsInSelect { get; set; }

    public int SelectOrdinal { get; set; } = -1;
}