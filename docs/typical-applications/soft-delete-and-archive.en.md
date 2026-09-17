# Soft Deletes and Historical Data

LiteOrm has no built-in soft delete: there is no `[SoftDelete]` attribute and no switch that rewrites `Delete` into an `Update`. The two primitives available are fixed slices (`[Column(Constant = ...)]`) and runtime `Expr` conditions, and the criterion is still whether the rule is known at compile time:

| Primitive | Expresses | Injected into |
| --- | --- | --- |
| `TableDefinition.ConstFilter` aggregated from `[Column(Constant = ...)]` | A fixed table-level filter | Main table `WHERE`, `JOIN ... ON` of association queries, `UPDATE` / `DELETE` `WHERE` |
| Runtime `Expr` condition | A filter that varies by request, role or screen | Assembled by the caller into the query |

## Scenario 1: lists hide soft-deleted rows by default

**Requirement**: deleting a customer only sets a flag, every query hides deleted rows by default, and no query should have to write that condition.

**Approach**: hang "not deleted" on a read-only view model so read paths carry it automatically:

```csharp
[Table("Customers")]
public class CustomerView : ObjectBase
{
    [Column("Id", IsPrimaryKey = true)]
    public long Id { get; set; }

    [Column("Name")]
    public string? Name { get; set; }

    [Column("IsDeleted", Constant = false)]
    public bool IsDeleted => false;
}
```

```sql
SELECT * FROM "Customers" "T0" WHERE "T0"."IsDeleted" = 0
```

Notes:

- The `Constant` value is a compile-time constant and the property is read-only (`=> false`), so the model means "rows visible here are always undeleted". A boolean slice is inlined as a literal rather than a parameter.
- Writes must go through the real entity (without `Constant`). Otherwise even the update that sets `IsDeleted` to `true` is blocked by the condition, and no maintenance operation can touch a deleted row.
- A slice declared on a view model also applies to counts and exports, provided those entry points use the same view type.
- Under `TableInfo` source generation (NativeAOT) the attribute slice does not produce a `ConstFilter`; assign `TableDefinition.ConstFilter` at runtime instead, as shown in example 3 of [Tenant Isolation](./tenant-isolation.en.md).

## Scenario 2: recycle bin and admin views of deleted data

**Requirement**: administrators can see deleted rows, ordinary users cannot, and the deletion time and reason are visible.

**Approach**: make the switch an explicit field on the query request and decide in the condition-assembling function:

```csharp
using static LiteOrm.Common.Expr;

private LogicExpr BuildCustomerFilter(CustomerQueryRequest request, ICurrentUser user)
{
    var filter = Prop(nameof(Customer.Id)) > 0;

    if (!request.IncludeDeleted && !user.IsAdmin)
        filter &= Prop(nameof(Customer.IsDeleted)) == false;

    return filter;
}
```

Notes:

- The recycle-bin entry point omits the undeleted condition but must be limited to administrators or the data owner, otherwise deleted rows are exposed to everyone.
- If counts, reports and exports each decide on their own whether deleted rows are included, the list says 100 and the count says 120. Put `IncludeDeleted` on the request and have every entry point read the same value.
- Detail, update and delete still need their own checks; see scenario 3 of [Data Permissions](./data-permission.en.md).

## Scenario 3: turn the delete action into a flag update

**Requirement**: deletes must leave a trace, including who deleted the row and why.

**Approach**: express soft delete as one service method so there is a single entry point:

```csharp
public interface ICustomerService : IEntityService<Customer>
{
    Task<bool> SoftDeleteAsync(long id, string reason, CancellationToken cancellationToken = default);
}

public sealed class CustomerService : EntityService<Customer>, ICustomerService
{
    public CustomerService(IServiceProvider serviceProvider) : base(serviceProvider) { }

    public async Task<bool> SoftDeleteAsync(long id, string reason, CancellationToken cancellationToken = default)
    {
        var customer = await GetObjectAsync(id, cancellationToken: cancellationToken);
        if (customer is null || customer.IsDeleted) return false;

        customer.IsDeleted = true;
        customer.DeletedTime = DateTime.Now;
        customer.DeletedReason = reason;

        return await UpdateAsync(customer, cancellationToken);
    }
}
```

Most delete methods are `virtual` and could be overridden into soft deletes:

| Method | `virtual`? |
| --- | --- |
| `Delete(T)` / `DeleteAsync(T)` | Yes |
| `DeleteID(object, params string[])` | Yes |
| `DeleteAll(LogicExpr, params string[])` | Yes |
| `BatchDelete` / `BatchDeleteAsync` / `BatchDeleteID` / `BatchDeleteIDAsync` | Yes |
| `DeleteIDAsync(object, string[]?, CancellationToken)` | No |
| `DeleteAllAsync(LogicExpr, string[]?, CancellationToken)` | No |

Notes:

- The last two asynchronous entry points are direct interface implementations that a subclass cannot override. Rather than overriding what you can and remembering what you cannot, put the delete semantics in your own service method.
- Keep hard deletes for operations tooling and run them explicitly through `IObjectDAO<T>`; never mix them into the business delete entry point.
- A soft delete is an `Update`, so it triggers `OnUpdating` / `OnUpdated` and never `OnDeleted`. If auditing relies on the delete event, detect `IsDeleted` flipping from `false` to `true` inside `OnUpdating`, or write the audit entry explicitly in the business method. See [Audit and Change Tracking](./audit-and-change-tracking.en.md).

