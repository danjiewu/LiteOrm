using System;
using System.Collections.Generic;
using System.Linq;

namespace LiteOrm.CodeGen;

public sealed class TableSchema
{
    public string Name { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public List<ColumnSchema> Columns { get; } = new();

    public List<ForeignKeySchema> ForeignKeys { get; } = new();

    public ColumnSchema? GetColumn(string columnName)
    {
        return Columns.FirstOrDefault(c => string.Equals(c.Name, columnName, StringComparison.OrdinalIgnoreCase));
    }
}
