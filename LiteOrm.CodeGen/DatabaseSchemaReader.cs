using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace LiteOrm.CodeGen;

public sealed class DatabaseSchemaReader
{
    private readonly DAOContextPoolFactory _poolFactory;
    private readonly SqlBuilderFactory _sqlBuilderFactory;

    public DatabaseSchemaReader(DAOContextPoolFactory poolFactory, SqlBuilderFactory sqlBuilderFactory)
    {
        _poolFactory = poolFactory ?? throw new ArgumentNullException(nameof(poolFactory));
        _sqlBuilderFactory = sqlBuilderFactory ?? throw new ArgumentNullException(nameof(sqlBuilderFactory));
    }

    public DatabaseSchema ReadSchema(string? dataSource = null, IEnumerable<string>? tables = null)
    {
        var pool = string.IsNullOrWhiteSpace(dataSource) ? _poolFactory.GetPool() : _poolFactory.GetPool(dataSource);
        var sqlBuilder = _sqlBuilderFactory.GetSqlBuilder(pool.ProviderType, dataSource);
        var context = pool.PeekContext();
        try
        {
            var schema = new DatabaseSchema
            {
                DataSource = dataSource ?? string.Empty,
                ProviderType = pool.ProviderType
            };

            var wanted = tables?.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var tableName in GetTableNames(context.DbConnection))
            {
                if (wanted != null && !wanted.Contains(tableName))
                    continue;

                var table = ReadTableSchema(context.DbConnection, sqlBuilder, tableName);
                if (table != null)
                    schema.Tables.Add(table);
            }

            EnrichForeignKeys(context.DbConnection, schema);
            return schema;
        }
        finally
        {
            pool.ReturnContext(context);
        }
    }

    private static IEnumerable<string> GetTableNames(DbConnection connection)
    {
        var providerName = connection.GetType().FullName ?? string.Empty;
        if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var tableName in GetSqliteTableNames(connection))
                yield return tableName;
            yield break;
        }

        var rows = connection.GetSchema("Tables").Rows.Cast<DataRow>();
        foreach (var row in rows)
        {
            var tableType = Convert.ToString(row.Table.Columns.Contains("TABLE_TYPE") ? row["TABLE_TYPE"] : null);
            var tableName = Convert.ToString(row.Table.Columns.Contains("TABLE_NAME") ? row["TABLE_NAME"] : null);
            if (string.IsNullOrWhiteSpace(tableName))
                continue;
            if (!string.IsNullOrWhiteSpace(tableType) &&
                !tableType.Contains("TABLE", StringComparison.OrdinalIgnoreCase) &&
                !tableType.Equals("BASE TABLE", StringComparison.OrdinalIgnoreCase))
                continue;
            if (tableName.StartsWith("sqlite_", StringComparison.OrdinalIgnoreCase))
                continue;

            yield return tableName;
        }
    }

    private static IEnumerable<string> GetSqliteTableNames(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var tableName = reader.GetString(0);
            if (!string.IsNullOrWhiteSpace(tableName))
                yield return tableName;
        }
    }

    private static TableSchema? ReadTableSchema(DbConnection connection, ISqlBuilder sqlBuilder, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {sqlBuilder.ToSqlName(tableName)} WHERE 1=0";
        using var reader = command.ExecuteReader(CommandBehavior.SchemaOnly);
        var columnSchema = reader.GetColumnSchema();
        if (columnSchema == null || columnSchema.Count == 0)
            return null;

        var table = new TableSchema
        {
            Name = tableName,
            ClassName = CodeGenNaming.ToClassName(tableName)
        };

        int ordinal = 0;
        foreach (var column in columnSchema)
        {
            var columnName = column.ColumnName ?? column.BaseColumnName;
            if (string.IsNullOrWhiteSpace(columnName))
                continue;

            table.Columns.Add(new ColumnSchema
            {
                Name = columnName,
                PropertyName = CodeGenNaming.ToPropertyName(columnName),
                ClrType = NormalizeClrType(column.DataType),
                IsNullable = column.AllowDBNull ?? true,
                IsPrimaryKey = column.IsKey ?? false,
                IsAutoIncrement = column.IsAutoIncrement ?? false,
                Length = ToNullableInt(column.ColumnSize),
                Ordinal = ordinal++,
                DefaultValue = null
            });
        }

        return table;
    }

    private static void EnrichForeignKeys(DbConnection connection, DatabaseSchema schema)
    {
        var providerName = connection.GetType().FullName ?? string.Empty;
        if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var table in schema.Tables)
                LoadSqliteForeignKeys(connection, table);
        }
    }

    private static void LoadSqliteForeignKeys(DbConnection connection, TableSchema table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA foreign_key_list({QuoteSqliteIdentifier(table.Name)})";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            table.ForeignKeys.Add(new ForeignKeySchema
            {
                Name = reader["id"]?.ToString(),
                SourceColumn = reader["from"]?.ToString() ?? string.Empty,
                TargetTable = reader["table"]?.ToString() ?? string.Empty,
                TargetColumn = reader["to"]?.ToString() ?? string.Empty
            });
        }
    }

    private static string QuoteSqliteIdentifier(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"") + "\"";
    }

    private static int? ToNullableInt(int? value)
    {
        return value > 0 ? value : null;
    }

    private static Type NormalizeClrType(Type? type)
    {
        if (type == null)
            return typeof(string);

        if (type == typeof(DBNull))
            return typeof(string);

        return type;
    }
}
