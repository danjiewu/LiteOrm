# Wiring Up Audit and Change Tracking

Audit needs answer two different questions: "who called which operation when", and "what did this record change from and to". The first comes from service invocation events, the second from entity service events. This page covers when each fires, what data is available, and where to write it.

## 1. The three event families

| Events | Defined in | Fires when | Good for |
| --- | --- | --- | --- |
| `IEntityServiceEvent<T>` | `LiteOrm` / `LiteOrm.Common` | Around entity inserts, updates and deletes | Change details, field diffs |
| `IServiceInvokingEvent` / `IServiceInvokedEvent` / `IServiceExceptionEvent` | `LiteOrm.DependencyInjection` | Before, after and on failure of a service method | Operation logs, timings, failure reasons |
| `DAOContextPool.DatabaseSync.OnTableSyncing` | `LiteOrm` | Before a table sync | DDL auditing (rarely needed) |

All three take subscribers from the container, so no business code has to change.

## 2. Entity service events

The interface has 16 members: eight `Before` callbacks returning `bool` (returning `false` cancels the operation) and eight `After` callbacks returning `void`. When you only care about a few, extend `EntityServiceEventBase<T>`; the base class returns `true` from every `Before` and implements every `After` as an empty method:

```csharp
public sealed class OrderAuditEvent : EntityServiceEventBase<Order>
{
    private readonly ICurrentUser _user;
    private readonly IAuditWriter _writer;

    public OrderAuditEvent(ICurrentUser user, IAuditWriter writer)
    {
        _user = user;
        _writer = writer;
    }

    public override bool OnInserting(Order entity)
    {
        _writer.Record(new AuditEntry("Order", entity.Id, "Insert", _user.Id, null, Serialize(entity)));
        return true;
    }

    public override void OnUpdated(Order entity)
        => _writer.Record(new AuditEntry("Order", entity.Id, "Update", _user.Id, null, Serialize(entity)));
}
```

Registration:

```csharp
services.AddScoped<IEntityServiceEvent<Order>, OrderAuditEvent>();
```

Subscribers are resolved from the container and may take dependencies such as `ICurrentUser`, `ILogger` or a repository. The subscriber collection is requested from the container the first time an event notification fires, so an exception thrown from a subscriber constructor surfaces on the first entity operation rather than when `IEntityService<Order>` is resolved.

### When `After` callbacks fire

Not every path raises them. The differences matter when you build the audit record:

| Method | `After` fires when |
| --- | --- |
| `Insert` / `Update` / `Delete` | The operation returns `true` |
| `UpdateOrInsert` | The result is `Inserted` or `Updated` |
| `DeleteID` | The key-based delete succeeds |
| `DeleteAll` / `UpdateAll` | The affected row count is greater than 0 |
| `BatchDeleteID` | Always |
| `BatchInsert` / `BatchUpdate` / `BatchDelete` | Once per row |

Batch methods run every `Before` callback first; cancelled elements never enter the working set, and `After` only fires for the elements that were actually written. Out of 100 rows, if 3 are cancelled by a `Before` callback, `After` fires 97 times.

### Limits of the trigger path

Entity service events are raised by `EntityService<T>`. Writing through `IObjectDAO<T>` directly, or running custom SQL through `DataDAO<T>`, raises nothing. "All business writes go through the service layer" therefore has to be a code rule, ideally enforced by an architecture test that forbids business projects from referencing `IObjectDAO<T>`.

## 3. Reading the previous value and diffing fields

`OnUpdating(Order entity)` receives the new value. The old one has to be read:

```csharp
public sealed class OrderDiffEvent : EntityServiceEventBase<Order>
{
    private readonly IEntityViewService<Order> _viewService;
    private readonly IAuditWriter _writer;

    public OrderDiffEvent(IEntityViewService<Order> viewService, IAuditWriter writer)
    {
        _viewService = viewService;
        _writer = writer;
    }

    public override bool OnUpdating(Order entity)
    {
        var before = _viewService.GetObject(entity.Id);
        if (before is not null)
            _writer.Record(BuildDiff(entity.Id, before, entity));

        return true;
    }

    private static AuditEntry BuildDiff(long id, Order before, Order after) { /* compare column by column */ }
}
```

Three notes:

- The callbacks have their own return semantics; `OnUpdating` returns `bool`. To write audit asynchronously, do not override the synchronous callback with `async void`. Collect the changes instead and flush them in a batch after the transaction commits, or make auditing an explicit step at the service entry point.
- A read through a view DAO may land on a read-only replica. With replication lag the "previous" value can be older than the committed one. When the audit must be exact, bind the previous-value read to the primary (read once at the service entry point, then call the service).
- Sharded entities need `tableArgs` (`GetObject(id, tableArgs)`), otherwise the previous value may come from the default table. For the diff itself, iterate column metadata rather than reflecting over properties and comparing strings.

