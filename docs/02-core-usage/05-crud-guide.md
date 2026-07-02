# CRUD 指南

本文聚焦 LiteOrm 的写入、更新、删除和批量操作。查询能力请统一参考 [查询指南](./04-query-guide.md)。

## 1. 插入

### 单条插入

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

### 批量插入

```csharp
await userService.BatchInsertAsync(users);
```

### 批量初始化示例

使用批量插入来初始化部门、用户和销售记录，适合作为导入或初始化脚本参考：

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
bool success = await userService.UpdateOrInsertAsync(user);
Console.WriteLine(success); // true 表示执行成功
```

如果你需要区分本次到底是插入还是更新，可以直接使用 DAO 层的 `UpdateOrInsertResult`。

### 批量 Upsert

```csharp
await userService.BatchUpdateOrInsertAsync(users);
```

### Batch Upsert 示例

同一批数据中既有“已存在需要更新”的实体，也有“需要新增”的实体：

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
user.UserName = "admin_v2";
await userService.UpdateAsync(user);
```

### 批量更新

```csharp
foreach (var user in users)
{
    user.Age += 1;
}

await userService.BatchUpdateAsync(users);
```

### 批量更新示例

先查询出部门，再集中修改负责人，最后一次性提交：

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

### 使用 `timestamp` 做乐观并发更新

如果你希望更新时额外校验“读取时版本”和“提交时版本”是否一致，可以给实体声明一个 `timestamp` 列，然后使用 `ObjectDAO<T>` 的 `Update(entity, timestamp)` / `UpdateAsync(entity, timestamp)` 重载。

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
user.Version = originalVersion + 1; // 实体上的值会写回数据库

bool updated = await dao.UpdateAsync(user, originalVersion);
if (!updated)
{
    Console.WriteLine("发生并发冲突，记录已被其他人修改。");
}
```

这一重载的行为要点：

- 实体上的 `Version` 是将要写入数据库的新值。
- `timestamp` 参数是查询时拿到的旧值，会被放进 `WHERE` 条件里做并发校验。
- 当返回 `false` 时，通常表示主键存在，但 `timestamp` 已不匹配。
- 通用 `IEntityService<T>` / `IEntityServiceAsync<T>` 的 `Update` 重载不带 `timestamp` 参数；需要乐观并发时，请直接使用 `ObjectDAO<T>`，或在自定义 Service 中封装 DAO。
- `BatchUpdate` / `BatchUpdateAsync` 不会自动附带 `timestamp` 并发校验。

> 完整示例可参考 `LiteOrm.Tests\ObjectDAOTests.cs` 和 `LiteOrm.Tests\Models\TestTimestampUser.cs`。

### 条件更新

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

### UpdateExpr 实战示例

`UpdateExpr` 的几种典型玩法：

```csharp
using static LiteOrm.Common.Expr;
var update = new UpdateExpr(new TableExpr(typeof(User)), Prop("UserName") == "UpdateDemo_Bob")
    .Set(("Age", Const(35)));

int affected = await userService.UpdateAllAsync(update);
```

也可以直接在 `SET` 子句里写算术表达式或函数表达式：

```csharp
using static LiteOrm.Common.Expr;
var agePlusFive = new UpdateExpr(new TableExpr(typeof(User)), Prop("UserName") == "UpdateDemo_Carol")
    .Set(("Age", Prop("Age") + Const(5)));

var rename = new UpdateExpr(new TableExpr(typeof(User)), Prop("UserName") == "UpdateDemo_Bob")
    .Set(("UserName", Func("CONCAT", Prop("UserName"), Const("_v2"))));
