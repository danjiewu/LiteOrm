# Changelog

## v8.1.4 (2026-08-24)

### Breaking Changes

	- Properties of complex types (arrays/collections, custom classes) are **no longer auto-mapped as table columns**; they require an explicit `[Column]` (and, as appropriate, `DbType = Array`).Known scalars (numerics, `string`/`char`, `byte[]`, `Guid`, dates, enums) and `Json`/`Jsonb` mapped types are still auto-mapped.
- `EntityService<T>` / `EntityService<T, TView>` / `EntityViewService<T>` constructors now take an `IServiceProvider`, resolving the required `ObjectDAO<T>` / `ObjectViewDAO<T>` from the container; derived service constructors were updated accordingly. No changes are needed under DI.

### Enhancements

- Both `ColumnAttribute` and `ForeignColumnAttribute` now expose `ConverterType` to declare a column-level converter. When reading a foreign projection column, `ForeignColumn` **prefers its own declared converter, otherwise falls back to the target column's**.
- Cross-numeric-type conversion is now registered by default (covering `decimal`/`float`/`double` and the integer family, e.g. `Decimal→Int32`), so no manual registration is needed.
- Value-conversion mechanism optimized: conversion now converges on a delegating converter, resolved with a fixed priority of column-level converter → registry keyed by (value type, database value type) → direct assignment; null/`DBNull`/empty-string values are short-circuited up front, and no generic fallback is applied when nothing is registered or no conversion is needed.
- Registered cross-dialect SQL functions for all database builders so that C# method names converted via `LambdaExprConverter` no longer produce invalid SQL due to name mismatch or dialect differences:

  | Function | Base (SQLite/MySQL/SQLServer) | Oracle | PostgreSQL | SQL Server |
  |----------|------|--------|------------|------------|
  | `ToLower` | `LOWER` | — | — | — |
  | `ToUpper` | `UPPER` | — | — | — |
  | `Pow` | `POWER` | — | — | — |
  | `Char` | `CHAR` | `CHR` | `CHR` | — |
  | `Ceiling` | `CEILING` | `CEIL` | `CEIL` | — |
  | `Truncate` | `TRUNCATE(x,0)` | `TRUNC` | `TRUNC` | `ROUND(x,0,1)` |
  | `Concat` | `CONCAT` | `||` | — | — |
  | `Log` | `LOG` | `LN` | `LN` | — |
  | `Log10` | `LOG10` | `LOG(10,x)` | `LOG` | — |
  | `Atan2` | `ATAN2` | — | — | `ATN2` |
  | `Max` (scalar) | `GREATEST` | — | — | — |
  | `Min` (scalar) | `LEAST` | — | — | — |
  | `Max`/`Min` (SQLite) | — | — | — | `max`/`min` |

  - `Max`/`Min` distinguish aggregate (`MAX`/`MIN`) from scalar (`GREATEST`/`LEAST`) via `IsAggregate`; SQLite's `max`/`min` support both.
  - Standard same-name functions (`Abs`, `Round`, `Floor`, `Sqrt`, `Exp`, `Sin`/`Cos`/`Tan`/`Asin`/`Acos`/`Atan`, `Sign`, `Replace`, `Coalesce`, `Upper`/`Lower` via ExprExtensions) need no registration — default rendering works across databases.
