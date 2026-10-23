# API 索引

LiteOrm 已不再把独立的 `API_REFERENCE` 文档作为主入口维护。

本文档改为按使用场景整理接口、能力入口和扩展点，便于在 docs 体系内快速定位信息。

## 快速入口

- [示例索引](./example-index.md)
- [生成 SQL 示例](./sql-examples.md)
- [数据库差异与兼容性说明](./database-compatibility.md)

## 按使用场景查阅

### 配置与启动

- `RegisterLiteOrm()`（`LiteOrm.DependencyInjection`，Autofac + AOP）
- `AddLiteOrm()`（纯 MS DI，基础库内置，无 AOP）
- `RegisterSqlBuilder(...)`
- `SqlBuilder.BulkProvider`（批量插入提供程序）
- `[AutoRegister]`（`Lifetime` / `Policy` / `Enabled` / `Key` / `AutoActivate`）：标记需要自动注册到 DI 容器的类或接口
- `[assembly: LiteOrmCodeGen]`（`LiteOrmCodeGenAttribute`）：显式启用源生成；开启 AOT 构建时自动启用
- 数据源配置、连接池配置、只读副本配置

对应文档：

- [配置参考](./configuration-reference.md)
- [AOT 与源生成](../advanced-topics/aot.md)
- [数据库差异与兼容性说明](./database-compatibility.md)

### 手动构造（不使用 DI 宿主）

- `LiteOrmContext`：`new LiteOrmContext(ILoggerFactory? loggerFactory = null)` 起链
  - `AddDataSource<TConnection>(name, connectionString, @default, syncTable, sqlBuilder, poolSize, maxPoolSize, paramCountLimit, keepAliveDuration)`：连接池参数与建表同步在此一次设定；提供程序类型取 `TConnection`
  - `AddDataSource(DataSourceConfig config, bool @default = false)`：在代码里直接构造 `DataSourceConfig` 时使用
  - `CreateSession()` → `SessionManager`（同时绑定为 `SessionManager.Current`）
  - `GetDataSource(name)` / `DataSources` / `DefaultDataSourceName`
  - `Dispose()`
- DAO 构造：`new ObjectDAO<T>(session)` / `new ObjectViewDAO<T>(session)` / `new DataDAO<T>(session)` / `new DataViewDAO<T>(session)`
- `DataSourceConfig`（`Name` / `ConnectionString` / `ProviderType` / `SqlBuilderType` / `PoolSize` / `MaxPoolSize` / `ParamCountLimit` / `KeepAliveDuration` / `SyncTable` / `ReadOnlyConfigs`）
  - `ProviderType` / `SqlBuilderType` 均为可赋值的 `Type?`。类型名字符串只在 `appsettings.json` 的 `Provider` / `SqlBuilder` 键里使用，加载配置时立即解析为 `Type`（失败抛 `TypeLoadException`）

对应文档：

- [第一个完整示例（手动构造，无 DI）](../getting-started/first-example-manual.md)

### 实体映射与视图模型

- `[Table]`
- `[Column]`（含 `ColumnMode.Computed` 计算列与 `Expression` 表达式）
- `[PropertyOrder]`
- `[ForeignType]`
- `[ForeignColumn]`
- `[TableJoin]`
- `AutoExpand`

对应文档：

- [实体映射与数据源](../core-usage/entity-mapping.md)
- [关联查询](../core-usage/associations.md)

### 查询接口

