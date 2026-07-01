# 事务处理

LiteOrm 支持两种事务管理方式：声明式事务和手动事务。

## 场景选型

| 场景 | 推荐方式 | 原因 |
|------|----------|------|
| 标准业务服务方法 | 声明式事务 | 代码简洁，边界清晰 |
| 需要显式控制提交/回滚时机 | 手动事务 | 控制粒度更高 |
| 组合多个 DAO / Service 写入 | 声明式事务优先 | 更贴近业务封装 |
| 基础设施层、批处理、特殊事务边界 | 手动事务 | 更适合细粒度控制 |

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
        await InsertAsync(user);
        order.UserId = user.Id;
        await _orderService.InsertAsync(order);
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
- 避免在事务方法里启动脱离当前调用链的后台任务；这类任务不会自动继承当前事务边界

### 1.4 业务闭环示例

```csharp
[Transaction]
public async Task SubmitOrderAsync(CreateOrderInput input)
{
    var order = new Order
    {
        UserId = input.UserId,
        Amount = input.Amount
    };

    await _orderService.InsertAsync(order);

    foreach (var item in input.Items)
    {
        await _orderItemService.InsertAsync(new OrderItem
        {
            OrderId = order.Id,
            ProductId = item.ProductId,
            Quantity = item.Quantity
        });
    }

    await _auditLogService.InsertAsync(new AuditLog
    {
        Action = "SubmitOrder",
        RefId = order.Id.ToString()
    });
}
```

这个模式适合“主表 + 明细 + 审计日志”一类的典型业务事务。

### 1.5 失败回滚示例

下面是一个实用的失败回滚场景：先创建用户，再插入一条故意不合法的销售记录，让事务自动回滚。

```csharp
var newUser = new User { UserName = "ThreeTierUser", Age = 25 };
var initialSale = new SalesRecord
{
    ProductName = new string('A', 300), // 故意超过字段长度，触发异常
    Amount = 1
};

bool success = await factory.BusinessService
    .RegisterUserWithInitialSaleAsync(newUser, initialSale);
```

这个例子很适合验证“异常发生后，主流程已插入的数据是否也被撤回”。

## 2. 手动事务

通过 `SessionManager` 手动控制事务。

### 2.1 基本用法

```csharp
var sessionManager = SessionManager.Current;
sessionManager.BeginTransaction();
try
{
    // 执行多个操作
    await userService.InsertAsync(user);
    await orderService.InsertAsync(order);

    sessionManager.Commit();
}
catch
{
    sessionManager.Rollback();
    throw;
}
```

### 2.2 事务隔离级别

```csharp
var sessionManager = SessionManager.Current;
sessionManager.BeginTransaction(IsolationLevel.ReadCommitted);
try
{
    // 操作
    sessionManager.Commit();
}
catch
{
    sessionManager.Rollback();
    throw;
}
```

### 2.3 查询也可以纳入事务边界

```csharp
var sessionManager = SessionManager.Current;
sessionManager.BeginTransaction(IsolationLevel.ReadCommitted);
try
{
    var users = await userService.SearchAsync(u => u.Age >= 18);
    sessionManager.Commit();
}
catch
{
    sessionManager.Rollback();
    throw;
}
```

### 2.4 与声明式事务的取舍

- 如果业务边界天然就是一个 Service 方法，优先用声明式事务。
- 如果你需要在循环、批次或中间状态上决定何时提交，改用手动事务更合适。
- 无论哪种方式，都建议把真正的事务边界控制在业务应用层，而不是控制器层。

## 3. 事务传播与嵌套行为

LiteOrm 的事务传播遵循"加入现有事务"语义：当一个 `[Transaction]` 方法调用另一个 `[Transaction]` 方法时，内层方法**不会**开启新事务，而是复用外层已开启的事务。

### 3.1 传播规则

| 调用上下文 | 内层 `[Transaction]` 行为 |
|-----------|---------------------------|
| 无外层事务 | 开启新事务，方法结束提交 |
| 已有外层事务 | **加入外层事务**，不重复 Begin/Commit |
| 外层事务失败 | 整条调用链回滚，内层不可见 |

### 3.2 嵌套调用示例

