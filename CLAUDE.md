# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and test commands

```bash
# Build the entire solution
dotnet build .\LiteOrm.sln

# Run full test suite (no-build is faster if you just built)
dotnet test .\LiteOrm.sln --no-build

# Run a single test class or method by name filter
dotnet test .\LiteOrm.Tests\LiteOrm.Tests.csproj --no-build --filter "FullyQualifiedName~LiteOrm.Tests.ServiceTests.EntityService_InsertAndGetObject_ShouldWork"

# Run a specific test file's tests
dotnet test .\LiteOrm.Tests\LiteOrm.Tests.csproj --no-build --filter "FullyQualifiedName~LiteOrm.Tests.Expr"

# CodeGen CLI (entity generation from database schema)
dotnet run --project .\LiteOrm.CodeGen -- entity --data-source SQLite --namespace Demo.Models --table TestUsers,TestDepartments

# CodeGen CLI (view/query generation from SQL)
dotnet run --project .\LiteOrm.CodeGen -- select --data-source SQLite --view-name UserReportView --namespace Demo.Models --sql "SELECT u.Id, u.Name FROM Users u WHERE u.Age >= 18 ORDER BY u.Name"
```

No lint command exists; build + tests are the validation baseline.

## Architecture

LiteOrm is a lightweight .NET ORM targeting `net10.0`, `net8.0`, `netstandard2.1`, and `netstandard2.0`. It sits between micro-ORMs (Dapper) and full ORMs (EF Core).

### Project layout

| Project | Role |
|---|---|
| `LiteOrm.Common` | Shared contracts: mapping attributes, `Expr` object model, visitors/converters, service interfaces, `SqlSegment` types |
| `LiteOrm` | Runtime: DI registration (Autofac), DAO layer, `ExprSqlConverter`, SQL builders per provider, AOP-driven generic services |
| `LiteOrm.Tests` | xUnit v3 + SQLite in-memory integration tests |
| `LiteOrm.CodeGen` | CLI that generates entity classes and SELECT-based view/query code from live database schema |
| `LiteOrm.Demo` | Feature playground (queries, transactions, sharding, window functions) |
| `LiteOrm.WebDemo` | ASP.NET app that dynamically emits controllers from generic services |
| `LiteOrm.Benchmark` | BenchmarkDotNet comparisons vs Dapper, EF Core, SqlSugar, FreeSql |

### Registration and DI

`RegisterLiteOrm()` on `IHostBuilder` (in `LiteOrm/Classes/LiteOrmServiceExtensions.cs`) switches the host to Autofac, scans for `[AutoRegister]` types, and wires up the full stack. The container is `Autofac` with Castle DynamicProxy for AOP interception. Providers are configured via `appsettings.json` under the `LiteOrm` section.

### The Expr pipeline — how queries become SQL

Three query authoring styles, all converging on the same `Expr` tree:

1. **Lambda** — `userService.SearchAsync(u => u.Age > 18)` → `LambdaExprConverter` converts C# expression trees to `Expr` trees.
2. **Expr** — Direct construction: `Expr.Prop("Age") > 18 & Expr.Prop("Status") == 1`.
3. **ExprString** — Parameterized interpolated strings: `$"WHERE Age > {minAge}"`, safe against SQL injection.

All paths produce an `Expr` tree (class hierarchy rooted at `LiteOrm.Common/Expr/Expr.cs`). The tree is then:
- Validated by `ExprValidator`
- Converted to SQL by `ExprSqlConverter.ToSql()` in `LiteOrm/Converter/ExprSqlConverter.cs`
- Rendered with provider-specific quoting/formatting by an `ISqlBuilder` implementation (e.g., `MySqlBuilder`, `SqlServerBuilder`, `PostgreSqlBuilder`, `OracleBuilder`, `SQLiteBuilder`)
- Database-specific function translation is handled by `SqlHandlerMap` — a per-provider registry mapping function names to `FunctionSqlHandler` delegates, extensible via `RegisterFunctionSqlHandler<T>()`

