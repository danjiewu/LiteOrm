# LiteOrm API Usage Guide (for AI)

## Environment and packages

- Runtime: `.NET 8.0+`; `netstandard2.0` covers .NET Framework 4.6.1+.
- NuGet: `LiteOrm` (brings `LiteOrm.Common`), `LiteOrm.DependencyInjection` (Autofac + Castle; AOP transactions/permissions/logging), `LiteOrm.Remote` / `LiteOrm.Remote.Server` (remote invocation).
- Drivers: SQL Server `Microsoft.Data.SqlClient`, MySQL `MySqlConnector`, PostgreSQL `Npgsql`, Oracle `Oracle.ManagedDataAccess.Core`, SQLite `Microsoft.Data.Sqlite`.
- Databases: SQL Server 2012+, MySQL 8.0+, Oracle 12c+, PostgreSQL, SQLite; domestic/compatible engines (DM, KingbaseES, GaussDB, OceanBase, TiDB, GreatDB) inherit the corresponding mainstream dialect.

## 1. Configuration and registration

### appsettings.json

```json
{
  "LiteOrm": {
    "Default": "DefaultConnection",
    "DataSources": [
      {
        "Name": "DefaultConnection",
        "ConnectionString": "Server=(local);Database=TestDB;User Id=sa;Password=123456;",
        "Provider": "System.Data.SqlClient.SqlConnection, System.Data.SqlClient",
        "SqlBuilder": null,
        "KeepAliveDuration": "00:10:00",
        "PoolSize": 16,
        "MaxPoolSize": 100,
        "ParamCountLimit": 1000,
        "SyncTable": false,
        "ReadOnlyConfigs": [
          {
            "ConnectionString": "Server=replica;Database=TestDB;User Id=sa;Password=123456;",
            "KeepAliveDuration": null,
            "PoolSize": null,
            "MaxPoolSize": null,
            "ParamCountLimit": null
          }
        ]
      }
    ]
  }
}
```

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| `Default` | `string` | — | Default data source name |
| `DataSources[].Name` | `string` | — | Data source name (referenced by the `DataSource` parameter on `[Table]`) |
| `DataSources[].ConnectionString` | `string` | — | Database connection string |
| `DataSources[].Provider` | `string` | — | Fully qualified connection type name in the format `TypeName, AssemblyName` (resolved to `ProviderType` on load) |
| `DataSources[].SqlBuilder` | `string` | `null` | Fully qualified SQL builder type name (optional; resolved to `SqlBuilderType` on load, auto-matched from `Provider` when omitted) |
| `DataSources[].KeepAliveDuration` | `TimeSpan` | `00:10:00` | Connection keep-alive duration (`00:00:00` = unlimited) |
| `DataSources[].PoolSize` | `int` | `16` | Maximum number of cached connections in the pool |
| `DataSources[].MaxPoolSize` | `int` | `100` | Maximum concurrent connection limit |
| `DataSources[].ParamCountLimit` | `int` | `1000` | Maximum SQL parameter count (`0` = unlimited) |
| `DataSources[].SyncTable` | `bool` | `false` | Whether to auto-sync table creation; pool-level default, overridable per entity type via the `[Table(SyncTable = ...)]` attribute or the `DatabaseSync.OnTableSyncing` event |
| `DataSources[].ReadOnlyConfigs[]` | `array` | `[]` | Read-only replica configuration list (read/write splitting); omitted fields inherit from the primary data source |

### Service registration

LiteOrm offers three ways to wire up, chosen by whether you need a DI container and AOP interception:

| Way | Package | Call site | DI container | AOP interception |
| --- | --- | --- | --- | --- |
| `LiteOrmContext` | `LiteOrm` (base library) | `new LiteOrmContext()` directly | ❌ not needed | ❌ |
| `AddLiteOrm()` | `LiteOrm` (base library) | `builder.Services.AddLiteOrm(...)` | ✅ MS DI | ❌ |
| `RegisterLiteOrm()` | `LiteOrm.DependencyInjection` | `builder.Host.RegisterLiteOrm(...)` | ✅ MS DI + Autofac bridge | ✅ Autofac + Castle dynamic proxy |

#### AddLiteOrm (plain MS DI, no AOP)

```csharp
using LiteOrm;

builder.Services.AddLiteOrm();                        // Basic registration

builder.Services.AddLiteOrm(options =>
{
    options.AutoRegisterServices = true;              // Default true; auto-registers [AutoRegister] types
    options.ConfigureServices = sc => sc.AddMyApp();  // Optional custom service registration hook
});
```

#### RegisterLiteOrm (Autofac + AOP)

```csharp
using LiteOrm.DependencyInjection;

builder.Host.RegisterLiteOrm();                        // Basic registration

builder.Host.RegisterLiteOrm(options =>
{
    options.AutoRegisterServices = true;                          // Default true; scans and registers [AutoRegister] types (with interception)
    // Scope tracking is enabled by default; no configuration needed
    options.Assemblies = new[] { typeof(MyService).Assembly };    // Restrict scanned assemblies (defaults to scanning all)
    options.RegisterSqlBuilder("DefaultConnection", new MySqlBuilder()); // Register by data source name
    options.RegisterSqlBuilder(typeof(SqlConnection), new MySqlBuilder()); // Register by connection type
});
```

#### LiteOrmContext (no DI container)

When you don't want any DI container, use `LiteOrmContext` to register data sources and create a session, then pass the session straight to a DAO constructor. This line is **fully separate** from the two above: it registers no services and never reads `IConfiguration`, and `CreateSession()` binds the new session to `SessionManager.Current`.

```csharp
using LiteOrm;
using Microsoft.Data.Sqlite;

using var context = new LiteOrmContext()
    .AddDataSource<SqliteConnection>("DefaultConnection", "Data Source=app.db", syncTable: true);

using var session = context.CreateSession();
var dao = new ObjectDAO<User>(session);
var viewDao = new ObjectViewDAO<UserView>(session);

await dao.InsertAsync(new User { UserName = "admin", Age = 18 });
var adults = await viewDao.Search(Expr.Prop("Age") > 18).ToListAsync();
```

- `AddDataSource<TConnection>(name, connectionString, @default, syncTable, sqlBuilder, poolSize, maxPoolSize, paramCountLimit, keepAliveDuration)`: the provider type comes from `TConnection`, and pool options plus table sync are configured in that **single call** — the context deliberately offers no follow-up configuration methods.
- Build the config explicitly with `AddDataSource(DataSourceConfig config, bool @default = false)` when needed; `DataSourceConfig.ProviderType` / `SqlBuilderType` are assignable `Type?` values (code can only assign a `Type`; the `Provider` / `SqlBuilder` JSON keys in `appsettings.json` are resolved to a `Type` on load and throw `TypeLoadException` on failure).
- Deliberately narrow surface: `AddDataSource` / `CreateSession` / `DataSources` / `DefaultDataSourceName` / `GetDataSource` / `Dispose`, with the logger factory injected through the constructor.
- **Pool creation timing**: the first `CreateSession()` builds every connection pool once, from the data sources configured at that moment; any later `AddDataSource` throws `InvalidOperationException`, so all `AddDataSource` calls must come before the first `CreateSession()`.
- `Dispose()` only tears down the pool factory and leaves `SessionManager.Current` untouched, so it does not affect the DI line in the same process.
- When you need AOP (`[Transaction]` / `[ServicePermission]` / `[ServiceLog]`), you must switch to `RegisterLiteOrm()`; neither `LiteOrmContext` nor `AddLiteOrm()` provides AOP.

#### `[AutoRegister]` auto-registration mechanism

Types marked with `[AutoRegister]` are automatically registered into the DI container. The registration method is chosen automatically by the build mode — no manual switching required:

