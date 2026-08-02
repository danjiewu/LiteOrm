# LiteOrm 8.1.0 Upgrade Guide & Breaking Changes

This guide is for users upgrading from `v8.0.x` to `v8.1.0`. It describes the breaking changes, their impact, and how to migrate.

## Version Overview

| Package | Old Version | New Version |
|---|---|---|
| `LiteOrm` | 8.0.21 | 8.1.0 |
| `LiteOrm.Common` | 8.0.21 | 8.1.0 |
| `LiteOrm.Framework` | 8.0.22 | 8.1.0 |

> Note: `LiteOrm.Remote` (8.0.3) and `LiteOrm.Remote.Server` (8.0.3) versions are unchanged.

---

## Summary of Changes

| # | Change | Severity |
|---|---|---|
| 1 | `AutoRegisterAttribute` moved from `LiteOrm.Common` to `LiteOrm.Framework`; namespace changed to `LiteOrm.Framework` | High |
| 2 | DI entry points merged into a single `RegisterLiteOrm()` (Autofac + Castle); `RegisterLiteOrmFramework()` and MS DI `RegisterLiteOrm()` removed | High |
| 3 | `AutoRegisterAttribute.Lifetime` switched to built-in `ServiceLifetime`; default changed from `Singleton` to `Scoped` | Medium |
| 4 | Core `LiteOrm` DAO/Service no longer use `[AutoRegister]`; they are explicitly registered by `LiteOrm.Framework` | High |
| 5 | `LiteOrm` core removed the `Microsoft.Extensions.Hosting` dependency; DI integration is provided only by `LiteOrm.Framework` | High |
| 6 | `DataSourceProvider` switched to an explicit configuration API and no longer accepts `IConfiguration` | Medium |
| 7 | `BulkProvider` now uses `BulkProviderAttribute` to declare the database connection type | Medium |
| 8 | `[AutoRegister(false)]` markers removed from core interfaces | Low |

---

## 1. `AutoRegisterAttribute` Moved to `LiteOrm.Framework`

### What Changed

`AutoRegisterAttribute` moved from the `LiteOrm.Common` assembly to the `LiteOrm.Framework` assembly.

- **Namespace changed**: from `LiteOrm.Common` to `LiteOrm.Framework`. Every source file using `[AutoRegister]` must add `using LiteOrm.Framework;`.
- **Assembly changed**: the type now lives in `LiteOrm.Framework.dll`.

### Impact

- Projects that reference only `LiteOrm` / `LiteOrm.Common` (not `LiteOrm.Framework`) can no longer compile code containing `[AutoRegister]`.
- Source files using the `LiteOrm.Common` namespace without `using LiteOrm.Framework;` will fail to compile.
- Assembly-level reflection such as `typeof(AutoRegisterAttribute).Assembly` now returns the `LiteOrm.Framework` assembly.

### Migration

1. Ensure projects using `[AutoRegister]` reference the `LiteOrm.Framework` package:
   ```xml
   <PackageReference Include="LiteOrm.Framework" Version="8.1.0" />
   ```
2. Add the following to every source file using `[AutoRegister]`:
   ```csharp
   using LiteOrm.Framework;
   ```

The `Lifetime` enum was removed; `AutoRegisterAttribute.Lifetime` now uses the built-in `ServiceLifetime` (see section 3).

---

## 2. DI Entry Points Merged into a Single `RegisterLiteOrm()`

### What Changed

`LiteOrm.Framework` previously offered two DI integration entry points. They are now merged into one:

- ~~`RegisterLiteOrmFramework()`~~ (Autofac + Castle DynamicProxy) → **removed**
- ~~`RegisterLiteOrm()`~~ (MS DI) → **removed**
- **`RegisterLiteOrm()`** (Autofac + Castle DynamicProxy) → the only entry point

The merged `RegisterLiteOrm()` lives in the `LiteOrm.Framework` namespace in `LiteOrmServiceExtensions`. It uses the Autofac container with Castle DynamicProxy interception support. `LiteOrm.Framework\FrameworkServiceExtensions.cs` was deleted.

