# Transactions

LiteOrm supports two main transaction styles: declarative transactions for service methods and manual transactions for cases that need explicit control.

## 1. Which style should I choose?

| Scenario | Recommended style |
|------|--------------------|
| Normal business service methods | `[Transaction]` |
| Multi-step workflows inside one service | `[Transaction]` |
| Custom commit / rollback timing | `SessionManager` |
| Infrastructure jobs or unusual boundaries | `SessionManager` |

## 2. Declarative transactions

Use `[Transaction]` on a `public` service method:

```csharp
[Transaction]
public async Task CreateUserWithOrderAsync(User user, Order order)
{
    await InsertAsync(user);
    order.UserId = user.Id;
    await _orderService.InsertAsync(order);
}
```

### Notes

- LiteOrm relies on Castle.Core dynamic proxy support here.
- The method must be invoked through the proxied service interface.
- Nested transactional service calls join the current transaction by default.

## 3. Manual transactions

Use `SessionManager` when the application must decide exactly when to commit or roll back:

```csharp
using var session = SessionManager.Current.BeginTransaction();
try
{
    await userService.InsertAsync(user);
    await orderService.InsertAsync(order);
    session.Commit();
}
catch
{
    session.Rollback();
    throw;
}
```

You can also specify an `IsolationLevel`:

```csharp
using var session = SessionManager.Current.BeginTransaction(IsolationLevel.ReadCommitted);
```

## 4. Propagation behavior

| Situation | Behavior |
|------|----------|
| No active transaction | Create a new transaction |
| Active transaction already exists | Join the existing transaction |
| Any step fails | Roll back the full transaction scope |

## 5. `SessionManager` scope behavior

`SessionManager` coordinates the current scope's connections and transaction state:

- existing connections in the scope join the transaction
- newly opened LiteOrm connections also join it
- creating a new DI scope is the normal way to isolate transaction context

## 6. Practical advice

- Keep transaction boundaries in the application or business layer.
- Prefer a single `EntityService` workflow method over scattering partial writes across controllers.
- Use manual transactions when a loop, checkpoint, or conditional commit is part of the design.

## Related Links

- [Back to English docs hub](../SUMMARY.en.md)
- [Associations](../02-core-usage/05-associations.en.md)
- [Sharding and TableArgs](./02-sharding-and-tableargs.en.md)
- [Performance](./03-performance.en.md)