## Scenario 4: the same business key can be created again after a soft delete

**Requirement**: the row is still in the table and still holds the unique key, so recreating a customer with the same code fails.

**Approach**: three options and their costs:

| Option | Unique key | Cost |
| --- | --- | --- |
| Add the delete flag | `UNIQUE (Code, IsDeleted)` | Only deletable once; the second delete collides with the first |
| Add the delete timestamp | `UNIQUE (Code, DeletedTime)` | NULL handling in unique constraints varies by database |
| Rewrite the business key on delete | `Code = 'C001#deleted#20260914'` | The key stops being readable; the original value needs separate capture |

```csharp
[Table("Customers")]
public class Customer : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("Code", IsUnique = true)]
    public string Code { get; set; } = string.Empty;

    [Column("IsDeleted")]
    public bool IsDeleted { get; set; }
}
```

Notes:

- The more robust route is splitting: keep the unique constraint for live rows only and move history into an archive table, which removes the conflict entirely.
- Archive tables are hosted by sharding or a separate data source; see scenario 6.
- Whichever option is chosen, a migration script must clean up existing duplicates first, otherwise the constraint cannot be created.

## Scenario 5: associations and cascading soft deletes

**Requirement**: after a parent row is soft deleted, child queries must not join the deleted parent, and deleting a parent marks the children.

**Approach**: give the child view its own slice and put the cascade inside one transaction:

```csharp
[Table("Orders")]
public class OrderView : ObjectBase
{
    [Column("Id", IsPrimaryKey = true)]
    public long Id { get; set; }

    [Column("CustomerId")]
    [ForeignType(typeof(Customer), Alias = "Customer")]
    public long CustomerId { get; set; }

    [Column("IsDeleted", Constant = false)]
    public bool IsDeleted => false;
}
```

```csharp
[Transaction]
public async Task<bool> DeleteCustomerAsync(long customerId, string reason, CancellationToken cancellationToken = default)
{
    var affected = await customerService.UpdateAllAsync(
        Expr.Update<Customer>(c => new Customer { IsDeleted = true, DeletedReason = reason }, c => c.Id == customerId),
        null, cancellationToken);

    await orderService.UpdateAllAsync(
        Expr.Update<Order>(o => new Order { IsDeleted = true }, o => o.CustomerId == customerId),
        null, cancellationToken);

    return affected > 0;
}
```

Notes:

- After the parent is soft deleted, a child that joins directly on the foreign key still sees the deleted row. The child view needs its own slice, or the join condition needs `IsDeleted = false`.
- A cascading soft delete must complete inside one transaction. `[Transaction]` and `ExecuteInTransaction` bring every data source context inside the same `SessionManager` into one transaction, so parent and child updates either both succeed or both roll back.
- A joined table's slice is prepared with its alias when the view is built and is applied to the `JOIN ... ON` of association queries, so no extra condition is needed at query time. The DAO key-based reads (`GetObject`, `ExistsKey`) use the model's own `From` fragment, which does not carry that condition.

## Scenario 6: archiving historical data

**Requirement**: the table keeps growing, history must move out, and queries must still reach it by range.

**Approach**: pick by scale:

| Scale | Approach | Notes |
| --- | --- | --- |
| Up to tens of millions per table | Add a time column, partition by time | Queries still carry a time range and indexes must cover the time column |
| Split by time | `[Table("Orders_{0}")]` + `TableArgs` | Archive table names carry the month; queries pass `tableArgs` |
| Hot/cold split | `[Table("Orders", DataSource = "ArchiveDb")]` | Archive lives in its own database, master keeps hot data only |

```csharp
[Table("Orders_{0}")]
public class OrderArchive : ObjectBase, IArged
{
    [Column("Id", IsPrimaryKey = true)]
    public long Id { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    string[] IArged.TableArgs => new[] { CreateTime.ToString("yyyyMM") };
}

// Read one month of archive data
var archived = await archiveService.SearchAsync(From<OrderArchive>("202609"));
```

Notes:

- Run archiving as a standalone batch job that moves data in primary-key ranges, one transaction per batch, to avoid long transactions and lock waits.
- Sharding details and `TableArgs` propagation rules are in [Sharding and TableArgs](../advanced-topics/sharding-and-tableargs.en.md).
- Whether deleted rows move with the archive is a retention decision. Deleted rows left on the master keep participating in filtering, and a highly skewed `IsDeleted` column gains little from a dedicated index; a composite index such as `(IsDeleted, frequently-filtered column)` works better.

## Related links

- [Back to index](../README.md)
- [Data Permissions](./data-permission.en.md)
- [Audit and Change Tracking](./audit-and-change-tracking.en.md)
- [Tenant Isolation](./tenant-isolation.en.md)
- [Sharding and TableArgs](../advanced-topics/sharding-and-tableargs.en.md)
- [Transactions](../di/transactions.en.md)
