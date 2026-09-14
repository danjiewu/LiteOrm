# Three Levels of Multi-Tenant Isolation

The hard part of multi-tenancy is rarely "where to store the tenant id". It is deciding which layer carries the isolation. Whether isolation lives on rows, on table names or on connections decides how filtering, routing and connection selection are written, and which spots end up leaking.

LiteOrm has no "multi-tenant switch". It gives you four primitives:

| Isolation level | Primitives | Granularity |
| --- | --- | --- |
| Rows in a shared table | Runtime `Expr` conditions (or a `GenericSqlExpr` building block) | Row |
| A fixed slice across tables | `[Column(Constant = ...)]` → `TableDefinition.ConstFilter` | Table |
| Separate physical tables | `[Table("Orders_{0}")]` + `TableArgs` (`IArged` / `WithArgs` / `From<T>(tableArgs)`) | Table name |
| Separate databases | `[Table(DataSource = "...")]`, or a DAO overriding `DataSource` | Connection |

The four can be stacked, for example "database per tenant, then monthly tables inside each database". The rest of this page walks through each layer and the details that are easy to miss.

## 1. Shared table: row-level isolation

The tenant id is an ordinary column and the condition is assembled at the query entry point:

```csharp
using static LiteOrm.Common.Expr;

var filter = BuildBusinessFilter(request)
    & (Prop(nameof(Order.IsDeleted)) == false)
    & (Prop(nameof(Order.TenantId)) == currentTenantId);

var orders = await orderService.SearchAsync(
    From<OrderView>()
        .Where(filter)
        .OrderBy(Prop(nameof(Order.CreateTime)).Desc())
        .Section(0, 20)
);
```

Notes:

- The tenant condition is part of the query, not a post-filter over results. Filtering in memory after the query breaks `Count`, paging totals, aggregations and exports, and the data has already been read into the process.
- Lists, counts, exports, bulk updates and bulk deletes should all go through the same condition-building function. Otherwise one entry point will eventually be missed.
- Single-object detail, update and delete still need an explicit ownership check. Row filtering only covers what a query returns, not what a caller can address by primary key.

To stop passing the tenant around as a parameter, promote the condition to a reusable building block:

```csharp
using static LiteOrm.Common.Expr;

GenericSqlExpr.Register("TenantFilter", (context, sqlBuilder, outputParams, _) =>
{
    var tenantId = TenantContext.Current?.TenantId
        ?? throw new InvalidOperationException("Tenant not resolved.");

    string paramName = outputParams.Count.ToString();
    outputParams.Add(new Param(sqlBuilder.ToParamName(paramName), tenantId));
    return $"{sqlBuilder.ToSqlName(nameof(Order.TenantId))} = {sqlBuilder.ToSqlParam(paramName)}";
});

var filter = BuildBusinessFilter(request) & Expr.Sql("TenantFilter");
```

The value comes from the context instead of a formal parameter, and it is still parameterized rather than concatenated into the SQL text. For the safety boundary of `GenericSqlExpr`, see [Security](../03-advanced-topics/08-security.md).

## 2. Fixed slice: `ConstFilter` for "this table only ever holds one kind of tenant"

When a model serves exactly one fixed slice, such as platform-owned data, an archive tenant or a legacy-compatibility model, push the rule into metadata:

```csharp
public enum TenantKind
{
    Platform = 1,
    Merchant = 2
}

[Table("Orders")]
public class PlatformOrder : ObjectBase
{
    [Column("Id", IsPrimaryKey = true)]
    public long Id { get; set; }

    [Column("TenantKind", Constant = TenantKind.Platform)]
    public TenantKind TenantKind => TenantKind.Platform;
}
```

`[Column(Constant = ...)]` is aggregated into `TableDefinition.ConstFilter` during the metadata stage. When SQL is generated, the main-table condition goes into `WHERE`, joined-table conditions go into `JOIN ... ON`, and `UPDATE` / `DELETE` statements carry it as well.

This expresses a rule that is fixed at compile time. A tenant coming from a token, a request header or a session is not that kind of rule. Writing a runtime value into `ConstFilter` produces one shared condition for every tenant, which in a multi-tenant system is a severe defect.

## 3. Physical shards: the tenant decides the table name

Put a placeholder in the table name and supply the tenant code as `TableArgs`:

```csharp
[Table("Orders_{0}")]
public class TenantOrder : ObjectBase, IArged
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }

    string[]? IArged.TableArgs => new[] { TenantContext.Current?.TenantId ?? throw new InvalidOperationException("Tenant not resolved.") };
}
```

On the write path, `EntityService` detects `IArged` and carries `TableArgs` automatically. The physical name is `string.Format("Orders_{0}", "tenant_a")`, that is `Orders_tenant_a`. Bulk writes group by `TableArgs` first and then execute once per tenant. Calling `ObjectDAO<T>` directly does none of this; you would pass `WithArgs` yourself, which is another reason to route sharded writes through the service layer.

Reads can be written three ways, ordered from narrowest to widest scope:

