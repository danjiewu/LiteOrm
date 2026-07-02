# CRUD Guide

This page focuses on LiteOrm's write operations: insert, update, delete, upsert, and batching. For query capabilities, please refer to the [Query Guide](./04-query-guide.en.md).

## 1. Insert

### Single Insert

```csharp
var user = new User
{
    UserName = "admin",
    Age = 30,
    CreateTime = DateTime.Now,
    DeptId = 1
};

bool success = await userService.InsertAsync(user);
```

### Batch Insert

```csharp
await userService.BatchInsertAsync(users);
```

### Batch Initialization Example

Use batch inserts to initialize departments, users, and sales records. This pattern is ideal for seed data initialization, demo data generation, or pre-import data preparation:

```csharp
var depts = new List<Department>
{
    new() { Id = 1, Name = "Headquarters" },
    new() { Id = 2, Name = "R&D Center", ParentId = 1 },
    new() { Id = 3, Name = "Marketing", ParentId = 1 }
};

await deptService.BatchInsertAsync(depts);

var users = new List<User>
{
    new() { Id = 1, UserName = "Admin", Age = 35, CreateTime = DateTime.Now, DeptId = 1 },
    new() { Id = 2, UserName = "Lead Dev", Age = 32, CreateTime = DateTime.Now, DeptId = 2 }
};

await userService.BatchInsertAsync(users);
```

### Upsert

```csharp
bool success = await userService.UpdateOrInsertAsync(user);
Console.WriteLine(success); // true indicates successful execution
```

If you need to distinguish between insert and update, use the DAO layer's `UpdateOrInsertResult` instead.

### Batch Upsert

```csharp
await userService.BatchUpdateOrInsertAsync(users);
```

### Batch Upsert Example

The same batch contains entities that need updating (already exist) and entities that need inserting (new):

```csharp
var users = new List<TestUser>
{
    new TestUser { Name = "Upsert A", Age = 10, CreateTime = DateTime.Now },
    new TestUser { Name = "Upsert B", Age = 20, CreateTime = DateTime.Now }
};
await service.BatchInsertAsync(users);

var existingUser = users[0];
existingUser.Age = 15; // Update existing record

var newUser = new TestUser
{
    Name = "Upsert C",
    Age = 30,
    CreateTime = DateTime.Now
};

await service.BatchUpdateOrInsertAsync(new[] { existingUser, newUser });
```

After execution, `Upsert A` will be updated and `Upsert C` will be inserted.

## 2. Update

### Update by Entity

```csharp
var user = await userService.SearchOneAsync(u => u.Id == 1);
user.UserName = "admin_v2";
await userService.UpdateAsync(user);
```

### Batch Update

```csharp
foreach (var user in users)
{
    user.Age += 1;
}

await userService.BatchUpdateAsync(users);
```

### Batch Update Example

Query departments first, then modify managers in bulk, and finally commit in one batch:

```csharp
var updateDepts = new List<Department>();

async Task MarkManager(int deptId, int managerId)
{
    var dept = await deptService.GetObjectAsync(deptId);
    if (dept != null)
    {
        dept.ManagerId = managerId;
        updateDepts.Add(dept);
    }
}

await MarkManager(1, 1);
await MarkManager(2, 2);
await MarkManager(4, 6);

await deptService.BatchUpdateAsync(updateDepts);
```

This pattern is suitable for admin backend scenarios where you "read entities first, modify multiple objects, then batch commit."

### Optimistic Concurrency with `timestamp`

If you want an update to verify that the version read earlier still matches the current database row, declare a `timestamp` column and use the `ObjectDAO<T>` overloads `Update(entity, timestamp)` or `UpdateAsync(entity, timestamp)`.

```csharp
[Table("Users")]
public class User : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserName")]
    public string? UserName { get; set; }

    [Column("Version", IsTimestamp = true)]
    public int Version { get; set; }
}
```

```csharp
var dao = serviceProvider.GetRequiredService<ObjectDAO<User>>();
var viewDao = serviceProvider.GetRequiredService<ObjectViewDAO<User>>();

var user = await viewDao.GetObject(1).FirstOrDefaultAsync();
int originalVersion = user.Version;

user.UserName = "admin_v2";
user.Version = originalVersion + 1; // the value on the entity is written back

bool updated = await dao.UpdateAsync(user, originalVersion);
if (!updated)
{
    Console.WriteLine("Concurrency conflict: the row was changed by someone else.");
}
```

Important behavior details:

- The `Version` value on the entity is the new value written to the database.
- The `timestamp` argument is the original value and is added to the `WHERE` clause for concurrency checking.
- A return value of `false` usually means the primary key matched but the `timestamp` no longer matched.
- The generic `IEntityService<T>` / `IEntityServiceAsync<T>` update methods do not expose a `timestamp` overload. For optimistic concurrency, use `ObjectDAO<T>` directly or wrap it in a custom service.
- `BatchUpdate` / `BatchUpdateAsync` do not automatically apply `timestamp` concurrency checks.

> For a complete example, see `LiteOrm.Tests\ObjectDAOTests.cs` and `LiteOrm.Tests\Models\TestTimestampUser.cs`.

### Conditional Update

```csharp
using static LiteOrm.Common.Expr;
await objectDao.UpdateAsync(
    Update<User>()
        .Where(Prop("Age") < 18)
        .Set(
            ("Age", Value(18)),
            ("CreateTime", Value(DateTime.Now))
        )
);
```

### UpdateExpr Practical Examples

Several typical uses of `UpdateExpr`:

```csharp
using static LiteOrm.Common.Expr;
var update = new UpdateExpr(new TableExpr(typeof(User)), Prop("UserName") == "UpdateDemo_Bob")
    .Set(("Age", Const(35)));

int affected = await userService.UpdateAllAsync(update);
```

You can also write arithmetic expressions or function expressions directly in the `SET` clause:

```csharp
using static LiteOrm.Common.Expr;
var agePlusFive = new UpdateExpr(new TableExpr(typeof(User)), Prop("UserName") == "UpdateDemo_Carol")
    .Set(("Age", Prop("Age") + Const(5)));

var rename = new UpdateExpr(new TableExpr(typeof(User)), Prop("UserName") == "UpdateDemo_Bob")
    .Set(("UserName", Func("CONCAT", Prop("UserName"), Const("_v2"))));
```

### Update with Lambda Expressions (Recommended)

If you don't want to assemble an `UpdateExpr` by hand, you can call the `UpdateAll` / `UpdateAllAsync` Lambda extension methods directly. They accept two Lambda expressions: the first defines which fields to update and the values to set, and the second defines the `WHERE` condition. The framework internally converts them to an `UpdateExpr` via `LambdaExprConverter`, so the syntax is closer to EF Core and benefits from compile-time type checking.

```csharp
// Equivalent to: UPDATE Users SET Age = 29 WHERE UserName = 'UpdateDemo_Alice'
await userService.UpdateAllAsync(
    u => new User { Age = 29 },
    u => u.UserName == "UpdateDemo_Alice"
);
```

The update expression can also reference the original entity fields for arithmetic (e.g. increment, string concatenation), and the `WHERE` condition supports logical combinations such as `&&` / `||`:

```csharp
// Equivalent to: UPDATE Users SET Age = Age + 1, CreateTime = @now WHERE Age >= 28
await userService.UpdateAllAsync(
    u => new User { Age = u.Age + 1, CreateTime = DateTime.Now },
    u => u.Age >= 28
);
```

Usage notes:

- The first Lambda must be an `Expression<Func<T, T>>` whose body is a `new T { ... }` form (a `MemberInitExpression`). Each `=` binding is translated into a `SET` clause.
- The second Lambda is an `Expression<Func<T, bool>>`, i.e. a regular WHERE condition. It can be omitted (when `whereExpression` is not provided, all rows are updated — use with caution).
- Referencing the original field (e.g. `u.Age + 1`) is translated to `Prop("Age") + Const(1)`, corresponding to the SQL `Age = Age + 1`.
- You can also pass dynamic table name arguments via `params string[] tableArgs`.

```csharp
// Sync version + dynamic table name
userService.UpdateAll(
    u => new User { Age = 30 },
    u => u.UserName == "UpdateDemo_Bob",
    "Users_2026" // dynamic table name
);
```

## 3. Delete

### Delete by Entity

```csharp
var user = await userService.SearchOneAsync(u => u.Id == 1);
await userService.DeleteAsync(user);
```

### Delete by Primary Key

```csharp
await userService.DeleteAsync(1);
```

### Batch Delete

```csharp
await userService.BatchDeleteAsync(users);
await userService.BatchDeleteIDAsync(new[] { 1, 2, 3 });
```

### Complete Batch Insert/Update/Delete Cycle

A complete validation cycle suitable for copying:

