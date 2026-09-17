# Tenant Isolation

Multi-tenant work usually comes down to three questions: which layer holds the tenant condition, where the tenant value comes from, and what a single missed entry point costs.

LiteOrm ships no built-in tenant mechanism; what it gives you is a set of primitives, and the tenant story has to be assembled from them. This article defines a tenant interface, then walks four examples covering four isolation layers: row filtering, physical sharding, a fixed slice, and physical databases.

| Isolation layer | Primitive | Granularity | Example |
| --- | --- | --- | --- |
| One shared table, row isolation | Runtime `Expr` condition | Row | Example 1 |
| Separate physical table | `[Table("Orders_{0}")]` + `TableArgs` | Table name | Example 2 |
| One shared table, fixed slice | `TableDefinition.ConstFilter` | Row | Example 3 |
| Separate physical database | Overriding the DAO's `DataSource` | Connection | Example 4 |

The layers combine. A common layout is "one database per tenant, monthly tables inside it".

## The tenant interface

The tenant comes from a header, token or session, and one process serves several tenants at once. Start by funnelling "who is the current tenant" through one interface:

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

    public string? DataSourceName => _accessor.HttpContext?.Request.Headers["X-Tenant-Db"].ToString();
}

builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
```

Entities that need scoping implement a marker interface. The interface declares only the tenant column; the implementing type supplies the current value with an expression-bodied property:

```csharp
public interface ITenantEntity
{
    string TenantId { get; }
}

[Table("Orders")]
public class Order : ObjectBase, ITenantEntity
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("TenantId")]
    public string TenantId => TenantContext.Current?.TenantId
        ?? throw new InvalidOperationException("Tenant not resolved.");

    [Column("Amount")]
    public decimal Amount { get; set; }
}
```

The entity now knows both the tenant column's name and where its value comes from, so every registration and slice below refers to that one name.

Notes:

- The static accessor behind the tenant context (`TenantContext.Current` above) must be `AsyncLocal` or request-scoped. A singleton makes every tenant read the same value.
- Write the tenant column as a read-only expression-bodied property. It still participates in read mapping, and on write the value comes from the database or the entity property, so business code never assigns it by hand.
- Cache keys must include the tenant. A key such as `"order:123"` hits across tenants in a shared-table setup.

## Example 1: build `Expr` from the current tenant

**Requirement**: the `Orders` table is shared by all tenants and the tenant id is an ordinary column. Lists, counts, exports and bulk updates must never surface another tenant's rows.

**Approach**: treat the tenant condition as part of the query itself, assembled by one function that every entry point calls:

```csharp
using static LiteOrm.Common.Expr;

public static class OrderFilters
{
    public static LogicExpr For(OrderQueryRequest request)
    {
        var filter = (Prop(nameof(ITenantEntity.TenantId)) == TenantContext.Current?.TenantId)
            & (Prop(nameof(Order.IsDeleted)) == false);

        if (!string.IsNullOrEmpty(request.Keyword))
            filter &= Prop(nameof(Order.Title)).Like($"%{request.Keyword}%");

        return filter;
    }
}
```

Listing and counting call the same function, only the outermost statement differs:

```csharp
var page = await orderViewService.SearchAsync(
    From<OrderView>().Where(OrderFilters.For(request)).OrderBy(Prop(nameof(Order.CreateTime)).Desc()).Section(0, 20));

var total = await orderViewService.CountAsync(OrderFilters.For(request));
```

**Strength**: it is direct. The condition sits inside the query, so anyone reading the code sees which tenant the statement is scoped to; logging, breakpoints and unit tests need no detour. `Prop(...)` emits an alias-qualified column reference, so joining another table that also has a `TenantId` column is unambiguous. Expression queries, conditional update and conditional delete all use the same function.

**Weakness**: it can only constrain the current table, meaning the one table you wrote into that specific statement. Three concrete consequences:

- Another table in an association query is unconstrained. When an order brings along a department that is itself tenant-scoped, `OrderFilters.For(...)` does not reach the joined department rows.
- `EXISTS` subqueries and the `WHERE` of `UPDATE` / `DELETE` must all carry it by hand, and each entry point you miss is a tenant you leak. Bulk update and bulk delete need the same scope condition; see scenario 2 of [Data Permissions](./data-permission.en.md).
- Primary-key paths need their own object-level check. The condition decides what a query returns, not what a primary-key operation touches, so detail, update and delete still need an ownership check.

## Example 2: shard by tenant with `TableArgs`

**Requirement**: one physical table per tenant, with the name derived from the tenant code.

**Approach**: put a placeholder in the table name and implement `IArged` so writes carry the argument automatically:

```csharp
[Table("Orders_{0}")]
public class TenantOrder : ObjectBase, IArged, ITenantEntity
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }

    [Column("TenantId")]
    public string TenantId => TenantContext.Current?.TenantId
        ?? throw new InvalidOperationException("Tenant not resolved.");

    string[] IArged.TableArgs => new[] { TenantId };
}
```

Queries can specify the argument in three ways, from narrowest to widest reuse:

```csharp
// 1) Single statement
var orders = await orderViewService.SearchAsync(From<TenantOrder>("tenant_a"));

