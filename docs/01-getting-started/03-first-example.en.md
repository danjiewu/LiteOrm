# First End-to-End Example (Base Library Only)

This article walks through a minimal runnable example demonstrating the typical workflow of using **only the `LiteOrm` base library** (without `LiteOrm.DependencyInjection`, Autofac, or Castle dynamic proxies): initializing with `AddLiteOrm()`, defining entities, and performing insert, query, and pagination operations through the generic entity service `EntityService<User>`.

> **Applicable scenarios**: console apps, batch scripts, unit tests, or projects that want MS DI-managed lifetimes without AOP interception.
>
> If you use ASP.NET Core and need Autofac integration, AOP transactions/permissions/logging, and similar capabilities, see [First End-to-End Example (DI)](./05-first-example-di.en.md).

## 0. Project Setup

```bash
dotnet new console -n LiteOrmCoreDemo
cd LiteOrmCoreDemo
dotnet add package LiteOrm
dotnet add package Microsoft.Data.Sqlite
```

> The base library `LiteOrm` automatically brings in `LiteOrm.Common`, so you don't need to install it separately. This example uses SQLite, which requires no additional database service.

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

## 2. Initialize LiteOrm

The recommended way is to use the base library's built-in `AddLiteOrm()` (plain MS DI) to complete initialization — it loads data sources from the `LiteOrm` section of `IConfiguration`, registers core services, and supports registering custom services, without manually `new`-ing various low-level objects.

> **Note**: Since 8.1, `RegisterLiteOrm()` moved from the `LiteOrm` base package to `LiteOrm.DependencyInjection` (namespace changed from `LiteOrm` to `LiteOrm.DependencyInjection`). If you need Autofac integration / AOP, use `RegisterLiteOrm()` from the `LiteOrm.DependencyInjection` package; the base library only provides `AddLiteOrm()` (plain MS DI).

### 2.1 Initialize via AddLiteOrm (Recommended)

`AddLiteOrm()` lives in the base library (namespace `LiteOrm`, no extra package required). It loads data sources from the `LiteOrm` section of `IConfiguration` and registers core services such as `SessionManager`, DAOs, and generic entity services. When using it, there is no need to call `SessionManager.SetCurrent(...)` manually — the framework binds the current scope instance automatically when registering `SessionManager`.

#### Step 1: Prepare the Data Source in appsettings.json

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

#### Step 2: Call AddLiteOrm to Register

```csharp
using LiteOrm;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

// 1. Create a Host (loads appsettings.json automatically) and register LiteOrm on it
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLiteOrm();

// 2. Build the Host; its Services property is the ServiceProvider
var host = builder.Build();
var serviceProvider = host.Services;
```

> **What does `AddLiteOrm()` register?**
> - Singleton: `IDataSourceProvider` (loaded from the `LiteOrm` section of `IConfiguration`), `DAOContextPoolFactory`, `TableInfoProvider`.
> - Scoped: `SessionManager`, generic `ObjectDAO<>` / `ObjectViewDAO<>`, `EntityService<>` / `EntityViewService<>` (including interface registrations such as `IObjectDAO<>`, `IEntityService<>`).
> - `AddLiteOrm()` binds `SessionManager.Current` automatically when registering `SessionManager`, so no manual `SessionManager.SetCurrent(...)` call is required.
> - If `AutoRegisterServices = true` (default), custom services and DAOs marked with `[AutoRegister]` are auto-registered as well (runtime assembly scanning in non-AOT mode; registered at compile time by the source generator in AOT mode). This example uses the generic entity service directly, so no custom service is needed.

> **Which packages do I need?** `AddLiteOrm()` resolves `IConfiguration` from the DI container. `Host.CreateApplicationBuilder` automatically loads `appsettings.json` and registers configuration (including `IConfiguration`), so a console app only needs the `Microsoft.Extensions.Hosting` package — no manual `ServiceCollection` construction required.

#### Step 3: Create a Scope and Resolve Services