```csharp
// 1) Explicit on a single statement
var orders = await orderService.SearchAsync(
    From<TenantOrder>("tenant_a").Where(Prop(nameof(TenantOrder.Amount)) > 100m)
);

// 2) On the DAO, reused by a batch of operations
var dao = viewDAO.WithArgs("tenant_a");
var count = dao.Count(Expr.Prop(nameof(TenantOrder.Amount)) > 100m);

// 3) Override CreateSqlBuildContext so every Expr / ExprString / DAO query inherits it
public class TenantOrderViewDAO : ObjectViewDAO<TenantOrder>
{
    public TenantOrderViewDAO(SessionManager sessionManager) : base(sessionManager) { }

    public override SqlBuildContext CreateSqlBuildContext(bool initTable = false)
    {
        var context = base.CreateSqlBuildContext(initTable);
        context.TableArgs = new[] { TenantContext.Current?.TenantId ?? throw new InvalidOperationException("Tenant not resolved.") };
        return context;
    }
}
```

The third option turns a missing tenant into an exception instead of a silent fallback to an unformatted table name or another tenant's table.

### Watch out for the override behaviour

A `TableExpr` carrying its own `TableArgs` overrides whatever the context inherited:

```csharp
var context = dao.CreateSqlBuildContext();
context.TableArgs = new[] { "tenant_a" };

// This statement targets tenant_b, not tenant_a
From<TenantOrder>("tenant_b");
```

Inside multi-tenant code, hand-writing table arguments on a `TableExpr` can carry a query out of its tenant boundary. Treat that as a review blocker, or ban it with an analyzer rule.

Every `TableArgs` entry is also validated as a SQL identifier (`Expr.ThrowIfInvalidSqlName`). A tenant code containing quotes, semicolons or spaces fails at assignment time, not when the SQL is built.

## 4. Database per tenant

There are two entry points for choosing a connection:

```csharp
// Fixed per entity
[Table("Orders", DataSource = "OrderDbEast")]
public class EastOrder : ObjectBase { /* ... */ }
```

```csharp
// Chosen at runtime from the tenant: a DAO overriding DataSource
public class TenantOrderDAO : ObjectDAO<TenantOrder>
{
    public TenantOrderDAO(SessionManager sessionManager) : base(sessionManager) { }

    protected override string? DataSource => TenantContext.Current?.DataSourceName;
}
```

With the second option, register the subclass where the framework type is expected, otherwise the service layer still resolves the default `ObjectDAO<T>`:

```csharp
services.AddScoped<ObjectDAO<TenantOrder>, TenantOrderDAO>();
services.AddScoped<ObjectViewDAO<TenantOrder>, TenantOrderViewDAO>();
```

Data sources themselves are registered at startup (`AddDataSource` / `RegisterLiteOrm` reading configuration). At runtime you only choose among them.

> The `[DataSource]` attribute is currently metadata only; the framework does not use it to pick a connection. To pick a connection, use `[Table(DataSource = ...)]` or override `DAOBase.DataSource`.

## 5. Carrying the tenant context

Prefer a scoped context object over threading the tenant through every call:

```csharp
public interface ITenantContext
{
    string TenantId { get; }
    string? DataSourceName { get; }
}

public sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpTenantContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public string TenantId => _accessor.HttpContext?.Request.Headers["X-Tenant"].ToString()
        ?? throw new InvalidOperationException("Tenant header is missing.");
}
```

Constraints worth respecting:

- The context must be scoped or request-bound. A singleton makes every tenant read the same value.
- If `IArged.TableArgs` depends on a static context, make sure that context is backed by `AsyncLocal`, otherwise concurrent requests mix tenants. Freezing the tenant into the entity at construction time is steadier.
- Cache keys must include the tenant. A key such as `"order:123"` collides across tenants in shared-table mode.

## 6. Transactions and multi-tenancy

All data source contexts inside one `SessionManager` are enlisted by `BeginTransaction`; read-only connections are skipped. Tenant isolation usually means one request touches one tenant, so the normal path is fine. A background task that writes across tenants inside a single transaction needs to be split into separate scopes, otherwise one tenant's failure rolls back another tenant's writes.

## 7. Common mistakes

| Mistake | Consequence | Correct approach |
| --- | --- | --- |
| Putting the current tenant into `ConstFilter` | One shared condition for all tenants | Runtime conditions via `Expr` / `GenericSqlExpr` |
| Treating row filtering as authorization | Any primary key can reach another tenant's rows | Add an ownership check on single-object operations |
| Hand-writing table arguments on a `TableExpr` | The query leaves its tenant table | Inject `TableArgs` from one context |
| Caching tenant metadata in a singleton | Cross-tenant leakage | Include the tenant in cache keys |
| Forgetting to register the custom DAO | Traffic still goes to the default connection | Register the subclass as `ObjectDAO<T>` / `ObjectViewDAO<T>` |

## Related

- [Back to index](../README.md)
- [Permission filtering and user scope](../06-di/02-permission-filtering.en.md)
- [Sharding and table arguments](../03-advanced-topics/02-sharding-and-tableargs.en.md)
- [Data permissions and authorization gaps](./02-data-permission.en.md)
- [Audit trails and change tracking](./03-audit-trail.en.md)
- [Concurrency and read paths](./06-concurrency-and-read-path.en.md)