| Build mode | Registration method | Notes |
| --- | --- | --- |
| AOT / trim (`PublishAot` / `IsAotCompatible` / `PublishTrimmed` etc. set to `true`) | Compile-time source generator (`LiteOrm.Generators.AutoRegisterGenerator`) | Emits registration code; a module initializer hooks it up. No reflection |
| Non-AOT (default) | Runtime assembly scan | `LiteOrmAutoRegistration.Apply()` reflects over referenced assemblies for `[AutoRegister]` types |

- The `AutoRegisterServices` option (default `true`) only governs auto-registration of **user-defined** services and DAOs; the two methods are dispatched automatically via `RuntimeFeature.IsDynamicCodeSupported`. Set to `false` to skip that and register custom services manually.
- The framework's built-in generic DAOs and services (`ObjectDAO<>` / `ObjectViewDAO<>` / `EntityService<>` / `EntityViewService<>` and their interfaces) are unaffected by this option — they are always registered by `AddLiteOrm()` and remain available even when `AutoRegisterServices` is `false`.
- The registration scope is controlled by the `Policy` enum `RegisterPolicy` on `[AutoRegister]`: `All` (default — the implementation type itself + interfaces), `Self` (itself only), `Interface` (interfaces only).
- `RegisterLiteOrm()`: Autofac assembly-scan registration; automatically applies Castle interceptors from `[InterceptAttribute]`, the `IEntityService` interface family, and types carrying `[Service]` (`IsService=true`) via `ServiceInvokeInterceptor`.
- Source generation is enabled automatically under an AOT build; to generate it in an ordinary build too (for example, to validate AOT compatibility early), declare `[assembly: LiteOrmCodeGen]` (`LiteOrmCodeGenAttribute`) on the assembly.

For the full checklist under an AOT build — DI wiring, association metadata, constant slices, converter declarations — see [NativeAOT Support](../advanced-topics/aot.en.md).

## 2. Entity and view definitions

```csharp
[Table("Users")]
public class User : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    [Column("UserName")]
    public string? UserName { get; set; }
    [Column("Age")]
    public int Age { get; set; }
    [Column("DeptId")]
    [ForeignType(typeof(Department), Alias = "Dept")]
    public int? DeptId { get; set; }
}

// View model (used for association queries)
public class UserView : User
{
    [ForeignColumn(typeof(Department), Property = "DeptName")]
    public string? DeptName { get; set; }
}
```

### `[Table]` Attribute

| Parameter | Description |
|-----------|-------------|
| `Name` | Database table name, supports placeholder for sharding. |
| `DataSource` | Specifies the data source for the current entity. |
| `SyncTable` | Entity-level table-structure sync mode, enum `SyncTableMode` (`Default` / `Never` / `Always`), defaults to `Default`. `Never`/`Always` overrides the data-source-level `SyncTable` config. |
| `AutoGenerateColumns` | Whether to auto-generate columns for properties without a `[Column]`, defaults to `true`. When set to `false`, only properties explicitly marked with `[Column]` are mapped as columns and unmarked ones are ignored; `[ForeignColumn]` is unaffected. |

```csharp
[Table("Logs", SyncTable = SyncTableMode.Always)] // Always auto-create, even when the data source has SyncTable=false
public class Log { ... }

[Table("Legacy", SyncTable = SyncTableMode.Never)] // Never auto-create, even when the data source has SyncTable enabled
public class Legacy { ... }
```

> `SyncTable` decision priority: `OnTableSyncing` event > `[Table(SyncTable = ...)]` (`Never`/`Always`) > pool-level `SyncTable`.

## 3. Service definitions

```csharp
// Entity type == view type (EntityService<T>, equivalent to EntityService<T, T>)
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<User>, IEntityViewServiceAsync<User>
{ }
public class UserService : EntityService<User>, IUserService { }

// Entity type != view type (EntityService<T, TView>; TView must inherit from T)
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<UserView>, IEntityViewServiceAsync<UserView>
{ }
public class UserService : EntityService<User, UserView>, IUserService { }
```

## 4. API reference

### IEntityService<T> (create, update, delete)

| Method | Return type |
| --- | --- |
| `Insert(T entity)` | `bool` |
| `Update(T entity)` | `bool` |
| `UpdateOrInsert(T entity)` | `bool` |
| `Delete(T entity)` | `bool` |
| `DeleteID(object id, params string[] tableArgs)` | `bool` |
| `DeleteAll(LogicExpr expr, params string[] tableArgs)` | `int` |
| `UpdateAll(UpdateExpr expr, params string[] tableArgs)` | `int` |
| `BatchInsert(IEnumerable<T> entities)` | `void` |
| `BatchUpdate(IEnumerable<T> entities)` | `void` |
| `BatchUpdateOrInsert(IEnumerable<T> entities)` | `void` |
| `BatchDelete(IEnumerable<T> entities)` | `void` |
| `Batch(IEnumerable<EntityOperation<T>> entities)` | `void` |

### IEntityServiceAsync<T> (async create, update, delete)

