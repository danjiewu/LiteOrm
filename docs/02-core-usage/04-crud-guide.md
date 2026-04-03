# CRUD 指南

本文聚焦 LiteOrm 的写入、更新、删除和批量操作。查询能力请统一参考 [查询指南](./03-query-guide.md)。

## 1. 插入

### 单条插入

```csharp
var user = new User
{
    UserName = "admin",
    Email = "admin@test.com",
    Age = 30,
    CreateTime = DateTime.Now
};

bool success = await userService.InsertAsync(user);
```

### 批量插入

```csharp
await userService.BatchInsertAsync(users);
```

### 来自 Demo 的批量初始化示例

`LiteOrm.Demo\Data\DbInitializer.cs` 中使用批量插入来初始化部门、用户和销售记录，适合作为导入或初始化脚本参考：

```csharp
var depts = new List<Department>
{
    new() { Id = 1, Name = "集团总部" },
    new() { Id = 2, Name = "研发中心", ParentId = 1 },
    new() { Id = 3, Name = "市场部", ParentId = 1 }
};

await deptService.BatchInsertAsync(depts);

var users = new List<User>
{
    new() { Id = 1, UserName = "Admin", Age = 35, CreateTime = DateTime.Now, DeptId = 1 },
    new() { Id = 2, UserName = "研发负责人", Age = 32, CreateTime = DateTime.Now, DeptId = 2 }
};

await userService.BatchInsertAsync(users);
```

这个模式适合种子数据初始化、演示数据生成、批量导入前的数据准备。

### Upsert

```csharp
var result = await userService.UpdateOrInsertAsync(user);
Console.WriteLine(result.OperationType); // Insert / Update
```

### 批量 Upsert

```csharp
await userService.BatchUpdateOrInsertAsync(users);
```

### 来自测试的 Batch Upsert 示例

下面的例子提炼自 `LiteOrm.Tests\ServiceTests.cs`：同一批数据中既有“已存在需要更新”的实体，也有“需要新增”的实体。

```csharp
var users = new List<TestUser>
{
    new TestUser { Name = "Upsert A", Age = 10, CreateTime = DateTime.Now },
    new TestUser { Name = "Upsert B", Age = 20, CreateTime = DateTime.Now }
};
await service.BatchInsertAsync(users);

var existingUser = users[0];
existingUser.Age = 15; // 更新现有记录

var newUser = new TestUser
{
    Name = "Upsert C",
    Age = 30,
    CreateTime = DateTime.Now
};

await service.BatchUpdateOrInsertAsync(new[] { existingUser, newUser });
```

执行后，`Upsert A` 会被更新，`Upsert C` 会被插入。

## 2. 更新

### 根据实体更新

```csharp
var user = await userService.SearchOneAsync(u => u.Id == 1);
user.Email = "newemail@test.com";
await userService.UpdateAsync(user);
```

### 批量更新

```csharp
foreach (var user in users)
{
    user.Status = 1;
}

await userService.BatchUpdateAsync(users);
```

### 来自 Demo 的批量更新示例

`LiteOrm.Demo\Data\DbInitializer.cs` 中先查询出部门，再集中修改负责人，最后一次性提交：

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

适合“先读出实体，修改多个对象，再批量提交”的后台管理场景。

### 条件更新

```csharp
await objectDao.UpdateAsync(
    Expr.Update<User>()
        .Where(Expr.Prop("Age") < 18)
        .Set(
            ("Status", Expr.Value(0)),
            ("UpdateTime", Expr.Value(DateTime.Now))
        )
);
```

### 来自 Demo 的 UpdateExpr 实战示例

`LiteOrm.Demo\Demos\UpdateExprDemo.cs` 演示了 `UpdateExpr` 的几种典型玩法：

```csharp
var update = new UpdateExpr(new TableExpr(typeof(User)), Expr.Prop("UserName") == "UpdateDemo_Bob")
    .Set(("Age", Expr.Const(35)));

int affected = await userService.UpdateAsync(update);
```

也可以直接在 `SET` 子句里写算术表达式或函数表达式：