```csharp
using static LiteOrm.Common.Expr;
var users = new List<TestUser>
{
    new TestUser { Name = "Batch 1", Age = 10, CreateTime = DateTime.Now },
    new TestUser { Name = "Batch 2", Age = 20, CreateTime = DateTime.Now }
};

await service.BatchInsertAsync(users);

var inserted = await viewService.SearchAsync(Lambda<TestUser>(u => u.Name!.StartsWith("Batch")));

foreach (var user in inserted)
    user.Age += 5;

await service.BatchUpdateAsync(inserted);
await service.BatchDeleteAsync(inserted);
```

This example is ideal for validating that batch interfaces cover the entire path of "insert → update → delete."

### Conditional Delete

```csharp
using static LiteOrm.Common.Expr;
await userService.DeleteAsync(u => u.CreateTime < DateTime.Today.AddYears(-1));
await objectDao.Delete(Prop("Age") < 18 & Prop("UserName").StartsWith("Temp"));
```

### Conditional Delete Example

Delete conditions in a sharding scenario — they also apply to regular tables (just drop the `tableArgs` parameter):

```csharp
int deleted = await service.DeleteAsync(
    l => l.Amount > 400 && l.Event == "DeleteEvent",
    tableArgs: new[] { "202401" }
);
```

## 4. Return Values and Behavior

| Method Type | Common Return Value | Meaning |
| --- | --- | --- |
| `Insert/Update/Delete` | `bool` | Whether execution was successful. |
| Conditional update/delete | `int` | Number of affected rows. |
| Service layer `UpdateOrInsert` | `bool` | Whether execution was successful. |
| DAO layer `UpdateOrInsert` | `UpdateOrInsertResult` | Indicates whether this was an insert or update. |

## 5. Service Interface Quick Reference

### `IEntityService<T>` / `IEntityServiceAsync<T>`

Entity-level writes and batch operations (parameters are strongly typed `T` or `IEnumerable<T>`):

- `Insert` / `InsertAsync`
- `Update` / `UpdateAsync`
- `UpdateOrInsert` / `UpdateOrInsertAsync`
- `Delete` / `DeleteAsync`
- `BatchInsert` / `BatchInsertAsync`
- `BatchUpdate` / `BatchUpdateAsync`
- `BatchUpdateOrInsert` / `BatchUpdateOrInsertAsync`
- `BatchDelete` / `BatchDeleteAsync`
- `Batch` / `BatchAsync`: mixed batch processing, each record can specify `OpDef.Insert` / `Update` / `Delete`

Inherited from the non-generic `IEntityService` / `IEntityServiceAsync`, which also includes expression- or primary-key-based methods:

- `UpdateAll` / `UpdateAllAsync`: update by `UpdateExpr` condition, returns number of affected rows
- `DeleteAll` / `DeleteAllAsync`: delete by `LogicExpr` condition, returns number of affected rows
- `DeleteID` / `DeleteIDAsync`: delete by primary key
- `BatchDeleteID` / `BatchDeleteIDAsync`: batch delete by primary keys

> The `UpdateAll` / `DeleteAll` methods above can also be called directly via the Lambda extension methods provided by `LambdaExprExtensions` (see the "Update with Lambda Expressions" and "Conditional Delete" sections in this document).

If you also need conditional search, pagination, `Exists`, `Count`, etc., please refer to the [Query Guide](./04-query-guide.en.md).

## 6. Mixed Batch Processing and Upsert Supplement

In addition to homogeneous operations like `BatchInsertAsync`, `BatchUpdateAsync`, and `BatchDeleteAsync`, LiteOrm also supports putting different operations into the same batch.

```csharp
var newUser = new TestUser { Name = "Mixed 1", Age = 10, CreateTime = DateTime.Now };

var ops = new List<EntityOperation<TestUser>>
{
    new EntityOperation<TestUser> { Entity = newUser, Operation = OpDef.Insert },
    new EntityOperation<TestUser> { Entity = existingUser, Operation = OpDef.Delete }
};

await service.BatchAsync(ops);
```

Suitable for "insert new batch of data while deleting old data" sync migration scenarios.

## 7. Practical Recommendations

- Prioritize batch interfaces for high-frequency write scenarios.
- When you need explicit control over SQL structure, use `Expr.Update<T>()` and DAO.
- Cross-table business operations should be placed in Service layer with transaction coordination.

## Related Links

- [Back to docs hub](../README.md)
- [Query Guide](./04-query-guide.en.md)
- [Transactions](../03-advanced-topics/01-transactions.en.md)
- [Performance Optimization](../03-advanced-topics/03-performance.en.md)