| Method | Return type |
| --- | --- |
| `InsertAsync(T entity, CancellationToken ct = default)` | `Task<bool>` |
| `UpdateAsync(T entity, CancellationToken ct = default)` | `Task<bool>` |
| `UpdateOrInsertAsync(T entity, CancellationToken ct = default)` | `Task<bool>` |
| `DeleteAsync(T entity, CancellationToken ct = default)` | `Task<bool>` |
| `DeleteIDAsync(object id, string[] tableArgs = null, CancellationToken ct = default)` | `Task<bool>` |
| `DeleteAllAsync(LogicExpr expr, string[] tableArgs = null, CancellationToken ct = default)` | `Task<int>` |
| `UpdateAllAsync(UpdateExpr expr, string[] tableArgs = null, CancellationToken ct = default)` | `Task<int>` |
| `BatchInsertAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `BatchUpdateAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `BatchDeleteAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `BatchAsync(IEnumerable<EntityOperation<T>> entities, CancellationToken ct = default)` | `Task` |

### IEntityViewService<TView> (query, Expr style)

| Method | Return type |
| --- | --- |
| `GetObject(object id, params string[] tableArgs)` | `TView` |
| `SearchOne(Expr expr, params string[] tableArgs)` | `TView` |
| `Search(Expr expr = null, params string[] tableArgs)` | `List<TView>` |
| `SearchAs<TResult>(SelectExpr selectExpr, params string[] tableArgs)` | `List<TResult>` |
| `SearchOneAs<TResult>(SelectExpr selectExpr, params string[] tableArgs)` | `TResult` |
| `ForEach(Expr expr, Action<TView> func, params string[] tableArgs)` | `void` |
| `ExistsID(object id, params string[] tableArgs)` | `bool` |
| `Exists(Expr expr, params string[] tableArgs)` | `bool` |
| `Count(Expr expr = null, params string[] tableArgs)` | `int` |

### IEntityViewServiceAsync<TView> (async query)

| Method | Return type |
| --- | --- |
| `GetObjectAsync(object id, string[] tableArgs = null, CancellationToken ct = default)` | `Task<TView>` |
| `SearchOneAsync(Expr expr, string[] tableArgs = null, CancellationToken ct = default)` | `Task<TView>` |
| `SearchAsync(Expr expr = null, string[] tableArgs = null, CancellationToken ct = default)` | `Task<List<TView>>` |
| `SearchAsAsync<TResult>(SelectExpr selectExpr, params string[] tableArgs)` | `Task<List<TResult>>` |
| `SearchOneAsAsync<TResult>(SelectExpr selectExpr, params string[] tableArgs)` | `Task<TResult>` |
| `ForEachAsync(Expr expr, Func<TView, Task> func, string[] tableArgs = null, CancellationToken ct = default)` | `Task` |
| `ExistsIDAsync(object id, string[] tableArgs = null, CancellationToken ct = default)` | `Task<bool>` |
| `ExistsAsync(Expr expr, string[] tableArgs = null, CancellationToken ct = default)` | `Task<bool>` |
| `CountAsync(Expr expr = null, string[] tableArgs = null, CancellationToken ct = default)` | `Task<int>` |

### Lambda expression extension methods

> These methods come from `LambdaExprExtensions` and can be used without modifying the service class.

| Method | Return type |
| --- | --- |
| `DeleteAll(Expression<Func<T, bool>> expression, params string[] tableArgs)` | `int` |
| `DeleteAllAsync(Expression<Func<T, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)` | `Task<int>` |
| `Search(Expression<Func<TView, bool>> expression, string[] tableArgs = null)` | `List<TView>` |
| `Search(Expression<Func<IQueryable<TView>, IQueryable<TView>>> expression, string[] tableArgs = null)` | `List<TView>` |
| `Search(IQueryable<TView> query, string[] tableArgs = null)` | `List<TView>` |
| `SearchOne(Expression<Func<TView, bool>> expression, string[] tableArgs = null)` | `TView` |
| `SearchOne(Expression<Func<IQueryable<TView>, IQueryable<TView>>> expression, string[] tableArgs = null)` | `TView` |
| `SearchOne(IQueryable<TView> query, string[] tableArgs = null)` | `TView` |
| `Exists(Expression<Func<TView, bool>> expression, params string[] tableArgs)` | `bool` |
| `Count(Expression<Func<TView, bool>> expression, params string[] tableArgs)` | `int` |
| `SearchAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)` | `Task<List<TView>>` |
| `SearchAsync(Expression<Func<IQueryable<TView>, IQueryable<TView>>> expression, string[] tableArgs = null, CancellationToken ct = default)` | `Task<List<TView>>` |
| `SearchAsync(IQueryable<TView> query, string[] tableArgs = null, CancellationToken ct = default)` | `Task<List<TView>>` |
| `SearchOneAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)` | `Task<TView>` |
| `SearchOneAsync(Expression<Func<IQueryable<TView>, IQueryable<TView>>> expression, string[] tableArgs = null, CancellationToken ct = default)` | `Task<TView>` |
| `SearchOneAsync(IQueryable<TView> query, string[] tableArgs = null, CancellationToken ct = default)` | `Task<TView>` |
| `ExistsAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)` | `Task<bool>` |
| `CountAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)` | `Task<int>` |
| `SearchAs(Expression<Func<IQueryable<TView>, IQueryable<TResult>>> expression, string[] tableArgs = null)` | `List<TResult>` |
| `SearchAs(IQueryable<TResult> query, string[] tableArgs = null)` | `List<TResult>` |
| `SearchOneAs(Expression<Func<IQueryable<TView>, IQueryable<TResult>>> expression, string[] tableArgs = null)` | `TResult` |
| `SearchOneAs(IQueryable<TResult> query, string[] tableArgs = null)` | `TResult` |
| `SearchAsAsync(Expression<Func<IQueryable<TView>, IQueryable<TResult>>> expression, string[] tableArgs = null, CancellationToken ct = default)` | `Task<List<TResult>>` |
| `SearchAsAsync(IQueryable<TResult> query, string[] tableArgs = null, CancellationToken ct = default)` | `Task<List<TResult>>` |
| `SearchOneAsAsync(Expression<Func<IQueryable<TView>, IQueryable<TResult>>> expression, string[] tableArgs = null, CancellationToken ct = default)` | `Task<TResult>` |
| `SearchOneAsAsync(IQueryable<TResult> query, string[] tableArgs = null, CancellationToken ct = default)` | `Task<TResult>` |

> Additional note: Service query APIs include the `Expr` overloads, these Lambda extensions (including the `SearchAs` / `SearchOneAs` / `SearchAsAsync` / `SearchOneAsAsync` projections), the `IQueryable<T>` overloads, and `SearchAs(...)` / `SearchAsAsync(...)` based on `SelectExpr`; `Expr.Query` and the IQueryable Lambda form both remain available. If you need `ExprString`, full SQL, or DataTable-oriented queries, switch to DAO.

### ObjectDAO<T> (create, update, delete only)

| Method | Return type |
| --- | --- |
| `Insert(T entity)` | `bool` |
| `Update(T entity, object timestamp = null)` | `bool` |
| `Delete(T entity)` | `bool` |
| `Delete(LogicExpr expr)` | `int` |
| `Update(UpdateExpr expr)` | `int` |
| `BatchInsert(IEnumerable<T> entities)` | `void` |
| `BatchUpdate(IEnumerable<T> entities)` | `void` |
| `BatchDelete(IEnumerable<T> entities)` | `void` |
| `UpdateOrInsert(T entity)` | `UpdateOrInsertResult` |
| `BatchUpdateOrInsert(IEnumerable<T> entities)` | `void` |
| `InsertAsync(T entity, CancellationToken ct = default)` | `Task<bool>` |
| `UpdateAsync(T entity, object timestamp = null, CancellationToken ct = default)` | `Task<bool>` |
| `DeleteAsync(T entity, CancellationToken ct = default)` | `Task<bool>` |
| `DeleteAsync(LogicExpr expr, CancellationToken ct = default)` | `Task<int>` |
| `UpdateAsync(UpdateExpr expr, CancellationToken ct = default)` | `Task<int>` |
| `BatchInsertAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `BatchUpdateAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `BatchDeleteAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `UpdateOrInsertAsync(T entity, CancellationToken ct = default)` | `Task<UpdateOrInsertResult>` |
| `BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `DeleteByKeys(params object[] keys)` | `bool` |
| `DeleteByKeysAsync(object[] keys, CancellationToken ct = default)` | `Task<bool>` |
| `BatchDeleteByKeys(IEnumerable keys)` | `void` |
| `BatchDeleteByKeysAsync(IEnumerable keys, CancellationToken ct = default)` | `Task` |

### ObjectViewDAO<T> (query only)

`EnumerableResult<T>` supports: `.ToList()` / `.ToListAsync()` / `.FirstOrDefault()` / `.FirstOrDefaultAsync()` / `.GetResult()` / `.GetResultAsync()` / `await foreach`

| Method | Return type |
| --- | --- |
| `Search(Expr expr = null)` | `EnumerableResult<T>` |
| `Search(Expression<Func<IQueryable<T>, IQueryable<T>>> expr)` | `EnumerableResult<T>` |
| `Search(IQueryable<T> query)` | `EnumerableResult<T>` |
| `Search(ref ExprString sqlBody, bool isFull = false)` | `EnumerableResult<T>` |
| `SearchAs<TResult>(SelectExpr selectExpr, Func<AutoLockDataReader, TResult> readerFunc = null)` | `EnumerableResult<TResult>` |
| `SearchAs<TResult>(Expression<Func<IQueryable<T>, IQueryable<TResult>>> expr, Func<AutoLockDataReader, TResult> readerFunc = null)` | `EnumerableResult<TResult>` |
| `SearchAs<TResult>(IQueryable<TResult> query, Func<AutoLockDataReader, TResult> readerFunc = null)` | `EnumerableResult<TResult>` |
| `SearchAs<TResult>(ref ExprString sqlBody)` | `EnumerableResult<TResult>` |
| `GetObject(params object[] keys)` | `EnumerableResult<T>` |
| `Count(Expr expr)` | `ValueResult<int>` |
| `Exists(object o)` / `Exists(T o)` | `ValueResult<bool>` |
| `ExistsKey(params object[] keys)` | `ValueResult<bool>` |
| `Exists(Expr expr)` | `ValueResult<bool>` |

### DataViewDAO<T> (query returning DataTable)

`DataTableResult` supports: `.GetResult()` / `.GetResultAsync()`

| Method | Return type |
| --- | --- |
| `Search(Expr expr)` | `DataTableResult` |
| `Search(string[] propertyNames, Expr expr)` | `DataTableResult` |
| `Search(ref ExprString sqlBody, bool isFull = false)` | `DataTableResult` |
| `Search<TInput, TResult>(IQueryable<TResult> query)` (extension) | `DataTableResult` |
| `Search<TInput, TResult>(string[] propertyNames, IQueryable<TResult> query)` (extension) | `DataTableResult` |

### DataDAO<T> (column-value bulk update)

Updates by an "attribute name → value" set without loading the entity — useful when only a few columns change.

| Method | Return type |
| --- | --- |
| `UpdateAllValues(IEnumerable<KeyValuePair<string, object>> values, LogicExpr expr)` | `NonQueryResult` |
| `UpdateValues(IEnumerable<KeyValuePair<string, object>> values, params object[] keys)` | `NonQueryResult` |

The keys in `values` are entity **property names** (not column names); a missing property throws. The returned `NonQueryResult` runs via `.Execute()` / `.ExecuteAsync()`.

## 5. Transactions

```csharp
// Declarative
[Transaction]
public void Transfer() { ... }

