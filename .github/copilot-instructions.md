# LiteOrm Copilot Instructions

## Build and test commands

- Build the whole repo: `dotnet build .\LiteOrm.sln`
- Run the full test suite: `dotnet test .\LiteOrm.sln --no-build`
- Run a single test: `dotnet test .\LiteOrm.Tests\LiteOrm.Tests.csproj --no-build --filter "FullyQualifiedName~LiteOrm.Tests.ServiceTests.EntityService_InsertAndGetObject_ShouldWork"`
- No dedicated lint command or lint workflow is defined in this repository; use build + tests as the validation baseline.

## High-level architecture

- `LiteOrm.Common` holds the shared contract layer: mapping attributes (`[Table]`, `[Column]`, `[ForeignType]`, `[ForeignColumn]`, `[TableJoin]`), the `Expr` object model and visitors/converters, base types such as `ObjectBase`, and the generic service interfaces.
- `LiteOrm` is the runtime implementation. `RegisterLiteOrm()` switches the host to Autofac, auto-registers `[AutoRegister]` types, sets up `SessionManager`, `DAOContextPoolFactory`, provider-specific `SqlBuilder` instances, and the generic DAO/service stack.
- Generic service behavior is AOP-driven rather than hand-written in each service. `EntityViewService<TView>` is intercepted by `ServiceInvokeInterceptor`, and attributes like `[Transaction]`, `[ServicePermission]`, and `[ServiceLog]` control transactions, permission checks, and service logging.
- Querying centers on the `Expr` pipeline. Simple code usually starts with lambda overloads, which are converted into `Expr`; lower-level code can build `Expr` trees directly or use `ExprString` inside custom DAO code; SQL generation then flows through provider-specific SQL builders.
- `LiteOrm.CodeGen` is a CLI layered on the same registration/configuration path. The `entity` command generates attribute-based models from schema, and `select` turns a basic `SELECT` into a view model plus generated `Expr`/`SelectExpr` builder code.
- `LiteOrm.Demo` is the main feature playground: it registers a service factory proxy with `AddServiceGenerator<ServiceFactory>()` and runs demos for practical queries, transactions, sharding, window functions, update expressions, and related-entity filtering.
- `LiteOrm.WebDemo` uses the generic service layer rather than custom controllers for each model. At startup it discovers eligible `ObjectBase` models with `DisplayName`, emits controllers dynamically, and routes generic CRUD/PageQuery endpoints through `IEntityServiceAsync<T>` and `IEntityViewServiceAsync<TView>`.
- `LiteOrm.Tests` mixes pure unit tests with SQLite-backed integration tests. Service and DAO behavior is exercised primarily through `ServiceTests`, `ObjectViewDAOTests`, `DataViewTests`, `DataDAOTests`, and `Service\LambdaExprExtensionsTests`, with broader `Expr*`, metadata, SQL-segment, and codegen coverage around them.

## Key conventions

- Entity models inherit `ObjectBase`. Query/view models usually inherit the entity type instead of duplicating columns, then add joined fields with `[ForeignColumn]`.
- Relationships are declared in metadata, not scattered across query code. Use `[ForeignType]` on FK properties, `[TableJoin]` for explicit or reused join paths, and `[ForeignColumn]` on view models for projected related fields.
- Custom business services usually stay thin. The common pattern is an interface that composes `IEntityService<T>`, `IEntityServiceAsync<T>`, `IEntityViewService<TView>`, and `IEntityViewServiceAsync<TView>`, with an implementation that simply inherits `EntityService<T>` or `EntityService<T, TView>` and adds only custom methods.
- Prefer attribute-driven registration over manual DI wiring. The repository leans on `[AutoRegister]` scanning and generic service resolution; when a grouped facade is needed, use `services.AddServiceGenerator<TFactory>()` and resolve the generated interface proxy.
- Sharding uses table arguments. Types that route to physical tables implement `IArged` and expose `TableArgs`; service and DAO APIs also accept explicit `tableArgs`. Preserve that pattern instead of hardcoding table names into queries or services.
- Database-backed tests use `[Collection("Database")]` plus `TestBase`/`DatabaseFixture`. That fixture builds the host once, forces serialized execution, and clears the SQLite test tables before each test class.
- Keep `LiteOrm.Tests` aligned with the public service and DAO surface. When adding or changing `EntityService`, `EntityViewService`, `ObjectViewDAO`, `DataViewDAO`, `DataDAO`, or their extension helpers, extend the corresponding focused test files instead of only adding broad end-to-end coverage elsewhere.
- Oracle entry points set `OracleConfiguration.BindByName = true` before building the host (`LiteOrm.Demo`, `LiteOrm.CodeGen`, and test fixture startup). Keep that in place when changing Oracle-related startup or commands.
- For API and configuration lookups, prefer `README.en.md` and `docs\05-reference\05-ai-guide.en.md`. They document the supported registration options, service interfaces, DAO APIs, and the intended Lambda/Expr/ExprString usage split.