```csharp
var agePlusFive = new UpdateExpr(new TableExpr(typeof(User)), Expr.Prop("UserName") == "UpdateDemo_Carol")
    .Set(("Age", Expr.Prop("Age") + Expr.Const(5)));

var rename = new UpdateExpr(new TableExpr(typeof(User)), Expr.Prop("UserName") == "UpdateDemo_Bob")
    .Set(("UserName", Expr.Func("CONCAT", Expr.Prop("UserName"), Expr.Const("_v2"))));
```

## 3. 删除

### 根据实体删除

```csharp
var user = await userService.SearchOneAsync(u => u.Id == 1);
await userService.DeleteAsync(user);
```

### 根据主键删除

```csharp
await userService.DeleteAsync(1);
```

### 批量删除

```csharp
await userService.BatchDeleteAsync(users);
await userService.BatchDeleteIDAsync(new[] { 1, 2, 3 });
```

### 来自测试的批量增改删闭环示例

`LiteOrm.Tests\ServiceTests.cs` 中有一组很适合复制的闭环验证：

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

这个例子很适合验证批量接口是否能覆盖“插入 → 更新 → 删除”的整条路径。

### 条件删除

```csharp
await userService.DeleteAsync(u => u.CreateTime < DateTime.Today.AddYears(-1));
await objectDao.Delete(Expr.Prop("Status") == 0 & Expr.Prop("IsActive") == false);
```

### 来自测试的条件删除示例

以下例子提炼自分表测试，但删除条件本身同样适用于普通表：

```csharp
int deleted = await service.DeleteAsync(
    l => l.Amount > 400 && l.Event == "DeleteEvent",
    tableArgs: new[] { "202401" }
);
```

如果不是分表场景，去掉 `tableArgs` 即可。

## 4. 返回值与行为说明

| 方法类型 | 常见返回值 | 含义 |
| --- | --- | --- |
| `Insert/Update/Delete` | `bool` | 是否成功执行。 |
| 条件更新/删除 | `int` | 受影响行数。 |
| `UpdateOrInsert` | `UpdateOrInsertResult` | 告知本次是插入还是更新。 |

## 5. Service 接口速览

### `IEntityService<T>` / `IEntityServiceAsync<T>`

- `Insert` / `InsertAsync`
- `Update` / `UpdateAsync`
- `Delete` / `DeleteAsync`
- `BatchInsert` / `BatchInsertAsync`
- `BatchUpdate` / `BatchUpdateAsync`
- `BatchDelete` / `BatchDeleteAsync`
- `UpdateOrInsert` / `UpdateOrInsertAsync`

如果你还需要按条件搜索、分页、`Exists`、`Count` 等能力，请转到 [查询指南](./03-query-guide.md)。

## 6. 混合批处理与 Upsert 补充

除了 `BatchInsertAsync`、`BatchUpdateAsync`、`BatchDeleteAsync` 这类同构操作，LiteOrm 还支持把不同操作放进同一批处理中。

```csharp
var newUser = new TestUser { Name = "Mixed 1", Age = 10, CreateTime = DateTime.Now };

var ops = new List<EntityOperation<TestUser>>
{
    new EntityOperation<TestUser> { Entity = newUser, Operation = OpDef.Insert },
    new EntityOperation<TestUser> { Entity = existingUser, Operation = OpDef.Delete }
};

await service.BatchAsync(ops);
```

这个例子来自 `LiteOrm.Tests\ServiceTests.cs`，适合需要“新增一批数据，同时删除旧数据”的同步迁移场景。

## 7. 实战建议

- 高频写入场景优先考虑批量接口。
- 需要显式控制 SQL 结构时，可使用 `Expr.Update<T>()` 和 DAO。
- 跨多表业务操作建议放到 Service 中并配合事务。

## 相关链接

- [返回目录](../SUMMARY.md)
- [查询指南](./03-query-guide.md)
- [事务管理](../03-advanced-topics/01-transactions.md)
- [性能优化](../03-advanced-topics/03-performance.md)