// 2) Carried on a DAO and reused by a batch of operations
var dao = viewDAO.WithArgs("tenant_a");
var count = dao.Count(Prop(nameof(TenantOrder.Amount)) > 100m);
```

```csharp
// 3) Override CreateSqlBuildContext so every query on this DAO inherits it
public sealed class TenantOrderViewDAO : ObjectViewDAO<TenantOrder>
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

**Strength**: the tenant dimension lands in the real table name instead of the `WHERE` clause, so a single-table statement looks exactly like it does in a single-tenant deployment. Cleanup, archiving and migration are table-level operations.

**Weakness**:

- On the write path, `EntityService<T>` detects `IArged` and adds the argument automatically; batch writes group by `TableArgs` and run group by group. Writing directly through `IObjectDAO<T>` does not do this, so sharded writes should go through the service layer.
- A `TableExpr` carrying its own `TableArgs` overrides what the context provided. With tenant context A, `From<TenantOrder>("tenant_b")` still reads table B. Statements like this inside multi-tenant code should be rejected in review.
- Every argument is validated as a SQL name, so a tenant code containing quotes, semicolons or spaces throws at assignment time rather than while building SQL.
- It solves table routing, not row authorization. When the same table carries another tenant dimension (organization, region), example 1 or 3 is still needed.

## Example 3: set the tenant condition as `ConstFilter`, with `GenericSqlExpr` reading the context directly

**Requirement**: dozens of query entry points need the tenant condition, and threading it through parameters loses it one layer down. The condition should be written once and picked up by every query, every association and every write path.

**Approach**: set the tenant condition as the fixed filter on the entity's table definition (`TableDefinition.ConstFilter`), with the fragment reading `TenantContext.Current` directly.

`ConstFilter` is normally aggregated from `[Column(Constant = ...)]` and holds compile-time constants. This route is a different one: assign `TableDefinition` directly, with a `GenericSqlExpr` supplying the value. Both routes land on the same property and behave identically at runtime.

Register the fragment first; the delegate reads the current tenant:

```csharp
using LiteOrm.Common;
using static LiteOrm.Common.Expr;

GenericSqlExpr.Register("TenantFilter", (context, sqlBuilder, outputParams, _) =>
{
    string tenant = TenantContext.Current?.TenantId
        ?? throw new InvalidOperationException("Tenant not resolved.");

    string paramName = outputParams.Count.ToString();
    outputParams.Add(new Param(sqlBuilder.ToParamName(paramName), tenant));
    return $"{sqlBuilder.ToSqlName(nameof(ITenantEntity.TenantId))} = {sqlBuilder.ToSqlParam(paramName)}";
});
```

Then attach it to every entity implementing the tenant interface. The key line is this one: `TableDefinition.ConstFilter` is a public read-write `get; set;` property, so getting the table definition and assigning it is all it takes. No custom metadata provider is required.

```csharp
public static class TenantEntitySetup
{
    public static void EnableTenantFilter(IEnumerable<Assembly> assemblies)
    {
        foreach (var type in assemblies.SelectMany(a => a.GetTypes())
                                       .Where(t => !t.IsAbstract && typeof(ITenantEntity).IsAssignableFrom(t)))
        {
            var tableDefinition = TableInfoProvider.Instance.GetTableDefinition(type);
            if (tableDefinition is null) continue;

            tableDefinition.ConstFilter = Expr.Sql("TenantFilter");
        }
    }
}
```

Call it once at startup; every later operation carries the tenant condition:

```csharp
TenantEntitySetup.EnableTenantFilter(AppDomain.CurrentDomain.GetAssemblies());

var orders = await orderViewService.Search(From<OrderView>());

// SELECT *
// FROM "Orders" "T0"
// WHERE "TenantId" = @0                            -- @0 = tenant_a
```

The value is resolved at SQL-generation time, so every query re-reads `TenantContext.Current` and a tenant switch inside the same process takes effect immediately.

**Where it applies.** This is the value it has over example 1: the condition enters the framework's own SQL generation instead of depending on the caller remembering to write it.