### Impact

- Projects using `RegisterLiteOrmFramework()` must switch to `RegisterLiteOrm()`.
- The old MS DI `RegisterLiteOrm()` from the `LiteOrm` core package (`LiteOrm/Classes/LiteOrmServiceExtensions.cs`) was removed along with the core package's Hosting dependency.

### Migration

```csharp
// Old way (any entry point)
Host.CreateDefaultBuilder(args).RegisterLiteOrmFramework();
// or
Host.CreateDefaultBuilder(args).RegisterLiteOrm();

// New way (single entry point)
Host.CreateDefaultBuilder(args).RegisterLiteOrm();
```

The optional `Action<LiteOrmOptions>` parameter is unchanged:

```csharp
Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm(options =>
    {
        options.Assemblies = new[] { typeof(MyService).Assembly };
        options.RegisterSqlBuilder("MyDataSource", new MySqlBuilder());
    });
```

---

## 3. `AutoRegisterAttribute.Lifetime` Default Changed to `Scoped` and Switched to Built-in `ServiceLifetime`

### What Changed

- The type of `AutoRegisterAttribute.Lifetime` changed from the custom `Lifetime` enum to the built-in `ServiceLifetime` (`Microsoft.Extensions.DependencyInjection` namespace); the `Lifetime` enum in `LiteOrm.Common` was removed.
- The default value of `AutoRegisterAttribute.Lifetime` changed from `Singleton` to `Scoped`. Types auto-registered without an explicit lifetime are now created per scope (one instance per request/`LifetimeScope`).

### Impact

- Types using `[AutoRegister]` **without an explicit lifetime** change from singleton to scoped behavior.
- Code referencing the `Lifetime` enum from `LiteOrm.Common` must switch to `ServiceLifetime` (add `using Microsoft.Extensions.DependencyInjection;`).
- Types that explicitly set `Lifetime` (e.g., `[AutoRegister(ServiceLifetime.Singleton)]`) are unaffected.

### Migration

- Replace `[AutoRegister(Lifetime.Scoped)]` style usages with `[AutoRegister(ServiceLifetime.Scoped)]` and add `using Microsoft.Extensions.DependencyInjection;`.
- To keep a type singleton, declare it explicitly:
  ```csharp
  [AutoRegister(ServiceLifetime.Singleton)]
  public class MyService : IMyService { }
  ```
- For stateless services, consider `ServiceLifetime.Transient` for a smaller footprint:
  ```csharp
  [AutoRegister(ServiceLifetime.Transient)]
  public class MyService : IMyService { }
  ```

---

## 4. Core DAO/Service `[AutoRegister]` Removed

### What Changed

The following types in the `LiteOrm` core package **no longer carry `[AutoRegister]`** and are no longer auto-registered by the `RegisterAutoService` assembly scan:

- `EntityService<T, TView>`, `EntityService<T>`, `EntityViewService<TView>`
- `ObjectDAO<T>`, `ObjectViewDAO<T>`, `DataDAO<T>`, `DataViewDAO<T>`, `DAOBase`
- `AttributeTableInfoProvider`, `BulkProviderFactory`, `DdlGen`

### Impact

In `v8.0.x`, these core types relied on `[AutoRegister]` to be discovered and registered. After upgrading, if you keep using the old auto-registration flow, these types will **not** be registered, and resolving `IEntityService<T>`, `ObjectDAO<T>`, etc. from DI will fail.

### Migration

`AddCoreLiteOrmServices()` in `LiteOrm.Framework` (invoked by the single `RegisterLiteOrm()`) now **explicitly registers** these core types. Users who use the recommended `Host` integration need no changes:

```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()
    .Build();
```

The explicit registrations include:

