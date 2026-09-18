# Soft Deletes and Historical Data

LiteOrm has no built-in soft delete: there is no `[SoftDelete]` attribute and no switch that rewrites `Delete` into an `Update`. The only primitives available are two, differing in whether the rule is known at compile time:

| Primitive | Capability | Injected into |
| --- | --- | --- |
| `TableDefinition.ConstFilter` aggregated from `[Column(Constant = ...)]` | Fixed table-level filter | Main table `WHERE`, `JOIN ... ON` of association queries, `UPDATE` / `DELETE` `WHERE` |
| Runtime `Expr` condition | Filter that varies by request, role or screen | Assembled by the caller into the query |

## Scenario 1: lists hide soft-deleted rows by default

Hang "not deleted" on a read-only view model so read paths carry it automatically:

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

- `Constant` is a compile-time value; the read-only property means "rows visible here are never deleted", and a boolean slice is inlined as a literal rather than a parameter.
- Writes must go through the real entity without `Constant`; otherwise even the update that sets `IsDeleted` to `true` is blocked by the condition.
- A slice on a view model also applies to counts and exports, provided those entry points use the same view type.
- Under `TableInfo` source generation (NativeAOT) the attribute slice does not produce a `ConstFilter`; assign `TableDefinition.ConstFilter` at runtime instead, as shown in example 3 of [Tenant Isolation](./tenant-isolation.en.md).

## Scenario 2: recycle bin and admin views of deleted data

Make the switch an explicit field on the request and decide in the condition-assembling function:

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

- The recycle-bin entry point omits that condition but must be limited to administrators or the data owner; otherwise deleted rows are exposed to everyone.
- Put `IncludeDeleted` on the request so lists, counts, reports and exports read the same value, avoiding a "list says 100, count says 120" mismatch.
- Detail, update and delete still need their own checks; see scenario 3 of [Data Permissions](./data-permission.en.md).

## Scenario 3: turn the delete action into a flag update

Express soft delete as one service method while recording who deleted the row and why:

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

All delete entry points are `virtual`, but overriding them all into soft deletes is not wise:

| Method | `virtual`? |
| --- | --- |
| `Delete(T)` / `DeleteAsync(T)` | Yes |
| `DeleteID(object, params string[])` | Yes |
| `DeleteAll(LogicExpr, params string[])` | Yes |
| `BatchDelete` / `BatchDeleteAsync` / `BatchDeleteID` / `BatchDeleteIDAsync` | Yes |
| `DeleteIDAsync(object, string[]?, CancellationToken)` | Yes |
| `DeleteAllAsync(LogicExpr, string[]?, CancellationToken)` | Yes |

- Soft delete also records who deleted and why; overriding these entry points one by one invites gaps and splits the semantics. Better to converge soft delete into one custom service method (like `SoftDeleteAsync` above) with a single entry point and clear responsibility.
- Keep hard deletes for operations tooling, run explicitly through `IObjectDAO<T>`, and never mix them into the business delete entry point.
- A soft delete is an `Update`, so it triggers `OnUpdating` / `OnUpdated` and never `OnDeleted`. To distinguish a delete in audit, detect `IsDeleted` flipping from `false` to `true` inside `OnUpdating`, or write the audit entry explicitly in the business method. See [Audit and Change Tracking](./audit-and-change-tracking.en.md).

## Scenario 4: the same business key can be created again after a soft delete

The row is still in the table and holds the unique key, so recreating with the same code fails. Three options and their costs:

| Option | Unique key | Cost |
| --- | --- | --- |
| Add the delete flag | `UNIQUE (Code, IsDeleted)` | Deletable only once; the second delete collides with the first |
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

- A more robust route is splitting: keep the unique constraint for live rows only and move history into an archive table, removing the conflict entirely.
- Archive tables are hosted by sharding or a separate data source; see scenario 6.
- A migration script must clean up existing duplicates first, otherwise the constraint cannot be created.

## Scenario 5: associations and cascading soft deletes

After a parent is soft deleted, child queries must not join the deleted parent, and deleting a parent marks the children. Give the child view its own slice and put the cascade in one transaction:

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

- Joining directly on the foreign key still sees the deleted row, so the child view needs its own slice, or the join condition needs `IsDeleted = false`.
- A cascade must complete inside one transaction. `[Transaction]` / `ExecuteInTransaction` bring every data source context in the same `SessionManager` into one transaction, so parent and child updates either both succeed or both roll back.
- A joined table's slice is prepared with its alias when the view is built and applied to the `JOIN ... ON` automatically. DAO key-based reads (`GetObject`, `ExistsKey`) use the model's own `From` fragment, which carries no such condition.

## Scenario 6: archiving historical data

Pick by scale:

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

- Run archiving as a standalone batch job that moves data in primary-key ranges, one transaction per batch, to avoid long transactions and lock waits.
- Sharding details and `TableArgs` propagation rules are in [Sharding and TableArgs](../advanced-topics/sharding-and-tableargs.en.md).
- Whether deleted rows move with the archive is a retention decision. Deleted rows left on the master keep participating in filtering, and the highly skewed `IsDeleted` column gains little from a dedicated index; a composite index such as `(IsDeleted, frequently-filtered column)` works better.

## Related links

- [Back to index](../README.md)
- [Data Permissions](./data-permission.en.md)
- [Audit and Change Tracking](./audit-and-change-tracking.en.md)
- [Tenant Isolation](./tenant-isolation.en.md)
- [Sharding and TableArgs](../advanced-topics/sharding-and-tableargs.en.md)
- [Transactions](../di/transactions.en.md)