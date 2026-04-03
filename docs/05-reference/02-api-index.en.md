# API Index

LiteOrm no longer uses standalone `API_REFERENCE` files as the primary entry point.
Use this page as a scenario-based index inside the docs set.

## Quick links

- [Example Index](./06-example-index.en.md)
- [Generated SQL Examples](./07-sql-examples.en.md)
- [Database Compatibility Notes](./08-database-compatibility.en.md)

## Browse by scenario

### Startup and configuration

- `RegisterLiteOrm()`
- `RegisterSqlBuilder(...)`
- `BulkProviderFactory`
- data source settings, connection pool settings, read-only replicas

Related guides:

- [Configuration and registration](../01-getting-started/03-configuration-and-registration.en.md)
- [Configuration reference](./01-configuration-reference.en.md)
- [Database Compatibility Notes](./08-database-compatibility.en.md)

### Entity mapping and view models

- `[Table]`
- `[Column]`
- `[ForeignType]`
- `[ForeignColumn]`
- `[TableJoin]`
- `AutoExpand`

Related guides:

- [Entity mapping and data sources](../02-core-usage/01-entity-mapping.en.md)
- [Associations](../02-core-usage/05-associations.en.md)

### Query APIs

- `Search` / `SearchAsync`
- `SearchOne` / `SearchOneAsync`
- `Exists` / `ExistsAsync`
- `Count` / `CountAsync`
- `Expr`, `LogicExpr`, `SelectExpr`
- `ObjectViewDAO<T>.Search(...)`
- `SearchAs<T>()`

Related guides:

- [Query guide](../02-core-usage/03-query-guide.en.md)
- [Example Index](./06-example-index.en.md)
- [Generated SQL Examples](./07-sql-examples.en.md)

### Write APIs

- `Insert` / `InsertAsync`
- `Update` / `UpdateAsync`
- `Delete` / `DeleteAsync`
- `BatchInsert` / `BatchUpdate`
- `UpdateOrInsert`
- `ObjectDAO<T>`
- `IBulkProvider`

Related guides:

- [CRUD guide](../02-core-usage/04-crud-guide.en.md)
- [Example Index](./06-example-index.en.md)
- [Generated SQL Examples](./07-sql-examples.en.md)

### Advanced features

- `[Transaction]`
- `SessionManager`
- `IArged` / `TableArgs`
- window function extensions
- `Expr.ExistsRelated(...)`

Related guides:

- [Transactions](../03-advanced-topics/01-transactions.en.md)
- [Sharding and TableArgs](../03-advanced-topics/02-sharding-and-tableargs.en.md)
- [Window functions](../03-advanced-topics/04-window-functions.en.md)
- [Example Index](./06-example-index.en.md)
- [Generated SQL Examples](./07-sql-examples.en.md)
- [Database Compatibility Notes](./08-database-compatibility.en.md)

### Extensibility

- `LambdaExprConverter.RegisterMethodHandler`
- `LambdaExprConverter.RegisterMemberHandler`
- `SqlBuilder.RegisterFunctionSqlHandler`
- `FunctionSqlHandler`
- `FunctionExprValidator`

Related guides:

- [Expression extension](../04-extensibility/01-expression-extension.en.md)
- [Function expression validator](../04-extensibility/02-function-validator.en.md)
- [Custom SqlBuilder and dialect extension](../04-extensibility/03-custom-sqlbuilder.en.md)
- [Database Compatibility Notes](./08-database-compatibility.en.md)

## Related links

- [Back to English docs hub](../SUMMARY.en.md)
- [Example Index](./06-example-index.en.md)
- [Generated SQL Examples](./07-sql-examples.en.md)
- [Database Compatibility Notes](./08-database-compatibility.en.md)