```csharp
services.AddScoped(typeof(EntityService<>));
services.AddScoped(typeof(IEntityService<>), typeof(EntityService<>));
services.AddScoped(typeof(IEntityServiceAsync<>), typeof(EntityService<>));
services.AddScoped(typeof(EntityViewService<>));
services.AddScoped(typeof(IEntityViewService<>), typeof(EntityViewService<>));
services.AddScoped(typeof(IEntityViewServiceAsync<>), typeof(EntityViewService<>));
services.AddScoped(typeof(ObjectDAO<>));
services.AddScoped(typeof(IObjectDAO<>), typeof(ObjectDAO<>));
services.AddScoped(typeof(ObjectViewDAO<>));
services.AddScoped(typeof(IObjectViewDAO<>), typeof(ObjectViewDAO<>));
services.AddScoped(typeof(DataDAO<>));
services.AddScoped(typeof(DataViewDAO<>));
services.AddScoped(typeof(IDataViewDAO<>), typeof(DataViewDAO<>));
services.AddSingleton<TableInfoProvider, AttributeTableInfoProvider>();
services.AddSingleton<BulkProviderFactory>();
```

> Note: Not relying on `[AutoRegister]` scanning means the scan surface is smaller and registration behavior is more deterministic. User-defined types (e.g., business services) can still use `[AutoRegister]` (now defined in `LiteOrm.Framework`) for auto-registration. For user-defined services resolved via interfaces, consider declaring `[AutoRegister(ServiceLifetime.Scoped, typeof(IMyService))]` explicitly.

---

## 5. `LiteOrm` Core Removed Hosting Dependency

### What Changed

The `LiteOrm` core project dropped the `Microsoft.Extensions.Hosting` package reference, keeping only `Microsoft.Extensions.Logging.Abstractions` (for `ILogger` calls).

The following types moved from `LiteOrm` core to `LiteOrm.Framework`:

| Old Location (LiteOrm) | New Location (LiteOrm.Framework) |
|---|---|
| `LiteOrmServiceExtensions` (MS DI `RegisterLiteOrm`) | Deleted, replaced by `LiteOrmServiceExtensions` (Autofac `RegisterLiteOrm`) |
| `LiteOrmCoreInitializer` (`IHostedService` table sync) | `LiteOrm.Framework\LiteOrmCoreInitializer.cs` |

### Impact

- Projects using `IHostBuilder.RegisterLiteOrm()` must reference `LiteOrm.Framework`.
- The only DI integration entry point is `RegisterLiteOrm()` in `LiteOrm.Framework` (Autofac + Castle DynamicProxy).

### Migration

```csharp
// Old way (only referenced LiteOrm)
Host.CreateDefaultBuilder(args).RegisterLiteOrm();

// New way (requires LiteOrm.Framework)
Host.CreateDefaultBuilder(args).RegisterLiteOrm();
```

---

## 6. `DataSourceProvider` Explicit Configuration API

### What Changed

The `LiteOrm` core `DataSourceProvider` **no longer accepts `IConfiguration`**. Connection configuration is provided explicitly:

```csharp
var provider = new DataSourceProvider();
provider.AddDataSource(new DataSourceConfig
{
    Name = "DefaultConnection",
    ConnectionString = "Data Source=demo.db",
    Provider = typeof(SqliteConnection).AssemblyQualifiedName
});
provider.SetDefaultDataSource("DefaultConnection");
```

New chainable APIs:

| Method | Description |
|---|---|
| `AddDataSource(DataSourceConfig)` | Add/overwrite a data source; returns `this` for chaining |
| `SetDefaultDataSource(string)` | Set the default data source |
| `RemoveDataSource(string)` | Remove a data source |
| `GetDataSource(string?)` | Get a data source (empty name falls back to default/first) |

### Impact

- In the `LiteOrm` core, code that constructs `DataSourceProvider` from `IConfiguration` must be rewritten.
- Users of `LiteOrm.Framework` are **unaffected**: Framework added `DataSourceProviderExtensions.LoadConfiguration(IConfiguration)` which reads the `LiteOrm` config section and populates the provider inside `AddCoreLiteOrmServices()`.

### Migration (when using the core package directly)