- The main table's `WHERE`, including expression queries such as `Search` / `Count` / `Exists`, and key-based read paths such as `GetObject` / `ExistsKey`.
- The `JOIN ... ON` of association queries. When an order view with a tenant slice joins departments, each table carries its own condition.
- The `EXISTS` subqueries produced by `ForeignExpr` / `Exists` / `ExistsRelated`; the target table's own `ConstFilter` is merged in first.
- The `WHERE` of `UPDATE` / `DELETE`, including key paths and bulk update and delete.

**Cost and limits**:

- A table with a fixed filter does not reuse the prepared-command cache: it rebuilds its SQL and command on every operation, at the cost of one extra SQL composition per call. Tables without a fixed filter keep using the cache.
- The fragment can only write bare column names. `sqlBuilder.ToSqlName` produces an unqualified `"TenantId"`, whereas framework-generated conditions are qualified (`"T0"."TenantId"`). That is fine for single-table statements, but as soon as the statement joins another table with a `TenantId` column the name can be rejected as ambiguous, so pair it with example 1 and write association conditions with `Prop(...)`.
- The fragment must be registered before SQL generation happens. Registering the same key twice does not overwrite, so a test that needs a different implementation must use a different key.
- The delegate is referenced from the metadata setup code, and once `TableInfoProvider` has cached a `TableDefinition` every later lookup returns the same object; an already-cached `TableView` is not rebuilt when the definition changes, so the assignment must precede the first query.
- Strictly speaking this is "supply a fixed filter's value at runtime through `GenericSqlExpr`", not "write the current tenant in as a compile-time constant". The reverse remains off limits: `[Column(Constant = "...")]` with one concrete tenant makes every tenant share that one condition.

## Example 4: shard by tenant with the DAO's `DataSource`

**Requirement**: tenant data lives in different databases and the connection follows the tenant.

**Approach**: pin an entity to one connection with `[Table(DataSource = ...)]`, or override the DAO's `DataSource` to choose per request:

```csharp
public sealed class TenantOrderDAO : ObjectDAO<TenantOrder>
{
    public TenantOrderDAO(SessionManager sessionManager) : base(sessionManager) { }

    protected override string? DataSource => TenantContext.Current?.DataSourceName;
}
```

```csharp
public sealed class TenantOrderViewDAO : ObjectViewDAO<TenantOrder>
{
    public TenantOrderViewDAO(SessionManager sessionManager) : base(sessionManager) { }

    protected override string? DataSource => TenantContext.Current?.DataSourceName;

    public override SqlBuildContext CreateSqlBuildContext(bool initTable = false)
    {
        var context = base.CreateSqlBuildContext(initTable);
        context.TableArgs = new[] { TenantContext.Current?.TenantId ?? throw new InvalidOperationException("Tenant not resolved.") };
        return context;
    }
}

services.AddScoped<ObjectDAO<TenantOrder>, TenantOrderDAO>();
services.AddScoped<ObjectViewDAO<TenantOrder>, TenantOrderViewDAO>();
```

`DataSource` is a `protected virtual` property on `DAOBase` that returns the data source name from the table definition by default. Both the write DAO and the view DAO must override it; reads and writes each go through their own.

**Strength**: the strongest isolation, since tenants share no physical resources, so backup, restore, migration and quotas can be handled per database. Connection selection and `TableArgs` compose without interfering: one picks the database, the other the table.

**Weakness**:

- Register the subclass in place of the base type, otherwise the service layer resolves the framework default `ObjectDAO<T>` and the connection never changes. The registration must cover every DAO type in use: `ObjectDAO<T>`, `ObjectViewDAO<T>`, `DataDAO<T>` and `DataViewDAO<T>`, with view DAOs on `DataViewDAO<T>` the easiest to miss.
- Data sources are registered at startup (`AddDataSource`, or `RegisterLiteOrm` reading configuration); runtime only selects among them. When tenant connection strings come from a database or a config centre at runtime, line the registration up with tenant loading.
- A batch that spans tenants lands on different connections with no cross-database transaction available, so split it into separate scopes. All data source contexts inside one `SessionManager` join the same transaction when `BeginTransaction` runs, read-only connections are skipped, so one tenant's failure rolls back another tenant's writes.
- Example 3's `ConstFilter` still applies: sharding by database does not remove the need for row filtering, because one database may still hold another tenant dimension.

## Related links

- [Back to index](../README.md)
- [Permission Filtering and User Scopes](../di/permission-filtering.en.md)
- [Sharding and TableArgs](../advanced-topics/sharding-and-tableargs.en.md)
- [Data Permissions](./data-permission.en.md)
- [Audit and Change Tracking](./audit-and-change-tracking.en.md)
- [Concurrency and Read/Write Splitting](./concurrency-and-read-write-splitting.en.md)
