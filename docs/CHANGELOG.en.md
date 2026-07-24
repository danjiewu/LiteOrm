# Changelog

## v8.0.20 (Unreleased)

### Added
- Added `RawSql` marker type to `ExprString` for inlining non-parameterizable dynamic values (`6f401b6`)
- Added CTE recursive keyword support (`81fade6`)
- Added table-level `SyncTable` config to override sync strategy per entity (`038e93b`)
- Added `ShortId` utility for 8-char Base62 random strings (`18d70be`)
- Added `Id` property to `DAOContext`; `ContextId` included in logs/exceptions (`18d70be`)
- Added Remote/Server authentication mechanism: SignIn endpoint + ticket-based; the client provides tickets via `ICredentialsResolver`, the server issues tickets via `IRemoteAuthenticationHandler`, supporting Cookie/JWT and other auth schemes

### Changed
- `DatabaseSync` appends UPDATE to fill defaults for non-nullable value-type columns when adding columns (`8fd9662`)
- `SessionManager` transaction ID now uses `ShortId` (`18d70be`)

### Fixed
- Fixed `ParamCountLimit` configuration not taking effect; `DAOContext.ParamCountLimit` is now read-only and sourced from the owning `DAOContextPool`; default value adjusted from 2000 to 1000 (`e4fa04b`)

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
