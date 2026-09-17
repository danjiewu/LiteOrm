# Concurrency and Read/Write Splitting in Practice

Concurrency, transactions and read paths often show up in the same incident: two users edit one row and one edit is silently overwritten, a value read inside a transaction is stale, or a write followed immediately by a read returns the old value. The scenarios below cover each case.

## Scenario 1: two users edit the same row

**Requirement**: while an order edit page is open, someone else changes the same record. Whoever submits last must not silently overwrite the earlier change.

**Approach**: mark a timestamp column on the entity and pass the value you read as an argument on update:

```csharp
[Table("TestTimestampUsers")]
public class TestTimestampUser : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Name")]
    public string? Name { get; set; }

    [Column("Version", IsTimestamp = true)]
    public int Version { get; set; }
}
```

```csharp
var user = await viewDao.GetObject(id).FirstOrDefaultAsync();
var oldVersion = user.Version;

user.Name = "New name";
user.Version = oldVersion + 1;          // the caller supplies the new value; the framework never increments it

bool ok = await dao.UpdateAsync(user, oldVersion);
if (!ok) throw new ConcurrencyConflictException("The row changed; reload and retry.");
```

The two statement shapes:

```sql
-- With a timestamp: the old value goes into WHERE
UPDATE TestTimestampUsers SET Name = ?, Version = ? WHERE Id = ? AND Version = ?

-- Without a timestamp argument: primary key only, the last write wins
UPDATE TestTimestampUsers SET Name = ?, Version = ? WHERE Id = ?
```

Notes:

- The column type is free: an `int` version, `rowversion` or a timestamp all work. The first column with `IsTimestamp = true` becomes `TableDefinition.TimestampColumn`.
- A conflict affects 0 rows and the method returns `false`; it does not throw. Ignoring the return value means giving up concurrency protection.
- The applicable methods are `Update(T, object? timestamp)` / `UpdateAsync(T, object? timestamp, CancellationToken)`. The new value comes from the current property value; the framework never increments or substitutes it.
- `UpdateOrInsert` reads then writes internally and carries no timestamp condition, so it does not fit read-modify-write flows. It targets idempotent writes.
- Passing a timestamp when the entity has no timestamp column throws `InvalidOperationException`.

## Scenario 2: deletes need concurrency protection too

**Requirement**: only the version the caller saw may be deleted; a delete must fail once someone else has changed the row.

**Approach**: delete statements are generated from the primary key only and carry no timestamp condition, so add the condition yourself:

```csharp
using static LiteOrm.Common.Expr;

// Conditional delete with the version in the predicate
var affected = orderService.DeleteAll(
    Expr.Lambda<TestTimestampUser>(u => u.Id == id && u.Version == oldVersion));

if (affected == 0) throw new ConcurrencyConflictException("The row changed.");
```

Notes:

- `Delete(entity)` and `DeleteID(id)` both delete by primary key and do not use the timestamp column.
- When the business needs "update then delete", do the timestamped update first inside the same transaction and delete only after it succeeds.
- The `DeleteAll` predicate can also carry the data scope; see [Data Permissions in Practice](./02-data-permission.en.md).

## Scenario 3: multi-step writes must be atomic

**Requirement**: transfers and stock deductions span several rows and must roll back as a whole on failure.

**Approach**: declarative and manual forms:

```csharp
// Declarative: applies to a service method
[Transaction]
public async Task TransferAsync(long fromId, long toId, decimal amount, CancellationToken cancellationToken = default)
{
    await accountService.UpdateAllAsync(
        Expr.Update<Account>(a => new Account { Balance = a.Balance - amount }, a => a.Id == fromId),
        null, cancellationToken);

    await accountService.UpdateAllAsync(
        Expr.Update<Account>(a => new Account { Balance = a.Balance + amount }, a => a.Id == toId),
        null, cancellationToken);
}
```

```csharp
// The isolation level can be specified when needed
[Transaction(IsolationLevel = IsolationLevel.Serializable)]
public void RebuildIndex(long tenantId) { /* ... */ }
```

```csharp
// Manual: wrap a block of custom logic
await SessionManager.Current!.ExecuteInTransactionAsync(async session =>
{
    await fromService.UpdateAsync(from, cancellationToken);
    await toService.UpdateAsync(to, cancellationToken);
});
```

Boundary rules:

| Rule | Behaviour |
| --- | --- |
| `BeginTransaction` while a transaction is active | Returns `false` and logs a warning; no nested transaction |
| Nested declarative transactions | The inner call reuses the outer one and its isolation level has no effect |
| Several data sources inside one `SessionManager` | All join the same transaction; read-only connections are skipped |
| Data source contexts created inside the transaction | Join the current transaction |
| Queries inside a transaction | Forced to the master, read replicas are ignored |
| Nested service calls | Reuse the outer transaction and do not raise `OnInvoking` / `OnInvoked` |

