# Concurrency, Transactions and Read Paths

These three tend to show up in the same incident: two users edit one row and one edit is silently overwritten; a read inside a transaction returns stale data; a row written a moment ago cannot be found. This page covers timestamp-based concurrency, transaction boundaries and the read-only replica path, plus what happens where they intersect.

## 1. Optimistic concurrency: the timestamp column

Mark one column on the entity:

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

The column type is open: an `int` version counter, a `rowversion`, a timestamp. The first column with `IsTimestamp = true` becomes `TableDefinition.TimestampColumn`.

Pass the value you originally read as a parameter:

```csharp
var user = await viewDao.GetObject(id).FirstOrDefaultAsync();
user.Name = "New name";
user.Version = user.Version + 1;          // the new value comes from the caller; the framework does not increment it
bool ok = dao.Update(user, timestamp: valueReadEarlier);
```

The two statement shapes:

```sql
-- With a timestamp: the old value goes into WHERE
UPDATE TestTimestampUsers SET Name = ?, Version = ? WHERE Id = ? AND Version = ?

-- Without a timestamp parameter: primary key only, last writer wins
UPDATE TestTimestampUsers SET Name = ?, Version = ? WHERE Id = ?
```

Behaviour details:

| Item | Detail |
| --- | --- |
| New value | Taken from the entity property; the framework never increments or substitutes it |
| Old value | Taken from the `timestamp` parameter and placed in `WHERE` |
| Conflict result | Zero rows affected, the method returns `false`, no exception |
| Applies to | `Update(T, object? timestamp)` / `UpdateAsync(T, object? timestamp, CancellationToken)` |
| Does not apply to | `UpdateOrInsert` (it reads then writes, without a timestamp condition); `DELETE` uses the primary key only |
| Missing timestamp column | Passing a timestamp when the entity declares no such column throws `InvalidOperationException` |

What happens after `false` is a business decision: re-read and merge, or tell the user the record changed. That is the semantics of optimistic concurrency, not an error.

Two things that are easy to miss:

- Deletes have no concurrency protection. If only the version you saw may be deleted, add a conditional delete (pass a `LogicExpr` with the timestamp to `DeleteAll`) or perform a timestamped update before deleting.
- `UpdateOrInsert` does not check concurrency. It fits idempotent writes, not read-then-modify workflows.

## 2. Transaction boundaries

Two forms, declarative and manual:

```csharp
// Declarative: applies to a service method
[Transaction]
public async Task TransferAsync(long fromId, long toId, decimal amount) { /* ... */ }

// With an explicit isolation level
[Transaction(IsolationLevel = IsolationLevel.Serializable)]
public void RebuildIndex(long tenantId) { /* ... */ }
```

```csharp
// Manual: wrap a block of custom logic
await SessionManager.Current!.ExecuteInTransactionAsync(async session =>
{
    await fromService.UpdateAsync(from);
    await toService.UpdateAsync(to);
});
```

Boundary rules:

| Rule | Behaviour |
| --- | --- |
| `BeginTransaction` while already in a transaction | Returns `false` and logs a warning; no nested transaction |
| Nested declarative calls | The inner call reuses the outer transaction; the inner isolation level is ignored |
| Several data sources in one `SessionManager` | All enlisted in the same transaction; read-only connections are skipped |
| A context created during the transaction | Joins the current transaction |
| Reads inside a transaction | Forced to the primary, the read-only setting is ignored |
| Nested service calls | Reuse the outer transaction and do not raise `OnInvoking` / `OnInvoked` |

The last row matters for auditing: service A calling service B does not produce two invocation records, so a correlation id has to be passed explicitly.

## 3. Read paths and read-only replicas

Read-only replicas are configured under the primary data source:

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

Pool settings left out inherit from the primary.

Selection rules:

| Condition | Connection used |
| --- | --- |
| A read through a view DAO (`IsView` is `true` on `ObjectViewDAO<T>` and `DataViewDAO<T>`) | A read-only replica |
| A write through `ObjectDAO<T>` | The primary |
| No `ReadOnlyConfigs` configured | Falls back to the primary |
| Inside a transaction | Forced to the primary |
| A second read in the same session | Reuses the replica chosen the first time |

Several replicas are used round-robin. The replica chosen within a session is cached and reused so queries do not hop between instances.

## 4. Two traps where these intersect

### Write then read immediately

Writes go to the primary while reads go to a replica, so replication lag shows up here:

```csharp
await orderService.UpdateAsync(order);
var latest = await orderService.GetObjectAsync(order.Id);   // hits a replica, may still be the old row
```

When a caller must read its own write, there are three options:

1. Put the read in the same transaction; reads inside a transaction fall back to the primary.
2. Read through the primary path (`ObjectDAO<T>`) instead of a view DAO.
3. Accept eventual consistency and make the workflow asynchronous.

### Using a replica's stale value for a concurrency check

The "old value" for a timestamp update usually comes from a query. If that query lands on a replica, the timestamp may be older than the current primary value, and a perfectly valid update is reported as a conflict:

```csharp
var user = await viewService.GetObjectAsync(id);     // replica read, Version may lag
user.Version = user.Version + 1;
var ok = await dao.UpdateAsync(user, valueReadEarlier);   // returns false although nobody else wrote
```

When the concurrency check has to be exact, pin the "read the current version" step to the primary. A common shape is a `[Transaction]` service method that reads and writes inside one transaction.

## 5. Common mistakes

| Mistake | Consequence |
| --- | --- |
| Assuming a timestamp conflict throws | It returns `false`; ignoring the result loses updates |
| Expecting the framework to increment the version | The new value comes from the entity property |
| Using `UpdateOrInsert` for concurrency control | It carries no timestamp condition |
| Expecting a read-only replica inside a transaction | Reads fall back to the primary |
| Using a replica value for optimistic concurrency | Lag produces false conflicts |
| Relying on delete for concurrency protection | `DELETE` matches the primary key only |
| Starting a background task that writes from inside a service method | The task does not inherit the transaction boundary |

## Related

- [Back to index](../README.md)
- [Transactions](../06-di/01-transactions.en.md)
- [Sharding and table arguments](../03-advanced-topics/02-sharding-and-tableargs.en.md)
- [Configuration reference](../05-reference/01-configuration-reference.en.md)
- [Audit trails and change tracking](./03-audit-trail.en.md)
- [Data mapping and value conversion](../03-advanced-topics/11-data-mapping.en.md)
