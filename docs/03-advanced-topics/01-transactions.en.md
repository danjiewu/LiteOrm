# Transactions

LiteOrm supports two transaction management approaches: declarative transactions and manual transactions.

## Choosing Between Approaches

| Scenario | Recommended Approach |
|----------|----------------------|
| Standard business service methods | Declarative |
| Need explicit control over commit/rollback timing | Manual |
| Combining multiple DAO/Service writes | Declarative preferred |
| Infrastructure layer, batch processing, special transaction boundaries | Manual |

## 1. Declarative Transactions

Use the `[Transaction]` attribute to mark methods. The framework automatically manages transaction begin, commit, and rollback.

### 1.1 Basic Usage

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
        await InsertAsync(user);                  // operates on User table
        order.UserId = user.Id;
        await _orderService.InsertAsync(order);   // operates on Order table
    }
}
```

### 1.2 Nested Calls

Declarative transactions support nesting. Nested methods reuse the same transaction without repeated Begin/Commit:

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
            await _orderItemService.AppendAsync(item);  // Inner [Transaction] reuses outer
        }
    }
}

public class OrderItemService : EntityService<OrderItem>, IOrderItemService
{
    [Transaction]
    public async Task AppendAsync(OrderItem item)
    {
        await InsertAsync(item);  // Inner exit does not commit; managed by outermost
    }
}
```

### 1.3 Caveats

- `[Transaction]` attribute requires Castle.Core dynamic proxy support
- Methods must be `public` and called through interface to take effect
- Avoid starting background tasks that are detached from the current call chain within transaction methods; such tasks will not automatically inherit the current transaction boundary
- In nested calls, the inner `IsolationLevel` is not applied; the whole transaction uses the outer method's isolation level

### 1.4 Business Complete Example

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

### 1.5 Failure Rollback Example

Below is a practical failure rollback scenario: create a user first, then insert an intentionally invalid sales record to trigger automatic transaction rollback.

```csharp
var newUser = new User { UserName = "ThreeTierUser", Age = 25 };
var initialSale = new SalesRecord
{
    ProductName = new string('A', 300), // Intentionally exceeds field length, triggers exception
    Amount = 1
};

bool success = await factory.BusinessService
    .RegisterUserWithInitialSaleAsync(newUser, initialSale);
```

## 2. Manual Transactions

Control transactions manually through `SessionManager`.

### 2.1 Basic Usage

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

### 2.2 Specify Isolation Level

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

### 2.3 Queries Can Also Be Included in Transaction Boundaries

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

### 2.4 Choosing Between Declarative and Manual Transactions

- If the business boundary is naturally a Service method, prefer declarative transactions.
- If you need to decide when to commit in loops, batches, or intermediate states, manual transactions are more suitable.
- Keep the actual transaction boundary at the business application layer, not the controller layer.

## 3. Sub-scope Transaction Isolation

When a piece of logic must run in an **independent transaction** (independent commit, rollback, isolation level), create a new DI scope. The `SessionManager.Current` inside the child scope does not affect the parent scope.

### 3.1 Basic Usage

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
        // Parent scope: may be inside a transaction
        await _mainService.UpdateAsync(record);

        // Child scope: a fully independent transaction
        using (var scope = _scopeFactory.CreateScope())
        {
            var isolatedSvc = scope.ServiceProvider.GetRequiredService<IAuditService>();
            // Even if the parent scope rolls back later, this audit log is committed independently
            await isolatedSvc.RecordAsync("processed", record.Id);
        }

        // Parent scope continues
        await _mainService.UpdateAsync(other);
    }
}
```

### 3.2 Independent Isolation Level

Inside a child scope you can use any isolation level, independent of the parent scope:

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

### 3.3 Cross-scope Call Comparison

| Call Style | Shares Transaction | Isolation Level |
|------------|-------------------|-----------------|
| `await _otherService.MethodAsync()` | Yes | Reuses outer |
| `using (scope = _scopeFactory.CreateScope())` | **Independent** | Can be specified separately |

### 3.4 Notes

- The child scope must be `Dispose`d, otherwise the `SessionManager` is not released and the connection is not returned to the pool.
- The child scope isolates the database transaction, not concurrency control. Multiple child scopes operating on the same resource in parallel still require `Serializable` or row locks.

## 4. How `timestamp` Relates to Transactions

`timestamp`-based optimistic concurrency and transactions are complementary, not competing, mechanisms:

- Transactions guarantee that a group of operations succeeds or fails as a unit.
- `timestamp` checks prevent lost updates, where a later write silently overwrites an earlier one.

A common combination is:

1. Wrap the business workflow in a transaction.
2. Update critical entities with `ObjectDAO<T>.Update(entity, timestamp)` or `UpdateAsync(entity, timestamp)`.
3. Treat a `false` return value as a concurrency conflict and stop the workflow.

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

Recommendation:

- Use transactions when you need business-level atomicity.
- Add `timestamp` checks when you need protection against concurrent overwrites.
- For important read-then-write flows, using both together is usually the safest choice.

## Related Links

- [Associations](../02-core-usage/06-associations.en.md)
- [Sharding and Table Routing](./02-sharding-and-tableargs.en.md)
- [Performance Optimization](./03-performance.en.md)
