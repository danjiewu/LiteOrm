# API 索引

LiteOrm 已不再把独立的 `API_REFERENCE` 文档作为主入口维护。

本文档改为按使用场景整理接口、能力入口和扩展点，便于在 docs 体系内快速定位信息。

## 快速入口

- [示例索引](./05-example-index.md)
- [生成 SQL 示例](./06-sql-examples.md)
- [数据库差异与兼容性说明](./07-database-compatibility.md)

## 按使用场景查阅

### 配置与启动

- `RegisterLiteOrm()`（`LiteOrm.DependencyInjection`，Autofac + AOP）
- `AddLiteOrm()`（纯 MS DI，基础库内置，无 AOP）
- `RegisterSqlBuilder(...)`
- `SqlBuilder.BulkProvider`（批量插入提供程序）
- `[AutoRegister]`（`Lifetime` / `Policy` / `Enabled` / `Key` / `AutoActivate`）—— 标记需要自动注册到 DI 容器的类或接口
- `[assembly: LiteOrmCodeGen]`（`LiteOrmCodeGenAttribute`）—— 显式启用源生成；开启 AOT 构建时自动启用
- 数据源配置、连接池配置、只读副本配置

对应文档：

- [配置参考](./01-configuration-reference.md)
- [AOT 与源生成](../03-advanced-topics/06-aot.md)
- [数据库差异与兼容性说明](./07-database-compatibility.md)

### 手动构造（不使用 DI 宿主）

- `LiteOrmContext`：`new LiteOrmContext(ILoggerFactory? loggerFactory = null)` 起链
  - `AddDataSource<TConnection>(name, connectionString, @default, syncTable, sqlBuilder, poolSize, maxPoolSize, paramCountLimit, keepAliveDuration)` —— 连接池参数与建表同步在此一次设定；提供程序类型取 `TConnection`
  - `AddDataSource(DataSourceConfig config, bool @default = false)` —— 在代码里直接构造 `DataSourceConfig` 时使用
  - `CreateSession()` → `SessionManager`（同时绑定为 `SessionManager.Current`）
  - `GetDataSource(name)` / `DataSources` / `DefaultDataSourceName`
  - `Dispose()`
- DAO 构造：`new ObjectDAO<T>(session)` / `new ObjectViewDAO<T>(session)` / `new DataDAO<T>(session)` / `new DataViewDAO<T>(session)`
- `DataSourceConfig`（`Name` / `ConnectionString` / `ProviderType` / `SqlBuilderType` / `PoolSize` / `MaxPoolSize` / `ParamCountLimit` / `KeepAliveDuration` / `SyncTable` / `ReadOnlyConfigs`）
  - `ProviderType` / `SqlBuilderType` 均为可赋值的 `Type?`。类型名字符串只在 `appsettings.json` 的 `Provider` / `SqlBuilder` 键里使用，加载配置时立即解析为 `Type`（失败抛 `TypeLoadException`）

对应文档：

- [第一个完整示例（手动构造，无 DI）](../01-getting-started/04-first-example-manual.md)

### 实体映射与视图模型

- `[Table]`
- `[Column]`（含 `ColumnMode.Computed` 计算列与 `Expression` 表达式）
- `[PropertyOrder]`
- `[ForeignType]`
- `[ForeignColumn]`
- `[TableJoin]`
- `AutoExpand`

对应文档：

- [实体映射与数据源](../02-core-usage/01-entity-mapping.md)
- [关联查询](../02-core-usage/08-associations.md)

### 查询接口

