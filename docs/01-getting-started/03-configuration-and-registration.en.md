# Configuration and Registration

LiteOrm reads a `LiteOrm` configuration section, then wires up services, DAO types, and optional dialect overrides during startup.

## 1. `appsettings.json` example

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
        "ReadOnlyConfigs": []
      }
    ]
  }
}
```

## 2. Important fields

| Setting | Purpose |
|------|---------|
| `Default` | Default data source name |
| `DataSources[].Name` | Identifier referenced by `[Table(DataSource = "...")]` |
| `Provider` | Fully qualified connection type name |
| `SqlBuilder` | Optional custom dialect type |
| `KeepAliveDuration` | Connection keep-alive duration |
| `PoolSize` / `MaxPoolSize` | Connection pool sizing |
| `ParamCountLimit` | Parameter-count cap for one SQL statement |
| `ReadOnlyConfigs` | Read replicas for read/write splitting |

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

### Registration with options

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterScope = true;
    options.Assemblies = new[] { typeof(MyService).Assembly };
    options.RegisterSqlBuilder("DefaultConnection", new MySqlBuilder());
});
```

## 4. Multi-data-source and read/write guidance

- Use `[Table(DataSource = "...")]` to bind an entity to a non-default source.
- Use `ReadOnlyConfigs` when reads can safely go to replicas.
- Register a custom `SqlBuilder` when a provider needs database-version-specific SQL.

## 5. Common questions

### What should `Provider` contain?

Use the full connection type name, for example `System.Data.SqlClient.SqlConnection, System.Data.SqlClient`.

### When do I need a custom `SqlBuilder`?

Usually when paging syntax, function SQL, or legacy database behavior differs from LiteOrm's default dialect.

## Related Links

- [Back to English docs hub](../README.md)
- [First End-to-End Example](./04-first-example.en.md)
- [Configuration Reference](../05-reference/01-configuration-reference.en.md)
- [Custom SqlBuilder and Dialect Extension](../04-extensibility/03-custom-sqlbuilder.en.md)