```csharp
// Create a scope (each scope gets its own SessionManager and Scoped service instances)
using var scope = serviceProvider.CreateScope();
var sp = scope.ServiceProvider;

// Resolve the generic entity service (EntityService<> registered by AddLiteOrm)
var userService = sp.GetRequiredService<EntityService<User>>();
```

> **Scopes and SessionManager**:
> `SessionManager` is registered as Scoped, so each `CreateScope()` call (e.g., each web request) produces an independent `SessionManager` instance. `AddLiteOrm()` binds `SessionManager.Current` automatically when registering `SessionManager` (resolving to the scope's instance), so no manual `SetCurrent` call or middleware is required.
>
> Tip: with `LiteOrm.DependencyInjection` (Autofac) no configuration is needed either — `RegisterLiteOrm()` enables scope tracking automatically and updates the current session on scope enter/exit.

Release resources when the application exits:

```csharp
// Dispose the host (auto-disposes singletons and unreleased scoped services, including the connection pool)
await host.DisposeAsync();
```

### 2.2 Two Ways to Register Custom Services (Optional)

This example uses the generic `EntityService<>` directly, so no custom service is needed. If your project needs to encapsulate business methods into services, register them with one of the following two approaches:

#### Method 1: `[AutoRegister]` Automatic Registration (Recommended)

Add the `[AutoRegister]` attribute to your custom service implementation class, and `AddLiteOrm()` scans and registers it automatically when `AutoRegisterServices = true` (default). The attribute supports specifying lifetime, registration policy, and key:

```csharp
[AutoRegister(Lifetime.Scoped, RegisterPolicy.All)]         // register implementation type + interfaces (default)
public class UserAppService : IUserAppService { /* ... */ }

[AutoRegister(Lifetime.Transient, RegisterPolicy.Interface)] // register interfaces only
public class ReportService : IReportService { /* ... */ }

[AutoRegister(Lifetime.Singleton, RegisterPolicy.Self)]      // register implementation type only
public class CacheHelper { /* ... */ }
```

> `Lifetime`: `Singleton` / `Scoped` / `Transient`; `RegisterPolicy`: `All` (type + interfaces, default) / `Interface` (interfaces only) / `Self` (type only). Custom services that depend on Scoped services should use `Scoped` or `Transient` lifetimes.

#### Method 2: Manual Registration via `options.ConfigureServices`

`LiteOrmOptions.ConfigureServices` runs after the core services are registered; register any custom service here, exactly like normal MS DI registration:

```csharp
builder.Services.AddLiteOrm(options =>
{
    options.ConfigureServices = services =>
    {
        services.AddScoped<IUserAppService, UserAppService>();
        services.AddSingleton<ICacheService, CacheService>();
    };
});
```

> The two methods can be mixed. Registrations in `ConfigureServices` run after `[AutoRegister]` auto-registration; for the same type, the later registration wins.

## 3. Full Call Loop (Insert, Query, Pagination)

The following closed-loop example walks through insert, conditional query, single-record query, pagination, update, count, existence check, and delete in sequence:

```csharp
// 1. 插入
var user = new User
{
    UserName = "demo-user",
    Age = 26,
    CreateTime = DateTime.Now
};
await userService.InsertAsync(user);
Console.WriteLine($"插入成功，自增 Id = {user.Id}");

// 2. 条件查询
var adults = await userService.SearchAsync(u => u.Age >= 18);
Console.WriteLine($"成年用户数量：{adults.Count}");

// 3. 单条查询
var current = await userService.SearchOneAsync(u => u.Id == user.Id);
Console.WriteLine($"查询到：{current?.UserName}, Age = {current?.Age}");

// 4. 分页查询
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderByDescending(u => u.CreateTime)
          .Skip(0)
          .Take(10)
);
Console.WriteLine($"分页结果：{page.Count} 条");

// 5. 更新
current!.UserName = "updated-demo-user";
await userService.UpdateAsync(current);

// 6. 统计
var count = await userService.CountAsync(u => u.Age >= 18);

// 7. 判断是否存在
var exists = await userService.ExistsAsync(u => u.UserName == "updated-demo-user");

// 8. 删除
if (exists)
{
    await userService.DeleteAsync(current);
}

Console.WriteLine($"Count={count}, Exists={exists}");
```

> - `InsertAsync`: inserts the entity into the database. If `Id` is an auto-increment column (`IsIdentity = true`), the entity's `Id` property is auto-populated after insertion.
> - `SearchAsync`: returns a collection; `SearchOneAsync`: returns a single record, or `null` when there is no match.
> - `SearchAsync`'s query builder supports combining `Where` / `OrderByDescending` / `Skip` / `Take` for pagination and sorting.

## 4. Base Library vs. LiteOrm.DependencyInjection Capability Comparison

| Capability | Base Library Only (`LiteOrm`) | Host Integration (`LiteOrm.DependencyInjection`) |
|------|----------------------|--------------------------------|
| Entity mapping / CRUD / queries | ✅ | ✅ |
| Manual transactions | ✅ `SessionManager.BeginTransaction()` | ✅ |
| Declarative transactions `[Transaction]` | ❌ | ✅ AOP interception |
| Permission filtering `[ServicePermission]` | ❌ | ✅ AOP interception |
| Automatic logging `[ServiceLog]` / `[Log]` | ❌ | ✅ AOP interception |
| DI container registration | ✅ `AddLiteOrm()` (MS DI, see above) | ✅ `RegisterLiteOrm()` (Autofac) |
| Config file binding | ✅ `LoadConfiguration` or `AddLiteOrm()` reads `IConfiguration` | ✅ `appsettings.json` auto-binding |
| Bulk import `IBulkProvider` | ✅ set `SqlBuilder.BulkProvider` directly | ✅ set `SqlBuilder.BulkProvider` directly |

> If you later need AOP capabilities, you can migrate smoothly from the base library to the host integration (`LiteOrm.DependencyInjection`); entity definitions and DAO/Service usage remain identical.

## 5. Common Beginner Issues

### Issue 1: `SQLite Error 1: 'no such table: Users'`

**Cause**: The `Users` table does not exist in the database.

**Solution**: Set `SyncTable = true` in the data source configuration so LiteOrm auto-creates the table from the entity definition (recommended for development). Alternatively, run the table-creation SQL manually.

### Issue 2: `Object reference not set to instance` or `SessionManager.Current` is null

**Cause**: In manual construction, you forgot to call `SessionManager.SetCurrent(() => sessionManager)` (with `AddLiteOrm()` the binding is automatic, so this does not occur).

**Solution**: In manual construction scenarios, make sure to call `SessionManager.SetCurrent(() => sessionManager)` before creating service instances; with `AddLiteOrm()` no manual call is needed — the framework binds automatically. Otherwise the DAO cannot obtain a database connection when executing SQL.

### Issue 3: `Function 'XXX' is not supported` exception

**Cause**: This should not happen anymore as SQL function mappings are now auto-registered. If you encounter this, it means the function is not in the built-in mapping.

**Solution**: Register the function's SQL handler manually via `sqlBuilder.RegisterFunctionSqlHandler(...)`.

## Run Verification Checklist

- [ ] `dotnet build` compiles without errors.
- [ ] For manual construction, `SessionManager.SetCurrent(...)` is called; with `AddLiteOrm()` the binding is automatic — no manual call needed.
- [ ] Entity classes are annotated with `[Table]` and `[Column]` attributes.
- [ ] Insert, query, and pagination operations return the expected results.
- [ ] `await host.DisposeAsync()` is called before the application exits to release resources (connection pool, etc.).

## Related Links

- [Back to docs hub](../README.md)
- [Installation](./02-installation.en.md)
- [Configuration Reference](../05-reference/01-configuration-reference.en.md)
- [First End-to-End Example (DI)](./05-first-example-di.en.md)
- [Entity Mapping and Data Sources](../02-core-usage/01-entity-mapping.en.md)
- [Query Overview](../02-core-usage/04-query-overview.en.md)
- [CRUD Guide](../02-core-usage/03-crud-guide.en.md)
