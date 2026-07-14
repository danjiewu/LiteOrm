# Changelog

## v8.0.20 (Unreleased)

### Added
- Added `RawSql` marker type to `ExprString` for inlining dynamic values unsuitable for parameterization (e.g. `LIMIT`/`OFFSET` row counts, `ASC`/`DESC` sort direction, dynamic column names); purely static text can be written directly in the literal (`6f401b6`)
- Added CTE recursive keyword support (`81fade6`)
- Added table-level `SyncTable` configuration on `[Table]` attribute, allowing per-entity `Never`/`Always` to override data-source-level sync strategy (`038e93b`)

---

## v8.0.19 (2026-07-06)

### Added
- Removed `ExceptionHook` mechanism, added `ExceptionHandling` global event for exception handling (`f552b91`)
- `ServiceDescription` extension methods moved to `LiteOrm.Common`, loading only from defined methods or types (`8c80c40`)
- Added `OnTableSyncing` hook to control table synchronization by `Type` (`5f17866`)
- Auto-increment column DDL supports start value and increment (`a0a7d93`)
- Added `Expression<Func<T, T>>` form of Update method (`6060360`)

### Changed
- Improved exception information on read failure (`0230f96`)

---

## v8.0.18 (2026-06-30)

### Added
- Added domestic database SqlBuilder support (`cd73fb7`)
- Added `JsonRemoteServiceTransport` transport implementation (`d8cddca`)
- Remote/Server unified support for `AutoRegisterEntityServices`, default `true` (`edc3ffb`)
- Remote calls to non-Service methods throw `NotSupportedException` (`e8c1ea2`)

### Changed
- Renamed `RemoteErrorInfo` properties (`ba1e2e1`, `4d815e2`)
- Optimized Remote serialization logic (`8383686`)
- Expr `Delete`/`Update` renamed to `DeleteAll`/`UpdateAll` to avoid naming conflicts (`f71d27b`)
- Optimized Remote service name resolution (`affe6fd`)
- Updated Remote generic registration logic (`f1d7191`)
- Updated serialization rules for `RemoteServiceRequest` (`4fbc273`)
- Modified Method matching strategy (`8a9b9cf`)
- Modified Remote auto-registration mechanism (`517a013`)

### Fixed
- Fixed bug where log `MethodName` was empty (`5159a82`)
- Fixed Server-side method matching failure (`60b8e20`)
- Fixed Remote.Server generic service name matching bug (`2ea5e2c`)

---

## v8.0.17 (2026-06-18)

### Added
- Added Remote module supporting remote proxy pattern (`e01a660`)
  - Supports creating standalone remote proxies (`e3daa0c`)
  - Added `.netstandard2.0/2.1` support (`acc8f4c`)
- `SessionManager` max SQL history count is now configurable (`75796e6`)
- Added `CycleDetector` to detect Expr circular references (`02df339`)
- Expr added `Cast` extension (`f09a309`)
- Added `SelectAll` extension method (`8e2073c`)
- Service added `SearchAs` method (`0578422`)
- Added ternary operator (`a ? b : c`) parsing to `CASE` statement (`eb0def4`)

### Changed
- Optimized DAOContext invalidation logic; cached DAOContext also validates expiry (`9ec248e`)
- Removed `PropEqual` and comparison operator `Prop` overloads (`a6dc0fe`)
- Updated `Case` extension method (`c7a00ce`)
- Optimized Like escape logic; escape fragment only generated when necessary (`804d85c`)
- Optimized TableView associated table sorting; `IfNull` unified to `COALESCE` (`ed2ebea`)
- Metadata column collection type changed from `Array` to `ReadOnlyCollection` (`233b578`)
- Removed `AndIf`, `OrIf`, `WhereIf`, `SetIf` extension methods (`a779a00`)

### Refactored
- Refactored `ExprVisitor` and `ExprValidator` to support multiple traversal and validation modes (`0c0499c`)
- Removed CodeGen and WebDemo projects (`fee1155`)

### Fixed
- Fixed bug where Join conditions without priority failed to add parentheses (`ebc87e6`)
- Fixed default SqlBuilder matching to correctly identify PostgreSql and SqlServer (`e664272`)
- Fixed property generation order for view extensions (`1dbe1f4`)

