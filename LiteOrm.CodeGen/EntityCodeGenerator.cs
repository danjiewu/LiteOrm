using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LiteOrm.CodeGen;

public sealed class EntityCodeGenerator
{
    public EntityGenerationResult Generate(DatabaseSchema schema, EntityGenerationOptions? options = null)
    {
        if (schema == null) throw new ArgumentNullException(nameof(schema));
        options ??= new EntityGenerationOptions();

        var result = new EntityGenerationResult();
        var selectedTables = options.Tables == null || options.Tables.Count == 0
            ? schema.Tables
            : schema.Tables.Where(t => options.Tables.Contains(t.Name, StringComparer.OrdinalIgnoreCase)).ToList();

        foreach (var table in selectedTables)
        {
            result.Files.Add(new GeneratedCodeFile
            {
                FileName = table.ClassName + ".cs",
                Content = GenerateTableCode(schema, table, options)
            });
        }

        return result;
    }

    private static string GenerateTableCode(DatabaseSchema schema, TableSchema table, EntityGenerationOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using LiteOrm.Common;");
        sb.AppendLine();
        sb.AppendLine($"namespace {options.Namespace};");
        sb.AppendLine();
        sb.AppendLine($"[Table(\"{table.Name}\")]");
        sb.AppendLine($"public class {table.ClassName}{(options.IncludeObjectBase ? " : ObjectBase" : string.Empty)}");
        sb.AppendLine("{");

        foreach (var column in table.Columns.OrderBy(c => c.Ordinal))
        {
            foreach (var attribute in BuildColumnAttributes(schema, table, column, options))
                sb.AppendLine("    " + attribute);

            sb.AppendLine($"    public {CodeGenNaming.ToCSharpTypeName(column.ClrType, column.IsNullable)} {column.PropertyName} {{ get; set; }}");
            sb.AppendLine();
        }

        if (table.Columns.Count > 0)
            sb.Length -= Environment.NewLine.Length * 2;

        sb.AppendLine();
        sb.AppendLine("}");
        return sb.ToString().TrimEnd();
    }

    private static IEnumerable<string> BuildColumnAttributes(DatabaseSchema schema, TableSchema table, ColumnSchema column, EntityGenerationOptions options)
    {
        var arguments = new List<string> { $"\"{column.Name}\"" };
        if (column.IsPrimaryKey)
            arguments.Add("IsPrimaryKey = true");
        if (column.IsAutoIncrement)
            arguments.Add("IsIdentity = true");
        if (column.Length.HasValue)
            arguments.Add($"Length = {column.Length.Value}");
        if (!column.IsNullable && (!column.ClrType.IsValueType || Nullable.GetUnderlyingType(column.ClrType) != null))
            arguments.Add("AllowNull = false");

        yield return $"[Column({string.Join(", ", arguments)})]";

        if (!options.EmitForeignTypes)
            yield break;

        foreach (var foreignKey in table.ForeignKeys.Where(fk => string.Equals(fk.SourceColumn, column.Name, StringComparison.OrdinalIgnoreCase)))
        {
            var targetTable = schema.GetTable(foreignKey.TargetTable);
            if (targetTable == null)
                continue;

            yield return $"[ForeignType(typeof({targetTable.ClassName}))]";
        }
    }
}