- `Search` / `SearchAsync`（含 IQueryable Lambda 与 `IQueryable<T>` 重载）
- `SearchAs` / `SearchAsAsync`（含 `Expression<Func<IQueryable<T>, IQueryable<TResult>>>` Lambda 投影扩展与 `IQueryable<TResult>` 重载）
- `SearchOne` / `SearchOneAsync`
- `SearchOneAs` / `SearchOneAsAsync`
- `Exists` / `ExistsAsync`
- `Count` / `CountAsync`
- `Expr`、`LogicExpr`、`SelectExpr`、`Expr.Query`
- `QuerySource<T>`（`Expr.From<T>()` 的返回类型，实现 `IQueryable<T>` / `IOrderedQueryable<T>`）与 `IQuerySourceExecutor<T>`
- 多表查询源 `QuerySource<T1, T2>` … `QuerySource<T1, …, T7>`，每次 `Join<TNext>` 追加一张右表
- `QuerySource<T>.Join<T2>(Expression<Func<T1, T2, bool>>, TableJoinType?)` / `JoinRelated<T2>(...)`；多表版本的条件参数按「已连接各表 + 新右表」顺序一表一个；`JoinRelated` 推断关联时按连接顺序从后向前匹配，主表最后
- `QuerySource<T>.GetQueryExpr()` 与 `IQueryable<T>.GetQueryExpr()` / `AsSource()`
- `LambdaExprExtensions` 的 `IQueryable<T>` 重载（`Search` / `SearchOne` / `SearchAsync` / `SearchOneAsync` / `SearchAs` / `SearchOneAs` / `SearchAsAsync` / `SearchOneAsAsync`）
- `DAOExtensions` 为 `IDataViewDAO<TInput>` 提供的 `IQueryable<TResult>` `Search` 重载
- `SelectAll()` / `Cast(DbValueType)`
- Lambda 三目运算符 `?:`（转为 `CASE`）
- 表达式名称与别名忽略大小写
- `DbValueType`（`Default` / `Json` / `Jsonb` / `Array`）与 `DbValueTypeMap`
- `ObjectViewDAO<T>.Search(...)`
- `SearchAs<T>()`
- `LiteOrm.Pgsql` 数组 / JSONB 扩展（`ArrayToString`、`ArrayAppend`、`Any`、`JsonbExtractPath` 等）
- `JsonExprExtensions`（`JsonExtract`、`JsonValue`、`JsonContains`、`JsonObject` 等）
- `RawSql`（`ExprString` 的辅助标记类型，专用于插入不适合参数化的动态值（如 `LIMIT`/`OFFSET` 行数、`ASC`/`DESC`、动态列名）；纯静态文本直接写字面量即可，详见 [ExprString 指南 - 第 8 节](../core-usage/exprstring-guide.md#8-插入原始-sqlrawsql)）

对应文档：

- [Expr 使用指南](../core-usage/expr-guide.md)
- [查询总览](../core-usage/query-overview.md)
- [示例索引](./example-index.md)
- [生成 SQL 示例](./sql-examples.md)

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

- [CRUD 指南](../core-usage/crud-guide.md)
- [事务管理](../di/transactions.md)
- [示例索引](./example-index.md)
- [生成 SQL 示例](./sql-examples.md)

### 服务层（实体服务）

- `IEntityService<T>` / `IEntityServiceAsync<T>`：实体增删改查、批量操作与 `UpdateOrInsert` 的服务契约
- `IEntityViewService<T>` / `IEntityViewServiceAsync<T>`：只读视图服务契约
- `EntityService<T>` / `EntityService<T, TView>` / `EntityViewService<T>`：上述契约的默认实现
- `IEntityServiceEvent<T>`：插入 / 更新 / 删除 / `UpdateOrInsert` / `DeleteID` / `DeleteAll` / `UpdateAll` 前后的回调（Before 返回 false 可取消；批量方法逐条触发单条事件）
- `EntityOperation<T>` / `OpDef`：事件参数中携带的实体与操作类型

对应文档：

- [视图模型与服务](../core-usage/view-models-and-services.md)
- [CRUD 指南](../core-usage/crud-guide.md)

### 高级特性

- `[Transaction]`
- `IServiceInvokingEvent` / `IServiceInvokedEvent` / `IServiceExceptionEvent`
- `SessionManager`
- `IArged` / `TableArgs`
- 窗口函数相关扩展
- `Expr.ExistsRelated(...)`

对应文档：

- [事务管理](../di/transactions.md)
- [日志与诊断](../di/logging.md)
- [分表分库与 TableArgs](../advanced-topics/sharding-and-tableargs.md)
- [窗口函数](../advanced-topics/window-functions.md)
- [示例索引](./example-index.md)
- [生成 SQL 示例](./sql-examples.md)
- [数据库差异与兼容性说明](./database-compatibility.md)

### 扩展开发

- `LambdaExprConverter.RegisterMethodHandler`
- `LambdaExprConverter.RegisterMemberHandler`
- `SqlBuilder.RegisterFunctionSqlHandler`
- `SqlBuilder.RegisterSimpleFunctionSqlHandler`
- `FunctionSqlHandler`
- `FunctionExprValidator`
- `CycleDetector` — 检测 Expr 树中的循环引用

对应文档：

- [表达式扩展](../extensibility/expression-extension.md)
- [函数验证器](../extensibility/function-validator.md)
- [自定义 SqlBuilder / 方言扩展](../extensibility/custom-sqlbuilder.md)
- [数据库差异与兼容性说明](./database-compatibility.md)

### 依赖注入与远程服务

- `RegisterLiteOrm()` / `RegisterLiteOrm(Action<LiteOrmOptions>)`（`LiteOrm.DependencyInjection`，Autofac + AOP）
- `AddLiteOrm()` / `AddLiteOrm(Action<LiteOrmOptions>)`（基础库，纯 MS DI，无 AOP）
- 工厂重载 `AddLiteOrm(Func<IServiceProvider, LiteOrmOptions>)` / `RegisterLiteOrm(Func<IServiceProvider, LiteOrmOptions>)`
- `AddLiteOrmRemote(...)` / `AddRemoteServer(...)` / `AddRemoteService<TService>()` / `AddRemoteServiceFactory<TFactory>()`
- `[Service]` / `[ServiceMethod]`
- `ITypeNameResolver` / `TypeNameResolverFactory` / `DefaultTypeResolver`

对应文档：

- [第一个完整示例（仅基础库）](../getting-started/first-example.md)
- [事务管理](../di/transactions.md)
- [权限过滤](../advanced-topics/permission-filtering.md)
- [日志与诊断](../di/logging.md)
- [远程服务调用](../advanced-topics/remote-service.md)

## 源码索引

想改框架代码或确认某个行为的实现位置，按这张表找文件。

| 想找什么 | 去哪个文件 |
|:---|:---|
| 连接 / 事务 / 命令缓存 | `LiteOrm/DAOContext/DAOContext.cs` |
| 连接池与只读副本 | `LiteOrm/DAOContext/DAOContextPool.cs` |
| 数据源到连接池 | `LiteOrm/DAOContext/DAOContextPoolFactory.cs` |
| 会话与连接绑定 | `LiteOrm/SessionManager/SessionManager.cs` |
| DAO 基类与命令装配 | `LiteOrm/DAO/DAOBase.cs` |
| 写 DAO | `LiteOrm/DAO/ObjectDAO.cs` |
| 读 DAO | `LiteOrm/DAO/ObjectViewDAO.cs` |
| 结果包装 | `LiteOrm/DAO/ResultTypes.cs` |
| 命令代理 | `LiteOrm/DbAccess/DbCommandProxy.cs` |
| 读取器持锁 | `LiteOrm/DbAccess/AutoLockDataReader.cs` |
| 行到对象映射 | `LiteOrm/Converter/DataReaderConverter.cs` |
| 业务服务与事件 | `LiteOrm/Service/EntityService.cs` |
| 无 DI 手动入口 | `LiteOrm/Classes/LiteOrmContext.cs` |
| MS DI 注册 | `LiteOrm/Register/LiteOrmServiceExtensions.cs` |
| 表达式 → SQL | `LiteOrm.Common/Converter/ExprSqlConverter.cs` |
| 表达式模型 | `LiteOrm.Common/Expr/Expr.cs` |
| 元数据模型 | `LiteOrm.Common/MetaData/TableDefinition.cs` |
| 特性 → 元数据 | `LiteOrm.Common/MetaData/AttributeTableInfoProvider.cs` |
| SQL 方言基类 | `LiteOrm/SqlBuilder/SqlBuilder.cs` |

## 扩展点速查

| 想改什么 | 从哪儿下手 |
|:---|:---|
| 加一种数据库方言 | 继承 `SqlBuilder`，注册 `SqlBuilderType`（见 [自定义 SqlBuilder](../extensibility/custom-sqlbuilder.md)） |
| 换值转换方式 | 实现 `IDbValueConverter` 并全局注册，列级可用 `ConverterType`（见 [数据映射与值转换](../advanced-topics/data-mapping.md)） |
| 加自定义表达式 | 扩展 `Expr` 子类，加 `ExprType`，在 `ExprSqlConverter` 里补分发 |
| 换元数据来源 | `TableInfoProvider.Set(factory)` |
| 写前后加钩子 | 实现 `IEntityServiceEvent<T>` 并注册到容器（见 [审计与变更追踪](../typical-applications/audit-and-change-tracking.md)） |
| AOT 下预注册映射 | `DataReaderConverter.RegisterMapper<T>()`，通常由源生成器产码（见 [AOT 支持](../advanced-topics/aot.md)） |

## 相关链接

- [返回文档目录](../README.md)
- [示例索引](./example-index.md)
- [生成 SQL 示例](./sql-examples.md)
- [数据库差异与兼容性说明](./database-compatibility.md)
