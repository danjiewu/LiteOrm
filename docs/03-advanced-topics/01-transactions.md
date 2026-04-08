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

### 1.5 来自 Demo 的回滚示例

`LiteOrm.Demo\Demos\TransactionDemo.cs` 里演示了一个很实用的失败回滚场景：先创建用户，再插入一条故意不合法的销售记录，让事务自动回滚。

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

- [返回目录](../README.md)
- 关联查询：[关联查询](../02-core-usage/05-associations.md)
- 分表分库：[分表分库](./02-sharding-and-tableargs.md)
- 性能优化：[性能优化](./03-performance.md)

