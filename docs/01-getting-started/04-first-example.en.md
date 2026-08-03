# First End-to-End Example (Core Library Only)

This article walks through a minimal runnable example demonstrating the typical workflow of using **only the `LiteOrm` core library** (without `LiteOrm.Framework`, Autofac, or Castle dynamic proxies): manual initialization, defining entities, inserting data, querying data, and paginated queries.

> **Applicable scenarios**: console apps, batch scripts, projects without a DI container, or scenarios where you want full control over lifetimes.
>
> If you use ASP.NET Core and need Autofac integration, AOP transactions/permissions/logging, dynamic Controllers, and similar capabilities, see [First End-to-End Example (Framework)](./05-first-example-framework.en.md).

## 0. Project Setup

```bash
dotnet new console -n LiteOrmCoreDemo
cd LiteOrmCoreDemo
dotnet add package LiteOrm
dotnet add package Microsoft.Data.Sqlite
```

> The core library `LiteOrm` automatically brings in `LiteOrm.Common`, so you don't need to install it separately. This example uses SQLite, which requires no additional database service.

## 1. Define the Entity

```csharp
using LiteOrm.Common;

[Table("Users")]
public class User
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserName")]
    public string? UserName { get; set; }

    [Column("Age")]
    public int Age { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }
}
```

> - `[Table("Users")]`: maps to the `Users` table in the database.
> - `[Column("Id", IsPrimaryKey = true, IsIdentity = true)]`: primary key and auto-increment.
> - The entity class does not need to inherit from `ObjectBase`; a plain POCO works.

## 2. Manually Initialize LiteOrm

The core library does not provide `RegisterLiteOrm()`; you must build the dependency chain manually. Data source configuration supports two approaches: manual code-based setup, or reading from `IConfiguration` sources such as `appsettings.json`.

### Option A: Manual Code-Based Configuration

```csharp
using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Data.Sqlite;

// 1. Configure data source
var dataSourceProvider = new DataSourceProvider();
dataSourceProvider.AddDataSource(new DataSourceConfig
{
    Name = "DefaultConnection",
    ConnectionString = "Data Source=LiteOrmDemo.db",
    Provider = typeof(SqliteConnection).AssemblyQualifiedName,
    SyncTable = true  // auto-create table (recommended for development)
});
dataSourceProvider.SetDefaultDataSource("DefaultConnection");

// 2. Create connection pool factory
var poolFactory = new DAOContextPoolFactory(dataSourceProvider);
DAOContextPoolFactory.Set(() => poolFactory);

// 3. Create session manager and set as current session
var sessionManager = new SessionManager(poolFactory);
SessionManager.SetCurrent(() => sessionManager);

// 4. Create DAO and service
var objectDAO = new ObjectDAO<User>();
var objectViewDAO = new ObjectViewDAO<User>();
var userService = new EntityService<User>(objectDAO, objectViewDAO);
```

### Option B: Read from Configuration File

The core library includes a built-in `LoadConfiguration` extension method that loads data source configuration directly from the `LiteOrm` section of an `IConfiguration`, eliminating the need to call `AddDataSource` one by one.

First, prepare `appsettings.json`:

```json
{
  "LiteOrm": {
    "Default": "DefaultConnection",
    "DataSources": [
      {
        "Name": "DefaultConnection",
        "ConnectionString": "Data Source=LiteOrmDemo.db",
        "Provider": "Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite",
        "SyncTable": true
      }
    ]
  }
}
```

Then use `LoadConfiguration` in place of manual `AddDataSource`:

```csharp
using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Extensions.Configuration;

// 1. Read configuration file
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json")
    .Build();

// 2. Load data sources from the LiteOrm section via LoadConfiguration
var dataSourceProvider = new DataSourceProvider();
dataSourceProvider.LoadConfiguration(configuration.GetSection("LiteOrm"));

// 3. Create connection pool factory
var poolFactory = new DAOContextPoolFactory(dataSourceProvider);
DAOContextPoolFactory.Set(() => poolFactory);

// 4. Create session manager and set as current session
var sessionManager = new SessionManager(poolFactory);
SessionManager.SetCurrent(() => sessionManager);

// 5. Create DAO and service
var objectDAO = new ObjectDAO<User>();
var objectViewDAO = new ObjectViewDAO<User>();
var userService = new EntityService<User>(objectDAO, objectViewDAO);
```

> Using `LoadConfiguration` requires additionally installing the `Microsoft.Extensions.Configuration` and `Microsoft.Extensions.Configuration.Json` packages. The core library itself only depends on `Microsoft.Extensions.Configuration.Abstractions` (which provides the `IConfiguration` interface).

> **Line-by-line explanation**:
> - `DataSourceProvider`: manages data source configuration. Add sources explicitly via `AddDataSource`, or load them in bulk from `IConfiguration` via `LoadConfiguration`; designate the default via `SetDefaultDataSource` or the `Default` key in the config section.
> - `LiteOrmSqlFunctionInitializer.Initialize()`: SQL function mappings are automatically registered via SqlBuilder's static constructor on first access—no manual call needed.
> - `DAOContextPoolFactory`: creates connection pools based on data source configuration and manages connection acquisition and recycling. Call `Set` to register it as the global singleton so DAOs can resolve the provider type internally via the static property.
> - `SessionManager`: manages database sessions, transactions, and async context. `SetCurrent` sets it as the session for the current async context.
> - `ObjectDAO<T>` / `ObjectViewDAO<T>`: data access objects for insert/update/delete and queries, respectively. Both have parameterless constructors and obtain global singletons via `TableInfoProvider.Instance` and `BulkProviderFactory.Instance` internally, no manual injection needed.
> - `EntityService<T>`: a business service wrapping the DAOs, providing methods such as `InsertAsync`, `SearchAsync`, `UpdateAsync`, and `DeleteAsync`.

## 2.5 Register and Resolve Services via ServiceProvider

The previous section showed how to build the dependency chain entirely by hand. If you prefer to use `Microsoft.Extensions.DependencyInjection` (MS DI) for lifetime management **without introducing LiteOrm.Framework / Autofac**, you can manually register the core types into an `IServiceCollection` and build a `ServiceProvider`.

This approach suits scenarios that need dependency injection but not AOP interception — unit tests, lightweight web APIs, or projects that want per-scope `SessionManager` lifetime management.

```csharp
using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// 1. Read configuration file and load data sources via LoadConfiguration
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json")
    .Build();

var dataSourceProvider = new DataSourceProvider();
dataSourceProvider.LoadConfiguration(configuration.GetSection("LiteOrm"));

// 2. Create pool factory and set as global singleton
var poolFactory = new DAOContextPoolFactory(dataSourceProvider);
DAOContextPoolFactory.Set(() => poolFactory);

// 3. Register services into the DI container
var services = new ServiceCollection();

// Singleton services
services.AddSingleton(dataSourceProvider);
services.AddSingleton(poolFactory);
services.AddSingleton<TableInfoProvider, AttributeTableInfoProvider>();
services.AddSingleton<BulkProviderFactory>();

// Scoped services — each scope gets its own SessionManager
services.AddScoped<SessionManager>();

// Generic DAOs and services (Scoped)
services.AddScoped(typeof(ObjectDAO<>));
services.AddScoped(typeof(ObjectViewDAO<>));
services.AddScoped(typeof(EntityService<>));
services.AddScoped(typeof(EntityViewService<>));

// 4. Build the ServiceProvider
var serviceProvider = services.BuildServiceProvider();

// 5. Delegate SessionManager resolution to the ServiceProvider
//    SessionManager.SetCurrent accepts a factory delegate that is
//    executed lazily on first access to SessionManager.Current and cached
SessionManager.SetCurrent(() => serviceProvider.GetService<SessionManager>());
```

