# CRUD Guide

This page focuses on LiteOrm's write operations: insert, update, delete, upsert, and batching. For query capabilities, please refer to the [Query Guide](./03-query-guide.md).

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

### Batch Initialization Example from Demo

`LiteOrm.Demo\Data\DbInitializer.cs` uses batch inserts to initialize departments, users, and sales records. This pattern is ideal for seed data initialization, demo data generation, or pre-import data preparation:

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

### Batch Upsert Example from Tests

The following example is extracted from `LiteOrm.Tests\ServiceTests.cs`: the same batch contains entities that need updating (already exist) and entities that need inserting (new).

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

### Batch Update Example from Demo

`LiteOrm.Demo\Data\DbInitializer.cs` queries departments first, then modifies managers in bulk, and finally commits in one batch:

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

### Conditional Update

```csharp
await objectDao.UpdateAsync(
    Expr.Update<User>()
        .Where(Expr.Prop("Age") < 18)
        .Set(
            ("Age", Expr.Value(18)),
            ("CreateTime", Expr.Value(DateTime.Now))
        )
);
```

### UpdateExpr Practical Examples from Demo

`LiteOrm.Demo\Demos\UpdateExprDemo.cs` demonstrates several typical uses of `UpdateExpr`:

```csharp
var update = new UpdateExpr(new TableExpr(typeof(User)), Expr.Prop("UserName") == "UpdateDemo_Bob")
    .Set(("Age", Expr.Const(35)));

int affected = await userService.UpdateAsync(update);
```

You can also write arithmetic expressions or function expressions directly in the `SET` clause:

```csharp
var agePlusFive = new UpdateExpr(new TableExpr(typeof(User)), Expr.Prop("UserName") == "UpdateDemo_Carol")
    .Set(("Age", Expr.Prop("Age") + Expr.Const(5)));

var rename = new UpdateExpr(new TableExpr(typeof(User)), Expr.Prop("UserName") == "UpdateDemo_Bob")
    .Set(("UserName", Expr.Func("CONCAT", Expr.Prop("UserName"), Expr.Const("_v2"))));
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

### Complete Batch Insert/Update/Delete Cycle from Tests

`LiteOrm.Tests\ServiceTests.cs` has a complete validation cycle suitable for copying:

```csharp
var users = new List<TestUser>
{
    new TestUser { Name = "Batch 1", Age = 10, CreateTime = DateTime.Now },
    new TestUser { Name = "Batch 2", Age = 20, CreateTime = DateTime.Now }
};

await service.BatchInsertAsync(users);

var inserted = await viewService.SearchAsync(Expr.Lambda<TestUser>(u => u.Name!.StartsWith("Batch")));

foreach (var user in inserted)
    user.Age += 5;

await service.BatchUpdateAsync(inserted);
await service.BatchDeleteAsync(inserted);
```

This example is ideal for validating that batch interfaces cover the entire path of "insert → update → delete."

### Conditional Delete

```csharp
await userService.DeleteAsync(u => u.CreateTime < DateTime.Today.AddYears(-1));
await objectDao.Delete(Expr.Prop("Age") < 18 & Expr.Prop("UserName").StartsWith("Temp"));
```

### Conditional Delete Example from Tests

The following example is extracted from sharding tests, but the delete conditions themselves apply to regular tables as well:

```csharp
int deleted = await service.DeleteAsync(
    l => l.Amount > 400 && l.Event == "DeleteEvent",
    tableArgs: new[] { "202401" }
);
```

For non-sharded tables, simply remove the `tableArgs` parameter.

## 4. Return Values and Behavior

| Method Type | Common Return Value | Meaning |
| --- | --- | --- |
| `Insert/Update/Delete` | `bool` | Whether execution was successful. |
| Conditional update/delete | `int` | Number of affected rows. |
| Service layer `UpdateOrInsert` | `bool` | Whether execution was successful. |
| DAO layer `UpdateOrInsert` | `UpdateOrInsertResult` | Indicates whether this was an insert or update. |

## 5. Service Interface Quick Reference

### `IEntityService<T>` / `IEntityServiceAsync<T>`

- `Insert` / `InsertAsync`
- `Update` / `UpdateAsync`
- `Delete` / `DeleteAsync`
- `BatchInsert` / `BatchInsertAsync`
- `BatchUpdate` / `BatchUpdateAsync`
- `BatchDelete` / `BatchDeleteAsync`
- `UpdateOrInsert` / `UpdateOrInsertAsync`

If you also need conditional search, pagination, `Exists`, `Count`, etc., please refer to the [Query Guide](./03-query-guide.en.md).

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

This example from `LiteOrm.Tests\ServiceTests.cs` is suitable for "insert new batch of data while deleting old data" sync migration scenarios.

## 7. Practical Recommendations

- Prioritize batch interfaces for high-frequency write scenarios.
- When you need explicit control over SQL structure, use `Expr.Update<T>()` and DAO.
- Cross-table business operations should be placed in Service layer with transaction coordination.

## Related Links

- [Back to docs hub](../README.md)
- [Query Guide](./03-query-guide.en.md)
- [Transactions](../03-advanced-topics/01-transactions.en.md)
- [Performance Optimization](../03-advanced-topics/03-performance.en.md)
