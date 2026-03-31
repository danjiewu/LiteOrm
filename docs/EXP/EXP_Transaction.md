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

## 4. 分布式事务

对于跨数据库的分布式事务，建议使用以下方案：

1. **Saga 模式**：将分布式事务拆分为多个本地事务，搭配事件驱动
2. **最终一致性**：接受短暂不一致，通过补偿机制最终一致

```csharp
// Saga 模式示例
public class OrderSagaService
{
    private readonly IUserService _userService;
    private readonly IOrderService _orderService;
    private readonly IInventoryService _inventoryService;

    [Transaction]
    public async Task CreateOrderSaga(Order order)
    {
        // Step 1: 扣减库存
        await _inventoryService.ReserveAsync(order.ProductId, order.Quantity);

        // Step 2: 创建订单
        await _orderService.InsertAsync(order);

        // Step 3: 余额扣减（如果失败，前面的步骤不会回滚，需要补偿）
        try
        {
            await _userService.DeductBalanceAsync(order.UserId, order.Amount);
        }
        catch
        {
            // 补偿：释放库存
            await _inventoryService.ReleaseAsync(order.ProductId, order.Quantity);
            throw;
        }
    }
}
```

## 5. 事务与连接池

LiteOrm 使用连接池管理数据库连接：

- 事务开始时从池中获取连接
- 事务提交/回滚后释放连接回池
- 长时间事务会占用连接资源，应避免长时间开启事务

## 6. 常见问题

### Q: 声明式事务不生效？

检查以下几点：
1. 方法是否是 `public`
2. 是否通过注入的接口调用（而非直接调用实现类）
3. Castle.Core 动态代理是否正常工作

### Q: 事务中查询不到刚插入的数据？

默认隔离级别下，事务内的查询可能读到旧数据。可使用 `INSERTED` 提示或调整隔离级别。

### Q: 嵌套事务如何工作？

LiteOrm 使用数据库的嵌套事务（savepoint）机制，嵌套不创建新事务，仅设置保存点。

## 7. 下一步

- 分表分库：[EXP_Sharding](./EXP_Sharding.md)
- 性能优化：[EXP_Performance](./EXP_Performance.md)
