# LiteOrm 8.1 Upgrade Guide

This guide describes the changes required when upgrading to v8.1.x from **8.0.20 or earlier**. It is organized by version; under each version, changes are grouped into **Breaking Changes**, **New Features**, and **Improvements**.

## Version Overview

| Package | New Version |
|---|---|
| `LiteOrm` | 8.1.1 |
| `LiteOrm.Common` | 8.1.1 |
| `LiteOrm.DependencyInjection` | 8.1.1 (new in v8.1.0) |

---

## v8.1.1

### Breaking Changes

#### 1. DAO Constructor Injection (`SessionManager`)

> This section applies when upgrading from **v8.1.0 or earlier** to **v8.1.1** (older DAO constructors had no parameters).

As of v8.1.1, `DAOBase` and the DAO base classes (`ObjectDAO<T>`, `ObjectViewDAO<T>`, `DataDAO<T>`, `DataViewDAO<T>`) take a `SessionManager` constructor parameter; DAOs no longer depend on the static `SessionManager.Current`, which is kept solely as an external entry point.

- **DI scenarios** (`RegisterLiteOrm()` / `AddLiteOrm()`): no change needed — the container resolves `SessionManager` automatically.
- **Manual construction**: pass the `sessionManager` to the DAO constructors:

```csharp
// Old (v8.1.0 and lower)
var objectDAO = new ObjectDAO<User>();
var objectViewDAO = new ObjectViewDAO<User>();
var userService = new EntityService<User>(objectDAO, objectViewDAO);

// New (v8.1.1)
var objectDAO = new ObjectDAO<User>(sessionManager);
var objectViewDAO = new ObjectViewDAO<User>(sessionManager);
var userService = new EntityService<User>(objectDAO, objectViewDAO);
```

- Custom DAOs deriving from the DAO base classes must forward the `SessionManager`: `public MyDAO(SessionManager sessionManager) : base(sessionManager) { }`.
- `AddLiteOrm()` binds `SessionManager.Current` automatically when registering `SessionManager`; `RegisterLiteOrm()` enables scope tracking by default — no configuration required.

#### 2. Non-nullable `DbValueType` and `ConvertToDbValue` Signature Change

##### 2.1 `DbValueType` gains `Default`; `Column.DbType` becomes non-nullable

`ColumnAttribute.DbType` and `ColumnDefinition.DbType` changed from `DbValueType?` to non-nullable `DbValueType`, defaulting to `DbValueType.Default` (`-1`), which means "not specified — infer from the property type at runtime".

- The previous `DbType == null` "unspecified" checks become `DbType == DbValueType.Default`.
- Collection-typed properties (`int[]`, `string[]`, `List<T>`, etc.) without an explicit type are inferred as `DbValueType.Array` (previously `Json`).
- `DbValueType` gains `Jsonb` (PostgreSQL binary JSON) and `Array`.

##### 2.2 `ConvertToDbValue` parameter type change

`IDbConverter.ConvertToDbValue`'s parameter changed from `System.Data.DbType` to `DbValueType` (default `DbValueType.Object`). Custom `IDbConverter` / `SqlBuilder` implementations must update their signatures.

##### 2.3 `Param.DbType` type change

`Param.DbType` changed from `DbType?` to `DbValueType` (default `DbValueType.Default`); `DbParameter.DbType` is still derived via `DbValueTypeMap.ToDbType` inside `DAOBase.SetupCommand`, and array columns do not set `DbParameter.DbType`.

### New Features

#### Array / JSON Type Support

- `DbValueType` gains `Array` / `Json` / `Jsonb`; collection-typed properties are inferred as `Array`, PostgreSQL emits native array columns (`integer[]`, `text[]`, etc.), and other dialects fall back to text-JSON storage.
- New `LiteOrm.Pgsql` namespace with PgSQL-specific `Expr` extensions (`ArrayToString`, `ArrayAppend`, `Any`, `JsonbExtractPath`, etc.); `ANY` binds arrays as a single parameter.
- New `JsonExprExtensions` common JSON function extensions (`JsonExtract`, `JsonValue`, `JsonContains`, etc.), with per-dialect native JSON functions registered for MySQL / SQLite / SQL Server / Oracle / PostgreSQL.