// Manual
var sessionManager = SessionManager.Current;
sessionManager.BeginTransaction();
try { sessionManager.Commit(); }
catch { sessionManager.Rollback(); throw; }
```

### Concurrency and read/write splitting

- Optimistic concurrency: mark a column with `[Column("Version", IsTimestamp = true)]` and pass the old value to `Update(entity, oldVersion)` / `UpdateAsync(entity, oldVersion, ct)`. A conflict affects 0 rows and returns `false` (no exception); the new value comes from the entity's current property and is not incremented by the framework.
- Key-based `Delete` / `DeleteID` carry no timestamp condition; for protection use `DeleteAll(Prop("Id") == id & Prop("Version") == oldVersion)`.
- With `DataSources[].ReadOnlyConfigs`, queries from view DAOs (`IsView = true`) go to a read replica; queries inside a transaction always use the primary; one session reuses the replica it picked, and multiple replicas are used round-robin. For strong consistency, read inside a transaction or override `IsView => false`.

## 6. Advanced features

### Sharding

```csharp
[Table("Orders_{0}")]
public class Order : ObjectBase, IArged
{
    public string[] GetArgs() => new string[] { (UserId % 10).ToString() };
}
```

### Multiple data sources

```csharp
[Table("Users", DataSource = "Secondary")]
public class User : ObjectBase { ... }
```

### Custom DAO / service

```csharp
public interface IUserCustomDAO : IObjectViewDAO<UserView> { ... }
public class UserCustomDAO : ObjectViewDAO<UserView>, IUserCustomDAO { ... }

public interface IUserService : IEntityService<User>, IEntityServiceAsync<User> { ... }
public class UserService : EntityService<User>, IUserService { ... }
```

### ServiceFactory

```csharp
public interface ServiceFactory
{
    IUserService UserService { get; }
    IUserCustomDAO UserCustomDAO { get; }
}
services.AddServiceGenerator<ServiceFactory>();
var factory = scope.ServiceProvider.GetRequiredService<ServiceFactory>();
```

### Service exception handling event

Subscribe to service exceptions by injecting `IServiceExceptionEvent` (notification-only; does not affect the call flow):

```csharp
public class ExceptionEvent : IServiceExceptionEvent
{
    public void OnException(ServiceExceptionContext context)
    {
        // Access exception, method name, arguments, SQL stack, and more
    }
}

services.AddScoped<ExceptionEvent>();
services.AddScoped<IServiceExceptionEvent>(sp => sp.GetRequiredService<ExceptionEvent>());
```

- `ServiceInvokeInterceptor` notifies the call lifecycle through three injected event interfaces: `IServiceInvokingEvent` / `IServiceInvokedEvent` / `IServiceExceptionEvent`
- The exception event is notification-only; the exception is always rethrown as-is
- `RemoteServiceInvokeInterceptor.ExceptionHandling` remains a static event for remote services and can convert an exception into an agreed result

### Entity service events (IEntityServiceEvent<T>)

Implement `IEntityServiceEvent<T>` and register it in the container to hook audit logging, cache invalidation, change notification and similar logic around entity operations. Returning `false` from a Before callback cancels that operation (all Before callbacks must return `true` for it to proceed); After callbacks fire once the operation succeeds. The interface members are abstract methods, so extend `EntityServiceEventBase<T>` when you only care about a few callbacks.

```csharp
public class UserAuditEvent : EntityServiceEventBase<User>
{
    public override bool OnInserting(User entity) => entity.Age >= 0;   // return false to cancel the insert
    public override void OnInserted(User entity) { /* audit log, invalidate cache */ }
}

