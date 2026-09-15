# Audit and Change Tracking in Practice

Auditing usually means two things: knowing who called which interface and when, and knowing what a record changed from and to. The first comes from service call events, the second from entity service events. The scenarios below show how to wire each one up.

Three event families are available, and all subscribers are registered through the container, so no business code changes:

| Event | Defined in | Fires | Best for |
| --- | --- | --- | --- |
| `IEntityServiceEvent<T>` | `LiteOrm` / `LiteOrm.Common` | Before and after entity writes | Change details, field diffs |
| `IServiceInvokingEvent` / `IServiceInvokedEvent` / `IServiceExceptionEvent` | `LiteOrm.DependencyInjection` | Before a service call, after success, after an exception | Operation logs, latency, failure reasons |
| `DAOContextPool.DatabaseSync.OnTableSyncing` | `LiteOrm` | Before schema synchronisation | Schema audit (rarely used) |

## Scenario 1: record who changed which row and when

**Requirement**: every insert and update of an order leaves an audit entry with the primary key, the action and the operator.

**Approach**: derive from `EntityServiceEventBase<T>` and override only the callbacks of interest; the base class treats every other callback as "do not cancel":

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

```csharp
services.AddScoped<IEntityServiceEvent<Order>, OrderAuditEvent>();
```

Notes:

- `IEntityServiceEvent<T>` has 16 members: eight Before callbacks returning `bool` (returning `false` cancels the operation) and eight After callbacks returning `void`. Deriving from the base class lets you override a handful.
- Subscribers are resolved from the container, so they can take `ICurrentUser`, `ILogger`, repositories and so on. The subscriber set is requested from the container the first time an event fires, so an exception in a subscriber constructor surfaces on the first entity operation rather than when `IEntityService<Order>` is resolved.
- Events are raised by `EntityService<T>`. Writing through `IObjectDAO<T>` directly, or running custom SQL through `DataDAO<T>`, raises nothing. "Every business write goes through the service layer" should therefore be an explicit convention, ideally enforced by an architecture test that forbids business projects from referencing `IObjectDAO<T>`.
- Do not inject singleton services into subscribers to cache request-scoped state; that bleeds across requests.

## Scenario 2: record field-level changes

**Requirement**: the audit must answer "the order amount went from 100 to 80".

**Approach**: `OnUpdating(Order entity)` only carries the new values, so the old ones must be loaded:

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
}
```

Notes:

- The callback itself carries return-value semantics: `OnUpdating` returns `bool`. To write audit data asynchronously, do not override the synchronous callback with `async void`; collect the changes and flush them in a batch after the transaction commits, or write the audit entry explicitly at the service entry point.
- The old-value read may land on a read replica, and replication lag means the value read can be older than what was actually committed. When the audit must be exact, bind the old-value read to the master, for example read once at the service entry point before calling the write.
- Sharded entities need `tableArgs` (`GetObject(id, tableArgs)`), otherwise the old value may come from the default table.
- Do not diff fields by reflecting over every property and comparing strings. Iterating the column metadata in `TableDefinition.Columns` is more reliable.

## Scenario 3: audit entries must survive a business rollback

**Requirement**: when the business transaction rolls back, the audit entry stays.

**Approach**: write the audit entry in a scope outside the business transaction:

```csharp
public sealed class AuditWriter : IAuditWriter
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AuditWriter(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public void Record(AuditEntry entry)
    {
        using var scope = _scopeFactory.CreateScope();
        var dao = scope.ServiceProvider.GetRequiredService<IObjectDAO<AuditLog>>();
        dao.Insert(entry.ToLog());
    }
}
```

Notes:

- Marking the audit entity with another data source (`[Table("AuditEntries", DataSource = "AuditDb")]`) does not detach it from the business transaction. Every data source context inside one `SessionManager` joins the transaction started by `BeginTransaction`, including contexts created inside the transaction.
- `IServiceScopeFactory.CreateScope()` gets its own `SessionManager` and its own transaction boundary, which is the reliable way to make audit writes independent.
- If the requirement is the opposite, that a failed business operation leaves no audit entry, write the audit inside the same transaction and accept the cost of a same-database write.
- After callbacks fire once the statement succeeds, which is before the outer transaction commits. If the business later rolls back, the audit entries written stay in the database. Decide the semantic up front: record "attempted operations" or "changes that took effect".

## Scenario 4: interface call logging

**Requirement**: log the service name, method name, latency and failure reason of every service call, and correlate the calls of one session.

**Approach**: the three service event interfaces cover before, after success and after an exception:

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

Notes:

- `ServiceInvokeContext` exposes `ServiceType`, `ServiceName`, `Method`, `MethodName`, `Arguments`, `SessionId`, `Duration` and `Result`. `SessionId` correlates the calls of one session and is the field you reach for first when investigating.
- Events fire only for calls that go through an interface and only when the `LiteOrm.DependencyInjection` interceptor is active, so the method must be an interface method.
- Nested service calls do not raise events. When service A calls service B internally, only A's call is recorded. If the audit needs a full call chain, propagate a correlation id at the application layer.

## Scenario 5: audit cost during bulk writes

**Requirement**: a batch import of 10,000 rows slows down when an audit entry is written per row.

**Approach**: accumulate in memory and flush in batches, or restrict per-row auditing to critical entities.

```csharp
public sealed class BatchAuditEvent : EntityServiceEventBase<Order>
{
    private readonly IAuditBuffer _buffer;