The Expr tree can also be serialized/deserialized to/from a compact JSON format via `ExprJsonConverter` for frontend-to-backend query transmission.

### Expr type hierarchy

- `ValueTypeExpr` subclasses represent SQL value expressions (`ValueExpr`, `PropertyExpr`, `FunctionExpr`, `ValueBinaryExpr`, `UnaryExpr`, `ValueSet`).
- `LogicExpr` subclasses represent boolean conditions (`LogicBinaryExpr`, `AndExpr`, `OrExpr`, `NotExpr`, `ForeignExpr` for EXISTS subqueries, `LambdaExpr`, `GenericSqlExpr` for delegate-based dynamic SQL).
- `SqlSegment` subclasses represent SQL clauses with chainable `Source` references. `SourceExpr` is the abstract base for data sources (`FromExpr` for subquery/table sources, `TableJoinExpr` for JOINs). Other segments: `TableExpr`, `WhereExpr`, `GroupByExpr`, `HavingExpr`, `OrderByExpr`, `SectionExpr`, `SelectExpr`.
- `UpdateExpr` and `DeleteExpr` extend `Expr` directly (not `SqlSegment`).

Fluent extension methods on these types live in `ExprExtensions.cs` — methods like `.Where()`, `.OrderBy()`, `.Section()`, `.Select()`, `.And()`, `.Or()`, `.Contains()`, `.In()`, etc.

### Entity mapping

Attributes in `LiteOrm.Common/Attributes/`: `[Table]` on classes, `[Column]` on properties (with `IsPrimaryKey`, `IsIdentity`, etc.), `[ForeignType]` for declaring FK relationships, `[ForeignColumn]` on view-model properties for projected joined fields, `[TableJoin]` for explicit join paths.

### Service layer

Generic interfaces in `LiteOrm.Common/Service/`: `IEntityService<T>` (write ops), `IEntityViewService<TView>` (read/query ops), plus their async variants. The runtime implementation `EntityService<T, TView>` in `LiteOrm/Service/` is intercepted by `ServiceInvokeInterceptor` which handles `[Transaction]`, `[ServicePermission]`, `[ServiceLog]`, and `[Log]` attributes via AOP.

Custom services compose the generic interfaces and inherit `EntityService<T>` or `EntityService<T, TView>`, adding only custom methods — they stay thin.

### DAO layer

Low-level data access in `LiteOrm/DAO/`: `ObjectDAO` (entity CRUD), `ObjectViewDAO` (entity queries), `DataDAO` (raw table access), `DataViewDAO` (DataTable queries). These are the building blocks that `EntityService` and `EntityViewService` wrap. The `PreparedSql` class carries the generated SQL string together with its named `DbParameter` collection.

### Tests

- Use xUnit v3 (package `xunit.v3`) with `[Collection("Database")]` for serialized database test execution. The test project targets `net10.0` only.
- `DatabaseFixture` and `TestBase` both live in `LiteOrm.Tests/Infrastructure/TestBase.cs`. The fixture builds the host once with SQLite in-memory, registers a custom `REGEXP_LIKE` SQLite function, and clears tables before each test class.
- Models live in `LiteOrm.Tests/Models/`; example services in `Infrastructure/TestServices.cs`.
- Pure unit tests (no database) test `Expr*`, attributes, converters, metadata, and SQL segments.
- When adding or changing service/DAO behavior, extend the corresponding focused test files (`ServiceTests.cs`, `ObjectViewDAOTests.cs`, `DataViewTests.cs`, etc.) rather than only adding broad coverage elsewhere.

### Key conventions

- Entity models inherit `ObjectBase` (from `LiteOrm.Common/Model/ObjectBase.cs`). View models inherit the entity type and add `[ForeignColumn]` properties.
- Prefer `[AutoRegister]` scanning over manual DI wiring.
- Sharding uses `IArged.TableArgs` to route to physical tables — don't hardcode table names.
- Oracle entry points must set `OracleConfiguration.BindByName = true` before building the host.
- The library packages (`LiteOrm`, `LiteOrm.Common`) have nullable disabled; the test/console/web projects have nullable enabled.
