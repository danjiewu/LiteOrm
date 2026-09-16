# Tenant Isolation in Practice

Multi-tenant work usually comes down to three questions: which layer holds the tenant condition, where the tenant value comes from, and what a single missed entry point costs. Below are the common requirements broken into seven scenarios, each with complete code.

Start with the four isolation layers and the primitives each one uses:

| Isolation layer | Primitive | Granularity |
| --- | --- | --- |
| One shared table, row isolation | Runtime `Expr` conditions, or a registered `GenericSqlExpr` fragment | Row |
| Same schema, fixed slice | `TableDefinition.ConstFilter` aggregated from `[Column(Constant = ...)]` (compile-time constants only) | Table |
| Separate physical table | `[Table("Orders_{0}")]` + `TableArgs` (`IArged` / `From<T>(tableArgs)` / `WithArgs`) | Table name |
| Separate physical database | `[Table(DataSource = "...")]`, or a DAO overriding `DataSource` | Connection |

The layers combine. A common layout is "one database per tenant, monthly tables inside it".

## Scenario 1: one shared table serving every tenant

**Requirement**: the `Orders` table is shared by all tenants and the tenant id is an ordinary column. Lists, counts, exports and bulk updates must never surface another tenant's rows.

**Approach**: treat the tenant condition as part of the query itself, assembled by one function that every entry point calls:

```csharp
using static LiteOrm.Common.Expr;

public static class OrderFilters
{
    public static LogicExpr For(OrderQueryRequest request)
    {
        var tenantId = TenantContext.Current?.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved.");

        var filter = (Prop(nameof(Order.TenantId)) == tenantId)
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

Notes:

- Do not filter in memory after the query. It makes `Count` disagree with the page total, lets aggregate and export endpoints bypass the filter, and the forbidden rows already travelled into the application process.
- Bulk update and bulk delete need the same scope condition. See scenario 2 of [Data Permissions in Practice](./02-data-permission.en.md).
- Row filtering decides what a query returns, not what a primary-key operation touches. Detail, update and delete still need object-level checks.

## Scenario 2: the tenant condition should not be passed down through every call

**Requirement**: dozens of query entry points need the tenant condition, and threading it through parameters loses it one layer down.

**Approach**: register the condition as a reusable fragment that reads the tenant from context:

```csharp
using static LiteOrm.Common.Expr;

// Register once; registering the same key again does not overwrite the existing implementation
GenericSqlExpr.Register("TenantFilter", (context, sqlBuilder, outputParams, _) =>
{
    var tenantId = TenantContext.Current?.TenantId
        ?? throw new InvalidOperationException("Tenant not resolved.");

    string paramName = outputParams.Count.ToString();
    outputParams.Add(new Param(sqlBuilder.ToParamName(paramName), tenantId));
    return $"{sqlBuilder.ToSqlName(nameof(Order.TenantId))} = {sqlBuilder.ToSqlParam(paramName)}";
});
```

Call sites reference it by name and compose it like any other `Expr` condition:

```csharp
var filter = OrderFilters.For(request) & Expr.Sql("TenantFilter");

var orders = await orderViewService.SearchAsync(From<OrderView>().Where(filter));
```

Notes:

- The value goes through `outputParams.Add`, so it stays parameterized and never lands in the SQL text. See [Security](../03-advanced-topics/08-security.en.md).
- A fragment returning `null` or an empty string produces no SQL at all, so query conditions do not need a `"1 = 1"` placeholder. Write paths give no such guarantee; see [Data Permissions in Practice](./02-data-permission.en.md).
- The left operand of `&` must be a `LogicExpr`. `Expr.Sql(...)` returns a `GenericSqlExpr`, so declaring the assembling function's return type as `Expr` will not compile. Return `LogicExpr`.
- A fragment can only contain table and column names. A wrong property name surfaces while generating SQL instead of silently pointing at another column.

## Scenario 3: a model that only ever serves one tenant class

**Requirement**: platform-owned orders, archived tenants or legacy compatibility models have a rule that is fixed at compile time and should not be repeated in every query.

**Approach**: push the rule down into column metadata with `Constant`:

```csharp
public enum TenantKind
{
    Platform = 1,
    Merchant = 2
}

[Table("Orders")]
public class PlatformOrder : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }

    [Column("TenantKind", Constant = TenantKind.Platform)]
    public TenantKind TenantKind => TenantKind.Platform;
}
```

`[Column(Constant = ...)]` is aggregated into `TableDefinition.ConstFilter` while table metadata is built. The condition then appears in the main table `WHERE`, in `JOIN ... ON` for association queries, and in `UPDATE` / `DELETE` `WHERE` clauses:

```sql
-- Query: an enum slice is inlined as its underlying value
SELECT * FROM "Orders" "T0" WHERE "T0"."TenantKind" = 1                        -- Platform = 1

