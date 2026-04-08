# Performance

This page highlights the main performance levers in LiteOrm: pool sizing, projection, batch operations, async execution, and relationship-aware query design.

## 1. Connection pool tuning

Common defaults:

| Setting | Default |
|------|---------|
| `PoolSize` | `16` |
| `MaxPoolSize` | `100` |
| `KeepAliveDuration` | `00:10:00` |

Start with the defaults, then tune based on actual concurrency and database capacity.

## 2. Prefer parameterized queries

LiteOrm parameterizes Lambda, `Expr`, and interpolated `ExprString` usage:

```csharp
var users = await userService.SearchAsync(u => u.Age >= minAge);
var admins = await userViewDAO.Search($"WHERE UserName = {name}").ToListAsync();
```

This improves plan reuse and reduces SQL-injection risk.

## 3. Query only what you need

Use projection-oriented APIs when a view is narrower than the base entity:

```csharp
var result = await userService.SearchAs<UserView>(
    Expr.From<UserView>()
        .Where(Expr.Prop("Age") > 18)
        .Select("Id", "UserName", "DeptName")
);
```

Recommended result shapes:

- `ObjectViewDAO<T>` for normal typed queries
- `DataViewDAO<T>` when a `DataTable` is more appropriate
- `IAsyncEnumerable` for streaming large result sets

## 4. Batch before going lower level

Use `BatchInsertAsync`, `BatchUpdateAsync`, `BatchDeleteAsync`, and `BatchUpdateOrInsertAsync` before building custom import infrastructure.

For very large ETL or migration jobs, evaluate an `IBulkProvider` implementation through `BulkProviderFactory`:

```csharp
var factory = services.GetRequiredService<BulkProviderFactory>();
var provider = factory.GetProvider(dbConnection.GetType());
await provider.BulkInsertAsync(ToDataTable(users), dbConnection, transaction);
```

## 5. Async and parallelism

- Prefer async APIs such as `SearchAsync`, `InsertAsync`, and `CountAsync`
- Parallelize only independent queries
- Do not force parallelism when one relationship-aware query can replace many small round-trips

## 6. Avoid N+1 patterns

Use view models and relationship metadata so LiteOrm can generate the join you actually need.

When you only need a yes/no answer, prefer `ExistsAsync` over `CountAsync`.

## 7. Paging and large scans

- Large offset paging can become expensive
- Cursor-style paging is often faster for operational screens
- For large exports, stream rows with `await foreach` instead of materializing the whole set

## 8. Operational advice

- Keep LiteOrm registrations scoped appropriately in DI
- Release transaction scopes quickly
- Add real database indexes for the fields used in `WHERE`, `ORDER BY`, and join conditions

## Related Links

- [Back to English docs hub](../README.md)
- [Associations](../02-core-usage/05-associations.en.md)
- [Transactions](./01-transactions.en.md)
- [Expression Extension](../04-extensibility/01-expression-extension.en.md)
