# LiteOrm 8.1 Upgrade Guide

This guide describes the changes required when upgrading to v8.1.0.

## Version Overview

| Package | New Version |
|---|---|
| `LiteOrm` | 8.1.0 |
| `LiteOrm.Common` | 8.1.0 |
| `LiteOrm.DependencyInjection` | 8.1.0 (new) |

---

## Migration Steps

### Step 1: Reference the `LiteOrm.DependencyInjection` Package

The `RegisterLiteOrm()` extension method moved from the `LiteOrm` core package to `LiteOrm.DependencyInjection`, and the namespace changed from `LiteOrm` to `LiteOrm.DependencyInjection`.

```xml
<PackageReference Include="LiteOrm.DependencyInjection" Version="8.1.0" />
```

`LiteOrm.DependencyInjection` transitively references `LiteOrm` and `LiteOrm.Common`; no need to declare them separately.

Update `using`:

```csharp
// Old
using LiteOrm;

// New
using LiteOrm.DependencyInjection;
```

The `RegisterLiteOrm()` method signature is unchanged; the calling convention remains the same.

### Step 2: Update `BulkProvider` Usage (If You Have Custom Implementations)

`BulkProviderFactory`, `BulkProviderAttribute`, and the `[AutoRegister(Key = ...)]` marker have all been removed. Custom `IBulkProvider` implementations no longer need any marker—just implement the interface and assign it directly to the `BulkProvider` property of the matching `SqlBuilder`:

```csharp
// Old: looked up by connection type via the factory (removed)
var provider = services.GetRequiredService<BulkProviderFactory>().GetProvider(dbConnection.GetType());

// New: assign directly to SqlBuilder.BulkProvider
SqlBuilderFactory.Instance.GetSqlBuilder(typeof(MySqlConnection)).BulkProvider = new MySqlBulkCopyProvider();
```

When `SqlBuilder.BulkProvider` is unset it returns `null`, and `BatchInsert`/`BatchInsertAsync` automatically fall back to multi-value INSERT or row-by-row inserts.

---

## FAQ

### Q1: `IEntityService<T>` can't be resolved from DI after upgrade?

Make sure the host uses `RegisterLiteOrm()` (from `LiteOrm.DependencyInjection`). Core types (`EntityService<T>`, `ObjectDAO<T>`, etc.) are no longer registered via `[AutoRegister]` scanning but are explicitly registered by `RegisterCoreServices()`.

### Q2: My business service doesn't declare `ServiceTypes`. Can it still be resolved via its interface?

Yes. When `ServiceTypes` is not specified, the framework infers the non-system-namespace interfaces implemented by the type as service types. User-defined services resolved via interfaces need no explicit `ServiceTypes`.

### Q3: Will my existing MS DI `IServiceCollection` registrations still work?

Yes. `RegisterLiteOrm()` uses `AutofacServiceProviderFactory` internally to bridge MS DI. Existing `services.AddXxx()` registrations remain effective.

---

## Verification

After upgrading, ensure:

```bash
dotnet build .\LiteOrm.sln
dotnet test .\LiteOrm.sln
```

The full test suite (1922 tests) passing is the verification baseline for this release.
