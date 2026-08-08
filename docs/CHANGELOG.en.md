# Changelog

## v8.1.1 (2026-08-07)

### Breaking Changes
- `[AutoRegister]`'s `ServiceTypes` (previously `Type[]`) is now an enum `AutoRegisterServiceTypes`: `All` (default, implementation type itself + interfaces), `Self` (itself only), `Interface` (interfaces only). Replace the old `[AutoRegister(Lifetime.Scoped, typeof(IFoo))]` syntax with `[AutoRegister(AutoRegisterServiceTypes.Interface, Lifetime = Lifetime.Scoped)]`.
- The `DAOBase` and derived DAO constructors (`ObjectDAO<T>`, `ObjectViewDAO<T>`, `DataDAO<T>`, `DataViewDAO<T>`) now require a `SessionManager` parameter and no longer depend on the static `SessionManager.Current`. When constructing DAOs manually, pass the `SessionManager`; under DI the container resolves it automatically. `SessionManager.Current` is kept solely as an external entry point, and `AddLiteOrm()` binds it to the current scope instance automatically.

### Added

- Added `AutoRegisterServices` option to `RegisterLiteOrm()`'s `LiteOrmOptions` (default `true`); set to `false` to skip automatic scan registration (`009d2c3`)
- `EntityService<T>`, `EntityViewService<T>`, `ObjectDAO<T>`, `ObjectViewDAO<T>`, `DataDAO<T>`, `DataViewDAO<T>` base classes now carry `[AutoRegister(AutoRegisterServiceTypes.All, Lifetime = Lifetime.Scoped)]`, so derived classes inherit the registration behavior automatically.

### Changed

- Non-AOT builds now auto-register via runtime assembly scan (`LiteOrmAutoRegistration.Apply()`) instead of emitting source code; AOT builds still use the compile-time source generator, dispatched automatically by `RuntimeFeature.IsDynamicCodeSupported` (`009d2c3`)
- `AutoRegisterGenerator` AOT detection aligned with `TableInfoGenerator`, reading `build_property.enableaotanalyzer` / `enabletrimanalyzer` analyzer properties (`009d2c3`)
- In Autofac auto-registration (`RegisterLiteOrm()`), a type (or its interface) carrying `[Service]` (`IsService = true`) is automatically intercepted with `ServiceInvokeInterceptor` — no explicit `[Intercept]` needed.
- Removed the `LiteOrmOptions.RegisterScope` option from `RegisterLiteOrm()`; scope tracking is now always enabled automatically (`ScopeExtensions.RegisterScope` is called internally).

---

## v8.1.0 (2026-08-02)

### Breaking Changes

This release introduces several breaking changes. See the [8.1 Upgrade Guide](./upgrade-guides/01-upgrade-guide-8.1.en.md) for migration details.

- `RegisterLiteOrm()` moved from the `LiteOrm` base package to the new `LiteOrm.DependencyInjection` package; namespace changed from `LiteOrm` to `LiteOrm.DependencyInjection`
- Custom `IBulkProvider` implementations no longer use any attribute markers; `BulkProviderFactory` and `BulkProviderAttribute` were removed in favor of assigning directly to the `SqlBuilder.BulkProvider` property (`0f7fe25`)

### Added
- Added core `AddLiteOrm()`: plain MS DI registration (no Autofac / AOP) that applies `[AutoRegister]` source-generated registrations (`f1b2ef1`, `464b044`, `afecea3`)
- Added AOT / NativeAOT support: the `LiteOrm.Generators` source generator emits entity / DAO / Service / type registration code at compile time; `ExprJsonConverter`, `LambdaExprConverter`, `DAOContextPoolFactory`, `SqlBuilderFactory` etc. are now AOT-safe (`90d75f1`, `1205f4f`, `1eb9dc0`, `0058f05`, `3ca894c`, `a5cfa31`)
- Added the `LiteOrm.DependencyInjection` package (renamed from the host-integration project); DI capabilities split out of the base library (`b45aeeb`, `0322465`, `b0b4177`)

### Changed
- Moved `PreparedSql` to `LiteOrm.Common`; parameter type changed from `KeyValuePair` to custom `Param` (`f50c72e`)
- Lowered target dependency package versions to reduce conflicts (`ad695e6`)
- Host integration / Remote use a singleton `ProxyGenerator` for better performance (`8f8753d`)
- `AttributeTableInfoProvider` no longer depends on `SqlBuilderFactory`, `DataSourceProvider` (`b50b49a`)
- Optimized table creation locking to avoid deadlocks (`148f2ac`)
- DAO and Service now carry AOT-related attribute annotations (`36641fa`, `0599305`, `1737234`, `e68ded4`)
- `ColumnDefinition.DbType` is now nullable; DbType is inferred automatically at runtime (`09bd95d`)