```

### 使用 Lambda 表达式更新（推荐）

如果不想手工拼 `UpdateExpr`，可以直接调用 `UpdateAll` / `UpdateAllAsync` 的 Lambda 扩展方法。它接受两个 Lambda 表达式：第一个定义“更新哪些字段、赋什么值”，第二个定义 `WHERE` 条件。框架内部会通过 `LambdaExprConverter` 自动转换为 `UpdateExpr`，因此写法更接近 EF Core，且具备编译期类型检查。

```csharp
// 等价于 UPDATE Users SET Age = 29 WHERE UserName = 'UpdateDemo_Alice'
await userService.UpdateAllAsync(
    u => new User { Age = 29 },
    u => u.UserName == "UpdateDemo_Alice"
);
```

更新表达式中还可以引用原实体字段参与运算（例如自增、字符串拼接），`WHERE` 条件也支持 `&&` / `||` 等逻辑组合：

```csharp
// 等价于 UPDATE Users SET Age = Age + 1, CreateTime = @now WHERE Age >= 28
await userService.UpdateAllAsync(
    u => new User { Age = u.Age + 1, CreateTime = DateTime.Now },
    u => u.Age >= 28
);
```

使用要点：

- 第一个 Lambda 必须为 `Expression<Func<T, T>>`，且主体是 `new T { ... }` 形式的 `MemberInitExpression`，每个 `=` 绑定都会被翻译为一个 `SET` 子句。
- 第二个 Lambda 为 `Expression<Func<T, bool>>`，即普通的 WHERE 条件，可以省略（不传 `whereExpression` 时表示更新全部记录，请谨慎使用）。
- 引用原字段（如 `u.Age + 1`）会被翻译为 `Prop("Age") + Const(1)`，对应 SQL `Age = Age + 1`。
- 还可以通过 `params string[] tableArgs` 传入动态表名参数。

```csharp
// 同步版本 + 动态表名
userService.UpdateAll(
    u => new User { Age = 30 },
    u => u.UserName == "UpdateDemo_Bob",
    "Users_2026" // 动态表名
);
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

### 批量增改删闭环示例

一组适合复制的闭环验证：

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

这个例子很适合验证批量接口是否能覆盖“插入 → 更新 → 删除”的整条路径。

### 条件删除

```csharp
using static LiteOrm.Common.Expr;
await userService.DeleteAsync(u => u.CreateTime < DateTime.Today.AddYears(-1));
await objectDao.Delete(Prop("Age") < 18 & Prop("UserName").StartsWith("Temp"));
```

### 条件删除示例

分表场景下的删除条件，同样适用于普通表（去掉 `tableArgs` 即可）：

```csharp
int deleted = await service.DeleteAsync(
    l => l.Amount > 400 && l.Event == "DeleteEvent",
    tableArgs: new[] { "202401" }
);
```

## 4. 返回值与行为说明

| 方法类型 | 常见返回值 | 含义 |
| --- | --- | --- |
| `Insert/Update/Delete` | `bool` | 是否成功执行。 |
| 条件更新/删除 | `int` | 受影响行数。 |
| Service 层 `UpdateOrInsert` | `bool` | 是否成功执行。 |
| DAO 层 `UpdateOrInsert` | `UpdateOrInsertResult` | 告知本次是插入还是更新。 |

## 5. Service 接口速览

### `IEntityService<T>` / `IEntityServiceAsync<T>`

实体级写入与批量操作（参数为强类型 `T` 或 `IEnumerable<T>`）：

- `Insert` / `InsertAsync`
- `Update` / `UpdateAsync`
- `UpdateOrInsert` / `UpdateOrInsertAsync`
- `Delete` / `DeleteAsync`
- `BatchInsert` / `BatchInsertAsync`
- `BatchUpdate` / `BatchUpdateAsync`
- `BatchUpdateOrInsert` / `BatchUpdateOrInsertAsync`
- `BatchDelete` / `BatchDeleteAsync`
- `Batch` / `BatchAsync`：混合批处理，每条记录可指定 `OpDef.Insert` / `Update` / `Delete`

继承自非泛型 `IEntityService` / `IEntityServiceAsync`，还包含以下按表达式或主键操作的方法：

- `UpdateAll` / `UpdateAllAsync`：按 `UpdateExpr` 条件更新，返回受影响行数
- `DeleteAll` / `DeleteAllAsync`：按 `LogicExpr` 条件删除，返回受影响行数
- `DeleteID` / `DeleteIDAsync`：按主键删除
- `BatchDeleteID` / `BatchDeleteIDAsync`：按主键批量删除

> 上述 `UpdateAll` / `DeleteAll` 也都可以通过 `LambdaExprExtensions` 提供的 Lambda 扩展方法直接调用（见本文“使用 Lambda 表达式更新”及“条件删除”小节）。

如果你还需要按条件搜索、分页、`Exists`、`Count` 等能力，请转到 [查询指南](./04-query-guide.md)。

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

适合需要“新增一批数据，同时删除旧数据”的同步迁移场景。

## 7. 实战建议

- 高频写入场景优先考虑批量接口。
- 需要显式控制 SQL 结构时，可使用 `Expr.Update<T>()` 和 DAO。
- 跨多表业务操作建议放到 Service 中并配合事务。

## 相关链接

- [返回目录](../README.md)
- [查询指南](./04-query-guide.md)
- [事务管理](../03-advanced-topics/01-transactions.md)
- [性能优化](../03-advanced-topics/03-performance.md)


