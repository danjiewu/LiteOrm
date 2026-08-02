# Configuration and Registration

LiteOrm offers two configuration approaches—**Core Library Only (manual API)** and **Framework (DI auto-binding)**. Choose the one that fits whether your project uses a DI container:

- **Core library only**: no DI container required, no `appsettings.json`, data sources are configured manually in code. Suited for console apps, batch scripts, or projects without a DI container.
- **Framework integration**: configuration is declared in `appsettings.json`, and `RegisterLiteOrm()` automatically performs DI binding, DAO registration, and dialect resolution at startup. Suited for ASP.NET Core and other projects that need Autofac and AOP capabilities.

> **Beginner tip**: If this is your first time configuring, start with the simplest setup—a single data source using SQLite. Once the basic flow works, gradually add multi-data-source, read/write splitting, and other advanced configurations.

## Approach 1: Core Library Only

When using only the `LiteOrm` core library, there is no DI container dependency (no Autofac) and no `appsettings.json` is read; all configuration is done manually in code. Simply configure data sources through the `AddDataSource` API of `DataSourceProvider`:

```csharp
var dataSourceProvider = new DataSourceProvider();
dataSourceProvider.AddDataSource(new DataSourceConfig
{
    Name = "DefaultConnection",
    ConnectionString = "Data Source=demo.db",
    Provider = typeof(SqliteConnection).AssemblyQualifiedName,
    SyncTable = true
});
dataSourceProvider.SetDefaultDataSource("DefaultConnection");
LiteOrmSqlFunctionInitializer.Initialize();
var poolFactory = new DAOContextPoolFactory(dataSourceProvider);
DAOContextPoolFactory.Set(() => poolFactory);
var sessionManager = new SessionManager(poolFactory);
SessionManager.SetCurrent(() => sessionManager);
```

> **Notes**:
> - The core library does not depend on a DI container and does not read `appsettings.json`; all configuration is done in code.
> - `AddDataSource` adds a data source; `SetDefaultDataSource` designates the default data source.
> - `LiteOrmSqlFunctionInitializer.Initialize()` registers the built-in SQL function mappings and must be called.
> - For the complete core-library initialization flow (including DAO/Service construction), see [First End-to-End Example (Core Library Only)](./04-first-example.en.md).

## Approach 2: Framework Integration

When using `LiteOrm.Framework`, configuration is declared in `appsettings.json`, and `RegisterLiteOrm()` automatically performs DI binding, DAO registration, and dialect resolution at startup.

### `appsettings.json` example

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

#### Minimal Configuration Examples by Database

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

### Configuration Fields

| Setting | Purpose | Required | Default |
| --- | --- | --- | --- |
| `Default` | Default data source name. | Yes | - |
| `DataSources[].Name` | Data source identifier, referenced by `[Table(DataSource = ...)]`. | Yes | - |
| `DataSources[].ConnectionString` | Database connection string. | Yes | - |
| `DataSources[].Provider` | Fully qualified connection type name, in the form `TypeName, AssemblyName`. | Yes | - |
| `DataSources[].SqlBuilder` | Optional custom dialect builder. | No | `null` (auto-detect) |
| `DataSources[].KeepAliveDuration` | Connection keep-alive duration. | No | `00:10:00` |
| `DataSources[].PoolSize` | Maximum number of cached connections in the pool. | No | `16` |
| `DataSources[].MaxPoolSize` | Upper limit of concurrent connections. | No | `100` |
| `DataSources[].ParamCountLimit` | Parameter count cap for a single SQL statement. | No | `1000` |
| `DataSources[].SyncTable` | Whether to auto-sync table creation (pool-level default; can be overridden per entity via `[Table(SyncTable = ...)]`). | No | `false` |
| `DataSources[].ReadOnlyConfigs` | Read-replica configuration for read/write splitting. | No | `[]` |

> **Beginner advice**: For your first setup, only configure the three required fields: `Name`, `ConnectionString`, and `Provider`. Use defaults for the rest.

### Registration Patterns

> `RegisterLiteOrm()` is defined in the `LiteOrm.Framework` package. Install it with `dotnet add package LiteOrm.Framework` and add `using LiteOrm.Framework;` before use.

#### Console or Worker Application

```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()
    .Build();
```

#### ASP.NET Core Application

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();
```

> **Important**: `RegisterLiteOrm()` must be called on `builder.Host` (not `builder.Services`), because it replaces the underlying DI container with Autofac.

#### Registration with Options

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterScope = true;
    options.Assemblies = new[] { typeof(MyService).Assembly };
    options.RegisterSqlBuilder("main", new MySqlBuilder());
});
```

#### Complete Program.cs Example

> Here is a complete ASP.NET Core `Program.cs` showing the typical placement of LiteOrm registration:

```csharp
using LiteOrm.Framework;

var builder = WebApplication.CreateBuilder(args);

// Add controller services
builder.Services.AddControllers();

// Register LiteOrm (must be called on builder.Host)
builder.Host.RegisterLiteOrm();

var app = builder.Build();

app.MapControllers();
app.Run();
```

> **Note**: `RegisterLiteOrm()` is defined in the `LiteOrm.Framework` package; reference `LiteOrm.Framework` and add `using LiteOrm.Framework;`.

### Logging Integration

LiteOrm runtime logging is built on `Microsoft.Extensions.Logging`. Service-call logs, exception logs, and slow-query logs follow whatever providers the host application has configured.

#### Host Logging Example

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Host.RegisterLiteOrm();
```

#### What `options.LoggerFactory` is for

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

- The host DI `ILoggerFactory`: handles normal service invocation logs.
- `options.LoggerFactory`: mainly for framework registration and assembly-scan output.

For detailed usage, see [Logging and Diagnostics](../06-framework/03-logging.en.md).

### Multi-Data-Source and Read/Write Guidance

- Use `[Table(DataSource = "...")]` on an entity to bind it to a data source.
- For read-heavy, write-light scenarios, use `ReadOnlyConfigs` to configure read replicas:
  - By default, query/view APIs prefer read-only connections.
  - Within the same `Session`, the first selected read-only replica is cached and reused (avoiding re-polling on every query).
  - In transactions, reads are forced back to the primary connection for consistency.
  - When no read replica is configured, reads fall back to the primary connection.
- When database dialect differences are involved, register a `SqlBuilder` explicitly.

### Common Questions

#### What should `Provider` contain?

Use the full connection type name, for example `System.Data.SqlClient.SqlConnection, System.Data.SqlClient`.

#### When do I need a custom `SqlBuilder`?

When the database version is older, or when paging syntax or function behavior differs from the default implementation, you need a custom `SqlBuilder`.

#### Common Beginner Configuration Mistakes

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

#### How to verify your configuration is correct?

After starting the application, check the console output. If you see a log message like `LiteOrm initialized successfully`, the configuration is correct. If an exception occurs, check:

1. Whether the connection string can actually connect to the database (test with a database management tool first).
2. Whether the `Provider` type name matches the installed NuGet package.
3. Whether the database service is running.

## Related Links

- [Back to docs hub](../README.md)
- [First End-to-End Example (Core Library Only)](./04-first-example.en.md)
- [First End-to-End Example (Framework)](./05-first-example-framework.en.md)
- [Configuration Reference](../05-reference/01-configuration-reference.en.md)
- [Custom SqlBuilder / Dialect Extension](../04-extensibility/03-custom-sqlbuilder.en.md)