---

## v8.0.16 (2026-05-27)

### Added
- Added `Expr.Reduce` extension (`c206a6d`)
- Added `ServiceExceptionHook` for global or Attribute-based custom exception handling (`d61d72f`)
- Added `PropertyOrder` attribute sorting (`7f7dd0e`)

### Changed
- Expression SqlName is case-insensitive (`63a9d60`)
- `ToSqlSelectSetType` renamed to `ToSelectSetTypeSql` (`5669984`)
- `AndExpr`/`OrExpr` use set deduplication (`cb466b7`)
- `ExistsRelated` logic optimized (`07556af`)
- `TableDefinition.ConstFilter` is now mutable (`24c772d`)
- `AutoRegister` default lifetime changed to Singleton (`bd823ed`)
- `Update`, `GetObject`, `Delete`, `Exists` use numeric parameter names (`adcf2c4`)
- Non-primitive value types include type marker in serialization (`c14c53e`)
- Modified `SelectExpr` priority and scope logic (`f427a62`)
- Modified `TableExpr` serialization format; properties at type level (`c5cc577`)

### Fixed
- Fixed non-generic method `ServiceDescription` cache error (`7bb3892`)
- Fixed Timestamp column not taking effect (`378759d`)
- Oracle auto-increment sequence return parameter uses fixed name to avoid keyword conflicts (`b005a9a`)
- Fixed table creation SQL (`ddbc69c`)
- Fixed CTE expression SQL generation (`8199a7e`)
- Fixed exists method bug (`ab62ace`)

### Refactored
- `FromExpr` and `TableJoinExpr` refactored to support subqueries as source (`8ec2c1d`)

---

## v8.0.15 (2026-05-10)

### Added
- Added CTE expression support (`cc4f8c2`)

---

## v8.0.14 (2026-04-28)

### Added
- Added CodeGen project (`c862ffd`)
- Added `StringExprConverter` with `Parse`/`ParsePagedQuery` methods by entity type (`b4d422f`)

### Changed
- Optimized generic Controller and dynamic Controller code generation (`8a215f3`)

### Refactored
- `UpdateExpr`, `DeleteExpr` no longer classified as `SqlSegment` (`fa0355d`)

### Fixed
- Fixed Insert method error with non-parameter return for auto-increment columns (`073b4f7`)

---

## v8.0.13 (2026-04-10)

### Added
- Added property constant filter mechanism (`ad1148c`)
- `TableJoin` supports specifying foreign table primary key (`7cf1afc`)
- `ForeignType` can declare multiple (`35f4e47`)

### Changed
- Optimized `ForeignColumn` table matching; auto-matches by entity type (`9649b2d`)
- Disabled auto-expand association tables; now specified at association time (`9717515`, `dc71969`)
- Optimized log format (`34af777`)
- `And`/`Or` serialization uses compact mode (`4090979`)
- Optimized SQL format generation (`b904948`)
- Lambda ignores implicit/explicit conversion operators (`f7ff97b`)
- Lambda `Contains` resolves by name only; correctly handles `Span` extension (`f6a5edd`)
- Optimized Lambda value resolution; ignores `op_Implicit`, `op_Explicit` (`109f22e`)
- Database name quote character defaults to `"` (`2c315cc`)
- `Update` Set element type changed to `SetItem` (`2c315cc`)
- Modified SQL indentation logic (`b8a14b1`)
- `SqlBuilder.BuildConcatSql` uses `ValueStringBuilder` (`f8b0cea`)
- Lambda expression registration uses static method initialization (`7858b14`)
- Oracle identity columns from sequence or expression auto-add identity column in batch insert (`34ba25f`)

### Refactored
- `LogicSet` split into `AndExpr` and `OrExpr` (`6dd1063`)
- Optimized `ToSql` implementation (`ab7fcbc`)

### Fixed
- Fixed Oracle `DateDiff` function bug (`2c315cc`)
- Fixed `select union` format error; batch insert with identity column uses `BuildBatchIdentityInsertSql` (`c5f3115`)
- Fixed Oracle auto-increment column insert error (`77e5518`)
- Fixed `To` extension method parsing when Expr is empty (`b904948`)
- Fixed `ExistsRelated` alias generation for foreign key via external column (`b8a14b1`)
- Fixed serialization bug (`a694a0f`)