-- A slice on another view model: the joined table's slice goes into ON, a boolean slice is inlined too
SELECT * FROM "Orders" "T0"
LEFT JOIN "Departments" "Dept" ON "T0"."DepartmentId" = "Dept"."Id" AND "Dept"."State" = 1
WHERE "T0"."IsDeleted" = 0                                                     -- Enabled = 1

-- Update and delete
UPDATE "Customers" SET "Name" = @0 WHERE "Id" = @1 AND "IsDeleted" = 0
DELETE FROM "Customers" WHERE "Id" = @0 AND "IsDeleted" = 0
```

Notes:

- `Constant` only accepts compile-time constants: enum members, enum names, numbers, strings, booleans. The framework converts them to the property type.
- Enum and boolean slices are both inlined as their underlying value: an enum becomes its numeric value (`= 1`), a boolean becomes `= 0` / `= 1`. A slice is a compile-time constant, so it takes no parameter slot and the parameter count of a slice condition never varies with metadata.
- The condition means "this table only ever contains this class of data". Writing the current request's tenant id here makes every tenant share one fixed condition, which is the worst class of multi-tenant failure.
- `INSERT` is not affected: the statement carries no slice condition, because the slice value comes from the entity property itself (the read-only property above is always `Platform`, and that is what gets inserted). With a writable property there is another side to watch: a value outside the slice is not corrected on insert, so the new row becomes invisible to the model immediately. Check the value before writing.
- A joined table's slice reaches the `JOIN ... ON` of association queries. The DAO key-based reads (`GetObject`, `ExistsKey`) use the model's own `From` fragment, which only emits the join keys and carries no joined-table slice condition, so switch to an expression query such as `Search(...)` when the joined table must be constrained too. For the full boundary see [Permission Filtering and User Scope Control](../06-di/02-permission-filtering.en.md).
- When the assembly enables `TableInfo` source generation (NativeAOT builds, or an explicit `[assembly: LiteOrmCodeGen(LiteOrmCodeGenKind.TableInfo)]`), table metadata comes from `CommonTableInfoProvider` and the generated `ColumnInfo` carries no `Constant`. None of the conditions above appear in that case. Those projects use the runtime fragment covered in the next scenario.

## Scenario 4: the tenant value is only known at startup, and should apply automatically

**Requirement**: the same entity serves a different tenant or partition per deployment, with the value coming from configuration or an environment variable. Attribute arguments must be compile-time constants, so they cannot express it, and no entry point should have to remember the condition by hand.

**Approach**: wrap the tenant source in one fragment, register it once, and pass the tenant value as `Arg` when constructing the expression through `GenericSqlExpr.Get`:

```csharp
using LiteOrm.Common;
using static LiteOrm.Common.Expr;

public static class TenantFilter
{
    private const string Key = "TenantFilter";

    /// <summary>Call once during startup; every later query reads the configured value.</summary>
    public static void Register(IConfiguration configuration)
    {
        string? tenantId = configuration["Tenant:Id"];
        if (string.IsNullOrEmpty(tenantId))
            throw new InvalidOperationException("Tenant is not configured.");

        GenericSqlExpr.Register(Key, (context, sqlBuilder, outputParams, arg) =>
        {
            if (arg is not string tenant || tenant.Length == 0)
                throw new InvalidOperationException("Tenant is not resolved.");

            string paramName = outputParams.Count.ToString();
            outputParams.Add(new Param(sqlBuilder.ToSqlParam(paramName), tenant));
            return $"{sqlBuilder.ToSqlName(nameof(Order.TenantId))} = {sqlBuilder.ToSqlParam(paramName)}";
        });

        DefaultTenantId = tenantId;   // fills Arg at construction time
    }

    public static string? DefaultTenantId { get; private set; }

    public static LogicExpr For(string? tenantId = null)
    {
        string tenant = string.IsNullOrEmpty(tenantId) ? DefaultTenantId! : tenantId;
        if (string.IsNullOrEmpty(tenant))
            throw new InvalidOperationException("Tenant is not resolved.");

        return GenericSqlExpr.Get(Key, tenant);
    }
}
```

Call sites write a single `TenantFilter.For()`, and the tenant value never travels through a parameter:

```csharp
var orders = await orderViewService.SearchAsync(
    From<OrderView>().Where(TenantFilter.For() & (Prop(nameof(Order.IsDeleted)) == false)));