services.AddScoped<IEntityServiceEvent<User>, UserAuditEvent>();
```

Overridable callbacks: `OnInserting` / `OnUpdating` / `OnUpdatingOrInserting` / `OnDeleting` plus the matching `OnInserted` / `OnUpdated` / `OnUpdatedOrInserted` / `OnDeleted`; the ID-based `OnDeleteIDing` / `OnBatchDeleteIDing`; the condition-based `OnDeleteAlling` / `OnUpdateAlling` and their `*ed` counterparts (carrying the affected row count). Batch methods fire one event per row.

Event subscribers are resolved lazily: constructing an entity service instance never touches them, and they are only requested from the container and cached the first time an event notification actually fires, after which every operation reuses the same collection. An exception thrown from a subscriber's constructor therefore surfaces on the first entity operation rather than when `IEntityService<T>` is resolved.

> `IEntityServiceEvent<T>` is an entity business event; it is a separate mechanism from the interceptor-level `IServiceInvokingEvent` / `IServiceInvokedEvent` / `IServiceExceptionEvent`.

## 7. Attribute quick reference

| Attribute | Purpose |
| --- | --- |
| `[Table("TableName")]` | Specifies the table name; optional `DataSource` parameter |
| `[Column("ColName", IsPrimaryKey, IsIdentity, IdentityStart, IdentityIncreasement)]` | Specifies column name and properties; `IdentityStart`/`IdentityIncreasement` customize auto-increment start value and increment (column-level on SQL Server/Dameng/Oracle, MySQL via table-level `AUTO_INCREMENT=n`, not supported on SQLite/PG) |
| `[ForeignType(typeof(T), Alias, AutoExpand)]` | Specifies the foreign-key related type; `AutoExpand` extends relation paths |
| `[TableJoin(typeof(T), ForeignKeys, AliasName, AutoExpand)]` | Type-level relation definition supporting composite keys and path reuse |
| `[ForeignColumn(typeof(T), Property)]` | Column projected from a related table (for view models) |
| `[Transaction]` | Declarative transaction |
| `[AutoRegister]` | Automatically registers the type into the DI container; AOT mode uses a compile-time source generator, non-AOT mode uses runtime assembly scan |
| `[assembly: LiteOrmCodeGen]` | Assembly-level switch that explicitly enables source generation (auto-enabled under an AOT build); use it to validate AOT compatibility early in an ordinary build |

## 8. Expr expression system

### Three query styles

| Style | Suitable for |
| --- | --- |
| Lambda expression `u => u.Age > 18` | Regular fixed conditions (preferred) with compile-time type safety |
| Expr object (operators / fluent methods) | Dynamically accumulated conditions, cross-layer reuse, and Expr-only capabilities |
| ExprString interpolated string | Condition fragments or full SQL inside custom DAOs |

### Expr static factory methods

| Method | Return type | Description |
| --- | --- | --- |
| `Expr.Prop("Name")` | `PropertyExpr` | Property expression |
| `Expr.Prop("alias", "Name")` | `PropertyExpr` | Property expression with a table alias |
| `Expr.Value(obj)` | `ValueExpr` | Parameterized value |
| `Expr.Const(obj)` | `ValueExpr` | Constant value (inlined into SQL, not parameterized) |
| `Expr.Func("ABS", expr)` | `FunctionExpr` | Function call |
| `Expr.Aggregate("SUM", expr, isDistinct)` | `FunctionExpr` | Aggregate function (`IsAggregate=true`) |
| `Expr.Concat(e1, e2)` | `ValueSet` | CONCAT string composition (extension method) |
| `Expr.Lambda<T>(u => u.Age > 18)` | `LogicExpr` | Converts Lambda to Expr |
| `Expr.From(type, tableArgs)` | `TableExpr` | Builds a chained-query starting point from a `Type`; use `Expr.From<T>()` for a `QuerySource<T>` |
| `Expr.From<T>(tableArgs)` | `QuerySource<T>` | Builds a query source; handled by the lambda parser inside a lambda, composes IQueryable queries outside it, needs an executor to run |
| `Expr.From<T>()...GetQueryExpr()` | `Expr` | Turns a composed IQueryable chain into an Expr, no executor needed |
| `QuerySource<T1>.Join<T2>(on)` / `JoinRelated<T2>()` | `QuerySource<T1, T2>` | Joins one right table and returns a query source whose element is `(T1, T2)` |
| `QuerySource<T1,...,Tn>.Join<Tn+1>(on)` / `JoinRelated<Tn+1>()` | `QuerySource<T1,...,Tn+1>` | Appends one right table per call, chaining up to seven elements; the tuple stays flat and each step names only the new right table |
| `IQueryable<T>.AsSource()` | `QuerySource<T>` | Narrows the `IQueryable<T>` static type back to the query source for async methods |
| `Expr.Sql("key", arg)` | `GenericSqlExpr` | Dynamic SQL fragment |
| `Expr.Delete<T>(tableArgs)` | `DeleteExpr` | Creates a DELETE expression |
| `Expr.If(condition, thenExpr, elseExpr)` | `FunctionExpr` | IF expression |
| `Expr.Case(cases, elseExpr)` | `FunctionExpr` | CASE expression (tuple array form) |
| `Expr.Case(params (LogicExpr, ValueTypeExpr)[])` | `FunctionExpr` | CASE expression (tuple array form, no ELSE) |
| `Expr.Case(params Expr[])` | `FunctionExpr` | CASE expression (alternating argument form) |
| `Expr.Query<T>(expression)` | `Expr` | Converts an IQueryable Lambda to Expr |
| `Expr.Query<T, TResult>(expression)` | `Expr` | Converts an IQueryable Lambda with a return value to Expr |
| `Expr.Exists(type, innerExpr, tableArgs)` | `ForeignExpr` | EXISTS query on a related table |
| `Expr.ExistsRelated(type, innerExpr, tableArgs)` | `ForeignExpr` | EXISTS query using automatic relation discovery |
| `Expr.Exists<T>(lambda)` | `bool` | Used only inside Lambda expressions to build EXISTS queries (direct calls throw an exception) |
| `Expr.ExistsRelated<T>(lambda)` | `bool` | Used only inside Lambda expressions to build automatic-related EXISTS queries (direct calls throw an exception) |
| `Expr.FromRelated<T>(tableArgs)` | `QuerySource<T>` | Same as `Expr.From<T>()`, plus the automatic relation condition inside a lambda |

> `Join` / `JoinRelated` are intended for entity tables only. View models already generate association JOINs through `[ForeignColumn]`; do not mix them with views. Use entity types for explicit multi-table joins and `ExistsRelated` for related-table filtering.

Additional rule:

- Expression objects such as `PropertyExpr`, `TableExpr`, `ForeignExpr`, `FunctionExpr`, `SelectExpr`, `SelectItemExpr`, `CommonTableExpr`, and `GenericSqlExpr` treat names and aliases as **case-insensitive** when comparing objects and calculating hash codes.
- As a result, `Prop("User", "Name")` and `Prop("user", "name")` are treated as the same expression, and aliases `T0` and `t0` are treated as the same alias.

### ExprVisitor traversal methods

`ExprVisitor` provides four traversal modes. All methods accept an optional `CancellationToken` parameter for external interruption:

| Method | Traversal mode | Short-circuit | Description |
|--------|---------------|--------------|-------------|
| `ExprVisitor.Visit(Func<Expr,bool>, root, order?, ct?)` | `Func<Expr,bool>` delegate | ✅ | Returns `false` or cancel to stop |
| `ExprVisitor.Visit(Action<Expr>, root, order?, ct?)` | `Action<Expr>` delegate | ❌ | Always completes (unless cancelled) |
| `ExprVisitor.Visit(IExprNodeVisitor, root, ct?)` | `IExprNodeVisitor` interface | ❌ | BeginVisit (pre) + EndVisit (post) |
| `ExprVisitor.Validate(ExprValidator, root, order?, ct?)` | `ExprValidator` base class | ✅ | Validate returns `false` or cancel to stop |

> `IExprNodeVisitor.BeginVisit(Expr, CancellationToken)` and `EndVisit(Expr, CancellationToken)` receive the cancellation token. If cancelled via `CancellationTokenSource.Cancel()` inside a callback, traversal will stop.

### CycleDetector circular reference detection

`CycleDetector` uses `ExprVisitor` to detect circular references in `Expr` trees (e.g., `Source` chain loops):

| Method | Description |
|--------|-------------|
| `CycleDetector.HasCycle(Expr root)` | Whether a circular reference exists |
| `CycleDetector.FindCycle(Expr root)` | Returns the node causing the cycle, or `null` |
| `CycleDetector.Detect(Expr root)` | Returns `CycleResult` (with `CycleNode` and `Path`) |

Detection is based on reference equality (`ReferenceEquals`) and uses `CancellationTokenSource` to interrupt traversal without exception-based control flow.

### Operator overloads

Operators on `PropertyExpr` / `ValueTypeExpr`:

| Operator | Description | Return type |
| --- | --- | --- |
| `==` `!=` `>` `<` `>=` `<=` | Comparison | `LogicExpr` |
| `+` `-` `*` `/` `%` | Arithmetic | `ValueTypeExpr` |
| `-expr` `~expr` | Unary negation / bitwise NOT | `ValueTypeExpr` |

Operators on `LogicExpr`:

| Operator | Description | Return type |
| --- | --- | --- |
| `&` | AND (returns the other side when left or right is null, useful for dynamic accumulation) | `AndExpr` |
| `\|` | OR (returns the other side when left or right is null, useful for dynamic accumulation) | `OrExpr` |
| `!` | NOT | `NotExpr` |

Additional notes:

- The Lambda conditional operator `a ? b : c` is automatically converted into `Expr.If(...)`, then rendered as SQL `CASE`

### PropertyExpr extension methods

| Category | Methods |
| --- | --- |
| Comparison | `.Equal(v)` `.NotEqual(v)` `.GreaterThan(v)` `.LessThan(v)` `.GreaterThanOrEqual(v)` `.LessThanOrEqual(v)` |
| Set | `.In(IEnumerable)` `.In(params items)` `.In(Expr)` |
| Range | `.Between(low, high)` |
| String | `.Like(pattern)` `.Contains(text)` `.StartsWith(text)` `.EndsWith(text)` |
| Null | `.IsNull()` `.IsNotNull()` |
| Type conversion | `.Cast(DbValueType)` |
| Alias | `.As("alias")` → `SelectItemExpr` |
| Aggregate | `.Count(isDistinct)` `.Sum()` `.Avg()` `.Max()` `.Min()` `.ListAgg(separator, orderBy)` |
| Sort | `.Asc()` `.Desc()` → `OrderByItemExpr` |

### LogicExpr extension methods

`.And(right)` `.Or(right)` `.Not()`

### Chained query construction

`Expr.From<T>()` is the entry point and supports the following chained calls in SQL clause order:

```csharp
using static LiteOrm.Common.Expr;
var query = From(typeof(User))
    .Where(Prop("Age") > 18)                         // WhereExpr
    .SelectAll()                                     // SelectExpr, equivalent to SELECT *
    .Where(Prop("Status") == 1)
    .OrderBy(Prop("DeptId").Asc())                   // OrderByExpr
    .Section(0, 20);                                 // SectionExpr (skip, take)