- `ISqlBuilder` gains `TryAppendSqlLiteral`: string constants (`Expr.Const(string)`) containing only regular characters (no backslash or control characters) are inlined directly as `'value'` (single quotes escaped via `''`); strings with special characters fall back to parameterization. String constants like `Expr.Const(" ")` can now be used in computed column expressions.
- Added Lambda-to-SQL-function mappings for `System.Text.RegularExpressions.Regex` methods, supporting static form `Regex.M(...)`, instance form `new Regex(pattern).M(...)`, and closure-variable form `regex.M(...)` (instance forms evaluate the Regex object and read `Pattern` via reflection):

  | C# Method/Member | SQL Function | Description |
  |------------------|--------------|-------------|
  | `Regex.IsMatch(input, pattern)` | `REGEXP_LIKE` | Regex match predicate (WHERE) |
  | `Regex.Replace(input, pattern, replacement)` | `REGEXP_REPLACE` | Regex replace |
  | `Regex.Match(input, pattern).Value` | `REGEXP_SUBSTR` | Extract first match substring |
  | `Regex.Match(input, pattern).Index` | `REGEXP_INSTR - 1` | First match position (converted to 0-based to match C# `Match.Index`) |
  | `Regex.Match(input, pattern).Success` | `REGEXP_LIKE` | Whether matched (predicate) |

  - Dialect registration: `REGEXP_LIKE` uses the `REGEXP` operator on MySQL and the `~` operator on PostgreSQL; `REGEXP_REPLACE`/`REGEXP_INSTR`/`REGEXP_SUBSTR`/`REGEXP_COUNT` default to rendering same-name functions (native on Oracle/MySQL 8.0+/PostgreSQL).
  - `SqlBuilder.DefaultFunctionSqlHandler` renders as `FunctionName(args)`; the `RegisterFunctionSqlHandler(functionName)` name-only overload directly reuses the default rendering.
- Oracle identifiers are no longer forced to uppercase, consistent with other databases.

### Fixes

- Fixed a bug in custom `SearchAs`/`SearchOneAs` queries where results could not be read correctly when a custom `SelectItem`'s column name differed from the result property name and no alias was given (an `AS` clause is now added automatically).
- Fixed an error in several call sites that bound the whole entity object as a raw value (`No mapping exists from object type ...`).
- `SortProperty` now excludes indexer properties, fixing a false circular-dependency error caused by a built-in `Item` (indexer) colliding with a custom `Item` property.
- `SearchAsAsync` / `SearchOneAsAsync` now include the `CancellationToken` parameter declared by the interface, fixing `EntityViewService<T>` / `RemoteViewServiceAsyncProxy<T>` not implementing the interface members.

---

## v8.1.3 (2026-08-18)

### Breaking Changes

- Unified use of `DbValueType` instead of `DbType` internally; conversion to `System.Data.DbType` only at database operation boundaries. Merged `DbTypeMap` into `DbValueTypeMap`.
- `IDbConverter.GetDbType(Type)` renamed to `GetDbValueType(Type)`; added `GetDefaultLength(DbValueType)`.
- `DbValueType.Array` is now a bitmask (value 128), composable with scalar types via bitwise OR (e.g., `DbValueType.Int32 | DbValueType.Array`).
- `Expr.Cast` / `SqlBuilder.GetSqlTypeName` / `GetDefaultLength` parameters changed to `DbValueType`.

### Improvements

- Computed column supports `ValueTypeExpr` form (`ColumnDefinition.ExpressionExpr`).
- `DataReaderConverter` infers dialect-specific reader type via the current `SqlBuilder` when `DbValueType.Default`.

---

## v8.1.2 (2026-08-17)

### AOT Compilation Improvements

- **Fixed AutoRegisterGenerator enum/property name mismatch**: corrected `AutoRegisterServiceTypes` to `RegisterPolicy` and named argument `ServiceTypes` to `Policy`.
- **Removed global trim warning suppression** (`SuppressTrimAnalysisWarnings`); added `DynamicallyAccessedMembers` annotation chains and `UnconditionalSuppressMessage` so trimming/AOT warnings are visible at compile time.

### New Features

- **Computed column supports `ValueTypeExpr` form**: the `ColumnDefinition.ExpressionExpr` property allows setting computed column expressions dynamically using Expr trees such as `Expr.Prop("Price") * Expr.Prop("Quantity")`; rendering is handled by `ExprSqlConverter` to produce SQL, only fixed SQL expressions (property references, constants, functions, arithmetic) that produce no parameters are allowed — a `NotSupportedException` is thrown if the rendered expression produces parameterized values. Takes precedence over the string-form `Expression` when both are set.

---

## v8.1.1 (2026-08-07)

### Breaking Changes
- `[AutoRegister]`'s `ServiceTypes` (previously `Type[]`) is now an enum `RegisterPolicy`: `All` (default, implementation type itself + interfaces), `Self` (itself only), `Interface` (interfaces only). Replace the old `[AutoRegister(Lifetime.Scoped, typeof(IFoo))]` syntax with `[AutoRegister(RegisterPolicy.Interface, Lifetime = Lifetime.Scoped)]`.
- The `DAOBase` and derived DAO constructors (`ObjectDAO<T>`, `ObjectViewDAO<T>`, `DataDAO<T>`, `DataViewDAO<T>`) now require a `SessionManager` parameter and no longer depend on the static `SessionManager.Current`. When constructing DAOs manually, pass the `SessionManager`; under DI the container resolves it automatically. `SessionManager.Current` is kept solely as an external entry point, and `AddLiteOrm()` binds it to the current scope instance automatically.
- `ColumnAttribute.DbType` and `ColumnDefinition.DbType` changed from `DbValueType?` to non-nullable `DbValueType`, defaulting to the new `DbValueType.Default` (meaning "not specified, infer from the property type at runtime"). The previous `DbType == null` checks are replaced by `DbType == DbValueType.Default`.
- `IDbConverter.ConvertToDbValue` parameter changed from `DbType` to `DbValueType` (default `DbValueType.Object`); it no longer accepts a `DbType` argument.
- `Param.DbType` type changed from `DbType?` to `DbValueType` (default `DbValueType.Default`).
- `DbValueType` gains `Default = -1`, `Jsonb = 29`, and `Array = 30`. Collection-typed properties without an explicit type are now inferred as `Array` (previously `Json`).

### New Features

- Added `AutoRegisterServices` option to `RegisterLiteOrm()`'s `LiteOrmOptions` (default `true`); set to `false` to skip automatic scan registration (`009d2c3`)
- `EntityService<T>`, `EntityViewService<T>`, `ObjectDAO<T>`, `ObjectViewDAO<T>`, `DataDAO<T>`, `DataViewDAO<T>` base classes now carry `[AutoRegister(RegisterPolicy.All, Lifetime = Lifetime.Scoped)]`, so derived classes inherit the registration behavior automatically.
- Array type support: collection properties are inferred as `DbValueType.Array`; PostgreSQL emits native array columns (`integer[]`, `text[]`, etc.), other dialects fall back to text-JSON storage.
- PostgreSQL array functions: parsing and SQL generation for `array_to_string`, `array_append`, `ANY`, etc.; `ANY` binds arrays as a single parameter.
- New `LiteOrm.Pgsql` namespace with PgSQL-specific `ValueTypeExpr` extensions (`ArrayToString`, `ArrayAppend`, `Any`, `Contains`, `JsonbExtractPath`, `JsonbExtractPathText`, `JsonbContains`, `JsonbBuildObject`, `JsonbBuildArray`).
- JSON/JSONB types: `DbValueType.Json`/`Jsonb` support; PostgreSQL emits `JSON`/`JSONB` columns, MySQL emits `JSON` columns.
- New `JsonExprExtensions` common JSON function extensions (`JsonExtract`, `JsonValue`, `JsonQuery`, `JsonContains`, `JsonObject`, `JsonArray`, `IsJson`), with per-dialect native JSON functions registered for MySQL / SQLite / SQL Server / Oracle / PostgreSQL.
- Added Lambda-style `SearchAs` / `SearchOneAs` / `SearchAsAsync` / `SearchOneAsAsync` extensions to the Service layer (`Expression<Func<IQueryable<T>, IQueryable<TResult>>>` projection form).
- Entities now support computed (non-actual) columns: `ColumnAttribute.Expression` + `ColumnMode.Computed` — no physical column is generated, and results / query conditions are rendered from the expression.

### Improvements

- Non-AOT builds now auto-register via runtime assembly scan (`LiteOrmAutoRegistration.Apply()`) instead of emitting source code; AOT builds still use the compile-time source generator, dispatched automatically by `RuntimeFeature.IsDynamicCodeSupported` (`009d2c3`)
- `AutoRegisterGenerator` AOT detection aligned with `TableInfoGenerator`, reading `build_property.enableaotanalyzer` / `enabletrimanalyzer` analyzer properties (`009d2c3`)
- In Autofac auto-registration (`RegisterLiteOrm()`), a type (or its interface) carrying `[Service]` (`IsService = true`) is automatically intercepted with `ServiceInvokeInterceptor` — no explicit `[Intercept]` needed.
- Removed the `LiteOrmOptions.RegisterScope` option from `RegisterLiteOrm()`; scope tracking is now always enabled automatically (`ScopeExtensions.RegisterScope` is called internally).
- `ConvertFromDbValue` now converts array/collection values into target collections such as `List<T>`.
- The `TableInfoGenerator` source generator adapted to the non-nullable `DbValueType`.

---

## v8.1.0 (2026-08-02)

### Breaking Changes

This release introduces several breaking changes. See the [8.1 Upgrade Guide](./upgrade-guides/01-upgrade-guide-8.1.en.md) for migration details.

- `RegisterLiteOrm()` moved from the `LiteOrm` base package to the new `LiteOrm.DependencyInjection` package; namespace changed from `LiteOrm` to `LiteOrm.DependencyInjection`
- Custom `IBulkProvider` implementations no longer use any attribute markers; `BulkProviderFactory` and `BulkProviderAttribute` were removed in favor of assigning directly to the `SqlBuilder.BulkProvider` property (`0f7fe25`)

### New Features
- Added core `AddLiteOrm()`: plain MS DI registration (no Autofac / AOP) that applies `[AutoRegister]` source-generated registrations (`f1b2ef1`, `464b044`, `afecea3`)
- Added AOT / NativeAOT support: the `LiteOrm.Generators` source generator emits entity / DAO / Service / type registration code at compile time; `ExprJsonConverter`, `LambdaExprConverter`, `DAOContextPoolFactory`, `SqlBuilderFactory` etc. are now AOT-safe (`90d75f1`, `1205f4f`, `1eb9dc0`, `0058f05`, `3ca894c`, `a5cfa31`)
- Added the `LiteOrm.DependencyInjection` package (renamed from the host-integration project); DI capabilities split out of the base library (`b45aeeb`, `0322465`, `b0b4177`)

### Improvements
- Moved `PreparedSql` to `LiteOrm.Common`; parameter type changed from `KeyValuePair` to custom `Param` (`f50c72e`)
- Lowered target dependency package versions to reduce conflicts (`ad695e6`)
- Host integration / Remote use a singleton `ProxyGenerator` for better performance (`8f8753d`)
- `AttributeTableInfoProvider` no longer depends on `SqlBuilderFactory`, `DataSourceProvider` (`b50b49a`)
- Optimized table creation locking to avoid deadlocks (`148f2ac`)
- DAO and Service now carry AOT-related attribute annotations (`36641fa`, `0599305`, `1737234`, `e68ded4`)
- `ColumnDefinition.DbType` is now nullable; DbType is inferred automatically at runtime (`09bd95d`)

---

## v8.0.20 (2026-07-28)

### New Features
- Added `RawSql` marker type to `ExprString` (`6f401b6`)
- Added CTE recursive keyword support (`81fade6`)
- Added table-level `SyncTable` config (`038e93b`)
- Added `ShortId` utility (digits + lowercase letters) (`18d70be`)
- Added `Id` property and consecutive-failure invalidation to `DAOContext` (`18d70be`, `4831a82`)
- Added Remote/Server authentication with `ClientId/Secret` mode and multi-session identity isolation (`285de8b`, `37e0d2b`, `47eb3f1`, `b2e354b`)
- Added `RequestID` to `RemoteInvoke` for request tracing (`e092218`)

### Improvements
- `DatabaseSync` appends UPDATE to fill defaults for non-nullable value-type columns (`8fd9662`)
- `SessionManager` lifecycle refactored; `Current` now resolves from current scope (`0698464`, `ce2435b`)
- `LiteOrmCoreInitializer` injects `IComponentContext` instead of `SessionManager`, eliminating captive dependency
- `HttpRemoteServiceTransport` disabled `HttpClient.UseCookies`; credentials now managed by `ICredentialsResolver` (`b456ab2`, `d322c04`, `37e0d2b`)

### Fixed
- Fixed `ParamCountLimit` configuration not taking effect; default adjusted to 1000 (`e4fa04b`)

---

## v8.0.19 (2026-07-06)

### New Features
- Removed `ExceptionHook` mechanism, added `ExceptionHandling` global event for exception handling (`f552b91`)
- Added `OnTableSyncing` hook to control table synchronization by `Type` (`5f17866`)
- Auto-increment column DDL supports start value and increment (`a0a7d93`)
- Added `Expression<Func<T, T>>` form of Update method (`6060360`)

---

## v8.0.18 (2026-06-30)

### New Features
- Added domestic database SqlBuilder support (`cd73fb7`)
- Added `JsonRemoteServiceTransport` transport implementation (`d8cddca`)
- Remote/Server unified support for `AutoRegisterEntityServices`, default `true` (`edc3ffb`)

### Improvements
- Expr `Delete`/`Update` renamed to `DeleteAll`/`UpdateAll` to avoid naming conflicts (`f71d27b`)

### Fixed
- Fixed Server-side method matching failure (`60b8e20`)
- Fixed Remote.Server generic service name matching bug (`2ea5e2c`)

---

## v8.0.17 (2026-06-18)

### New Features
- Added Remote module supporting remote proxy pattern (`e01a660`)
- Added `CycleDetector` to detect Expr circular references (`02df339`)
- Added ternary operator (`a ? b : c`) parsing to `CASE` statement (`eb0def4`)

### Refactored
- Refactored `ExprVisitor` and `ExprValidator` to support multiple traversal and validation modes (`0c0499c`)

### Fixed
- Fixed bug where Join conditions without priority failed to add parentheses (`ebc87e6`)
- Fixed default SqlBuilder matching to correctly identify PostgreSql and SqlServer (`e664272`)

---

## v8.0.16 (2026-05-27)

### New Features
- Added `Expr.Reduce` extension (`c206a6d`)
- Added `PropertyOrder` attribute sorting (`7f7dd7e`)

### Refactored
- `FromExpr` and `TableJoinExpr` refactored to support subqueries as source (`8ec2c1d`)

### Fixed
- Fixed Timestamp column not taking effect (`378759d`)

---

## v8.0.15 (2026-05-10)

### New Features
- Added CTE expression support (`cc4f8c2`)

---

## v8.0.14 (2026-04-28)

### New Features
- Added CodeGen project (`c862ffd`)
- Added `StringExprConverter` with `Parse`/`ParsePagedQuery` methods by entity type (`b4d422f`)

### Fixed
- Fixed Insert method error with non-parameter return for auto-increment columns (`073b4f7`)

---

## v8.0.13 (2026-04-10)

### New Features
- Added property constant filter mechanism (`ad1148c`)
- `TableJoin` supports specifying foreign table primary key (`7cf1afc`)
- `ForeignType` can declare multiple (`35f4e47`)

### Refactored
- `LogicSet` split into `AndExpr` and `OrExpr` (`6dd1063`)

---

## v8.0.12 (2026-04-02)

### New Features
- Added `ExprValidator` validation mechanism (`2c9245e`)
- Added `TableExpr` and `TableJoinExpr` with serialization (`1ee64b3`, `5b2a116`)
- Added window function support (`b7245d1`)
- Added `ExistsRelated` method for auto-association (`6aa5ff2`)
- Added SqlGen ExprString parsing and `ExprInterpolatedStringHandler` (`6eac5d5`, `bf0f85d`)
- Added `DDLGenerator` for table DDL generation (`fc91353`)
- Added pre-generated entity `DataReaderConverter` (`8ac1ca6`)
- Added Lambda sharding (`b94ca29`)
- Added `ForeignExists` method (`2a5960b`)
- Added custom method handler and SQL builder (`31be232`)
- Added `IdentityIncreasement` configuration (`894cc61`)
- Added column default value support (`07b30b5`)

### Improvements
- Data reading optimized with dynamic compilation (`207fbe2`)
- Optimized session management; `SessionManager` lifecycle fully maintained by container Scope (`c3b52fc`)

### Fixed
- Fixed Sqlite `Now`/`Today` timezone issue (`8e6e0ed`)
- Fixed subquery SQL generation bug (`b25e120`)

---

## v8.0.10 / v8.0.11 (2026-03-11)

### New Features
- Custom `SqlBuilder` registration and configuration support (`60041c8`)

---

## v8.0.8 / v8.0.9 (2026-03-06)

### New Features
- Completed `ExprSqlConverter` ToSql implementation (`a41196e`)
- Implemented ExprString for `ObjectViewDAO` (`fd0f746`)
- Completed Expr API validation and tests (`5c5ba35`)

---

## v8.0.0 ~ v8.0.7 (2026-02-11)

### New Features
- Initial version; completed Expr API validation and tests (`5c5ba35`, `2948732`)