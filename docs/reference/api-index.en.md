# API Index

LiteOrm no longer uses standalone `API_REFERENCE` files as the primary entry point.

Use this page as a scenario-based index inside the docs set.

## Quick links

- [Example Index](./example-index.en.md)
- [Generated SQL Examples](./sql-examples.en.md)
- [Database Compatibility Notes](./database-compatibility.en.md)

## Browse by scenario

### Startup and configuration

- `RegisterLiteOrm()` (`LiteOrm.DependencyInjection`, Autofac + AOP)
- `AddLiteOrm()` (plain MS DI, built into the base library, no AOP)
- `RegisterSqlBuilder(...)`
- `SqlBuilder.BulkProvider` (bulk insert provider)
- `[AutoRegister]` (`Lifetime` / `Policy` / `Enabled` / `Key` / `AutoActivate`) — marks a class or interface for automatic DI registration
- `[assembly: LiteOrmCodeGen]` (`LiteOrmCodeGenAttribute`) — explicitly enables source generation; also enabled automatically under an AOT build
- data source settings, connection pool settings, read-only replicas

Related guides:

- [Configuration Reference](./configuration-reference.en.md)
- [AOT and Source Generation](../advanced-topics/aot.en.md)
- [Database Compatibility Notes](./database-compatibility.en.md)

### Manual construction (no DI host)

- `LiteOrmContext`: start the chain with `new LiteOrmContext(ILoggerFactory? loggerFactory = null)`
  - `AddDataSource<TConnection>(name, connectionString, @default, syncTable, sqlBuilder, poolSize, maxPoolSize, paramCountLimit, keepAliveDuration)` — pool options and table sync are set here in one call; the provider type comes from `TConnection`
  - `AddDataSource(DataSourceConfig config, bool @default = false)` — for cases where the `DataSourceConfig` is built directly in code
  - `CreateSession()` → `SessionManager` (also binds `SessionManager.Current`)
  - `GetDataSource(name)` / `DataSources` / `DefaultDataSourceName`
  - `Dispose()`
- DAO construction: `new ObjectDAO<T>(session)` / `new ObjectViewDAO<T>(session)` / `new DataDAO<T>(session)` / `new DataViewDAO<T>(session)`
- `DataSourceConfig` (`Name` / `ConnectionString` / `ProviderType` / `SqlBuilderType` / `PoolSize` / `MaxPoolSize` / `ParamCountLimit` / `KeepAliveDuration` / `SyncTable` / `ReadOnlyConfigs`)
  - `ProviderType` / `SqlBuilderType` are assignable `Type?` values. A type-name string is only used in the `Provider` / `SqlBuilder` keys of `appsettings.json`, resolved to a `Type` immediately when configuration is loaded (throwing `TypeLoadException` on failure)

Related guides:

- [First Full Example (Manual, No DI)](../getting-started/first-example-manual.en.md)

### Entity mapping and view models

- `[Table]`
- `[Column]` (including `ColumnMode.Computed` computed columns and the `Expression` property)
- `[PropertyOrder]`
- `[ForeignType]`
- `[ForeignColumn]`
- `[TableJoin]`
- `AutoExpand`

Related guides:

- [Entity mapping and data sources](../core-usage/entity-mapping.en.md)
- [Associations](../core-usage/associations.en.md)

### Query APIs

