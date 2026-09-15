# Soft Deletes and Historical Data

LiteOrm has no built-in soft delete: no `[SoftDelete]` attribute and no switch that rewrites `Delete` into `Update`. What it offers are two primitives you can build on, and the boundaries are yours to define. This page covers how to build it and where it leaks.

## 1. What the framework provides

| Primitive | Expresses | Where it is injected |
| --- | --- | --- |
| `[Column(Constant = ...)]` → `TableDefinition.ConstFilter` | A fixed table-level filter | Main-table `WHERE`, joined `JOIN ... ON`, and `UPDATE` / `DELETE` |
| A runtime `Expr` condition | A filter that varies by request, role or scenario | Assembled by the caller into the query |

The test is still "can this be decided at compile time". If deleted rows are never visible, `Constant` is enough. A recycle bin, an administrator viewing deleted data, or statistics by state need a runtime condition.

## 2. Read side: two ways to filter

### 2.1 A view model carrying the fixed slice

Attach "not deleted" to a read-only view model so the read path picks up the condition automatically:

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


### 2.2 Runtime conditions

When the filter has to open up by role or scenario, use a runtime condition:

```csharp
using static LiteOrm.Common.Expr;

private Expr BuildCustomerFilter(CustomerQueryRequest request, ICurrentUser user)
{
    var filter = Prop(nameof(Customer.Id)) > 0;

    if (!request.IncludeDeleted && !user.IsAdmin)
        filter &= Prop(nameof(Customer.IsDeleted)) == false;

    return filter;
}
```

The rules match data permissions: all query entry points share one builder, and detail, update and delete still need their own check. Entry points such as the recycle bin skip the "not deleted" condition but must be restricted to administrators or the row owner.

## 3. Write side: how the delete action is expressed

Most delete methods are `virtual`, so they can be overridden into soft deletes:

| Method | `virtual` |
| --- | --- |
| `Delete(T)` / `DeleteAsync(T)` | Yes |
| `DeleteID(object, params string[])` | Yes |
| `DeleteAll(LogicExpr, params string[])` | Yes |
| `BatchDelete` / `BatchDeleteAsync` / `BatchDeleteID` / `BatchDeleteIDAsync` | Yes |
| `DeleteIDAsync(object, string[]?, CancellationToken)` | No |
| `DeleteAllAsync(LogicExpr, string[]?, CancellationToken)` | No |

The last two are direct implementations of the async interface members and cannot be overridden by a subclass. Rather than overriding them one by one and remembering which gaps remain, put the delete semantics in your own service method:

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

That leaves one entry point, which is also a good place for audit and permission checks. Keep hard deletes for operational tooling, running explicitly through `IObjectDAO<T>`.

One detail: a soft delete is an `Update`, so it raises `OnUpdating` / `OnUpdated`. If the audit trail listens for `OnDeleted`, soft-deleted rows never show up as deletions. Either detect in `OnUpdating` that `IsDeleted` went from false to true and record a deletion, or write the audit explicitly in the business method.

## 4. Handling unique constraints

The classic soft-delete failure is a unique index. The row is still in the table, still occupying the business key, so the same code cannot be created again. Three ways out:

| Approach | Unique key | Cost |
| --- | --- | --- |
| Add the delete flag to the key | `UNIQUE (Code, IsDeleted)` | Only one deletion is possible; a second delete collides |
| Add the delete timestamp | `UNIQUE (Code, DeletedTime)` | Null handling in unique constraints varies by database |
| Rewrite the key on delete | `Code = 'C001#deleted#20260914'` | The business key is no longer readable; the original needs separate tracking |

The steadier split is to keep the unique constraint for live rows only and move history into an archive table, carried by sharding or a separate data source (see below).

## 5. Relations and cascades

- After the parent is soft-deleted, child queries that `JOIN` straight on the foreign key still reach deleted rows. Either give the child view model its own `Constant` condition or add `IsDeleted = false` to the join condition.
- Cascade soft deletes belong in one transaction. `ExecuteInTransaction` / `[Transaction]` enlists every data source context in the same `SessionManager`, so parent and child updates commit or roll back together.

## 6. Statistics and indexes

- Statistics, reports and exports going through different entry points produce "the list shows 100 rows, the report says 120" discrepancies. Make "include deleted" an explicit request parameter instead of letting each entry point decide.
- The not-deleted filter uses an index on large tables. A standalone index on a heavily skewed `IsDeleted` column (99% false) buys little; a composite index such as `(IsDeleted, commonlyFilteredColumn)` or the existing index plus the filter is usually better.

## 7. Archiving historical data

| Scale | Approach | Notes |
| --- | --- | --- |
| Up to tens of millions in one table | Add a time column and partition by time | Queries still need a time range, and the index must cover it |
| Time-sliced | `[Table("Orders_{0}")]` + `TableArgs` | Archive tables carry the month in the name, chosen per query via `tableArgs` |
| Hot/cold split | `[Table("Orders", DataSource = "ArchiveDb")]` | Archive lives in its own database, the primary keeps hot data |

Sharding details are in [Sharding and table arguments](../03-advanced-topics/02-sharding-and-tableargs.en.md). Run archiving as a separate batch job that moves rows in primary-key ranges, one transaction per batch, avoiding long transactions and lock waits.

## 8. Common mistakes

| Mistake | Consequence |
| --- | --- |
| Expecting `Delete` to become a soft delete | There is no such switch; the rows are gone |
| Putting `Constant = false` on the entity | Maintenance operations on deleted rows are blocked by your own condition |
| Leaving the unique constraint untouched | The same business key cannot be recreated |
| Overriding `Delete` but missing `DeleteIDAsync` / `DeleteAllAsync` | Some entry points still hard delete |
| Auditing only `OnDeleted` | Soft deletes never reach the audit trail |
| Not excluding deleted rows in relation queries | Deleted parents still appear in child results |

## Related

- [Back to index](../README.md)
- [Permission filtering and user scope](../06-di/02-permission-filtering.en.md)
- [Sharding and table arguments](../03-advanced-topics/02-sharding-and-tableargs.en.md)
- [Transactions](../06-di/01-transactions.en.md)
- [Audit trails and change tracking](./03-audit-trail.en.md)
- [Three levels of multi-tenant isolation](./01-multi-tenancy.en.md)