After registration, create a scope and resolve services to perform database operations:

```csharp
// Create a scope (each scope gets its own SessionManager instance)
using var scope = serviceProvider.CreateScope();
var sp = scope.ServiceProvider;

// Resolve service from the DI container
var userService = sp.GetRequiredService<EntityService<User>>();

// Subsequent operations are identical to the manual approach
var user = new User
{
    UserName = "admin",
    Age = 30,
    CreateTime = DateTime.Now
};
await userService.InsertAsync(user);
Console.WriteLine($"Insert succeeded, auto-increment Id = {user.Id}");
```

> **Scopes and SessionManager**:
> `SessionManager` is registered as Scoped, so each `CreateScope()` call produces an independent instance. However, `SessionManager.SetCurrent` sets the session factory for the **current async context** (`AsyncLocal`) — the delegate executes only once on first access and the result is cached.
>
> In multi-scope scenarios (e.g., web requests) where each scope needs its own `SessionManager`, call `SessionManager.SetCurrent` when entering each scope:
>
> ```csharp
> using var scope = serviceProvider.CreateScope();
> var scopedSp = scope.ServiceProvider;
> // Set this scope's SessionManager for the current async context
> SessionManager.SetCurrent(() => scopedSp.GetService<SessionManager>());
> var userService = scopedSp.GetRequiredService<EntityService<User>>();
> ```

Release resources when the application exits:

```csharp
// Dispose the ServiceProvider (auto-disposes all singletons and unreleased scoped services)
await serviceProvider.DisposeAsync();
poolFactory.Dispose();
```

## 3. Insert a Record

```csharp
var user = new User
{
    UserName = "admin",
    Age = 30,
    CreateTime = DateTime.Now
};

await userService.InsertAsync(user);
Console.WriteLine($"插入成功，自增 Id = {user.Id}");
```

> `InsertAsync` inserts the entity into the database. If `Id` is an auto-increment column (`IsIdentity = true`), the entity's `Id` property is auto-populated after insertion.

## 4. Run a Query

```csharp
// 条件查询
var adults = await userService.SearchAsync(u => u.Age >= 18);
Console.WriteLine($"成年用户数量：{adults.Count}");

// 单条查询
var admin = await userService.SearchOneAsync(u => u.UserName == "admin");
Console.WriteLine($"查询到：{admin?.UserName}, Age = {admin?.Age}");
```

## 5. Pagination

```csharp
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderByDescending(u => u.CreateTime)
          .Skip(0)
          .Take(10)
);
Console.WriteLine($"分页结果：{page.Count} 条");
```

## 6. Full Call Loop

```csharp
// 1. 插入
var user = new User
{
    UserName = "demo-user",
    Age = 26,
    CreateTime = DateTime.Now
};
await userService.InsertAsync(user);

// 2. 查询
var current = await userService.SearchOneAsync(u => u.Id == user.Id);

// 3. 更新
current!.UserName = "updated-demo-user";
await userService.UpdateAsync(current);

// 4. 统计
var count = await userService.CountAsync(u => u.Age >= 18);

// 5. 判断是否存在
var exists = await userService.ExistsAsync(u => u.UserName == "updated-demo-user");

// 6. 删除
if (exists)
{
    await userService.DeleteAsync(current);
}

Console.WriteLine($"Count={count}, Exists={exists}");
```

## 7. Manual Transaction

