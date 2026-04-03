# LiteOrm AI 使用指南

本文是面向 AI 助手和需要快速建立结构认知的开发者的附录。完整说明请优先回到文档中心；本页适合作为速查地图，而不是独立的 API 参考大全。

## 1. 启动检查清单

1. 在 `appsettings.json` 中配置 `LiteOrm` 节点。
2. 调用 `RegisterLiteOrm()` 完成注册。
3. 使用 `[Table]` 和 `[Column]` 定义实体。
4. 需要关联字段、`[TableJoin]` 或更丰富投影时，再定义视图模型。
5. 根据职责选择层：业务流程优先用 `EntityService<T>` / `EntityService<T, TView>`，写入用 `ObjectDAO<T>`，类型化查询用 `ObjectViewDAO<T>`。

## 2. 核心类型地图

| 区域 | 主要类型 |
|------|----------|
| 启动与注册 | `RegisterLiteOrm`、`RegisterSqlBuilder`、`SqlBuilder` |
| 实体建模 | `[Table]`、`[Column]`、`IArged`、`TableArgs` |
| 关联能力 | `[ForeignType]`、`[ForeignColumn]`、`[TableJoin]`、`AutoExpand`、`Expr.ExistsRelated(...)` |
| 服务层 | `EntityService<T>`、`EntityService<T, TView>` |
| DAO 层 | `ObjectDAO<T>`、`ObjectViewDAO<T>`、`DataViewDAO<T>` |
| 表达式 | `Expr`、`LogicExpr`、`UpdateExpr`、`ExprString` |
| 扩展点 | `FunctionSqlHandler`、`FunctionExprValidator`、`LambdaExprConverter` |
| 批量写入 | `IBulkProvider`、`BulkProviderFactory` |

## 3. 查询与写入分层

- `EntityService<T>` / `IEntityServiceAsync<T>`：面向业务流程的写入操作。
- `IEntityViewService<TView>` / `IEntityViewServiceAsync<TView>`：面向查询的服务接口。
- `ObjectDAO<T>`：更底层的写入入口。
- `ObjectViewDAO<T>`：更底层的类型化查询入口。
- `DataViewDAO<T>`：更适合表格型结果的查询入口。

## 4. 三种查询写法

| 写法 | 适用场景 |
|------|----------|
| Lambda | 常规业务查询 |
| `Expr` | 动态条件拼装、查询构建器 |
| `ExprString` | 局部自定义 SQL 片段 |

## 5. 事务与高级能力

- `[Transaction]`：声明式事务方法。
- `SessionManager`：手动事务控制。
- `IArged` / `TableArgs`：分表分库路由。
- 自定义 `SqlBuilder`：旧数据库分页或方言差异处理。
- `FunctionExprValidator`：需要对白名单函数做策略校验时使用。

## 6. 使用建议

- 业务流程尽量放在 `EntityService` 系列中。
- 关联投影优先放在视图模型和 `ObjectViewDAO<T>` 中。
- 只有在查询形状确实动态时，再优先使用 `Expr` 或 `UpdateExpr`。
- 需要完整上下文时，请优先回到专题文档，而不是只看本页。

## 相关链接

- [返回目录](../SUMMARY.md)
- [API 索引](./02-api-index.md)
- [术语表](./03-glossary.md)
- [迁移映射](./04-migration-map.md)
