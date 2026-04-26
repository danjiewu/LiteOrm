using System;
using System.Collections.Generic;
using System.Linq;

namespace LiteOrm.CodeGen;

public sealed class DatabaseSchema
{
    public string DataSource { get; set; } = string.Empty;

    public Type? ProviderType { get; set; }

    public List<TableSchema> Tables { get; } = new();

    public TableSchema? GetTable(string tableName)
    {
        return Tables.FirstOrDefault(t => string.Equals(t.Name, tableName, StringComparison.OrdinalIgnoreCase));
    }
}
