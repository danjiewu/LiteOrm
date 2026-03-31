# 增删改查

本文详细介绍 LiteOrm 的增删改查操作，包括单条操作、批量操作、条件操作。

## 1. 插入操作

### 1.1 单条插入

```csharp
var user = new User
{
    UserName = "admin",
    Email = "admin@test.com",
    Age = 30,
    CreateTime = DateTime.Now
};

bool success = await userService.InsertAsync(user);
Console.WriteLine($"插入成功，用户ID: {user.Id}");
```

### 1.2 批量插入

```csharp
var users = new List<User>
{
    new User { UserName = "user1", Email = "user1@test.com", Age = 20 },
    new User { UserName = "user2", Email = "user2@test.com", Age = 25 },
    new User { UserName = "user3", Email = "user3@test.com", Age = 30 }
};

await userService.BatchInsertAsync(users);
```

### 1.3 插入或更新（Upsert）

```csharp
var user = new User
{
    Id = existingId,  // 如果设置了 ID 且数据库存在，则更新
    UserName = "updated",
    Email = "updated@test.com"
};

var result = await userService.UpdateOrInsertAsync(user);
Console.WriteLine($"操作类型: {result.OperationType}"); // Insert 或 Update
```

### 1.4 批量插入或更新

```csharp
await userService.BatchUpdateOrInsertAsync(users);
```

## 2. 更新操作

### 2.1 根据实体更新

更新实体对象后调用 `Update`，会更新所有已映射列：

```csharp
var user = await userService.SearchOneAsync(u => u.Id == 1);
user.Email = "newemail@test.com";
user.Age = 35;

await userService.UpdateAsync(user);
```

### 2.2 批量更新

```csharp
var users = await userService.SearchAsync(u => u.Status == 0);
foreach (var user in users)
{
    user.Status = 1;
}

await userService.BatchUpdateAsync(users);
```

### 2.3 根据条件更新

使用 `Expr.Update<T>()` 链式构造进行条件更新：

```csharp
// 更新所有 Age < 18 的用户状态为 0
await objectDao.UpdateAsync(
    Expr.Update<User>()
        .Where(Expr.Prop("Age") < 18)
        .Set(
            ("Status", Expr.Value(0)),
            ("UpdateTime", Expr.Value(DateTime.Now))
        )
);
```

## 3. 删除操作

### 3.1 根据实体删除

```csharp
var user = await userService.SearchOneAsync(u => u.Id == 1);
await userService.DeleteAsync(user);
```

### 3.2 根据主键删除

```csharp
await userService.DeleteAsync(1);  // 删除 ID 为 1 的记录
```

### 3.3 批量删除

```csharp
// 删除多个实体
var users = await userService.SearchAsync(u => u.Status == -1);
await userService.BatchDeleteAsync(users);

// 根据主键批量删除
await userService.BatchDeleteIDAsync(new[] { 1, 2, 3 });
```

### 3.4 根据条件删除

```csharp
// Lambda 方式
await userService.DeleteAsync(u => u.CreateTime < DateTime.Today.AddYears(-1));

// Expr 方式
await objectDao.Delete(Expr.Prop("Status") == 0 & Expr.Prop("IsActive") == false);
```

## 4. 查询操作

### 4.1 查询所有

```csharp
var allUsers = await userService.SearchAsync();  // 无条件查询所有
```

### 4.2 根据主键查询

```csharp
var user = await userService.GetObjectAsync(1);
```

### 4.3 条件查询单条

```csharp
var user = await userService.SearchOneAsync(u => u.UserName == "admin");
```

### 4.4 条件查询列表

```csharp
var adults = await userService.SearchAsync(u => u.Age >= 18);
```

### 4.5 排序分页查询

```csharp
var page = await userService.SearchAsync(
    q => q.Where(u => u.Status == 1)
          .OrderByDescending(u => u.CreateTime)
          .Skip(10).Take(20)
);
```

### 4.6 EXISTS 查询

