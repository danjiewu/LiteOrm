# API 索引

LiteOrm 已不再把独立的 `API_REFERENCE` 文档作为主入口维护。

本文档改为按使用场景整理接口、能力入口和扩展点，便于在 docs 体系内快速定位信息。

## 快速入口

- [示例索引](./06-example-index.md)
- [生成 SQL 示例](./07-sql-examples.md)
- [数据库差异与兼容性说明](./08-database-compatibility.md)

## 按使用场景查阅

### 配置与启动

- `RegisterLiteOrm()`
- `RegisterSqlBuilder(...)`
- `BulkProviderFactory`
- 数据源配置、连接池配置、只读副本配置

对应文档：

- [配置与注册](../01-getting-started/03-configuration-and-registration.md)
- [配置项速查](./01-configuration-reference.md)
- [数据库差异与兼容性说明](./08-database-compatibility.md)

### 实体映射与视图模型

- `[Table]`
- `[Column]`
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
- `SearchAs` / `SearchAsAsync`
- `SearchOne` / `SearchOneAsync`
- `Exists` / `ExistsAsync`
- `Count` / `CountAsync`
- `Expr`、`LogicExpr`、`SelectExpr`
- `SelectAll()` / `Cast(DbType)`
- Lambda 三目运算符 `?:`（转为 `CASE`）
- 表达式名称与别名忽略大小写
- `ObjectViewDAO<T>.Search(...)`
- `SearchAs<T>()`

对应文档：

- [Expr 使用指南](../02-core-usage/06-expr-guide.md)
- [查询总览](../02-core-usage/04-query-overview.md)
- [示例索引](./06-example-index.md)
- [生成 SQL 示例](./07-sql-examples.md)

### 写入接口

- `Insert` / `InsertAsync`
- `Update` / `UpdateAsync`
- `UpdateAll` / `UpdateAllAsync`（按 `UpdateExpr` 条件更新）
- `ObjectDAO<T>.Update(entity, timestamp)` / `UpdateAsync(entity, timestamp)`
- `Delete` / `DeleteAsync`
- `DeleteAll` / `DeleteAllAsync`（按 `LogicExpr` 条件删除）
- `BatchInsert` / `BatchUpdate`
- `UpdateOrInsert`
- `ObjectDAO<T>`
- `IBulkProvider`

对应文档：

- [CRUD 指南](../02-core-usage/03-crud-guide.md)
- [事务管理](../03-advanced-topics/01-transactions.md)
- [示例索引](./06-example-index.md)
- [生成 SQL 示例](./07-sql-examples.md)

### 高级特性

- `[Transaction]`
- `[ExceptionHook]` / `IServiceExceptionHook`
- `SessionManager`
- `IArged` / `TableArgs`
- 窗口函数相关扩展
- `Expr.ExistsRelated(...)`

对应文档：

- [事务管理](../03-advanced-topics/01-transactions.md)
- [日志与诊断](../03-advanced-topics/07-logging.md)
- [分表分库与 TableArgs](../03-advanced-topics/02-sharding-and-tableargs.md)
- [窗口函数](../03-advanced-topics/04-window-functions.md)
- [示例索引](./06-example-index.md)
- [生成 SQL 示例](./07-sql-examples.md)
- [数据库差异与兼容性说明](./08-database-compatibility.md)

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
- [数据库差异与兼容性说明](./08-database-compatibility.md)

## 相关链接

- [返回文档目录](../README.md)
- [示例索引](./06-example-index.md)
- [生成 SQL 示例](./07-sql-examples.md)
- [数据库差异与兼容性说明](./08-database-compatibility.md)
