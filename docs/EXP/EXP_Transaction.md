# 事务处理

LiteOrm 支持两种事务管理方式：声明式事务和手动事务。

## 1. 声明式事务

使用 `[Transaction]` 特性标记方法，框架自动管理事务的开启、提交和回滚。

### 1.1 基本用法

```csharp
public class UserService : EntityService<User>
{
    private readonly IOrderService _orderService;

    public UserService(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [Transaction]
    public async Task CreateUserWithOrder(User user, Order order)
    {
        await Insert(user);
        order.UserId = user.Id;
        await _orderService.Insert(order);
    }
}
```

### 1.2 嵌套调用

声明式事务支持嵌套，嵌套方法使用同一事务：

```csharp
[Transaction]
public async Task TransferMoney(long fromId, long toId, decimal amount)
{
    var fromAccount = await _accountService.GetObjectAsync(fromId);
    var toAccount = await _accountService.GetObjectAsync(toId);

    fromAccount.Balance -= amount;
    toAccount.Balance += amount;

    await _accountService.Update(fromAccount);
    await Update(fromAccount);  // 同一事务中

    await _accountService.Update(toAccount);
    await Update(toAccount);    // 同一事务中
}
```

### 1.3 注意事项

- `[Transaction]` 特性需要 Castle.Core 动态代理支持
- 方法必须是 `public` 且通过接口调用才能生效
- 避免在事务方法中使用 `await` 后切换线程，可能导致事务失效

## 2. 手动事务

通过 `SessionManager` 手动控制事务。

### 2.1 基本用法

```csharp
using var session = SessionManager.Current.BeginTransaction();
try
{
    // 执行多个操作
    await userService.InsertAsync(user);
    await orderService.InsertAsync(order);

    session.Commit();
}
catch (Exception ex)
{
    session.Rollback();
    throw;
}
```

### 2.2 事务隔离级别

```csharp
using var session = SessionManager.Current.BeginTransaction(IsolationLevel.ReadCommitted);
try
{
    // 操作
    session.Commit();
}
catch
{
    session.Rollback();
    throw;
}
```

### 2.3 只读事务

```csharp
using var session = SessionManager.Current.BeginTransaction(IsolationLevel.ReadCommitted);
try
{
    var users = await userService.SearchAsync(u => u.Status == 1);
    session.Commit();
}
catch
{
    session.Rollback();
    throw;
}
```

## 3. 事务传播行为

LiteOrm 的事务传播行为：

| 场景 | 行为 |
|------|------|
| 无现有事务 | 创建新事务 |
| 有现有事务 | 加入现有事务（嵌套） |
| 事务失败 | 全部回滚 |


## 4. 事务与 SessionManager

LiteOrm 使用 `SessionManager` 管理数据库连接及事务：
- 支持跨数据库的事务
- 事务开始时，当前 Scope 的 SessionManager 已有的数据库连接都将进入事务
- 在事务过程中获取的数据库连接也会自动加上事务
- 当前 Scope 下 LiteOrm 的所有数据库操作都会自动受当前事务管理
- 如需隔离事务，需要创建新的 Scope

## 5. 下一步

- 分表分库：[EXP_Sharding](./EXP_Sharding.md)
- 性能优化：[EXP_Performance](./EXP_Performance.md)
