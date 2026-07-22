# Configuration and Registration

LiteOrm reads a `LiteOrm` configuration section, then wires up services, DAO types, and optional dialect overrides during startup.

> **Beginner tip**: If this is your first time configuring, start with the simplest setup—a single data source using SQLite. Once the basic flow works, gradually add multi-data-source, read/write splitting, and other advanced configurations.

## 1. `appsettings.json` example

```json
{
  "LiteOrm": {
    "Default": "main",
    "DataSources": [
      {
        "Name": "main",
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
            "ConnectionString": "Server=localhost;Database=TestDb_ReadOnly;User Id=root;Password=123456;"
          }
        ]
      }
    ]
  }
}
```

### Minimal Configuration Examples by Database

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

## 2. Important fields

| Setting | Purpose | Required | Default |
|------|---------|---------|---------|
| `Default` | Default data source name | Yes | - |
| `DataSources[].Name` | Identifier referenced by `[Table(DataSource = "...")]` | Yes | - |
| `Provider` | Fully qualified connection type name | Yes | - |
| `SqlBuilder` | Optional custom dialect type | No | `null` (auto-detect) |
| `KeepAliveDuration` | Connection keep-alive duration | No | `00:10:00` |
| `PoolSize` / `MaxPoolSize` | Connection pool sizing | No | `16` / `100` |
| `ParamCountLimit` | Parameter-count cap for one SQL statement | No | `1000` |
| `DataSources[].SyncTable` | Whether to auto-sync table creation (pool-level default; can be overridden per entity type via the `[Table(SyncTable = ...)]` attribute) | No | `false` |
| `ReadOnlyConfigs` | Read replicas for read/write splitting | No | `[]` |

> **Beginner advice**: For your first setup, only configure the three required fields: `Name`, `ConnectionString`, and `Provider`. Use defaults for the rest.

## 3. Registration patterns

### Console or worker application

```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()
    .Build();
```

### ASP.NET Core application

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();
```

> **Important**: `RegisterLiteOrm()` must be called on `builder.Host` (not `builder.Services`), because it replaces the underlying DI container with Autofac.

### Registration with options

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterScope = true;
    options.Assemblies = new[] { typeof(MyService).Assembly };
    options.RegisterSqlBuilder("main", new MySqlBuilder());
});
```

### Complete Program.cs Example

> Here's a complete ASP.NET Core `Program.cs` showing the typical placement of LiteOrm registration:

```csharp
using LiteOrm;

var builder = WebApplication.CreateBuilder(args);

// Add controller services
builder.Services.AddControllers();

// Register LiteOrm (must be called on builder.Host)
builder.Host.RegisterLiteOrm();

var app = builder.Build();

app.MapControllers();
app.Run();
```

## 4. Logging integration

LiteOrm runtime logging is built on `Microsoft.Extensions.Logging`. Service-call logs, exception logs, and slow-query logs follow whatever providers the host application has configured.

### Host logging example

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Host.RegisterLiteOrm();
```

### What `options.LoggerFactory` is for

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.LoggerFactory = LoggerFactory.Create(logging =>
    {
        logging.AddConsole();
        logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
    });
});
```

- the host DI `ILoggerFactory` handles normal service invocation logs
- `options.LoggerFactory` is mainly for framework registration and assembly-scan output

For attribute usage and diagnostics guidance, see [Logging and Diagnostics](../03-advanced-topics/07-logging.en.md).

## 5. Multi-data-source and read/write guidance

- Use `[Table(DataSource = "...")]` to bind an entity to a non-default source.
- Use `ReadOnlyConfigs` when reads can safely go to replicas:
  - Query/view APIs prefer read-only connections by default.
  - Within the same `Session`, the first selected read-only replica is cached and reused.
  - In transactions, reads are forced back to the primary connection for consistency.
  - If `ReadOnlyConfigs` is empty, reads fall back to the primary connection.
- Register a custom `SqlBuilder` when a provider needs database-version-specific SQL.

## 6. Common questions

### What should `Provider` contain?

Use the full connection type name, for example `System.Data.SqlClient.SqlConnection, System.Data.SqlClient`.

### When do I need a custom `SqlBuilder`?

Usually when paging syntax, function SQL, or legacy database behavior differs from LiteOrm's default dialect.

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

- [Back to English docs hub](../README.md)
- [First End-to-End Example](./04-first-example.en.md)
- [Configuration Reference](../05-reference/01-configuration-reference.en.md)
- [Custom SqlBuilder and Dialect Extension](../04-extensibility/03-custom-sqlbuilder.en.md)