#### Service `SearchAs` Projection Extensions

The Service layer now offers IQueryable Lambda projections `SearchAs` / `SearchOneAs` / `SearchAsAsync` / `SearchOneAsAsync`, projecting results into custom classes or anonymous types (see [Lambda Guide](../02-core-usage/05-lambda-guide.en.md#6-projection-queriessearchas--searchoneas)).

#### Computed Columns (Non-Actual Columns)

`ColumnAttribute.Expression` + `ColumnMode.Computed` support computed columns: no physical column is generated and they are excluded from inserts/updates; SELECT returns the expression result and query conditions render the expression (see [Entity Mapping](../02-core-usage/01-entity-mapping.en.md)).

#### `AddLiteOrm()` binds `SessionManager.Current`

As of v8.1.1, `AddLiteOrm()` binds `SessionManager.Current` automatically when registering `SessionManager` (resolving to the scope's instance), so no middleware or manual `SessionManager.SetCurrent(...)` is required.

### Improvements

- Non-AOT builds now auto-register via runtime assembly scan (`LiteOrmAutoRegistration.Apply()`) instead of emitting source code; AOT builds still use the compile-time source generator, dispatched automatically by `RuntimeFeature.IsDynamicCodeSupported`.
- `AutoRegisterGenerator` AOT detection aligned with `TableInfoGenerator`.
- In Autofac auto-registration, a type (or its interface) carrying `[Service]` (`IsService = true`) is automatically intercepted with `ServiceInvokeInterceptor` — no explicit `[Intercept]` needed.
- Removed the `LiteOrmOptions.RegisterScope` option from `RegisterLiteOrm()`; scope tracking is now always enabled automatically.

---

## v8.1.0

### Breaking Changes

#### 1. `RegisterLiteOrm()` moved to the `LiteOrm.DependencyInjection` Package

`RegisterLiteOrm()` moved from the `LiteOrm` base package to the new `LiteOrm.DependencyInjection` package; the namespace changed from `LiteOrm` to `LiteOrm.DependencyInjection`.

```xml
<PackageReference Include="LiteOrm.DependencyInjection" Version="8.1.0" />
```

`LiteOrm.DependencyInjection` transitively references `LiteOrm` and `LiteOrm.Common`; no need to declare them separately.

Update `using`:

```csharp
// Old (8.0.20 or earlier)
using LiteOrm;

// New (8.1.0)
using LiteOrm.DependencyInjection;
```

The `RegisterLiteOrm()` method signature is unchanged, so your call sites do not need modification.

#### 2. `BulkProvider` Usage Change (If You Have Custom Implementations)

`BulkProviderFactory`, `BulkProviderAttribute`, and the `[AutoRegister(Key = ...)]` marker have all been removed. Custom `IBulkProvider` implementations no longer need any marker — just implement the interface and assign it directly to the `BulkProvider` property of the matching `SqlBuilder`. `GetSqlBuilder(typeof(MySqlConnection))` returns `MySqlBuilder.Instance`, so set it directly:

```csharp
// Old: looked up by connection type via the factory (removed)
var provider = services.GetRequiredService<BulkProviderFactory>().GetProvider(dbConnection.GetType());

// New: assign directly to SqlBuilder.BulkProvider
MySqlBuilder.Instance.BulkProvider = new MySqlBulkCopyProvider();
```

When `SqlBuilder.BulkProvider` is unset it returns `null`, and `BatchInsert`/`BatchInsertAsync` automatically fall back to multi-value INSERT or row-by-row inserts.

### New Features

#### Core `AddLiteOrm()` — Plain MS DI Registration (no Autofac)

When you do not want `LiteOrm.DependencyInjection` / Autofac, register the core services directly on `IServiceCollection`:

```csharp
using LiteOrm;

builder.Services.AddLiteOrm(options =>
{
    options.AutoRegisterServices = true;   // default true: apply [AutoRegister] source-generated registrations
    options.ConfigureServices = services => { /* add custom registrations */ };
});
```

`AddLiteOrm()` registers the core services and generic DAOs/services (`IEntityService<T>`, `IEntityViewService<T>`, `IObjectDAO<T>`, etc.), and applies the compile-time registrations of `[AutoRegister]` services.

#### Enhanced `[AutoRegister]` Mechanism

- `[AutoRegister]` can now be declared on a base class; derived classes inherit the registration behavior.
- The `LiteOrm.Generators` source generator scans `[AutoRegister]` types at compile time and emits registration code (equivalent to runtime reflection scanning, but without `Assembly.GetTypes()` and compatible with NativeAOT trimming). Both `RegisterLiteOrm()` and `AddLiteOrm()` apply it automatically.
- The registration scope is controlled by the `ServiceTypes` enum `AutoRegisterServiceTypes`: `All` (default — the implementation type itself and its interfaces), `Self` (itself only), `Interface` (interfaces only). The previous `Type[]` form is removed.
- The Service and DAO base classes (`EntityService<T>`, `ObjectDAO<T>`, etc.) now carry `[AutoRegister(AutoRegisterServiceTypes.All, Lifetime = Lifetime.Scoped)]`, so derived classes inherit it. Use `AutoRegisterServiceTypes.Interface` for interface resolution only, or `Self` for the implementation type only.

#### AOT / NativeAOT Support

- The **net8.0 / net10.0** targets are AOT-compatible (`IsAotCompatible`) and work under NativeAOT and full trimming.
- When building with `PublishAot=true` or trimming enabled, `LiteOrm.Generators` emits registration code for entity types, `SqlBuilder`/`DbConnection` types, DataReader mapping delegates and property accessors at compile time, so the runtime does not rely on `Expression.Compile()` or `Assembly.GetTypes()`.
- `Expr` trees are serialized via the source-generated `ExprJsonSerializerContext` — no reflection, NativeAOT-safe.
- When using `LiteOrm.DependencyInjection` AOP interception in an AOT publish, enable Castle DynamicProxy emulation (`ProxyGenerator.EnableDynamicProxyEmulation()`, Castle.Core 5.1+).

### Improvements

#### Dependency Version Adjustments

Dependency package versions for the netstandard2.0 / 2.1 targets were lowered to the minimum to reduce conflicts with host applications:

- `Microsoft.Extensions.*` (Configuration.Abstractions, Logging.Abstractions, DependencyInjection.Abstractions, etc.) → `2.2.0`
- `System.Text.Json` → `8.0.5`

---

## FAQ

### Q1: `IEntityService<T>` can't be resolved from DI after upgrade?

Make sure the host uses `RegisterLiteOrm()` (from `LiteOrm.DependencyInjection`). Core types (`EntityService<T>`, `ObjectDAO<T>`, etc.) are no longer registered via `[AutoRegister]` scanning but are explicitly registered by `RegisterCoreServices()`.

### Q2: My business service doesn't declare `ServiceTypes`. Can it still be resolved via its interface?

Yes. `[AutoRegister]`'s `ServiceTypes` defaults to `AutoRegisterServiceTypes.All`, which registers both the implementation type itself and its non-System-namespace interfaces, so interface-injected user services need no explicit `ServiceTypes`. To register interfaces only, use `[AutoRegister(AutoRegisterServiceTypes.Interface, Lifetime = Lifetime.Scoped)]`.

### Q3: Will my existing MS DI `IServiceCollection` registrations still work?

Yes. `RegisterLiteOrm()` uses `AutofacServiceProviderFactory` internally to bridge MS DI. Existing `services.AddXxx()` registrations remain effective. If you do not need Autofac / AOP, you can also switch to the new `services.AddLiteOrm()`.

### Q4: Do I need to change `appsettings.json` after upgrading?

No. `RegisterLiteOrm()` loads the data source configuration from the `LiteOrm` node of the host `IConfiguration` automatically; the existing configuration format is unchanged.

### Q5: Why is `SessionManager.Current` null?

Scope tracking is enabled automatically with `RegisterLiteOrm()` / `AddLiteOrm()` — no configuration required. In manual management scenarios, call `SessionManager.SetCurrent(...)` to set the current session.