Notes:

- The last rule directly affects auditing: service A calling service B does not produce two call records, so propagate a correlation id yourself. See [Audit and Change Tracking in Practice](./04-audit-and-change-tracking.en.md).
- A background task started inside a service method does not inherit the transaction boundary. Asynchronous follow-up writes must either join the same transaction or explicitly use their own scope.

## Scenario 4: read-heavy workload on read replicas

**Requirement**: report and list queries are heavy and should read from replicas.

**Approach**: replicas are declared under the master data source and nothing changes at runtime:

```json
{
  "LiteOrm": {
    "Default": "WriteDB",
    "DataSources": [
      {
        "Name": "WriteDB",
        "ConnectionString": "Server=master;...",
        "Provider": "...",
        "ReadOnlyConfigs": [
          { "ConnectionString": "Server=replica01;..." },
          { "ConnectionString": "Server=replica02;...", "PoolSize": 10 }
        ]
      }
    ]
  }
}
```

Pool settings left out inherit from the master configuration. Selection rules:

| Condition | Which connection |
| --- | --- |
| Query through a view DAO (`ObjectViewDAO<T>`, `DataViewDAO<T>` with `IsView` true) | Read replica |
| Write through `ObjectDAO<T>` | Master |
| No `ReadOnlyConfigs` configured | Falls back to the master |
| Inside a transaction | Forced to the master |
| Second query in the same session | Reuses the replica chosen for the first one |

Notes:

- Replicas are assigned round-robin, and the one chosen inside a session is cached and reused so a session does not switch servers on every query.
- The choice follows the DAO type, so keeping a write entity and a display view separate makes the split automatic.
- Replicas lag. Anything that depends on reading the newest value must explicitly go to the master; see the next scenario.

## Scenario 5: write then read returns the old value

**Requirement**: after submitting a change the caller is redirected to a detail page that still shows the previous data.

**Approach**: pin the "read my own write" step to the master:

```csharp
await orderService.UpdateAsync(order);

// Option 1: read inside the same transaction, where reads are forced back to the master
await SessionManager.Current!.ExecuteInTransactionAsync(async session =>
{
    var latest = await orderService.GetObjectAsync(order.Id);
});
```

```csharp
// Option 2: give the critical read path its own master DAO
public sealed class MasterOrderViewDAO : ObjectViewDAO<OrderView>
{
    public MasterOrderViewDAO(SessionManager sessionManager) : base(sessionManager) { }

    protected override bool IsView => false;   // after this override the DAO no longer reads replicas
}
```

```csharp
services.AddScoped<ObjectViewDAO<OrderView>, MasterOrderViewDAO>();
```

Notes:

- Choose among three routes by tolerance: read inside the transaction, give the critical entry point a master DAO, or make the flow asynchronous with confirmation.
- The replica choice is driven by the DAO's `IsView` (`DAOBase.IsView` defaults to `false`, `ObjectViewDAO<T>` overrides it to `true`), so overriding it back to `false` returns to the master without changing any query text.
- The old value used for timestamp concurrency usually comes from a query. If that query lands on a replica, the version read can be older than the master's current value and an otherwise valid update is rejected as a conflict. When the concurrency decision must be exact, pin the "read the current version" step to the master, for example by putting the critical update in a `[Transaction]` service method that reads and writes inside one transaction.
- The same applies to ownership checks in data permissions, where replica lag causes misjudgements. See scenario 3 of [Data Permissions in Practice](./02-data-permission.en.md).

## Scenario 6: common mistakes with concurrency and transactions

| Mistake | Consequence |
| --- | --- |
| Expecting a timestamp conflict to throw | It returns `false`; ignoring the value loses updates |
| Expecting the framework to increment a version | The new value comes from the entity property, never incremented |
| Using `UpdateOrInsert` for concurrency control | It carries no timestamp condition |
| Using deletes as concurrency protection | `DELETE` uses the primary key only |
| Expecting read replicas inside a transaction | Reads inside a transaction are forced to the master |
| Using a value read from a replica for optimistic concurrency | Lag causes false conflicts |
| Starting a background write inside a service method | The task does not inherit the transaction boundary |

## Related links

- [Back to index](../README.md)
- [Transactions](../06-di/01-transactions.en.md)
- [Configuration Reference](../05-reference/01-configuration-reference.en.md)
- [Sharding and TableArgs](../03-advanced-topics/01-sharding-and-tableargs.en.md)
- [Audit and Change Tracking in Practice](./04-audit-and-change-tracking.en.md)
- [Data Permissions in Practice](./02-data-permission.en.md)