```csharp
// 检查是否存在满足条件的记录
bool exists = await userService.ExistsAsync(u => u.UserName == "admin");

// EXISTS 子查询
var users = await userService.SearchAsync(
    u => Expr.Exists<Order>(o => o.UserId == u.Id && o.Amount > 1000)
);
```

### 4.7 COUNT 查询

```csharp
int count = await userService.CountAsync(u => u.Status == 1);
```

## 5. Service 接口一览

### 5.1 同步操作接口 IEntityService<T>

| 方法                                     | 说明     |
| -------------------------------------- | ------ |
| `Insert(T entity)`                     | 插入单条   |
| `Update(T entity)`                     | 更新单条   |
| `Delete(T entity)`                     | 删除单条   |
| `Delete(object id)`                    | 根据主键删除 |
| `BatchInsert(IEnumerable<T> entities)` | 批量插入   |
| `BatchUpdate(IEnumerable<T> entities)` | 批量更新   |
| `BatchDelete(IEnumerable<T> entities)` | 批量删除   |
| `UpdateOrInsert(T entity)`             | 插入或更新  |

### 5.2 异步操作接口 IEntityServiceAsync<T>

| 方法                                          | 说明     |
| ------------------------------------------- | ------ |
| `InsertAsync(T entity)`                     | 插入单条   |
| `UpdateAsync(T entity)`                     | 更新单条   |
| `DeleteAsync(T entity)`                     | 删除单条   |
| `DeleteAsync(object id)`                    | 根据主键删除 |
| `BatchInsertAsync(IEnumerable<T> entities)` | 批量插入   |
| `BatchUpdateAsync(IEnumerable<T> entities)` | 批量更新   |
| `BatchDeleteAsync(IEnumerable<T> entities)` | 批量删除   |
| `UpdateOrInsertAsync(T entity)`             | 插入或更新  |

### 5.3 查询操作接口 IEntityViewService<TView>

| 方法                     | 说明     |
| ---------------------- | ------ |
| `GetObject(object id)` | 根据主键获取 |
| `SearchOne(Expr expr)` | 条件查询单条 |
| `Search(Expr expr)`    | 条件查询列表 |
| `Exists(Expr expr)`    | 检查是否存在 |
| `Count(Expr expr)`     | 条件计数   |

### 5.4 异步查询接口 IEntityViewServiceAsync<TView>

| 方法                          | 说明     |
| --------------------------- | ------ |
| `GetObjectAsync(object id)` | 根据主键获取 |
| `SearchOneAsync(Expr expr)` | 条件查询单条 |
| `SearchAsync(Expr expr)`    | 条件查询列表 |
| `ExistsAsync(Expr expr)`    | 检查是否存在 |
| `CountAsync(Expr expr)`     | 条件计数   |

## 6. ObjectDAO vs EntityService

| 对比项  | ObjectDAO | EntityService |
| ---- | --------- | ------------- |
| 用法   | 直接操作实体    | 封装业务逻辑        |
| 增删改  | ✅ 支持      | ✅ 支持          |
| 查询   | ✅ 支持      | ✅ 支持          |
| 关联查询 | ✅ 支持      | ✅ 支持          |
| 事务管理 | 需手动处理     | 可声明式处理        |

### 6.1 ObjectDAO 示例

```csharp
public class UserDao : ObjectDAO<User>
{
    public async Task<List<User>> GetActiveUsersAsync()
    {
        return await Search(u => u.Status == 1).ToListAsync();
    }
}
```

### 6.2 EntityService 示例

```csharp
public class UserService : EntityService<User>
{
    public async Task<List<User>> GetActiveUsersAsync()
    {
        return await Search(u => u.Status == 1);
    }

    [Transaction]
    public async Task CreateUserWithDefaultRole(User user)
    {
        await Insert(user);
        // 同时创建默认角色等关联操作
    }
}
```

## 7. 下一步

- 进阶操作：[事务处理](./EXP/EXP_Transaction.md)
- 分表分库：[EXP\_Sharding](./EXP/EXP_Sharding.md)
- 性能优化：[EXP\_Performance](./EXP/EXP_Performance.md)

