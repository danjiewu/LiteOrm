# LiteOrm AI Guide

This appendix gives assistants and developers a compact map of the LiteOrm API surface. Start with the docs hub for full explanations; use this page as a shortcut, not as a standalone API reference.

## 1. Startup checklist

1. Configure the `LiteOrm` section in `appsettings.json`.
2. Call `RegisterLiteOrm()`.
3. Define entities with `[Table]` and `[Column]`.
4. Add view models when you need `[ForeignColumn]`, `[TableJoin]`, or richer projections.
5. Choose the right layer: `EntityService<T>` / `EntityService<T, TView>` for business workflows, `ObjectDAO<T>` for writes, and `ObjectViewDAO<T>` for typed queries.

## 2. Core type map

| Area | Main types |
|------|------------|
| startup | `RegisterLiteOrm`, `RegisterSqlBuilder`, `SqlBuilder` |
| entity mapping | `[Table]`, `[Column]`, `IArged`, `TableArgs` |
| relationships | `[ForeignType]`, `[ForeignColumn]`, `[TableJoin]`, `AutoExpand`, `Expr.ExistsRelated(...)` |
| services | `EntityService<T>`, `EntityService<T, TView>` |
| DAO layers | `ObjectDAO<T>`, `ObjectViewDAO<T>`, `DataViewDAO<T>` |
| expressions | `Expr`, `LogicExpr`, `UpdateExpr`, `ExprString` |
| extensibility | `FunctionSqlHandler`, `FunctionExprValidator`, `LambdaExprConverter` |
| bulk operations | `IBulkProvider`, `BulkProviderFactory` |

## 3. Query and write split

- `EntityService<T>` and `IEntityServiceAsync<T>`: write-oriented business operations.
- `IEntityViewService<TView>` and `IEntityViewServiceAsync<TView>`: query-oriented service surface.
- `ObjectDAO<T>`: lower-level write access.
- `ObjectViewDAO<T>`: lower-level typed query access.
- `DataViewDAO<T>`: query access when tabular results are a better fit.

## 4. Three query styles

| Style | Best for |
|------|----------|
| Lambda | routine application queries |
| `Expr` | dynamic filters and query builders |
| `ExprString` | small, localized custom SQL fragments |

## 5. Transactions and advanced features

- `[Transaction]` for declarative service methods.
- `SessionManager` for manual transactions.
- `IArged` / `TableArgs` for sharding.
- Custom `SqlBuilder` for legacy paging or dialect-specific SQL.
- `FunctionExprValidator` when user-driven function calls need policy checks.

## 6. Practical guidance

- Keep business workflows in the `EntityService` family.
- Keep relationship projection in view models and `ObjectViewDAO<T>`.
- Use `Expr` or `UpdateExpr` when the query shape is truly dynamic.
- Prefer the topic guides before treating this appendix as a full reference.

## Related links

- [Back to English docs hub](../SUMMARY.en.md)
- [API Index](./02-api-index.en.md)
- [Glossary](./03-glossary.en.md)
- [Migration map](./04-migration-map.en.md)