    public BatchAuditEvent(IAuditBuffer buffer) => _buffer = buffer;

    public override void OnInserted(Order entity)
        => _buffer.Add(new AuditEntry("Order", entity.Id, "Insert", 0, null, "{}"));
}

public sealed class AuditBuffer : IAuditBuffer, IAsyncDisposable
{
    private readonly List<AuditEntry> _pending = new();

    public void Add(AuditEntry entry) { lock (_pending) _pending.Add(entry); }

    // Flush once when the request scope ends
    public async ValueTask DisposeAsync() { /* batch write into the audit table */ }
}

services.AddScoped<IAuditBuffer, AuditBuffer>();
```

Notes:

- Register the buffer as scoped and flush on scope disposal so the flush lands at the end of the request; when a sharper point is needed, call flush in the `finally` block of the business method.
- Bulk methods raise one event per row: 10,000 rows means 10,000 Before and 10,000 After callbacks. When each callback writes to the database, the audit itself becomes the bottleneck.
- `BatchInsert` / `BatchUpdate` / `BatchDelete` also raise per-row events. Elements cancelled by a Before callback do not enter the remaining set, and After only fires for the elements that actually take part; if 3 of 100 are cancelled, After fires 97 times.
- Align the flush point with the transaction boundary. Flushing at the end of a request whose business transaction rolled back records "attempted operations", which is the same semantic choice as in scenario 3.

## Scenario 6: sensitive arguments in audits and logs

**Requirement**: audit logs must not contain passwords or identity numbers.

**Approach**: control framework logging with the parameter-level `[Log(false)]`:

```csharp
public void ChangePhone(long userId, string phone, [Log(false)] string idCardNumber) { /* ... */ }
```

Notes:

- Marked parameters appear as `*` in framework logs.
- `ServiceInvokeContext.Arguments` holds raw arguments with no masking, so a subscriber must decide for itself before writing `Arguments` anywhere.
- Classify the audit table as well: mask or encrypt phone numbers and identity numbers before they land. See [Sensitive Data Protection in Practice](./05-sensitive-data-protection.en.md).

## Scenario 7: when After callbacks fire

**Requirement**: confirm which writes reach the After callbacks so the audit has no gaps.

| Method | After fires when |
| --- | --- |
| `Insert` / `Update` / `Delete` | The operation returns `true` |
| `UpdateOrInsert` | The result is `Inserted` or `Updated` |
| `DeleteID` | The primary-key delete succeeds |
| `DeleteAll` / `UpdateAll` | Affected rows are greater than 0 |
| `BatchDeleteID` | Unconditionally |
| `BatchInsert` / `BatchUpdate` / `BatchDelete` | Once per row |

Two more points:

- A soft delete is an `Update`, so it triggers `OnUpdating` / `OnUpdated` and never `OnDeleted`. Detect soft deletes by watching `IsDeleted` flip from `false` to `true` inside `OnUpdating`.
- Calling the same service's write method inside an event callback recurses into the event. Write supporting data through a DAO or a separate scope.

## Related links

- [Back to index](../README.md)
- [Logging and Diagnostics](../06-di/03-logging.en.md)
- [Transactions](../06-di/01-transactions.en.md)
- [Soft Deletes and Historical Data in Practice](./03-soft-delete-and-archive.en.md)
- [Sensitive Data Protection in Practice](./05-sensitive-data-protection.en.md)
- [Data Permissions in Practice](./02-data-permission.en.md)