The core library does not provide AOP declarative transactions (the `[Transaction]` attribute requires the Framework's Castle interceptor), but you can control transactions manually via `SessionManager`:

```csharp
sessionManager.BeginTransaction();
try
{
    await userService.InsertAsync(new User { UserName = "user1", Age = 20, CreateTime = DateTime.Now });
    await userService.InsertAsync(new User { UserName = "user2", Age = 25, CreateTime = DateTime.Now });
    sessionManager.Commit();
}
catch
{
    sessionManager.Rollback();
    throw;
}
```

## 8. Resource Cleanup

In the core-library scenario, `SessionManager` and `DAOContextPoolFactory` hold database connections and should be disposed when done:

```csharp
// 应用退出时
sessionManager.Dispose();
poolFactory.Dispose();

// 如果使用了 SyncTable=true，数据库文件会自动创建
// 如果是 SQLite in-memory（Data Source=:memory:），连接关闭后数据丢失
```

## 9. Core Library vs. Framework Capability Comparison

| Capability | Core Library Only (`LiteOrm`) | Framework (`LiteOrm.Framework`) |
|------|----------------------|--------------------------------|
| Entity mapping / CRUD / queries | ✅ | ✅ |
| Manual transactions | ✅ `SessionManager.BeginTransaction()` | ✅ |
| Declarative transactions `[Transaction]` | ❌ | ✅ AOP interception |
| Permission filtering `[ServicePermission]` | ❌ | ✅ AOP interception |
| Automatic logging `[ServiceLog]` / `[Log]` | ❌ | ✅ AOP interception |
| DI container registration | ✅ manual MS DI registration (see §2.5) | ✅ `RegisterLiteOrm()` (Autofac) |
| Dynamic Controller generation | ❌ | ✅ |
| Config file binding | ✅ `LoadConfiguration` reads from `IConfiguration` | ✅ `appsettings.json` auto-binding |
| Bulk import `IBulkProvider` | ❌ (Factory can be built but not registered) | ✅ auto-registered |

> If you later need AOP capabilities, you can migrate smoothly from the core library to the Framework; entity definitions and DAO/Service usage remain identical.

## 10. Common Beginner Issues

### Issue 1: `SQLite Error 1: 'no such table: Users'`

**Cause**: The `Users` table does not exist in the database.

**Solution**: Set `SyncTable = true` in the data source configuration so LiteOrm auto-creates the table from the entity definition (recommended for development). Alternatively, run the table-creation SQL manually.

### Issue 2: `Object reference not set to instance` or `SessionManager.Current` is null

**Cause**: You forgot to call `SessionManager.SetCurrent(() => sessionManager)`.

**Solution**: Make sure to call `SessionManager.SetCurrent(() => sessionManager)` before creating service instances; otherwise the DAO cannot obtain a database connection when executing SQL.

### Issue 3: `Function 'XXX' is not supported` exception

**Cause**: This should not happen anymore as SQL function mappings are now auto-registered. If you encounter this, it means the function is not in the built-in mapping.

**Solution**: Register the function's SQL handler manually via `sqlBuilder.RegisterFunctionSqlHandler(...)`.

## Run Verification Checklist

- [ ] `dotnet build` compiles without errors.
- [ ] The initialization code calls `SessionManager.SetCurrent(...)` (manual construction or ServiceProvider approach).
- [ ] The initialization code calls `DAOContextPoolFactory.Set(() => poolFactory)`.
- [ ] When using the ServiceProvider approach, `SessionManager` is registered as Scoped and `SetCurrent` is called when entering a scope.
- [ ] Entity classes are annotated with `[Table]` and `[Column]` attributes.
- [ ] Insert and query operations return the expected results.
- [ ] `ServiceProvider` (or `SessionManager`) and `DAOContextPoolFactory` are disposed before the application exits.

## Related Links

- [Back to docs hub](../README.md)
- [Installation](./02-installation.en.md)
- [Configuration and Registration](../06-framework/01-configuration-and-registration.en.md)
- [First End-to-End Example (Framework)](./05-first-example-framework.en.md)
- [Entity Mapping and Data Sources](../02-core-usage/01-entity-mapping.en.md)
- [Query Overview](../02-core-usage/04-query-overview.en.md)
- [CRUD Guide](../02-core-usage/03-crud-guide.en.md)