```csharp
public class OrderService : EntityService<Order>
{
    private readonly IOrderItemService _orderItemService;

    [Transaction]
    public async Task SubmitOrderAsync(Order order, List<OrderItem> items)
    {
        await InsertAsync(order);                          // 外层事务已开启

        foreach (var item in items)
        {
            item.OrderId = order.Id;
            await _orderItemService.AppendAsync(item);     // 内层 [Transaction] 复用外层
        }

        // 外层方法正常结束后，整体事务提交
    }
}

public class OrderItemService : EntityService<OrderItem>, IOrderItemService
{
    [Transaction]                                          // 内层事务标记
    public async Task AppendAsync(OrderItem item)
    {
        await InsertAsync(item);
        // 内层方法结束不会提交，事务由最外层统一管理
    }
}
```

### 3.3 嵌套事务的隔离级别被忽略

**重要**：当外层已开启事务时，内层 `[Transaction(IsolationLevel = ...)]` 中指定的隔离级别**不会生效**，整个事务沿用外层方法的隔离级别。

```csharp
[Transaction(IsolationLevel = IsolationLevel.ReadCommitted)]
public async Task OuterAsync()
{
    // 事务以 ReadCommitted 开启
    await InnerAsync();
}

[Transaction(IsolationLevel = IsolationLevel.Serializable)]  // ⚠ 不生效
public async Task InnerAsync()
{
    // 仍在 ReadCommitted 事务中执行
}
```

如需让内层逻辑使用更高隔离级别，应将其拆分为独立的事务作用域（见第 4 节）。

### 3.4 异常传播与回滚

嵌套调用中任意一层抛出异常，整个事务（含外层已执行的写入）都会回滚：

```csharp
[Transaction]
public async Task OuterAsync()
{
    await InsertAsync(user);                // ✓ 已写入，但最终会回滚
    await InnerFailAsync();                 // ✗ 抛出异常
    await InsertAsync(log);                 // ✗ 不会执行
    // 整个事务回滚，user 不会落库
}

[Transaction]
public async Task InnerFailAsync()
{
    await InsertAsync(record);
    throw new InvalidOperationException("模拟业务失败");
}
```

### 3.5 后台任务的事务时序陷阱

`[Transaction]` 的事务上下文通过 `SessionManager.Current` 的 `AsyncLocal` 链路传递。`AsyncLocal` 会随 `ExecutionContext` 流入 `Task.Run`、`ThreadPool.QueueUserWorkItem` 等后台任务，因此后台任务**能**拿到同一个 `SessionManager`，与父流程共享同一事务。这恰恰是问题所在：

- **时序不确定**：父方法 `await` 完成时，后台任务可能尚未执行写入；父事务随之提交，后台任务的写入被丢失（或因事务已结束而失败）。
- **生命周期错配**：父事务提交/回滚后 `SessionManager` 可能被释放，后台任务继续使用会命中 `ObjectDisposedException`。
- **异常不可见**：后台任务的异常不会冒泡到父流程，事务的"全部成功或全部回滚"约定被破坏。

```csharp
[Transaction]
public async Task SubmitAsync()
{
    await InsertAsync(order);

    // ⚠ 后台任务与父流程共享同一事务，但时序不可控
    _ = Task.Run(async () =>
    {
        // 这里能拿到同一个 SessionManager，但父事务可能已经提交或结束
        await _auditService.InsertAsync(new AuditLog { RefId = order.Id.ToString() });
    });
}
```

正确做法二选一：

- **要与主事务一起提交**：直接 `await`，让后台逻辑纳入主调用链。
- **要在主事务之外独立执行**：在后台任务内创建新的 DI 作用域（见第 4 节），让其运行在独立事务中，并自行决定何时触发（如主事务提交成功后再入队）。

## 4. 子作用域事务隔离

当需要让一段逻辑运行在**独立事务**中（独立提交、独立回滚、独立隔离级别），可创建新的 DI 作用域。LiteOrm 在子作用域起始时会自动切换 `SessionManager.SetCurrentFactory`，使子作用域内的 `SessionManager.Current` 解析到全新实例。

### 4.1 触发机制

`LiteOrmServiceExtensions.RegisterScope` 在每个子 LifetimeScope 开始时执行：

