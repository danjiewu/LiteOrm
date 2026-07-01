# 事务处理

LiteOrm 支持两种事务管理方式：声明式事务和手动事务。

## 场景选型

| 场景 | 推荐方式 |
|------|----------|
| 标准业务服务方法 | 声明式事务 |
| 需要显式控制提交/回滚时机 | 手动事务 |
| 组合多个 DAO / Service 写入 | 声明式事务优先 |
| 基础设施层、批处理、特殊事务边界 | 手动事务 |

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
        await InsertAsync(user);                  // 操作 User 表
        order.UserId = user.Id;
        await _orderService.InsertAsync(order);   // 操作 Order 表
    }
}
```

### 1.2 嵌套调用

声明式事务支持嵌套，嵌套方法复用同一事务，不重复 Begin/Commit：

```csharp
public class OrderService : EntityService<Order>
{
    private readonly IOrderItemService _orderItemService;

    [Transaction]
    public async Task SubmitOrderAsync(Order order, List<OrderItem> items)
    {
        await InsertAsync(order);

        foreach (var item in items)
        {
            item.OrderId = order.Id;
            await _orderItemService.AppendAsync(item);  // 内层 [Transaction] 复用外层
        }
    }
}

public class OrderItemService : EntityService<OrderItem>, IOrderItemService
{
    [Transaction]
    public async Task AppendAsync(OrderItem item)
    {
        await InsertAsync(item);  // 内层结束不提交，事务由最外层统一管理
    }
}
```

### 1.3 注意事项

- `[Transaction]` 特性需要 Castle.Core 动态代理支持
- 方法必须是 `public` 且通过接口调用才能生效
- 避免在事务方法里启动脱离当前调用链的后台任务；这类任务不会自动继承当前事务边界
- 嵌套调用时内层的 `IsolationLevel` 不会生效，整个事务沿用外层隔离级别

### 1.4 业务闭环示例

```csharp
[Transaction]
public async Task SubmitOrderAsync(CreateOrderInput input)
{
    var order = new Order { UserId = input.UserId, Amount = input.Amount };
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

## 2. 手动事务

通过 `SessionManager` 手动控制事务。

### 2.1 基本用法

```csharp
var sessionManager = SessionManager.Current;
sessionManager.BeginTransaction();
try
{
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

### 2.2 指定隔离级别

```csharp
var sessionManager = SessionManager.Current;
sessionManager.BeginTransaction(IsolationLevel.ReadCommitted);
try
{
    await userService.InsertAsync(user);
    sessionManager.Commit();
}
catch
{
    sessionManager.Rollback();
    throw;
}
```

### 2.3 查询纳入事务边界

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

- 业务边界天然就是一个 Service 方法时，优先用声明式事务。
- 需要在循环、批次或中间状态上决定何时提交时，改用手动事务。
- 事务边界应控制在业务应用层，而不是控制器层。

## 3. 子作用域事务隔离

当需要让一段逻辑运行在**独立事务**中（独立提交、回滚、隔离级别），创建新的 DI 作用域即可。子作用域内的 `SessionManager.Current` 与父作用域互不影响。

### 3.1 基本用法

```csharp
public class ReportService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMainService _mainService;

    public ReportService(IServiceScopeFactory scopeFactory, IMainService mainService)
    {
        _scopeFactory = scopeFactory;
        _mainService = mainService;
    }

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

### 3.2 独立隔离级别

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

### 3.3 跨作用域调用对比

| 调用方式 | 是否共享事务 | 隔离级别 |
|---------|-------------|---------|
| `await _otherService.MethodAsync()` | 共享 | 复用外层 |
| `using (scope = _scopeFactory.CreateScope())` | **独立** | 可单独指定 |

### 3.4 注意事项

- 子作用域必须 `Dispose`，否则 `SessionManager` 不会释放，连接不归还连接池。
- 子作用域隔离的是数据库事务，不是并发控制；多个子作用域并行操作同一资源时仍需 `Serializable` 或行锁。

## 4. `timestamp` 与事务的关系

`timestamp` 乐观并发控制和事务不是互斥关系，它们解决的是两个不同问题：

- 事务：保证一组操作要么一起成功，要么一起失败。
- `timestamp`：防止“后提交覆盖先提交”的丢失更新。

典型组合方式：

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

- [关联查询](../02-core-usage/06-associations.md)
- [分表分库](./02-sharding-and-tableargs.md)
- [性能优化](./03-performance.md)