```

You can also explicitly project selected fields when needed:

```csharp
using static LiteOrm.Common.Expr;
var query = From(typeof(User))
    .Where(Prop("Age") > 18)                         // WhereExpr
    .GroupBy(Prop("DeptId"))                         // GroupByExpr
    .Having(Prop("Id").Count() > 5)                  // HavingExpr
    .Select(Prop("DeptId"),                          // SelectExpr
            Prop("Id").Count().As("Cnt"))
    .OrderBy(Prop("DeptId").Asc())                   // OrderByExpr
    .Section(0, 20);                                 // SectionExpr (skip, take)
```

`SelectExpr` can be used for `IN` subqueries:

```csharp
using static LiteOrm.Common.Expr;
// IN subquery
var subQuery = From(typeof(Department))
    .Where(Prop("Name") == "IT")
    .Select("Id");
var expr = Prop("DeptId").In(subQuery);
```

UpdateExpr / DeleteExpr (used by `ObjectDAO.Delete(LogicExpr)` / `ObjectDAO.Update(UpdateExpr)`, and the Service-layer `IEntityService<T>.DeleteAll(LogicExpr)` / `UpdateAll(UpdateExpr)` etc.):

```csharp
using static LiteOrm.Common.Expr;
var update = new UpdateExpr(From(typeof(User)), Prop("Id") == 1);
update.Set(("UserName", Value("NewName")), ("Age", Value(30)));

var delete = new DeleteExpr(From(typeof(User)), Prop("Age") < 18);
```

### ExprString

The interpolated string handler lets you embed Expr objects directly into DAO methods that accept `ExprString`, such as `Search(...)` and `SearchAs(...)`. Service query APIs do not expose `ExprString`:

```csharp
using static LiteOrm.Common.Expr;
// Embedded Expr objects are automatically converted into parameterized SQL fragments
var result = dao.Search($"WHERE {Prop("DeptName") == deptName} AND {Prop("Age") > 18}");

// Use full SQL through DAO together with isFull: true
var table = dataViewDao.Search(
    $"SELECT [Id], [UserName] FROM [Users] WHERE {Prop("Age")} > {minAge}",
    isFull: true
);
```

> When hand-writing table or column names in `ExprString`, you can use `[` and `]` as provider-agnostic quote placeholders. LiteOrm rewrites them to the current database's real identifier delimiters right before command execution.
>
> `ExprString` does not auto-expand `SelectExpr.With(name)` / `CommonTableExpr` into `WITH` SQL. When you need CTE, prefer the structured `Expr` / `SelectExpr` path; if you must use `ExprString`, handwrite the full `WITH ... SELECT ...` SQL.

#### RawSql — Inserting raw SQL fragments

`RawSql` is an independent `readonly struct` (**not** an `Expr`) that serves as an `ExprString` helper. When interpolated into an `ExprString`, its content is **spliced verbatim into the SQL** — no parameterization, no syntax processing, no `[ ]` placeholder replacement. It is used exclusively for **dynamic values unsuitable for parameterization**; purely static SQL text can be written directly in the `ExprString` literal — no `RawSql` needed:

```csharp
using LiteOrm.Common;
using static LiteOrm.Common.Expr;

// 1) LIMIT/OFFSET dynamic paging (numeric; range validation: non-negative integer + upper bound)
int pageSize = 20;
int offset = pageSize * pageIndex;
var paged = await dataViewDAO.Search(
    $"WHERE {Prop("Age")} >= {minAge} ORDER BY Id LIMIT {new RawSql(offset.ToString())}, {new RawSql(pageSize.ToString())}"
).ToListAsync();

// 2) Dynamic sort direction ASC/DESC (SQL keyword; enum whitelist)
string direction = ascending ? "ASC" : "DESC";
var sorted = await dataViewDAO.Search(
    $"WHERE {Prop("Age")} >= {minAge} ORDER BY Id {new RawSql(direction)}"
).ToListAsync();

// 3) Dynamic column name / sort field (identifier; whitelist: only real entity columns + charset validation)
string[] allowed = { "Id", "Name", "Age", "CreatedAt" };
string sortField = allowed.Contains(userField) ? userField : "Id";
var result = await dataViewDAO.Search(
    $"WHERE {Prop("Age")} >= {minAge} ORDER BY {new RawSql(sortField)} {new RawSql(direction)}"
).ToListAsync();
// Note: simple column names can also use Expr.Prop(sortField) (built-in name validation and quote wrapping, safer);
//       use RawSql only for complex expressions or when you need to bypass name validation.
```

**Security constraints (must follow)**:

| Rule | Description |
|------|--------------|
| Numeric dynamic values need range validation | e.g. `LIMIT` row count: non-negative integer + reasonable upper bound |
| String/token values need whitelist validation | `ASC`/`DESC` via enum whitelist; column names via entity column whitelist + charset validation |
| Do not use RawSql for static content | Hardcoded SQL fragments should be written directly in the `ExprString` literal; wrapping them in `RawSql` obscures the real intent |
| Not scanned by validators | `RawSql` is not an `Expr`; `ExprValidator.CreateQueryOnly()` does not scan it |
| No JSON round-trip | `RawSql` cannot be serialized/deserialized via `ExprJsonConverter`; frontend Expr JSON cannot carry it |
| Prefer Expr | Use `Expr.Prop` for simple column names (built-in name validation and quote wrapping); anything expressible via `Expr.Func`/`Expr.Sql` (pre-registered `GenericSqlExpr`) should not use `RawSql` |

> When you need to pass runtime strings/complex values inside custom SQL, register a callback via `GenericSqlExpr.Register` and parameterize using `context.OutputParams` inside the callback. See [ExprString Guide - Section 8](../core-usage/exprstring-guide.en.md#8-inserting-raw-sql-rawsql) and [Security](../advanced-topics/security.en.md).

### Common patterns

```csharp
using static LiteOrm.Common.Expr;
// Dynamically accumulate conditions (& is null-safe)
LogicExpr condition = null;
if (minAge.HasValue)  condition &= Prop("Age") >= minAge.Value;
if (deptId.HasValue)  condition &= Prop("DeptId") == deptId.Value;
if (!string.IsNullOrEmpty(name)) condition &= Prop("UserName").Contains(name);
var users = await dao.Search(condition).ToListAsync();

// EXISTS query on a related table
var expr = Exists<Department>(Prop("Name") == "IT");
// Equivalent Lambda form (inside a Lambda query):
var expr = Lambda<User>(u => Exists<Department>(d => d.Name == "IT"));

// SearchAs selects a subset of fields (prefer Lambda projection)
var result = dao.SearchAs(
    From<User>()
        .Where(u => u.Age > 18)
        .Select(u => new { u.Id, u.UserName })
).ToList();

// Use SelectExpr only when you need SelectExpr-only features (runtime column names, If(...).As("Level"), etc.)
var result2 = dao.SearchAs(
    From(typeof(User))
        .Where(Prop("Age") > 18)
        .Select("Id", "UserName")
).ToList();
```

### ExistsRelated notes

`ExistsRelated` filters the primary table by conditions on a related table without requiring the related fields to be explicitly exposed on the view model.

**Relation matching order: forward relations first; if no forward relation is found, reverse relations are tried. Multiple relation paths are combined with** **`OR`** **.**

```csharp
using static LiteOrm.Common.Expr;
// Filter users by related department
var expr = ExistsRelated<Department>(Prop("Name") == "IT");
var users = await objectViewDAO.Search(expr).ToListAsync();

// Lambda form
var lambdaExpr = Lambda<User>(u => ExistsRelated<Department>(d => d.Name == "IT"));
```

### Extension points (when needed)

- Custom functions: `SqlBuilder.Instance.RegisterFunctionSqlHandler("MyFunc", handler)`; render arguments inside the handler with `expr.Args[i].ToSql(context)` (non-root rendering, no context reset).
- Custom SQL fragments: `GenericSqlExpr.Register(key, callback)` — pre-registration is mandatory; parameterize values inside the callback.
- Expression extensions: `LambdaExprConverter.RegisterMethodHandler` / `RegisterMemberHandler` map business methods/properties to SQL expressions (see [Expression extension](../extensibility/expression-extension.en.md)).
- Dialect extensions: derive from `SqlBuilder` or a specific dialect builder, override paging, identifier quoting and type mapping, then register with `RegisterSqlBuilder` (see [Custom SqlBuilder](../extensibility/custom-sqlbuilder.en.md)).

## 9. CTE (common table expressions)

```csharp
using static LiteOrm.Common.Expr;

