using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LiteOrm.SqlToExpr;

internal sealed class HelperModelCodeGenerator
{
    public string GenerateEntityCode(DatabaseSchema schema, string modelNamespace, string mainTableName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using LiteOrm.Common;");
        sb.AppendLine();
        sb.AppendLine($"namespace {modelNamespace}");
        sb.AppendLine("{");

        var blocks = schema.Tables
            .Select(table => GenerateEntityCode(schema, table, mainTableName))
            .ToList();

        for (int i = 0; i < blocks.Count; i++)
        {
            foreach (var line in blocks[i].Split([Environment.NewLine], StringSplitOptions.None))
            {
                sb.AppendLine("    " + line);
            }

            if (i < blocks.Count - 1)
                sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString().TrimEnd();
    }

    private static string GenerateEntityCode(DatabaseSchema schema, TableSchema table, string mainTableName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[Table(\"{table.Name}\")]");
        sb.AppendLine($"public class {table.ClassName} : ObjectBase");
        sb.AppendLine("{");

        var orderedColumns = table.Columns
            .OrderBy(c => c.IsInSelect ? c.SelectOrdinal : int.MaxValue)
            .ThenBy(c => c.Ordinal)
            .ToList();

        for (int i = 0; i < orderedColumns.Count; i++)
        {
            var column = orderedColumns[i];
            var attributeArguments = new List<string> { $"\"{column.Name}\"" };
            if (column.IsPrimaryKey)
                attributeArguments.Add("IsPrimaryKey = true");

            if (!column.IsInSelect)
                attributeArguments.Add("ColumnMode = LiteOrm.Common.ColumnMode.Write");
            else if (column.IsPrimaryKey)
                attributeArguments.Add("ColumnMode = LiteOrm.Common.ColumnMode.Full");

            sb.AppendLine($"    [Column({string.Join(", ", attributeArguments)})]");
            foreach (var foreignKey in table.ForeignKeys.Where(fk => string.Equals(fk.SourceColumn, column.Name, StringComparison.OrdinalIgnoreCase)))
            {
                var targetTable = schema.GetTable(foreignKey.TargetTable);
                if (targetTable != null)
                    sb.AppendLine($"    [ForeignType(typeof({targetTable.ClassName}))]");
            }

            if (string.Equals(table.Name, mainTableName, StringComparison.OrdinalIgnoreCase)
                && column.IsInSelect
                && column.SelectOrdinal >= 0)
                sb.AppendLine($"    [PropertyOrder({column.SelectOrdinal})]");

            sb.AppendLine($"    public {CodeGenNaming.ToCSharpTypeName(column.ClrType, column.IsNullable)} {column.PropertyName} {{ get; set; }}");
            sb.AppendLine();
        }

        if (orderedColumns.Count > 0)
        {
            sb.Length -= Environment.NewLine.Length * 2;
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString().TrimEnd();
    }
}