---

## v8.0.12 (2026-04-02)

### Added
- Added `ExprValidator` validation mechanism (`2c9245e`)
- Added `TableExpr` and `TableJoinExpr` with serialization (`1ee64b3`, `5b2a116`)
- Added `IdentityIncreasement` configuration (`894cc61`)
- `DAOContextPool` added `ClearPool` method (`8e6e0ed`)
- Added `netstandard2.1` support (`8e6e0ed`)
- Added date format support; added `TimeSpan` type support (`eef7074`)
- Added `DateTime` subtraction support (`4768942`)
- Added `ExistsRelated` method for auto-association (`6aa5ff2`)
- Added SqlGen ExprString parsing (`6eac5d5`)
- Added `IObjectViewDAO` methods (`c63b3a7`)
- Added column default value support (`07b30b5`)
- Added window function support (`b7245d1`)
- Added `.net standard 2.0` ExprString support; main table default alias `T0` (`4aaa3bd`)
- Added `ExprVisitor.Visit` for Expr traversal (`fc91353`)
- Added `DDLGenerator` for table DDL generation (`fc91353`)
- Added pre-generated entity `DataReaderConverter` (`8ac1ca6`)
- BulkInsert supports auto table creation (`586ab62`)
- Added Lambda sharding (`b94ca29`)
- Added `ExprInterpolatedStringHandler` for interpolated string SQL building (`bf0f85d`)
- Added `ForeignExists` method (`2a5960b`)
- Completed `ExprSqlConverter` ToSql implementation (`OrderByExpr`, `SectionExpr`, `GroupByExpr`, `HavingExpr`, `WhereExpr`) (`a41196e`)
- Implemented ExprString for `ObjectViewDAO` (`fd0f746`)
- Added `ExprType` enum (`c82e1ac`)
- Added custom method handler and SQL builder (`31be232`)

### Changed
- Connection keep-alive default to 10 minutes (`214e399`)
- Optimized SQL name validation (`31be232`)
- `UpdateExpr` set list changed to `PropertyExpr` type; added `Set` extension (`cf61373`)
- Optimized DataReader; fallback to `SqlBuilder.ConvertFromDbValue` (`1be94b5`)
- Optimized Lambda parsing; computes expression values when possible (`eef7074`)
- `DbCommandProxy` optimized (`95f7274`)
- Optimized session context creation (`be1436c`)
- Optimized SQL generation with indentation (`6776934`)
- Optimized Expr static and extension methods (`607b9b0`, `8d47446`)
- Optimized default table alias generation (`b2ef1f0`)
- `Expr.Prop("T0.Name")` form removed; use `Expr.Prop("T0", "Name")`; original `Expr.Prop(string, object)` renamed to `Expr.PropEqual(string, object)` (`035c884`)
- `selectItemExpr` adds alias by default (`adcf3cd`)
- Data reading optimized with dynamic compilation (`207fbe2`)
- Removed `ObjectViewDAO<T>.ToList` and `SearchOne`; unified via `Search` (`d6b5f5d`)
- `SessionManager.Current` uses lazy loading (`658b85a`)
- Added `DAOContextPool` log info (`dcb7b37`)
- Modified log enum to reduce dependencies (`46c8cb0`)
- `DAOContext` releases Connection asynchronously (`ef335d0`)
- Optimized Command generation; supports async creation (`d559005`)
- Modified `CommandResult` logic (`a568840`)
- Modified `DAOContext` release logic (`377cfe0`)
- Modified `SelectExpr` logic (`f58e86e`)
- Added `SqlBuilder` configuration and custom `SqlBuilder` registration support (`60041c8`)
- Updated `InterpolatedStringHandler` condition to `NET8_0_OR_GREATER || NET10_0_OR_GREATER` (`8f9819b`)
- Optimized `ReplaceParam` using trie and `ValueStringBuilder` (`35be944`)
- Renamed `SelectItemExpr.Name` to `Alias` (`35be944`)
- Added lambda extensions for `DataViewDAO` and `ObjectViewDAO` (`35be944`)

