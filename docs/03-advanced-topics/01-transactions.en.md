# Transactions

LiteOrm supports two transaction management approaches: declarative transactions and manual transactions.

## Choosing Between Approaches

| Scenario | Recommended Approach | Reason |
|----------|---------------------|--------|
| Standard business service methods | Declarative | Clean code, clear boundaries |
| Need explicit control over commit/rollback timing | Manual | Finer granularity |
| Combining multiple DAO/Service writes | Declarative preferred | Closer to business encapsulation |
| Infrastructure layer, batch processing, special transaction boundaries | Manual | Better for fine-grained control |

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
        await InsertAsync(user);
        order.UserId = user.Id;
        await _orderService.InsertAsync(order);
    }
}
```

### 1.2 Nested Calls

Declarative transactions support nesting. Nested methods use the same transaction:

```csharp
[Transaction]
public async Task TransferMoney(long fromId, long toId, decimal amount)
{
    var fromAccount = await _accountService.GetObjectAsync(fromId);
    var toAccount = await _accountService.GetObjectAsync(toId);

    fromAccount.Balance -= amount;
    toAccount.Balance += amount;

    await _accountService.Update(fromAccount);
    await Update(fromAccount);  // Same transaction

    await _accountService.Update(toAccount);
    await Update(toAccount);    // Same transaction
}
```

### 1.3 Caveats

- `[Transaction]` attribute requires Castle.Core dynamic proxy support
- Methods must be `public` and called through interface to take effect
- Avoid starting background tasks that are detached from the current call chain within transaction methods; such tasks will not automatically inherit the current transaction boundary

### 1.4 Business Complete Example

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

This pattern is suitable for typical business transactions like "main table + details + audit log."

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

This example is ideal for verifying "whether data already inserted in the main flow is also rolled back after an exception occurs."

## 2. Manual Transactions

Control transactions manually through `SessionManager`.

### 2.1 Basic Usage

```csharp
var sessionManager = SessionManager.Current;
sessionManager.BeginTransaction();
try
{
    // Execute multiple operations
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

### 2.2 Transaction Isolation Level

```csharp
var sessionManager = SessionManager.Current;
sessionManager.BeginTransaction(IsolationLevel.ReadCommitted);
try
{
    // Operations
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
- Regardless of approach, it's recommended to keep the actual transaction boundary at the business application layer, not the controller layer.

## 3. Transaction Propagation and Nesting

LiteOrm's transaction propagation follows a "join existing transaction" semantic: when a `[Transaction]` method calls another `[Transaction]` method, the inner method does **not** start a new transaction but reuses the one already opened by the outer method.

### 3.1 Propagation Rules

| Calling Context | Inner `[Transaction]` Behavior |
|-----------------|--------------------------------|
| No outer transaction | Starts a new transaction; commits on method exit |
| Outer transaction exists | **Joins the outer transaction**; no repeated Begin/Commit |
| Outer transaction fails | The entire call chain rolls back; inner writes are not visible |

### 3.2 Nested Call Example

```csharp
public class OrderService : EntityService<Order>
{
    private readonly IOrderItemService _orderItemService;

    [Transaction]
    public async Task SubmitOrderAsync(Order order, List<OrderItem> items)
    {
        await InsertAsync(order);                          // Outer transaction opened

        foreach (var item in items)
        {
            item.OrderId = order.Id;
            await _orderItemService.AppendAsync(item);     // Inner [Transaction] reuses outer
        }

        // The whole transaction commits when the outermost method exits successfully
    }
}

public class OrderItemService : EntityService<OrderItem>, IOrderItemService
{
    [Transaction]                                          // Inner transaction marker
    public async Task AppendAsync(OrderItem item)
    {
        await InsertAsync(item);
        // The inner method exit does not commit; the outermost method manages the transaction
    }
}
```

### 3.3 Inner Isolation Level Is Ignored

**Important**: When an outer transaction is already active, the isolation level specified on an inner `[Transaction(IsolationLevel = ...)]` is **not applied**. The whole transaction uses the outer method's isolation level.

```csharp
[Transaction(IsolationLevel = IsolationLevel.ReadCommitted)]
public async Task OuterAsync()
{
    // Transaction opened with ReadCommitted
    await InnerAsync();
}

[Transaction(IsolationLevel = IsolationLevel.Serializable)]  // ⚠ No effect
public async Task InnerAsync()
{
    // Still executes in the ReadCommitted transaction
}
```

If an inner routine needs a higher isolation level, factor it out into an independent transaction scope (see Section 4).

### 3.4 Exception Propagation and Rollback

If any layer in a nested call throws, the entire transaction (including writes already done by the outer layer) rolls back:

```csharp
[Transaction]
public async Task OuterAsync()
{
    await InsertAsync(user);                // ✓ Written, but will be rolled back
    await InnerFailAsync();                 // ✗ Throws
    await InsertAsync(log);                 // ✗ Never executes
    // The whole transaction rolls back; user is not persisted
}

[Transaction]
public async Task InnerFailAsync()
{
    await InsertAsync(record);
    throw new InvalidOperationException("Simulated business failure");
}
```

### 3.5 Transaction Timing Pitfalls of Background Tasks

The transaction context of `[Transaction]` is propagated via the `AsyncLocal` chain of `SessionManager.Current`. `AsyncLocal` flows into background tasks such as `Task.Run` and `ThreadPool.QueueUserWorkItem` through `ExecutionContext`, so a background task **can** obtain the same `SessionManager` and share the same transaction with the parent flow. That is exactly where the problem lies:

- **Indeterminate timing**: When the parent method's `await` completes, the background task may not have performed its write yet. The parent transaction then commits, and the background task's write is lost (or fails because the transaction has already ended).
- **Lifetime mismatch**: After the parent transaction commits/rolls back, the `SessionManager` may be disposed; the background task continuing to use it will hit `ObjectDisposedException`.
- **Invisible exceptions**: Exceptions from the background task do not bubble up to the parent flow, breaking the "all-or-nothing" contract of the transaction.

```csharp
[Transaction]
public async Task SubmitAsync()
{
    await InsertAsync(order);

    // ⚠ The background task shares the same transaction with the parent flow, but the timing is uncontrollable
    _ = Task.Run(async () =>
    {
        // The same SessionManager is available here, but the parent transaction may already be committed or ended
        await _auditService.InsertAsync(new AuditLog { RefId = order.Id.ToString() });
    });
}
```

Choose one of the correct approaches:

- **To commit together with the main transaction**: `await` directly to bring the background logic into the main call chain.
- **To run independently of the main transaction**: create a new DI scope inside the background task (see Section 4) so it runs in an independent transaction, and decide when to trigger it yourself (e.g., enqueue only after the main transaction commits successfully).

## 4. Sub-scope Transaction Isolation

When a piece of logic must run in an **independent transaction** (independent commit, independent rollback, independent isolation level), create a new DI scope. LiteOrm automatically switches `SessionManager.SetCurrentFactory` at the start of each child scope so that `SessionManager.Current` inside the child scope resolves to a brand-new instance.

### 4.1 Trigger Mechanism

`LiteOrmServiceExtensions.RegisterScope` runs at the beginning of each child LifetimeScope:

```csharp
scope.ChildLifetimeScopeBeginning += (sender, e) =>
{
    var childScope = e.LifetimeScope;
    SessionManager.SetCurrentFactory(childScope.Resolve<SessionManager>);
    // When the child scope ends, the parent scope's SessionManager is restored
};
```

Therefore, all LiteOrm operations inside an `IServiceScope` created by `IServiceScopeFactory.CreateScope()` use an independent `SessionManager` and do not affect the parent scope's transaction.

### 4.2 Basic Usage

```csharp
public class ReportService
{
    private readonly IServiceScopeFactory _scopeFactory;

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

### 4.3 Independent Isolation Level

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

Suitable for combinations like "perform a strongly isolated inventory lock first, then return to the main flow's default transaction".

### 4.4 Cross-scope Call Comparison

| Call Style | Shares Transaction | Isolation Level |
|------------|-------------------|-----------------|
| `await _otherService.MethodAsync()` | Yes (same SessionManager) | Reuses outer |
| `using (scope = _scopeFactory.CreateScope())` | **Independent** | Can be specified separately |

### 4.5 Notes

- Entities inside a child scope that were queried from the parent scope may hold the old `SessionManager` context. When writing, re-query them via services inside the child scope or use detached objects.
- The child scope must be `Dispose`d, otherwise the `SessionManager` is not released and the connection is not returned to the pool.
- The child scope isolates the database transaction, not concurrency control. Multiple child scopes operating on the same resource in parallel still require `Serializable` or row locks.

## 5. Transactions and SessionManager

LiteOrm uses `SessionManager` to manage database connections and transactions:

- Supports cross-database transactions
- When a transaction begins, all database connections already held by the current Scope's SessionManager enter the transaction
- Database connections acquired during the transaction are automatically added to the transaction
- All LiteOrm database operations under the current Scope are automatically managed by the current transaction
- If transaction isolation is needed, create a new Scope (see Section 4)

## 6. How `timestamp` Relates to Transactions

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

- [Back to docs hub](../README.md)
- [Associations](../02-core-usage/06-associations.en.md)
- [Sharding and Table Routing](./02-sharding-and-tableargs.en.md)
- [Performance Optimization](./03-performance.en.md)