- `Search` / `SearchAsync`
- `SearchAs` / `SearchAsAsync`（含 `Expression<Func<IQueryable<T>, IQueryable<TResult>>>` Lambda 投影扩展）
- `SearchOne` / `SearchOneAsync`
- `SearchOneAs` / `SearchOneAsAsync`
- `Exists` / `ExistsAsync`
- `Count` / `CountAsync`
- `Expr`、`LogicExpr`、`SelectExpr`
- `SelectAll()` / `Cast(DbValueType)`
- Lambda 三目运算符 `?:`（转为 `CASE`）
- 表达式名称与别名忽略大小写
- `DbValueType`（`Default` / `Json` / `Jsonb` / `Array`）与 `DbValueTypeMap`
- `ObjectViewDAO<T>.Search(...)`
- `SearchAs<T>()`
- `LiteOrm.Pgsql` 数组 / JSONB 扩展（`ArrayToString`、`ArrayAppend`、`Any`、`JsonbExtractPath` 等）
- `JsonExprExtensions`（`JsonExtract`、`JsonValue`、`JsonContains`、`JsonObject` 等）
- `RawSql`（`ExprString` 的辅助标记类型，专用于插入不适合参数化的动态值（如 `LIMIT`/`OFFSET` 行数、`ASC`/`DESC`、动态列名）；纯静态文本直接写字面量即可，详见 [ExprString 指南 - 第 8 节](../02-core-usage/07-exprstring-guide.md#8-插入原始-sqlrawsql)）

对应文档：

- [Expr 使用指南](../02-core-usage/06-expr-guide.md)
- [查询总览](../02-core-usage/04-query-overview.md)
- [示例索引](./05-example-index.md)
- [生成 SQL 示例](./06-sql-examples.md)

### 写入接口

- `Insert` / `InsertAsync`
- `Update` / `UpdateAsync`
- `UpdateAll` / `UpdateAllAsync`（按 `UpdateExpr` 条件更新）
- `ObjectDAO<T>.Update(entity, timestamp)` / `UpdateAsync(entity, timestamp)`
- `Delete` / `DeleteAsync`
- `DeleteID` / `DeleteIDAsync`（按主键删除）
- `DeleteAll` / `DeleteAllAsync`（按 `LogicExpr` 条件删除）
- `BatchInsert` / `BatchUpdate`
- `UpdateOrInsert`
- `ObjectDAO<T>`
- `IBulkProvider`

对应文档：

- [CRUD 指南](../02-core-usage/03-crud-guide.md)
- [事务管理](../06-di/01-transactions.md)
- [示例索引](./05-example-index.md)
- [生成 SQL 示例](./06-sql-examples.md)

### 服务层（实体服务）

- `IEntityService<T>` / `IEntityServiceAsync<T>` —— 实体增删改查、批量操作与 `UpdateOrInsert` 的服务契约
- `IEntityViewService<T>` / `IEntityViewServiceAsync<T>` —— 只读视图服务契约
- `EntityService<T>` / `EntityService<T, TView>` / `EntityViewService<T>` —— 上述契约的默认实现
- `IEntityServiceEvent<T>` —— 插入 / 更新 / 删除 / `UpdateOrInsert` / `DeleteID` / `DeleteAll` / `UpdateAll` 前后的回调（Before 返回 false 可取消；批量方法逐条触发单条事件）
- `EntityOperation<T>` / `OpDef` —— 事件参数中携带的实体与操作类型

对应文档：

- [视图模型与服务](../02-core-usage/02-view-models-and-services.md)
- [CRUD 指南](../02-core-usage/03-crud-guide.md)

### 高级特性

- `[Transaction]`
- `IServiceInvokingEvent` / `IServiceInvokedEvent` / `IServiceExceptionEvent`
- `SessionManager`
- `IArged` / `TableArgs`
- 窗口函数相关扩展
- `Expr.ExistsRelated(...)`

对应文档：

- [事务管理](../06-di/01-transactions.md)
- [日志与诊断](../06-di/03-logging.md)
- [分表分库与 TableArgs](../03-advanced-topics/02-sharding-and-tableargs.md)
- [窗口函数](../03-advanced-topics/04-window-functions.md)
- [示例索引](./05-example-index.md)
- [生成 SQL 示例](./06-sql-examples.md)
- [数据库差异与兼容性说明](./07-database-compatibility.md)

### 扩展开发

- `LambdaExprConverter.RegisterMethodHandler`
- `LambdaExprConverter.RegisterMemberHandler`
- `SqlBuilder.RegisterFunctionSqlHandler`
- `FunctionSqlHandler`
- `FunctionExprValidator`
- `CycleDetector` — 检测 Expr 树中的循环引用

对应文档：

- [表达式扩展](../04-extensibility/01-expression-extension.md)
- [函数验证器](../04-extensibility/02-function-validator.md)
- [自定义 SqlBuilder / 方言扩展](../04-extensibility/03-custom-sqlbuilder.md)
- [数据库差异与兼容性说明](./07-database-compatibility.md)

### 依赖注入与远程服务

- `RegisterLiteOrm()` / `RegisterLiteOrm(Action<LiteOrmOptions>)`（`LiteOrm.DependencyInjection`，Autofac + AOP）
- `AddLiteOrm()` / `AddLiteOrm(Action<LiteOrmOptions>)`（基础库，纯 MS DI，无 AOP）
- 工厂重载 `AddLiteOrm(Func<IServiceProvider, LiteOrmOptions>)` / `RegisterLiteOrm(Func<IServiceProvider, LiteOrmOptions>)`
- `AddLiteOrmRemote(...)` / `AddRemoteServer(...)` / `AddRemoteService<TService>()` / `AddRemoteServiceFactory<TFactory>()`
- `[Service]` / `[ServiceMethod]`
- `ITypeNameResolver` / `TypeNameResolverFactory` / `DefaultTypeResolver`

对应文档：

- [第一个完整示例（仅基础库）](../01-getting-started/03-first-example.md)
- [事务管理](../06-di/01-transactions.md)
- [权限过滤](../06-di/02-permission-filtering.md)
- [日志与诊断](../06-di/03-logging.md)
- [远程服务调用](../03-advanced-topics/09-remote-service.md)

## 相关链接

- [返回文档目录](../README.md)
- [示例索引](./05-example-index.md)
- [生成 SQL 示例](./06-sql-examples.md)
- [数据库差异与兼容性说明](./07-database-compatibility.md)