```csharp
scope.ChildLifetimeScopeBeginning += (sender, e) =>
{
    var childScope = e.LifetimeScope;
    SessionManager.SetCurrentFactory(childScope.Resolve<SessionManager>);
    // 子作用域结束恢复父作用域的 SessionManager
};
```

因此 `IServiceScopeFactory.CreateScope()` 创建的 `IServiceScope` 内的所有 LiteOrm 操作都使用独立的 `SessionManager`，与父作用域的事务互不影响。

### 4.2 基本用法

```csharp
public class ReportService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public async Task RunIndependentTxnAsync()
    {
        // 父作用域：可能在事务中
        await _mainService.UpdateAsync(record);

        // 子作用域：完全独立的事务
        using (var scope = _scopeFactory.CreateScope())
        {
            var isolatedSvc = scope.ServiceProvider.GetRequiredService<IAuditService>();
            // 即使父作用域后续回滚，这里的审计日志也会独立提交
            await isolatedSvc.RecordAsync("processed", record.Id);
        }

        // 父作用域继续工作
        await _mainService.UpdateAsync(other);
    }
}
```

### 4.3 独立隔离级别

子作用域内可使用任意隔离级别，与父作用域互不影响：

```csharp
using (var scope = _scopeFactory.CreateScope())
{
    var sessionManager = scope.ServiceProvider.GetRequiredService<SessionManager>();
    sessionManager.BeginTransaction(IsolationLevel.Serializable);
    try
    {
        var svc = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        await svc.LockAndDecrementAsync(productId, quantity);
        sessionManager.Commit();
    }
    catch
    {
        sessionManager.Rollback();
        throw;
    }
}
```

适合"先做一次强隔离的库存锁定，再回到主流程的默认事务"等组合场景。

### 4.4 跨作用域调用对比

| 调用方式 | 是否共享事务 | 隔离级别 |
|---------|-------------|---------|
| `await _otherService.MethodAsync()` | 共享（同一 SessionManager） | 复用外层 |
| `using (scope = _scopeFactory.CreateScope())` | **独立** | 可单独指定 |

### 4.5 注意事项

- 子作用域内的实体对象若来自父作用域的查询，可能持有旧的 `SessionManager` 上下文；写入时应通过子作用域内的服务重新查询或使用脱管对象。
- 子作用域必须 `Dispose`，否则 `SessionManager` 不会释放，连接不归还连接池。
- 子作用域隔离的是数据库事务，不是并发控制；多个子作用域并行操作同一资源时仍需 `Serializable` 或行锁。

## 5. 事务与 SessionManager

LiteOrm 使用 `SessionManager` 管理数据库连接及事务：

- 支持跨数据库的事务
- 事务开始时，当前 Scope 的 SessionManager 已有的数据库连接都将进入事务
- 在事务过程中获取的数据库连接也会自动加上事务
- 当前 Scope 下 LiteOrm 的所有数据库操作都会自动受当前事务管理
- 如需隔离事务，需要创建新的 Scope（见第 4 节）

## 6. `timestamp` 与事务的关系

`timestamp` 乐观并发控制和事务不是互斥关系，它们解决的是两个不同问题：

- 事务：保证一组操作要么一起成功，要么一起失败。
- `timestamp`：防止“后提交覆盖先提交”的丢失更新。

典型组合方式是：

1. 用事务包裹一个完整业务流程。
2. 对关键实体更新时，使用 `ObjectDAO<T>.Update(entity, timestamp)` 或 `UpdateAsync(entity, timestamp)`。
3. 当返回 `false` 时，将其视为并发冲突并中止当前流程。

```csharp
[Transaction]
public async Task<bool> RenameUserAsync(int id, string newName)
{
    var user = await _userViewDao.GetObject(id).FirstOrDefaultAsync();
    if (user == null)
        return false;

    int originalVersion = user.Version;
    user.UserName = newName;
    user.Version = originalVersion + 1;

    return await _userDao.UpdateAsync(user, originalVersion);
}
```

建议：

- 需要保证业务原子性时使用事务。
- 需要防止并发覆盖时增加 `timestamp` 校验。
- 对“先查再改”的关键写操作，通常两者一起使用更稳妥。

## 相关链接

- [返回目录](../README.md)
- [关联查询](../02-core-usage/06-associations.md)
- [分表分库](./02-sharding-and-tableargs.md)
- [性能优化](./03-performance.md)


