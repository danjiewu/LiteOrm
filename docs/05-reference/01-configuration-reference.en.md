# Configuration Reference

This page is a complete reference for LiteOrm configuration fields, defaults, registration patterns, and usage recommendations. When using `LiteOrm.DependencyInjection`, configuration is declared in `appsettings.json` and `RegisterLiteOrm()` automatically performs DI binding, DAO registration, and dialect resolution at startup; with the base library only, the same configuration can be read via `LoadConfiguration` or `AddLiteOrm()`.

> **Beginner tip**: If this is your first time configuring, start with the simplest setup—a single data source using SQLite. For the first setup only the three required fields `Name`, `ConnectionString`, and `Provider` are needed; use defaults for the rest. Once the basic flow works, gradually add multi-data-source, read/write splitting, and other advanced configurations.

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
        "ParamCountLimit": 1000,
        "SyncTable": false,
        "ReadOnlyConfigs": [
          {
            "ConnectionString": "Server=localhost;Database=TestDb_ReadOnly;User Id=root;Password=123456;",
            "KeepAliveDuration": "00:15:00",
            "PoolSize": 32,
            "MaxPoolSize": 200,
            "ParamCountLimit": 1000
          }
        ]
      }
    ]
  }
}
```

## Minimal Configuration Examples by Database

> These are the most minimal configurations, containing only required fields. Copy and replace the connection string with your own.

**SQL Server:**
```json
{
  "LiteOrm": {
    "Default": "main",
    "DataSources": [
      {
        "Name": "main",
        "ConnectionString": "Server=localhost;Database=MyDb;Trusted_Connection=True;TrustServerCertificate=True;",
        "Provider": "Microsoft.Data.SqlClient.SqlConnection, Microsoft.Data.SqlClient"
      }
    ]
  }
}
```

**MySQL:**
```json
{
  "LiteOrm": {
    "Default": "main",
    "DataSources": [
      {
        "Name": "main",
        "ConnectionString": "Server=localhost;Database=MyDb;User Id=root;Password=123456;",
        "Provider": "MySqlConnector.MySqlConnection, MySqlConnector"
      }
    ]
  }
}
```

**PostgreSQL:**
```json
{
  "LiteOrm": {
    "Default": "main",
    "DataSources": [
      {
        "Name": "main",
        "ConnectionString": "Host=localhost;Database=MyDb;Username=postgres;Password=123456;",
        "Provider": "Npgsql.NpgsqlConnection, Npgsql"
      }
    ]
  }
}
```

**SQLite (recommended for beginners):**
```json
{
  "LiteOrm": {
    "Default": "main",
    "DataSources": [
      {
        "Name": "main",
        "ConnectionString": "Data Source=myapp.db",
        "Provider": "Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite"
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
| `ParamCountLimit` | `int` | `1000` | parameter-count limit per SQL statement |
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

For the `Provider` value of each database (including domestic/compatible databases), see [Database Compatibility Notes](./07-database-compatibility.en.md).

## Recommended Values by Scenario

| Scenario | PoolSize | MaxPoolSize |
|----------|----------|-------------|
| General business systems | `16` | `100` |
| Low-concurrency background jobs | `5` | `20` |
| High-concurrency writes / batch imports | `32` | `200` |
| Read-heavy workloads | `32` | `200` |

## Registration Patterns (`LiteOrm.DependencyInjection`)

> `RegisterLiteOrm()` is defined in the `LiteOrm.DependencyInjection` package. Install it with `dotnet add package LiteOrm.DependencyInjection` and add `using LiteOrm.DependencyInjection;` before use. **Important**: `RegisterLiteOrm()` must be called on `builder.Host` (not `builder.Services`), because it replaces the underlying DI container with Autofac.

### Console or Worker Application

```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()
    .Build();
```

### ASP.NET Core Application

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();
```

### Registration with Options

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterScope = true;
    options.Assemblies = new[] { typeof(MyService).Assembly };
    options.RegisterSqlBuilder("main", new MySqlBuilder());
});
```

### Complete Program.cs Example

> Here is a complete ASP.NET Core `Program.cs` showing the typical placement of LiteOrm registration:

```csharp
using LiteOrm.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add controller services
builder.Services.AddControllers();

// Register LiteOrm (must be called on builder.Host)
builder.Host.RegisterLiteOrm();

var app = builder.Build();

app.MapControllers();
app.Run();
```

> If you don't need Autofac / AOP, you can use the base library's built-in `AddLiteOrm()` (plain MS DI, called on `builder.Services`). See [First Complete Example (Base Library Only)](../01-getting-started/03-first-example.en.md).

## Multi-Data-Source and Read/Write Guidance

- Use `[Table(DataSource = "...")]` on an entity to bind it to a data source.
- For read-heavy, write-light scenarios, use `ReadOnlyConfigs` to configure read replicas:
  - By default, query/view APIs prefer read-only connections.
  - Within the same `Session`, the first selected read-only replica is cached and reused (avoiding re-polling on every query).
  - In transactions, reads are forced back to the primary connection for consistency.
  - When no read replica is configured, reads fall back to the primary connection.
- When database dialect differences are involved, register a `SqlBuilder` explicitly.

## Common Questions

### What should `Provider` contain?

Use the full connection type name, for example `Microsoft.Data.SqlClient.SqlConnection, Microsoft.Data.SqlClient`.

### When do I need a custom `SqlBuilder`?

When the database version is older, or when paging syntax or function behavior differs from the default implementation, you need a custom `SqlBuilder`.

### Common Beginner Configuration Mistakes

> Here are the most common issues beginners encounter during configuration:

**1. Calling `RegisterLiteOrm()` on `builder.Services`**

Wrong: `builder.Services.RegisterLiteOrm();` ❌

Correct: `builder.Host.RegisterLiteOrm();` ✅

Reason: LiteOrm needs to replace the host-level DI container with Autofac, so it must be called on `IHostBuilder`.

**2. Incorrect `Provider` format**

Wrong: `"Provider": "SqlConnection"` ❌ (missing namespace and assembly name)

Correct: `"Provider": "Microsoft.Data.SqlClient.SqlConnection, Microsoft.Data.SqlClient"` ✅

The format must be `FullTypeName, AssemblyName` (note the comma and space).

**3. Unescaped special characters in connection strings**

If your connection string contains backslashes (e.g., Windows paths), use double backslashes `\\` or forward slashes `/` in JSON:

```json
"ConnectionString": "Data Source=C:\\data\\myapp.db"
```

**4. Forgetting to install the database driver package**

Only the `LiteOrm` package is installed, but the corresponding database NuGet driver (e.g., `Microsoft.Data.Sqlite`, `MySqlConnector`) is missing. This causes a `TypeLoadException` at runtime.

**5. `Default` points to a non-existent data source name**

The `Default` value must exactly match one of the `DataSources[].Name` values, otherwise the framework cannot determine which data source to use by default.

### How to verify your configuration is correct?

After starting the application, check the console output. If you see a log message like `LiteOrm initialized successfully`, the configuration is correct. If an exception occurs, check:

1. Whether the connection string can actually connect to the database (test with a database management tool first).
2. Whether the `Provider` type name matches the installed NuGet package.
3. Whether the database service is running.

## Related Links

- [Back to docs hub](../README.md)
- [First End-to-End Example (DI)](../01-getting-started/05-first-example-di.en.md)
- [Transactions](../06-di/01-transactions.en.md)
- [Logging and Diagnostics](../06-di/03-logging.en.md)
- [Performance](../03-advanced-topics/03-performance.en.md)
- [API Index](./02-api-index.en.md)