// SELECT *
// FROM "Orders" "T0"
// WHERE "TenantId" = @0 AND "T0"."IsDeleted" = @1    -- @0 = tenant_a
```

Notes:

- The point is that the tenant value rides on `Arg` instead of ambient context. The fragment captures the value at the moment the expression is built, `Clone` carries it along, and the expression still means the same tenant after it has been cached, queued, or handed to another thread, instead of drifting with whatever `AsyncLocal` happens to be active.
- Declare the return type as `LogicExpr`. `GenericSqlExpr` derives from `LogicExpr`, so returning it composes with `&` and `|` directly.
- Guard against the empty string, not just `null`, and keep the fragment's default behaviour in mind: returning `null` or an empty string emits no SQL and rolls back the preceding `AND` with it. A `null` makes the tenant condition disappear entirely, while an empty string still produces `"TenantId" = @0` with an empty value, so the condition is present but matches no rows. Neither failure is easy to spot in a test, so the code above throws for both.
- Watch how columns are named inside the fragment: `sqlBuilder.ToSqlName` accepts a bare column name, so the result is an unqualified `"TenantId"`, whereas framework-generated conditions are qualified (`"T0"."IsDeleted"`). That is fine for single-table queries, but as soon as the statement joins another table with a `TenantId` column, an unqualified name can be rejected as ambiguous. Write the alias into the fragment for multi-table statements, or fall back to the `Prop(...)` form from scenario 1.
- The fragment is a statement condition: it lands in whichever statement you put it in `Where`. It is not a `ConstFilter`, so it does not automatically reach joined `JOIN ... ON` clauses, `EXISTS` subqueries, or every `UPDATE` / `DELETE`. Add `TenantFilter.For()` in those places yourself:

```csharp
var affected = await orderService.UpdateAsync(
    Expr.Update<Order>().Set((nameof(Order.Amount), Const(1m))).Where(TenantFilter.For()));

// UPDATE "Orders" SET "Amount" = @0 WHERE "TenantId" = @1
```

- On the source-generated path this approach is a closed solution: it never touches table metadata, so it does not matter whether `ColumnInfo` has a `Constant` field, and it works in AOT builds too.
- Keep the split with scenario 3 clear: `ConstFilter` states "this table only ever holds this class of data", while a fragment states "this query is scoped to this tenant". Only the latter works when one process serves several tenants, because the former is process-wide metadata that changes globally once set.
- Fragment keys are global, and re-registering an existing key does not overwrite the implementation; only the first delegate registered under a key is ever used. A test that needs a different implementation must use a different key rather than relying on re-registration.

## Scenario 5: the tenant decides the physical table name

**Requirement**: one physical table per tenant, with the name derived from the tenant code.

**Approach**: put a placeholder in the table name and implement `IArged` so writes carry the argument automatically:

```csharp
[Table("Orders_{0}")]
public class TenantOrder : ObjectBase, IArged
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }

    string[] IArged.TableArgs =>
        new[] { TenantContext.Current?.TenantId ?? throw new InvalidOperationException("Tenant not resolved.") };
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
        context.TableArgs = new[]
        {
            TenantContext.Current?.TenantId ?? throw new InvalidOperationException("Tenant not resolved.")
        };
        return context;
    }
}
```

Notes:

- On the write path, `EntityService<T>` detects `IArged` and adds the argument automatically; batch writes group by `TableArgs` and run group by group. Writing directly through `IObjectDAO<T>` does not do this, so sharded writes should go through the service layer.
- A `TableExpr` carrying its own `TableArgs` overrides what the context provided. With tenant context A, `From<TenantOrder>("tenant_b")` still reads table B. Statements like this inside multi-tenant code should be rejected in review.
- Every argument is validated as a SQL name, so a tenant code containing quotes, semicolons or spaces throws at assignment time rather than while building SQL.
- The third form fails loudly: an unresolved tenant throws instead of silently falling back to another table name.

## Scenario 6: where the tenant value comes from

**Requirement**: the tenant comes from a header, token or session, and one process serves several tenants at once.

**Approach**: carry it in a scoped context object instead of passing parameters around:

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

Notes:

- The context must be scoped or request-level. Registering it as a singleton makes every tenant read the same value.
- Implementations that depend on ambient state (for example `IArged.TableArgs`) must be `AsyncLocal`-based, otherwise concurrent requests bleed into each other. Freezing the tenant value into the entity when it is constructed is more robust.
- Cache keys must include the tenant. A key such as `"order:123"` hits across tenants in a shared-table setup.

## Scenario 7: the tenant decides the connection

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
services.AddScoped<ObjectDAO<TenantOrder>, TenantOrderDAO>();
services.AddScoped<ObjectViewDAO<TenantOrder>, TenantOrderViewDAO>();
```

Notes:

- Register the subclass in place of the base type, otherwise the service layer resolves the framework default `ObjectDAO<T>` and the connection never changes.
- Data sources are registered at startup (`AddDataSource`, or `RegisterLiteOrm` reading configuration); runtime only selects among them.
- All data source contexts inside one `SessionManager` join the same transaction when `BeginTransaction` runs, read-only connections are skipped. A background job that writes across tenants must use separate scopes, otherwise one tenant's failure rolls back another tenant's writes.

## Related links

- [Back to index](../README.md)
- [Permission Filtering and User Scopes](../06-di/02-permission-filtering.en.md)
- [Sharding and TableArgs](../03-advanced-topics/02-sharding-and-tableargs.en.md)
- [Data Permissions in Practice](./02-data-permission.en.md)
- [Audit and Change Tracking in Practice](./04-audit-and-change-tracking.en.md)
- [Concurrency and Read/Write Splitting in Practice](./06-concurrency-and-read-write-splitting.en.md)
