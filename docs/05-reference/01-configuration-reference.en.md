# Configuration Reference

This page is a complete reference for LiteOrm configuration fields, defaults, and usage recommendations.

## Complete Configuration Example

```json
{
  "LiteOrm": {
    "Default": "DefaultConnection",
    "DataSources": [
      {
        "Name": "DefaultConnection",
        "ConnectionString": "Server=localhost;Database=TestDb;User Id=root;Password=123456;",
        "Provider": "MySqlConnector.MySqlConnection, MySqlConnector",
        "SqlBuilder": null,
        "KeepAliveDuration": "00:10:00",
        "PoolSize": 16,
        "MaxPoolSize": 100,
        "ParamCountLimit": 2000,
        "SyncTable": false,
        "ReadOnlyConfigs": [
          {
            "ConnectionString": "Server=localhost;Database=TestDb_ReadOnly;User Id=root;Password=123456;",
            "KeepAliveDuration": "00:15:00",
            "PoolSize": 32,
            "MaxPoolSize": 200,
            "ParamCountLimit": 2000
          }
        ]
      }
    ]
  }
}
```

## Top-level Settings

| Field | Type | Default | Notes |
|------|------|---------|-------|
| `Default` | `string` | Required | default data source name, matches `DataSources[].Name` |
| `DataSources` | `array` | Required | data source configuration list, at least one required |

## `DataSources[]`

| Field | Type | Default | Notes |
|------|------|---------|-------|
| `Name` | `string` | Required | data source name, used by `[Table(DataSource = "...")]` |
| `ConnectionString` | `string` | Required | database connection string |
| `Provider` | `string` | Required | fully qualified connection type, format: `TypeName, AssemblyName` |
| `SqlBuilder` | `string` | `null` | custom SQL builder type, uses default if not set |
| `KeepAliveDuration` | `TimeSpan` | `00:10:00` | connection keep-alive duration, format: `HH:mm:ss` |
| `PoolSize` | `int` | `16` | cached connection count, controls pool pre-warming |
| `MaxPoolSize` | `int` | `100` | maximum concurrent connections |
| `ParamCountLimit` | `int` | `2000` | parameter-count limit per SQL statement |
| `SyncTable` | `bool` | `false` | whether to auto-sync table creation, disable in production |
| `ReadOnlyConfigs` | `array` | `[]` | read-only replica configuration list |

## `ReadOnlyConfigs[]`

All fields are optional. Missing fields inherit from the primary data-source configuration.

| Field | Type | Default | Notes |
|------|------|---------|-------|
| `ConnectionString` | `string` | Required | read-replica connection string |
| `KeepAliveDuration` | `TimeSpan` | Inherit | connection keep-alive duration, format: `HH:mm:ss` |
| `PoolSize` | `int` | Inherit | read-replica connection pool size |
| `MaxPoolSize` | `int` | Inherit | read-replica maximum concurrent connections |
| `ParamCountLimit` | `int` | Inherit | read-replica parameter-count limit per SQL statement |

## Common Provider Values

| Database | Provider Example |
|----------|------------------|
| MySQL | `MySqlConnector.MySqlConnection, MySqlConnector` |
| SQL Server | `System.Data.SqlClient.SqlConnection, System.Data.SqlClient` |
| PostgreSQL | `Npgsql.NpgsqlConnection, Npgsql` |
| Oracle | `Oracle.ManagedDataAccess.Client.OracleConnection, Oracle.ManagedDataAccess` |
| SQLite | `Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite` |

## Recommended Values by Scenario

| Scenario | PoolSize | MaxPoolSize |
|----------|----------|-------------|
| General business systems | `16` | `100` |
| Low-concurrency background jobs | `5` | `20` |
| High-concurrency writes / batch imports | `32` | `200` |
| Read-heavy workloads | `32` | `200` |

## Related Links

- [Back to English docs hub](../README.md)
- [Configuration and Registration](../01-getting-started/03-configuration-and-registration.en.md)
- [Performance](../03-advanced-topics/03-performance.en.md)
- [API Index](./02-api-index.en.md)