### Refactored
- Removed `LiteOrm.ChartGenerator` directory (`c0b87f1`)
- Removed `AggregateFunctionExpr` (`4ff4fa4`)
- Optimized table creation mechanism (`b567251`)
- Optimized Lambda converter (`8a97952`)
- `ObjectViewDAO.ConvertToObjectHandler` made static (`557c8dd`)
- Extracted `IDAOContext` interface to `LiteOrm.Common` (`3ebaf0c`)
- `ExprInterpolatedStringHandler` moved to LiteOrm project (`e292c23`)
- `SqlBuilder` and `CreateSqlBuildContext` made internal (`e3f1eb8`)
- `ExprInterpolatedStringHandler` refactored to use `DAOBase` parameter (`25db9d9`)
- Refactor Result types and update DAO implementations (`aafea93`)

### Fixed
- Fixed `FromExpr` serialization issue (`4b4b90b`)
- Fixed Test project build errors (`7024826`)
- Improved Exists method handling; fixed `ExistsRelated` error message (`feffcc1`)
- Fixed Sqlite `Now`/`Today` timezone issue (`8e6e0ed`)
- Fixed subquery SQL generation bug (`b25e120`)
- Fixed build errors; configured benchmark to short mode (`ee100b6`)
- Fixed `SumOver` method implementation (`c26d2c7`)
- Fixed Oracle auto-increment column DDL (`96cbadf`)
- Fixed single-table mode; table name without alias (`4afb92d`)

---

## v8.0.10 / v8.0.11 (2026-03-11)

### Added
- Custom `SqlBuilder` registration and configuration support (`60041c8`)

### Changed
- `ServiceInvokeInterceptor` throws original exception (`e88a0ad`)
- `ObjectViewDAO.ConvertToObjectHandler` made static (`557c8dd`)
- Data reading optimized with dynamic compilation (`207fbe2`)
- Removed `ObjectViewDAO<T>.ToList` and `SearchOne`; unified via `Search` (`d6b5f5d`)
- `selectItemExpr` adds alias by default (`adcf3cd`)
- Main table default alias changed to `T0` (`8aa2c31`)
- Benchmark changed from Service to DAO style (`e207344`)

### Fixed
- Fixed Assembly loading bug (`05f8b3a`)

---

## v8.0.8 / v8.0.9 (2026-03-06)

### Added
- Implemented ExprString for `ObjectViewDAO` (`fd0f746`)
- Completed `ExprSqlConverter` ToSql implementation (`a41196e`)
- Added `ExprInterpolatedStringHandler` for interpolated string SQL building (`bf0f85d`)
- Added `ForeignExists` method (`2a5960b`)
- Added Lambda sharding (`b94ca29`)
- Completed Expr API validation and tests (`5c5ba35`)

### Changed
- Optimized session management; `SessionManager` lifecycle fully maintained by container Scope (`c3b52fc`)
- Optimized `ReplaceParam` using trie and `ValueStringBuilder` (`35be944`)
- Added lambda extensions for `DataViewDAO` and `ObjectViewDAO` (`35be944`)
- Renamed `SelectItemExpr.Name` to `Alias` (`35be944`)
- Updated `InterpolatedStringHandler` condition to `NET8_0_OR_GREATER || NET10_0_OR_GREATER` (`8f9819b`)
- `ExprInterpolatedStringHandler` refactored to use `DAOBase` parameter (`25db9d9`)

### Refactored
- Optimized table creation mechanism (`b567251`)
- Optimized Lambda converter (`8a97952`)
- Extracted `IDAOContext` interface to `LiteOrm.Common` (`3ebaf0c`)
- `ExprInterpolatedStringHandler` moved to LiteOrm project (`e292c23`)
- `SqlBuilder` and `CreateSqlBuildContext` made internal (`e3f1eb8`)
- Refactor Result types and update DAO implementations (`aafea93`)

---

## v8.0.0 ~ v8.0.7 (2026-02-11)

### Added
- Initial version; completed Expr API validation and tests (`5c5ba35`, `2948732`)