## 4. Where the audit rows go

Whether audit rows live in the same database decides whether "business rolled back, audit kept" is acceptable.

Every data source context inside one `SessionManager` is enlisted by `BeginTransaction` (read-only connections are skipped), and contexts created later during the transaction join it. Marking an audit entity to a different data source therefore does not make it independent of the business transaction:

```csharp
[Table("AuditEntries", DataSource = "AuditDb")]
public class AuditEntry : ObjectBase { /* ... */ }
```

Two ways to make auditing independent:

1. Write the audit after the transaction commits. Keep the business service method free of the transaction and have the caller audit once the commit succeeds.
2. Use a separate scope. `IServiceScopeFactory.CreateScope()` yields an independent `SessionManager` with its own transaction boundary, so the audit write is unaffected by a business rollback.

Conversely, if "no audit when the business fails" is the requirement, write the audit inside the same transaction and accept the cost of writing to the same database.

`After` callbacks fire once the statement succeeds, while the outer transaction is still open. If the business later rolls back, the audit rows stay. Pick one semantic explicitly: audit "operations that were attempted" or "changes that took effect".

## 5. Service-level auditing

Call logs use the three event interfaces, which fire before the method runs, after it returns successfully, and after it throws:

```csharp
public sealed class OperationLogEvent : IServiceInvokingEvent, IServiceInvokedEvent, IServiceExceptionEvent
{
    private readonly ILogger<OperationLogEvent> _logger;

    public OperationLogEvent(ILogger<OperationLogEvent> logger) => _logger = logger;

    public void OnInvoking(ServiceInvokeContext context)
        => _logger.LogInformation("Invoke {Service}.{Method}", context.ServiceName, context.MethodName);

    public void OnInvoked(ServiceInvokeContext context)
        => _logger.LogInformation("Return {Service}.{Method} in {Duration}", context.ServiceName, context.MethodName, context.Duration);

    public void OnException(ServiceExceptionContext context)
        => _logger.LogError(context.Exception, "Failed {Service}.{Method}", context.ServiceName, context.MethodName);
}

services.AddScoped<IServiceInvokingEvent, OperationLogEvent>();
services.AddScoped<IServiceInvokedEvent, OperationLogEvent>();
services.AddScoped<IServiceExceptionEvent, OperationLogEvent>();
```

`ServiceInvokeContext` exposes `ServiceType`, `ServiceName`, `Method`, `MethodName`, `Arguments`, `SessionId`, `Duration` and `Result`. `SessionId` is the field that ties several service calls from one session together, which is what you correlate on when diagnosing an incident.

Two limits:

- Events only fire for calls through an interface, which requires the `LiteOrm.DependencyInjection` interceptor to be active.
- Nested service calls do not raise events. When service A calls service B internally, only A's call is logged. For a full call chain, propagate a correlation id from the application layer.

## 6. Masking

Neither audit nor logs should carry sensitive arguments. Framework logs are controlled with a parameter-level `[Log(false)]`:

```csharp
public void ChangePhone(long userId, string phone, [Log(false)] string idCardNumber) { /* ... */ }
```

Marked parameters appear as `*` in framework logs. Note that `ServiceInvokeContext.Arguments` holds raw arguments with no masking, so subscribers writing `Arguments` still have to decide for themselves.

The audit table itself needs the same treatment: mask or encrypt fields such as phone numbers and identity numbers before they are stored (see [Field encryption and masking](./04-field-encryption.en.md)).

## 7. Cost in bulk scenarios

Bulk writes fire events per row. A 10,000-row insert means 10,000 `Before` calls and 10,000 `After` calls, and a database round trip per callback makes auditing the bottleneck. Common approaches:

- Buffer in the subscriber and flush to the audit table in batches, taking care to flush at the end of the request.
- Share the bulk write channel with the business write, using the same `SessionManager`.
- Enable per-row auditing only for critical entities; for the rest, record the count and the range.

## 8. Common mistakes

| Mistake | Consequence |
| --- | --- |
| Writing through `IObjectDAO<T>` directly | No events, gaps in the audit trail |
| Treating `After` as "the transaction committed" | Audit rows survive a business rollback |
| Calling a write method of the same service from a subscriber | Events fire recursively |
| Caching state in a singleton subscriber | Data bleeds across requests |
| Writing audit into the same transaction and rejecting rollback | Audit and business disagree |

## Related

- [Back to index](../README.md)
- [Logging and diagnostics](../06-di/03-logging.en.md)
- [Transactions](../06-di/01-transactions.en.md)
- [Data permissions and authorization gaps](./02-data-permission.en.md)
- [Field encryption and masking](./04-field-encryption.en.md)
- [Concurrency and read paths](./06-concurrency-and-read-path.en.md)