// 1) Define a CTE: SelectExpr.With(alias) returns a CommonTableExpr
var activeUsers = From(typeof(User))
    .Where(Prop("Age") >= 18)
    .Select(Prop("Id").As("Id"), Prop("UserName").As("Name"))
    .With("ActiveUsers");

// 2) Build the main query from the CTE
var query = activeUsers
    .Where(Prop("Name").Contains("A"))
    .Select(Prop("Id"), Prop("Name"))
    .OrderBy(Prop("Id").Asc())
    .Section(0, 20);

// 3) Root rendering: the WITH clause is emitted automatically
var sql = query.ToPreparedSql(context);
```

- Only a `SelectExpr` can become a CTE: `SelectExpr.With(name)` → `CommonTableExpr`, which still supports `Where` / `Select` / `OrderBy` / `UnionAll`. Both sides of a `UnionAll` may reuse the same `CommonTableExpr`; the `WITH` clause is emitted once.
- Recursive CTE: reference the alias inside the definition with `new CommonTableExpr { Alias = "Tree" }` (`Source == null`, alias only) and `UnionAll` it with the anchor. `WITH RECURSIVE` is controlled by `ISqlBuilder.ExplicitRecursive`.
- Collection: when a `CommonTableExpr` is rendered it calls `SqlBuildContext.AddCommonTableExpr`, which records the definition in `context.CommonTableExprs` (one post-order walk of the definition body, dependencies first) and emits only the alias in place. After the root body is rendered, `ExprSqlConverter.BuildCteSql(context)` builds `WITH [RECURSIVE]` and places it at the front of the SQL.
- Root vs nested rendering: `ToPreparedSql(context)` / `ToSql(context, true)` are root calls — they run `context.Init()` and emit `WITH`; function handlers and `GenericSqlExpr` callbacks are nested and use `ToSql(context)` (default `isRoot = false`), which does not reset the context, and the CTEs they collect are emitted by the outermost call. **Never pass `true` from a nested call**, it clears collected CTEs and scopes.
- Alias validation: equal definitions are deduplicated; different definitions throw `InvalidOperationException`; an alias-only node without a definition throws.
- Parameter numbering: CTE definitions render after the main body, so their parameters are numbered after the body's conditions; `BuildCteSql` moves those parameters to the front of `OutputParams` so the collection matches the SQL text. Parameters bind by name, so ordering does not affect execution.
- `ExprString` does not expand CTEs automatically; use structured `Expr` / `SelectExpr`, or write the full `WITH ... SELECT ...` by hand.
- UPDATE / DELETE / SELECT statements assembled inside the DAO (`DataDAO.UpdateAllValues`, const filters, etc.) automatically place a CTE's `WITH` at the front.

## 10. Paging, sorting, grouping and window functions

```csharp
using static LiteOrm.Common.Expr;

var query = From(typeof(User))
    .Where(Prop("Age") >= 18)                  // WhereExpr
    .GroupBy(Prop("DeptId"))                   // GroupByExpr
    .Having(Prop("Id").Count() > 5)            // HavingExpr
    .Select(Prop("DeptId"), Prop("Id").Count().As("Cnt"))
    .OrderBy(Prop("Cnt").Desc())               // OrderByExpr; multiple OrderByItemExpr allowed
    .Section(0, 20);                           // SectionExpr: skip, take
```

- `.Section(skip, take)` is the only paging entry; repeated calls reuse the same `SectionExpr` and update `Skip` / `Take` in place.
- In JSON, paging is the `Skip` / `Take` fields of the `$section` layer; sorting is the `OrderBys` array of the `$orderby` layer (`{ "Field": ..., "Asc": bool }`).
- `LIMIT/OFFSET`, `OFFSET ... FETCH`, Oracle `ROW_NUMBER`, MySQL `LIMIT a,b` are handled by the dialect `SqlBuilder`; business code never branches on the database.
- Always add a unique tiebreaker when paging (for example another `Prop("Id").Desc()`), otherwise rows may repeat or be skipped.
- Window functions use `FunctionExpr.Over(partitionExprs, orderExprs)`:

```csharp
var runningTotal = Func("SUM", Prop("Amount"))
    .Over(new[] { Prop("ProductId") }, new[] { Prop("SaleTime").Asc() });
```

## 11. Expr JSON, frontend queries and SQL debugging

Serialize and deserialize with `System.Text.Json` (`ExprJsonConverter` emits the short format by default):

```csharp
string json = JsonSerializer.Serialize<Expr>(expr);
Expr reloaded = JsonSerializer.Deserialize<Expr>(json)!;
```

Short-format markers:

| Marker | Meaning |
| --- | --- |
| `#` | Column reference: `{"#":"Name"}`, `{"#":"T0.Name"}` |
| `@` | Parameter value: `{"@":42}` (deserialized as a parameterized `ValueExpr`) |
| Plain value | Constant (`42`, `"abc"`, `{"$datetime":"..."}`) |
| `$` | Operator or segment type: `{"$":"=="}`, `{"$":"func"}`, `{"$":"sql"}` |
| `!` | Negation: `{"!":{"#":"IsActive"}}` |
| `$and` / `$or` | Logical AND / OR |
| `$where` / `$select` / `$orderby` / `$section` / `$table` / `$join` / `$group` / `$having` | Query-structure segments |

- Segments nest by layer: `$section` wraps `$orderby`, which wraps `$where`; each layer's own properties (`Skip`/`Take`, `OrderBys`, `Where`) sit next to the `$` marker.
- Comparison operators: `==`, `!=`, `>`, `>=`, `<`, `<=`; string/set operators: `in`, `notin`, `like`, `notlike`, `contains`, `notcontains`, `startswith`, `notstartswith`, `endswith`, `notendswith`, `regexp`, `notregexp`.
- Full shapes: [Expr JSON serialization format](../extensibility/expr-serialization.en.md).

Always validate JSON received from a frontend:

```csharp
var validator = new ExprValidatorGroup(
    ExprValidator.CreateQueryOnly(),        // only SELECT-style expressions
    FunctionExprValidator.AllowRegisted     // only registered functions
);
if (!ExprVisitor.Validate(validator, expr))   // must use ExprVisitor.Validate to walk the whole tree
    throw new UnauthorizedAccessException($"Disallowed expression: {validator.FailedExpr}");
```

SQL generation and debugging (no database needed):

```csharp
var sqlGen = new SqlGen(typeof(User)) { SqlBuilder = SQLiteBuilder.Instance };
PreparedSql sql = sqlGen.ToSql(expr);       // root rendering; a CTE's WITH is emitted automatically
Console.WriteLine(sql.Sql);
foreach (Param p in sql.Params) Console.WriteLine($"{p.Name} = {p.Value}");
```

## 12. Security and parameterization

- All business values are parameterized through `SqlBuildContext.OutputParams`: `@n` on SQL Server / MySQL / PostgreSQL / SQLite, `:n` on Oracle. Never concatenate user input into SQL text.
- `Expr.Value(v)` is parameterized; `Expr.Const(v)` is inlined as a constant. Numeric/boolean constants may be inlined; strings, `DateTime` and the like are always parameterized.
- `null` comparisons become `IS NULL` automatically (`Prop("X") == null` → `X IS NULL`), avoiding the `= NULL` semantics bug.
- `Contains` / `Like` add wildcards and escape `%`, `_`, `[`, `]`, appending `ESCAPE '/'` when needed.
- Custom SQL fragments must be pre-registered, and the callback must render expressions instead of concatenating values:

