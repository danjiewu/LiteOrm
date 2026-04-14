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

### 1.5 Rollback Example from Demo

`LiteOrm.Demo\Demos\TransactionDemo.cs` demonstrates a useful failure rollback scenario: create a user first, then insert an intentionally invalid sales record to trigger automatic transaction rollback.

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

## 3. Transaction Propagation Behavior

LiteOrm's transaction propagation behavior:

| Scenario | Behavior |
|----------|----------|
| No existing transaction | Creates new transaction |
| Has existing transaction | Joins existing transaction (nested) |
| Transaction failure | Full rollback |

## 4. Transactions and SessionManager

LiteOrm uses `SessionManager` to manage database connections and transactions:

- Supports cross-database transactions
- When a transaction begins, all database connections already held by the current Scope's SessionManager enter the transaction
- Database connections acquired during the transaction are automatically added to the transaction
- All LiteOrm database operations under the current Scope are automatically managed by the current transaction
- If transaction isolation is needed, create a new Scope

## Related Links

- [Back to docs hub](../README.md)
- [Associations](../02-core-usage/05-associations.en.md)
- [Sharding and Table Routing](./02-sharding-and-tableargs.en.md)
- [Performance Optimization](./03-performance.en.md)
