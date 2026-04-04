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
- 数据源配置、连接池配置、只读库配置

对应文档：

- [配置与注册](../01-getting-started/03-configuration-and-registration.md)
- [配置项速查](./01-configuration-reference.md)
- [数据库差异与兼容性说明](./08-database-compatibility.md)

### 实体与视图建模

- `[Table]`
- `[Column]`
- `[ForeignType]`
- `[ForeignColumn]`
- `[TableJoin]`
- `AutoExpand`

对应文档：

- [实体映射与数据源](../02-core-usage/01-entity-mapping.md)
- [关联查询](../02-core-usage/05-associations.md)

### 查询接口

- `Search` / `SearchAsync`
- `SearchOne` / `SearchOneAsync`
- `Exists` / `ExistsAsync`
- `Count` / `CountAsync`
- `Expr`、`LogicExpr`、`SelectExpr`
- `ObjectViewDAO<T>.Search(...)`
- `SearchAs<T>()`

对应文档：

- [查询指南](../02-core-usage/03-query-guide.md)
- [示例索引](./06-example-index.md)
- [生成 SQL 示例](./07-sql-examples.md)

### 写入接口

- `Insert` / `InsertAsync`
- `Update` / `UpdateAsync`
- `Delete` / `DeleteAsync`
- `BatchInsert` / `BatchUpdate`
- `UpdateOrInsert`
- `ObjectDAO<T>`
- `IBulkProvider`

对应文档：

- [CRUD 指南](../02-core-usage/04-crud-guide.md)
- [示例索引](./06-example-index.md)
- [生成 SQL 示例](./07-sql-examples.md)

### 高级能力

- `[Transaction]`
- `SessionManager`
- `IArged` / `TableArgs`
- 窗口函数相关扩展
- `Expr.ExistsRelated(...)`

对应文档：

- [事务管理](../03-advanced-topics/01-transactions.md)
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

对应文档：

- [表达式扩展](../04-extensibility/01-expression-extension.md)
- [函数验证器](../04-extensibility/02-function-validator.md)
- [自定义 SqlBuilder / 方言扩展](../04-extensibility/03-custom-sqlbuilder.md)
- [数据库差异与兼容性说明](./08-database-compatibility.md)

## 相关链接

- [返回目录](../README.md)
- [示例索引](./06-example-index.md)
- [生成 SQL 示例](./07-sql-examples.md)
- [数据库差异与兼容性说明](./08-database-compatibility.md)
