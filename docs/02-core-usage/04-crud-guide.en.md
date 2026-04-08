# CRUD Guide

This page focuses on write-side operations in LiteOrm: insert, update, delete, upsert, and batching. For search patterns, use the [Query Guide](./03-query-guide.en.md).

## 1. Insert and upsert

```csharp
var user = new User
{
    UserName = "admin",
    Age = 30,
    CreateTime = DateTime.Now,
    DeptId = 1
};

bool inserted = await userService.InsertAsync(user);
UpdateOrInsertResult result = await objectDAO.UpdateOrInsertAsync(user);
await userService.BatchInsertAsync(users);
await objectDAO.BatchUpdateOrInsertAsync(users);
```

Use `UpdateOrInsertAsync` when the caller does not want to split "create" and "update" paths in advance.

## 2. Update

### Update from an entity

```csharp
var current = await userService.SearchOneAsync(u => u.Id == 1);
current.UserName = "admin_v2";
await userService.UpdateAsync(current);
```

### Conditional update with `UpdateExpr`

```csharp
int affected = await objectDAO.UpdateAsync(
    Expr.Update<User>()
        .Where(Expr.Prop("Age") < 18)
        .Set(
            ("Age", Expr.Value(18)),
            ("CreateTime", Expr.Value(DateTime.Now))
        )
);
```

`UpdateExpr` is the right choice when the update should be expressed as SQL logic rather than "load entity then save entity."

## 3. Delete

```csharp
await userService.DeleteAsync(current);
await userService.DeleteAsync(1);
await userService.BatchDeleteAsync(users);
await userService.BatchDeleteIDAsync(new[] { 1, 2, 3 });

int deleted = await objectDAO.DeleteAsync(
    Expr.Prop("Age") < 18 & Expr.Prop("UserName").StartsWith("Temp")
);
```

Use entity-based delete for ordinary business workflows and expression-based delete for admin tools, cleanup jobs, or bulk maintenance tasks.

## 4. Mixed batch operations

```csharp
var ops = new List<EntityOperation<TestUser>>
{
    new() { Entity = newUser, Operation = OpDef.Insert },
    new() { Entity = existingUser, Operation = OpDef.Delete }
};

await service.BatchAsync(ops);
```

This pattern is useful for migration jobs and "replace old set with new set" synchronization flows.

## 5. Return values and behavior

| Operation | Typical return value |
|------|-----------------------|
| `Insert` / `Update` / `Delete` | `bool` |
| Conditional `UpdateExpr` / delete | `int` affected rows |
| Service-layer `UpdateOrInsert` | `bool` |
| DAO-layer `UpdateOrInsert` | `UpdateOrInsertResult` |

## 6. Service-layer recap

Common write APIs on `IEntityService<T>` and `IEntityServiceAsync<T>` include:

- `Insert` / `InsertAsync`
- `Update` / `UpdateAsync`
- `Delete` / `DeleteAsync`
- `BatchInsert` / `BatchInsertAsync`
- `BatchUpdate` / `BatchUpdateAsync`
- `BatchDelete` / `BatchDeleteAsync`
- `UpdateOrInsert` / `UpdateOrInsertAsync`

## 7. Practical advice

- Use `EntityService` when write behavior is part of a business transaction.
- Use `ObjectDAO<T>` when you need lower-level write control.
- Prefer batching before reaching for custom bulk infrastructure.
- For large ETL or import paths, evaluate an `IBulkProvider` implementation.

## Related Links

- [Back to English docs hub](../README.md)
- [Query Guide](./03-query-guide.en.md)
- [Transactions](../03-advanced-topics/01-transactions.en.md)
- [Performance](../03-advanced-topics/03-performance.en.md)