```csharp
// Old way
var provider = new DataSourceProvider(configuration.GetSection("LiteOrm"));

// New way (core)
var provider = new DataSourceProvider();
provider.AddDataSource(new DataSourceConfig { ... });

// New way (Framework, auto-loaded from appsettings.json)
// handled internally by RegisterLiteOrm()
```

---

## 7. `BulkProviderAttribute` Replaces `[AutoRegister(Key = ...)]`

### What Changed

Custom `IBulkProvider` implementations previously declared their target database connection type via `[AutoRegister(Key = typeof(XxxConnection))]`. They now use the new `BulkProviderAttribute`:

```csharp
// Old way
[AutoRegister(Key = typeof(MySqlConnection))]
public class MySqlBulkCopyProvider : IBulkProvider { }

// New way
[BulkProvider(typeof(MySqlConnection))]
public class MySqlBulkCopyProvider : IBulkProvider { }
```

`BulkProviderFactory` now reads the mapping from `BulkProviderAttribute.DbConnectionType`.

### Impact

Custom `IBulkProvider` classes must update the attribute. Users without custom bulk providers are unaffected.

### Migration

Replace `[AutoRegister(Key = typeof(Conn))]` with `[BulkProvider(typeof(Conn))]`. `BulkProviderAttribute` is defined in `LiteOrm.Common` (namespace `LiteOrm.Common.Attributes`).

---

## 8. `[AutoRegister(false)]` Removed from Core Interfaces

### What Changed

The `[AutoRegister(false)]` markers were removed from these non-generic marker interfaces (used to exclude them from being registered as service types):

- `IObjectDAO`, `IObjectViewDAO`, `IObjectDAOAsync`
- `IEntityService`, `IEntityServiceAsync`, `IEntityViewService`, `IEntityViewServiceAsync`

`LiteOrm.Framework`'s scan logic (`RegisterAutoService`) now excludes these marker interfaces by their full names (`IsExcludedMarkerInterface`), preserving prior behavior.

### Impact

No source-level impact. Only relevant if your code reads `[AutoRegister]` from these interfaces via reflection (unlikely).

---

## FAQ

### Q1: `IEntityService<T>` can't be resolved from DI after upgrade?

Make sure the host uses `RegisterLiteOrm()` (from `LiteOrm.Framework`); it calls `AddCoreLiteOrmServices()` which explicitly registers the core entity services and DAOs.

### Q2: My business service uses `[AutoRegister]`. Does it still work?

Yes. `[AutoRegister]` is now defined in `LiteOrm.Framework`. As long as the project references `LiteOrm.Framework` and adds `using LiteOrm.Framework;`, auto-registration of user-defined types works as before. Note the default lifetime is now `Scoped`; declare `[AutoRegister(ServiceLifetime.Singleton)]` explicitly if you need a singleton.

### Q3: My business service doesn't declare `ServiceTypes`. Can it still be resolved via its interface?

Yes. `GetServiceTypes` keeps the interface-inference logic: when `ServiceTypes` is not specified, it infers the non-system-namespace interfaces implemented by the type as service types (excluding the non-generic marker interfaces of `LiteOrm.Common` / `LiteOrm.Service`). User-defined services resolved via interfaces need no explicit `ServiceTypes`.

### Q4: I only want the `LiteOrm` core without `LiteOrm.Framework`?

That's supported. The core package is now fully independent of DI integration and supports pure manual construction (`new EntityService<T>(...)`, `new ObjectDAO<T>(...)`). However, you must configure connections manually via `DataSourceProvider.AddDataSource(...)`.

### Q5: Why was the `AutoRegisterAttribute` namespace changed to `LiteOrm.Framework`?

To correctly reflect the assembly ownership, the namespace was adjusted to `LiteOrm.Framework` when the attribute moved. Source files must add `using LiteOrm.Framework;`.

---

## Verification

After upgrading, ensure:

```bash
dotnet build .\LiteOrm.sln
dotnet test .\LiteOrm.sln
```

The full test suite (1922 tests) passing is the verification baseline for this release.
