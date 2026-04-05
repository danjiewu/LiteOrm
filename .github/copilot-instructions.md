# LiteOrm Copilot Instructions

## Build and test commands

```powershell
dotnet build .\LiteOrm.sln
dotnet test .\LiteOrm.sln --no-build
dotnet test .\LiteOrm.Tests\LiteOrm.Tests.csproj --no-build --filter "FullyQualifiedName~LiteOrm.Tests.ServiceTests.EntityService_InsertAndGetObject_ShouldWork"
```

`LiteOrm.Tests` uses xUnit v3. Database-backed tests load `LiteOrm.Tests\appsettings.json` through a shared host fixture.

## Architecture

LiteOrm is split into two main libraries:

- `LiteOrm.Common` contains the mapping attributes, expression model (`Expr`), metadata abstractions, SQL segment types, and shared service contracts.
- `LiteOrm` contains the runtime: DI/bootstrap, DAO and service implementations, connection/session management, SQL builders, and interceptors.

The normal runtime path is:

1. `RegisterLiteOrm()` installs a custom service provider factory and scans assemblies for `[AutoRegister]` types instead of relying on manual DI registration.
2. `DataSourceProvider` reads the `LiteOrm` configuration section, and `DAOContextPoolFactory` / `DAOContextPool` create per-data-source pools, including optional read-only pools.
3. `AttributeTableInfoProvider` builds and caches `TableDefinition` and `TableView` metadata from `[Table]`, `[Column]`, `[ForeignType]`, `[ForeignColumn]`, and `[TableJoin]`.
4. `EntityService<T>` / `EntityService<T, TView>` sit above `ObjectDAO<T>` and `ObjectViewDAO<TView>`: write operations go through `ObjectDAO`, query operations go through the view side.
5. Query input can start as lambda expressions, `Expr`, or `ExprString`; DAO code turns that into provider-specific SQL through `SqlBuilderFactory` and the concrete builders under `LiteOrm\SqlBuilder\`.
6. `SessionManager` carries the current session through `AsyncLocal`; `ServiceInvokeInterceptor` layers logging, transaction handling, and exception enrichment around intercepted services.

`LiteOrm.Demo` is the best end-to-end usage sample. `LiteOrm.Tests` is also a reliable reference for canonical service registration, entity mapping, joins, and expression usage.

## Key conventions

- Prefer framework registration patterns over manual wiring: use `RegisterLiteOrm()` and `[AutoRegister]` for services, DAOs, factories, and interceptors.
- Custom business services usually inherit `EntityService<T>` or `EntityService<T, TView>`. When queries need joined fields, define a `TView : T` projection type and inject `ObjectViewDAO<TView>`.
- Mapping is attribute-driven. Schema and joins are expected to come from `[Table]`, `[Column]`, `[ForeignType]`, `[ForeignColumn]`, and `[TableJoin]`, not from hand-maintained mapping registries.
- Transaction behavior is declarative. `[Transaction]` is commonly placed on service contracts and methods, and interception code reads inherited attributes when applying transaction handling.
- Sharding/table routing uses `IArged.TableArgs`. Existing service and DAO code already groups batches by `TableArgs`, so extend that pattern instead of adding separate routing layers.
- `ExprString` is intended for parameterized interpolated SQL fragments: normal values become SQL parameters automatically. For multi-table SQL, set aliases explicitly; the handler assumes the main table alias is `T0`, and the source file notes that complex queries may need explicit aliases or the `{From}` placeholder.
- Database integration tests are serialized with `[Collection("Database")]` and the shared `TestBase` / `DatabaseFixture`. Preserve that pattern for tests that mutate shared tables.
- Start from `docs\README.md` for the documentation map and `docs\05-reference\05-ai-guide.md` for the expected LiteOrm API surface and registration style.
