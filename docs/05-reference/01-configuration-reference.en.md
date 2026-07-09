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
| `SyncTable` | `bool` | `false` | whether to auto-sync table creation, disable in production. Pool-level default; can be overridden per entity type via the `[Table(SyncTable = ...)]` attribute or the `DatabaseSync.OnTableSyncing` event. |
| `ReadOnlyConfigs` | `array` | `[]` | read-only replica configuration list |

### Entity-Level Sync Override (`[Table(SyncTable = ...)]`)

In addition to the pool-level switch, you can declare an **entity-level sync mode** on the entity class via the `SyncTable` property of the `[Table]` attribute. The `SyncTableMode` enum values are:

| Value | Description |
|-------|-------------|
| `Default` | Default value; follows the data-source-level `SyncTable` config, no override. |
| `Never` | This entity never auto-creates its table, even when the data source has `SyncTable` enabled. |
| `Always` | This entity always auto-creates its table, even when the data source has `SyncTable` disabled. |

```csharp
// Always auto-create this table, ignoring SyncTable=false on the data source
[Table("Logs", SyncTable = SyncTableMode.Always)]
public class Log { ... }

// Never auto-create this table, even when the data source has SyncTable enabled
[Table("Legacy", SyncTable = SyncTableMode.Never)]
public class Legacy { ... }
```

### Dynamic Sync Decision (`OnTableSyncing` Event)

The `SyncTable` decision priority, from highest to lowest, is: `OnTableSyncing` event subscribers > `[Table(SyncTable = ...)]` entity-level config (`Never` / `Always`) > pool-level `SyncTable`. If you need more dynamic control (e.g. based on runtime conditions), subscribe to the `OnTableSyncing` event on `DAOContextPool.DatabaseSync`:

```csharp
var pool = poolFactory.GetPool("SQLite");

// Scenario 1: pool-wide sync enabled, but only the User table is synced
pool.SyncTable = true;
pool.DatabaseSync.OnTableSyncing += (sender, e) =>
{
    e.ShouldSync = e.ObjectType == typeof(User);
};

// Scenario 2: pool-wide sync disabled, but green-light the Log table
pool.SyncTable = false;
pool.DatabaseSync.OnTableSyncing += (sender, e) =>
{
    if (e.ObjectType == typeof(Log)) e.ShouldSync = true;
};
```

The event args `TableSyncingEventArgs` carries:

| Property | Description |
|-----------|-------------|
| `ObjectType` | The entity type to sync. |
| `TableName` | The resolved table name (with `tableArgs` applied, useful for sharded-table decisions). |
| `ShouldSync` | Whether to sync; defaults to the entity-level `[Table(SyncTable = ...)]` (`Never`/`Always` overrides the pool config; `Default` falls back to the pool-level `SyncTable`), can be overridden by subscribers. |

> The decision logic is encapsulated in `DatabaseSync.ShouldSyncTable`, invoked by `EnsureTable` / `EnsureTableAsync` before executing DDL. With no subscribers, it falls back to the entity-level `[Table(SyncTable = ...)]` (and `Default` further falls back to the pool-level `SyncTable`).

## `ReadOnlyConfigs[]`

Provide at least `ConnectionString` for each read-only replica. Any omitted pool-related fields inherit from the primary data-source configuration.

| Field | Type | Default | Notes |
|------|------|---------|-------|
| `ConnectionString` | `string` | Required | read-replica connection string; no read-only pool is created when it is empty |
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