```csharp
GenericSqlExpr.Register("OwnerScope", (context, arg) =>
{
    if (arg is null) return null;
    LogicExpr own = Prop("OwnerId") == arg.AsValue();
    return own.ToSql(context);      // non-root rendering: parameterized, current scope preserved
});
```

- `RawSql` is only for values that cannot be parameterized (e.g. `LIMIT` counts, sort direction) and must be range- or whitelist-validated first; it is not an `Expr`, is not scanned by validators, and cannot round-trip through JSON.
- `ExprString` exists only at the DAO layer: literals are developer-authored SQL, plain values in format items are parameterized automatically, and `Expr` items are rendered as expressions. SQL identifiers may use `[` `]` placeholders that are replaced with the dialect's quote characters before execution.

## 13. Const filters and data permissions

- `[Column("State", Constant = RecordState.Enabled)]` adds a table-wide fixed filter, consolidated into `TableDefinition.ConstFilter`. It applies to queries, conditional UPDATE / DELETE, key-based read/update/delete, and batch statements, and becomes part of `JOIN ... ON` in association queries.
- `Constant` accepts enums, numbers (`Constant = 1`), strings (`Constant = "tenant_a"`) and other constants convertible to the property type; use it only for rules that never change.
- Runtime context (current user / tenant) should be layered with `Expr` at the query entry:

```csharp
LogicExpr filter = BuildBusinessFilter(request)
    & (Prop("IsDeleted") == false);
if (!IsAdmin(currentUser))
    filter &= Prop("CreatedByUserId") == currentUser.Id;
```

- When runtime conditions must also cover association and write paths, build a `GenericSqlExpr` and assign it to `TableDefinition.ConstFilter` (see [Permission filtering](../advanced-topics/permission-filtering.en.md) and [Data permissions](../typical-applications/data-permission.en.md)).
- List filtering does not replace object-level checks: detail, update and delete endpoints must still verify ownership explicitly.
- Multi-tenant isolation has four levels: runtime `Expr`, `TableArgs` sharding, `ConstFilter` slicing, and `DataSource` database routing.

## 14. Sharding, data mapping and custom converters

### Sharding

```csharp
[Table("Logs_{0}")]                 // {0} is replaced by TableArgs
public class Log : IArged
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }
    [Column("Level")] public string? Level { get; set; }
    [Column("CreateTime")] public DateTime CreateTime { get; set; }
    string[] IArged.TableArgs => new[] { CreateTime.ToString("yyyyMM") };
}

var logs = await logService.SearchAsync(
    l => l.CreateTime >= start && l.CreateTime <= end,
    tableArgs: new[] { "202603" });      // explicit shard arguments for queries
```

- Writes route by `IArged.TableArgs`; queries/deletes pass `tableArgs`; one placeholder corresponds to one argument.

### Data mapping

- `DbValueType` describes the database-side value type; arrays/JSON and other complex types need an explicit `DbType` (`Array`, `Json`, `Jsonb`), and custom types without `[Column]` are not mapped to columns automatically.
- `JsonNode` maps to `DbValueType.Json` automatically; arrays use native PostgreSQL arrays and fall back to text elsewhere.
- Register custom converters against the `SqlBuilder` generic type (register on the base type to cover every dialect):

```csharp
SqlBuilder.Instance.RegisterDbValueConverter<SqlBuilder, string, OrderSettings>(
    targetType: DbValueType.Json,
    fromDb: s => JsonSerializer.Deserialize<OrderSettings>(s)!,
    toDb: v => JsonSerializer.Serialize(v)
);
```

- A column can also declare `[Column(..., ConverterType = typeof(MyConverter))]`; under NativeAOT, declare or pre-register converters explicitly and keep a public parameterless constructor.

## 15. Logging, service authorization and remote services

- Logging: built on `Microsoft.Extensions.Logging`. `[ServiceLog(LogLevel = ServiceLogLevel.Debug, LogFormat = LogFormat.Full)]` can mark a method, class or interface; interfaces such as `IEntityServiceAsync<T>` default to Debug; slow-call logs come from the service interceptor.
- Service authorization: `[ServicePermission]` (authenticated), `[ServicePermission(AllowRoles = "Admin, Manager")]`, `[ServicePermission(true)]` (anonymous). Roles apply only after `IUserContext` (`IPrincipal UserPrincipal`) is registered; without it everything is allowed; failures throw `ServicePermissionException`. Requires the AOP from `RegisterLiteOrm()`.
- Remote services: both sides share a `[Service]` interface; the server calls `builder.Services.AddRemoteServer()`, the client uses `AddRemoteService<T>()` (or `AddRemoteServiceFactory<TFactory>()`) plus `RemoteServiceUri`. Signatures and call sites stay identical to local calls.

## 16. Performance and NativeAOT

- Pool: `PoolSize` (16), `MaxPoolSize` (100), `KeepAliveDuration` (10 minutes); `ParamCountLimit` (1000) caps parameters per statement and drives the batch size used by bulk methods.
- Query results are lazy: an unconsumed or undisposed `EnumerableResult<T>` / `DataTableResult` keeps the connection; consume with `ToList()` / `await foreach` and dispose promptly.
- Select only the columns you need (`SearchAs` projection); use `BatchInsert` / `BatchUpdate` / `BatchDelete` for bulk writes.
- NativeAOT: supported by the base library, with source-generated metadata and mappers. `LiteOrm.DependencyInjection` (Autofac/Castle) and `LiteOrm.Remote` are not AOT-compatible; AOP features (transaction/permission/log attributes) are unavailable — use manual transactions, runtime `Expr` and service events instead.
- Under AOT, projection/anonymous types have no generated mapper and fall back to reflection; custom converters must be declared or pre-registered.

## 17. Common mistakes and checklist

| Symptom | Cause / correct approach |
| --- | --- |
| Calling `Expr.Exists<T>(...)` outside a Lambda throws | It only works inside Lambda expressions handled by the parser; outside, use `Expr.Exists(type, innerExpr, tableArgs)` |
| Mixing `From<T>()` and `From(typeof(T))` breaks | `From<T>()` returns `QuerySource<T>` (IQueryable chain); `From(typeof(T))` returns `TableExpr` (Expr chain); do not mix them |
| No `ExprString` overload on Service | `ExprString` is DAO-only; switch to a DAO when you need hand-written SQL |
| Missing or duplicated `WITH` in SQL | Use `ToPreparedSql` / `ToSql(context, true)` at the root; use `ToSql(context)` inside handlers |
| Context cleared inside a custom function handler | Never call root `ToSql(context, true)` during rendering — it runs `Init()` |
| Duplicate or skipped rows when paging | Sorting lacks a unique tiebreaker |
| Ambiguous column names in multi-table queries | Qualify with `Expr.Prop("alias", "column")`, or use a view with `[ForeignColumn]` |
| `ConstFilter` seems ignored | `Column.Constant` must be convertible to the property type; for runtime conditions assign a `GenericSqlExpr` to `TableDefinition.ConstFilter` |
| UPDATE / DELETE always affects 0 rows | Check whether `ConstFilter` (including column constants) blocks the rows |
| Timestamp concurrency protection does nothing | Pass the old value as `timestamp` to `Update` / `UpdateAsync` and check the return value |
| `[Transaction]` has no effect | Requires `RegisterLiteOrm()`, a `public` method, and an interface proxy call |
| Writes are not visible inside a transaction | Queries in a transaction always use the primary; other sessions cannot see uncommitted rows |
| Read replica returns stale data | View DAO queries use the replica by default; for strong consistency, read inside a transaction or override `IsView => false` |
| Computed-column expression throws `NotSupportedException` | `ColumnDefinition.ExpressionExpr` only allows fixed SQL — `Expr.Value(...)` produces parameters |
| AOP / remote unavailable under AOT | Use `AddLiteOrm()`, manual transactions and service events |

## Related Links

- [Back to docs hub](../README.en.md)
- [API Index](./api-index.en.md)
- [Glossary](./glossary.en.md)