---

## v8.0.20 (2026-07-28)

### Added
- Added `RawSql` marker type to `ExprString` (`6f401b6`)
- Added CTE recursive keyword support (`81fade6`)
- Added table-level `SyncTable` config (`038e93b`)
- Added `ShortId` utility (digits + lowercase letters) (`18d70be`)
- Added `Id` property and consecutive-failure invalidation to `DAOContext` (`18d70be`, `4831a82`)
- Added Remote/Server authentication with `ClientId/Secret` mode and multi-session identity isolation (`285de8b`, `37e0d2b`, `47eb3f1`, `b2e354b`)
- Added `RequestID` to `RemoteInvoke` for request tracing (`e092218`)

### Changed
- `DatabaseSync` appends UPDATE to fill defaults for non-nullable value-type columns (`8fd9662`)
- `SessionManager` lifecycle refactored; `Current` now resolves from current scope (`0698464`, `ce2435b`)
- `LiteOrmCoreInitializer` injects `IComponentContext` instead of `SessionManager`, eliminating captive dependency
- `HttpRemoteServiceTransport` disabled `HttpClient.UseCookies`; credentials now managed by `ICredentialsResolver` (`b456ab2`, `d322c04`, `37e0d2b`)

### Fixed
- Fixed `ParamCountLimit` configuration not taking effect; default adjusted to 1000 (`e4fa04b`)

---

## v8.0.19 (2026-07-06)

### Added
- Removed `ExceptionHook` mechanism, added `ExceptionHandling` global event for exception handling (`f552b91`)
- Added `OnTableSyncing` hook to control table synchronization by `Type` (`5f17866`)
- Auto-increment column DDL supports start value and increment (`a0a7d93`)
- Added `Expression<Func<T, T>>` form of Update method (`6060360`)

---

## v8.0.18 (2026-06-30)

### Added
- Added domestic database SqlBuilder support (`cd73fb7`)
- Added `JsonRemoteServiceTransport` transport implementation (`d8cddca`)
- Remote/Server unified support for `AutoRegisterEntityServices`, default `true` (`edc3ffb`)

### Changed
- Expr `Delete`/`Update` renamed to `DeleteAll`/`UpdateAll` to avoid naming conflicts (`f71d27b`)

### Fixed
- Fixed Server-side method matching failure (`60b8e20`)
- Fixed Remote.Server generic service name matching bug (`2ea5e2c`)

---

## v8.0.17 (2026-06-18)

### Added
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

### Added
- Added `Expr.Reduce` extension (`c206a6d`)
- Added `PropertyOrder` attribute sorting (`7f7dd7e`)

### Refactored
- `FromExpr` and `TableJoinExpr` refactored to support subqueries as source (`8ec2c1d`)

### Fixed
- Fixed Timestamp column not taking effect (`378759d`)

---

## v8.0.15 (2026-05-10)

### Added
- Added CTE expression support (`cc4f8c2`)

---

## v8.0.14 (2026-04-28)

### Added
- Added CodeGen project (`c862ffd`)
- Added `StringExprConverter` with `Parse`/`ParsePagedQuery` methods by entity type (`b4d422f`)

### Fixed
- Fixed Insert method error with non-parameter return for auto-increment columns (`073b4f7`)

---

## v8.0.13 (2026-04-10)

### Added
- Added property constant filter mechanism (`ad1148c`)
- `TableJoin` supports specifying foreign table primary key (`7cf1afc`)
- `ForeignType` can declare multiple (`35f4e47`)

### Refactored
- `LogicSet` split into `AndExpr` and `OrExpr` (`6dd1063`)

---

## v8.0.12 (2026-04-02)

### Added
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

### Changed
- Data reading optimized with dynamic compilation (`207fbe2`)
- Optimized session management; `SessionManager` lifecycle fully maintained by container Scope (`c3b52fc`)

### Fixed
- Fixed Sqlite `Now`/`Today` timezone issue (`8e6e0ed`)
- Fixed subquery SQL generation bug (`b25e120`)

---

## v8.0.10 / v8.0.11 (2026-03-11)

### Added
- Custom `SqlBuilder` registration and configuration support (`60041c8`)

---

## v8.0.8 / v8.0.9 (2026-03-06)

### Added
- Completed `ExprSqlConverter` ToSql implementation (`a41196e`)
- Implemented ExprString for `ObjectViewDAO` (`fd0f746`)
- Completed Expr API validation and tests (`5c5ba35`)

---

## v8.0.0 ~ v8.0.7 (2026-02-11)

### Added
- Initial version; completed Expr API validation and tests (`5c5ba35`, `2948732`)