- `Search` / `SearchAsync`
- `SearchAs` / `SearchAsAsync` (including the `Expression<Func<IQueryable<T>, IQueryable<TResult>>>` Lambda projection extension)
- `SearchOne` / `SearchOneAsync`
- `SearchOneAs` / `SearchOneAsAsync`
- `Exists` / `ExistsAsync`
- `Count` / `CountAsync`
- `Expr`, `LogicExpr`, `SelectExpr`
- `SelectAll()` / `Cast(DbValueType)`
- Lambda conditional operator `?:` (rendered as `CASE`)
- case-insensitive expression names and aliases
- `DbValueType` (`Default` / `Json` / `Jsonb` / `Array`) and `DbValueTypeMap`
- `ObjectViewDAO<T>.Search(...)`
- `SearchAs<T>()`
- `LiteOrm.Pgsql` array / JSONB extensions (`ArrayToString`, `ArrayAppend`, `Any`, `JsonbExtractPath`, etc.)
- `JsonExprExtensions` (`JsonExtract`, `JsonValue`, `JsonContains`, `JsonObject`, etc.)
- `RawSql` (an `ExprString` helper marker type exclusively for inserting dynamic values unsuitable for parameterization (e.g. `LIMIT`/`OFFSET` row counts, `ASC`/`DESC`, dynamic column names); purely static text can just be written in the literal; see [ExprString Guide - Section 8](../core-usage/exprstring-guide.en.md#8-inserting-raw-sql-rawsql))

Related guides:

- [Expr Guide](../core-usage/expr-guide.en.md)
- [Query Overview](../core-usage/query-overview.en.md)
- [Example Index](./example-index.en.md)
- [Generated SQL Examples](./sql-examples.en.md)

### Write APIs

- `Insert` / `InsertAsync`
- `Update` / `UpdateAsync`
- `UpdateAll` / `UpdateAllAsync` (conditional update by `UpdateExpr`)
- `ObjectDAO<T>.Update(entity, timestamp)` / `UpdateAsync(entity, timestamp)`
- `Delete` / `DeleteAsync`
- `DeleteID` / `DeleteIDAsync` (delete by primary key)
- `DeleteAll` / `DeleteAllAsync` (conditional delete by `LogicExpr`)
- `BatchInsert` / `BatchUpdate`
- `UpdateOrInsert`
- `ObjectDAO<T>`
- `IBulkProvider`

Related guides:

- [CRUD guide](../core-usage/crud-guide.en.md)
- [Transactions](../di/transactions.en.md)
- [Example Index](./example-index.en.md)
- [Generated SQL Examples](./sql-examples.en.md)

### Service layer (entity services)

- `IEntityService<T>` / `IEntityServiceAsync<T>` — the service contract for entity CRUD, batch operations, and `UpdateOrInsert`
- `IEntityViewService<T>` / `IEntityViewServiceAsync<T>` — the read-only view service contract
- `EntityService<T>` / `EntityService<T, TView>` / `EntityViewService<T>` — default implementations of the contracts above
- `IEntityServiceEvent<T>` — callbacks around insert / update / delete / `UpdateOrInsert` / `DeleteID` / `DeleteAll` / `UpdateAll` (returning false from Before cancels the operation; batch methods fire one event per row)
- `EntityOperation<T>` / `OpDef` — the entity and operation type carried in event arguments

Related guides:

- [View models and services](../core-usage/view-models-and-services.en.md)
- [CRUD guide](../core-usage/crud-guide.en.md)

### Advanced features

- `[Transaction]`
- `IServiceInvokingEvent` / `IServiceInvokedEvent` / `IServiceExceptionEvent`
- `SessionManager`
- `IArged` / `TableArgs`
- window function extensions
- `Expr.ExistsRelated(...)`

Related guides:

- [Transactions](../di/transactions.en.md)
- [Logging and Diagnostics](../di/logging.en.md)
- [Sharding and TableArgs](../advanced-topics/sharding-and-tableargs.en.md)
- [Window functions](../advanced-topics/window-functions.en.md)
- [Example Index](./example-index.en.md)
- [Generated SQL Examples](./sql-examples.en.md)
- [Database Compatibility Notes](./database-compatibility.en.md)

### Extensibility

- `LambdaExprConverter.RegisterMethodHandler`
- `LambdaExprConverter.RegisterMemberHandler`
- `SqlBuilder.RegisterFunctionSqlHandler`
- `SqlBuilder.RegisterSimpleFunctionSqlHandler`
- `FunctionSqlHandler`
- `FunctionExprValidator`
- `CycleDetector` — detects circular references in Expr trees

Related guides:

- [Expression extension](../extensibility/expression-extension.en.md)
- [Function expression validator](../extensibility/function-validator.en.md)
- [Custom SqlBuilder and dialect extension](../extensibility/custom-sqlbuilder.en.md)
- [Database Compatibility Notes](./database-compatibility.en.md)

### Dependency injection and remote services

- `RegisterLiteOrm()` / `RegisterLiteOrm(Action<LiteOrmOptions>)` (`LiteOrm.DependencyInjection`, Autofac + AOP)
- `AddLiteOrm()` / `AddLiteOrm(Action<LiteOrmOptions>)` (base library, plain MS DI, no AOP)
- factory overloads `AddLiteOrm(Func<IServiceProvider, LiteOrmOptions>)` / `RegisterLiteOrm(Func<IServiceProvider, LiteOrmOptions>)`
- `AddLiteOrmRemote(...)` / `AddRemoteServer(...)` / `AddRemoteService<TService>()` / `AddRemoteServiceFactory<TFactory>()`
- `[Service]` / `[ServiceMethod]`
- `ITypeNameResolver` / `TypeNameResolverFactory` / `DefaultTypeResolver`

Related guides:

- [First complete example (base library only)](../getting-started/first-example.en.md)
- [Transactions](../di/transactions.en.md)
- [Permission filtering](../di/permission-filtering.en.md)
- [Logging and diagnostics](../di/logging.en.md)
- [Remote service invocation](../advanced-topics/remote-service.en.md)

## Related links

- [Back to docs hub](../README.md)
- [Example Index](./example-index.en.md)
- [Generated SQL Examples](./sql-examples.en.md)
- [Database Compatibility Notes](./database-compatibility.en.md)
