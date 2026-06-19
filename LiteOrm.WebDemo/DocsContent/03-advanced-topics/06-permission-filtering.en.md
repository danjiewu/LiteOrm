# Permission Filtering and User Scope Control

When a system needs rich querying while preventing regular users from reading or writing data they do not own, permission filtering cannot stop at the frontend UI layer. In LiteOrm, scope rules usually live at one of three layers:

1. **Runtime Expr**: append conditions from the current user, current tenant, or request arguments.
2. **Model-level `ConstFilter`**: carry fixed rules such as status, partition flags, or compatibility slices.
3. **`CreateSqlBuildContext` / `TableArgs`**: when tenant isolation is already expressed as physical table routing.

The key rule is: **current user / current tenant belongs to runtime context, so prefer Expr or `GenericSqlExpr`; only fixed rules should go into `TableDefinition.ConstFilter`.**  
In real projects, a query usually combines more than just a "permission condition". It often stacks:

- business conditions
- soft-delete conditions (for example `IsDeleted == false`)
- current-user / current-tenant scope conditions

## Scenario Matrix

| Scenario | Recommended approach | Why |
|------|----------|------|
| Admin views all orders | No user-scope filter attached | Preserves full operational/audit perspective |
| Regular user queries lists and counts | Append runtime `Expr` | The current user is request-scoped |
| Regular user reads detail, updates, or deletes | Explicit access check at the endpoint layer | Prevents bypassing list filtering |
| Model always represents one fixed state / slice | `Column.Constant` / `TableDefinition.ConstFilter` | The rule is invariant at the model level |
| Shared-table multi-tenancy | Append `TenantId == currentTenantId` at runtime | Isolation happens by rows in one table |
| Physical tenant sharding | `TableArgs` or override `CreateSqlBuildContext` | The tenant decides the real table name or route |

## 1. Filtering Behavior in WebDemo

### 1.1 QueryString queries and counts

`GET /api/orders/query` and `GET /api/orders/stats` typically build business filters first, then append soft-delete and current-user scope conditions:

```csharp
using static LiteOrm.Common.Expr;
filter &= Prop(nameof(DemoOrder.IsDeleted)) == false;
if (request.OnlyMine == true || !IsAdmin(currentUser))
{
    filter &= Prop(nameof(DemoOrder.CreatedByUserId)) == currentUser.Id;
}
```

The key point: **permission conditions are part of the query itself**, not an in-memory trim applied after results return.

### 1.2 Expr queries

`POST /api/orders/query/expr` follows the same pattern, injecting soft-delete and current-user scope rules into the native Expr before `SearchAsync` / `CountAsync`:

```csharp
using static LiteOrm.Common.Expr;
filter ??= Prop(nameof(DemoOrder.Id)) > 0;
filter &= Prop(nameof(DemoOrder.IsDeleted)) == false;

if (!IsAdmin(currentUser))
{
    filter &= Prop(nameof(DemoOrder.CreatedByUserId)) == currentUser.Id;
}
```

This ensures that whether the frontend uses a visual builder or submits a native `Source` chain Expr JSON directly, the backend permission boundary stays consistent.

### 1.3 Detail, update, and delete

List filtering does not replace object-level access control. Explicit access checks are still required for:

- `GET /api/orders/{id}`
- `PUT /api/orders/{id}`
- `DELETE /api/orders/{id}`

Returning a clear `403` is recommended so the frontend can distinguish "forbidden" from "not found".

## 2. Which Layer Should Carry the Filter

### 2.1 Prefer assembling business, soft-delete, and user-scope filters at the query entry

**Recommended:**

```csharp
using static LiteOrm.Common.Expr;
var filter = BuildBusinessFilter(request)
    & (Prop(nameof(Order.IsDeleted)) == false);

if (!IsAdmin(currentUser))
{
    filter &= Prop(nameof(Order.CreatedByUserId)) == currentUser.Id;
}

var result = await orderService.SearchAsync(
    From<OrderView>()
        .Where(filter)
        .OrderBy(Prop(nameof(Order.CreatedTime)).Desc())
        .Section(0, 20)
);
```

**Avoid:**

```csharp
var items = await orderService.SearchAsync(expr);
var myItems = items.Where(x => x.CreatedByUserId == currentUser.Id).ToList();
```

It is better to assemble "business conditions + `IsDeleted` + user scope" in one place and reuse that logic for lists, counts, exports, and similar queries.  
The second approach creates three problems:

1. `Count` and pagination totals become inaccurate.
2. Unfiltered aggregations, statistics, or exports remain possible.
3. The query layer has already read data that should not have been accessed.

### 2.2 `Column.Constant` and `TableDefinition.ConstFilter`

`Column.Constant` is a **global fixed filter for the table itself**, including association queries where it becomes part of `JOIN ... ON`. At the metadata layer, it is consolidated into `TableDefinition.ConstFilter`:

```csharp
public enum RecordState
{
    Disabled = 0,
    Enabled = 1
}

[Table("Departments")]
public class Department
{
    [Column("Id", IsPrimaryKey = true)]
    public int Id { get; set; }

    [Column("State", Constant = RecordState.Enabled)]
    public RecordState State => RecordState.Enabled;
}
```

`Constant` is not limited to enums. It also supports other constant values that can be converted to the property type, for example:

- `Constant = 1`: suitable for numeric columns such as `int` or `long`
- `Constant = "tenant_a"`: suitable for string columns
- `Constant = false`: suitable for boolean columns

If the property itself is an enum, these forms are still supported:

- `Constant = "Enabled"`: parse by enum member name
- `Constant = 1`: parse by integral value
- `Constant = RecordState.Enabled`: use the enum member directly

The pipeline is:

1. `Column.Constant` is parsed during metadata construction.
2. Multiple fixed-column conditions are merged into `TableDefinition.ConstFilter`.
3. When SQL is generated, main-table fixed filters go into `WHERE`.
4. Joined-table fixed filters go into `JOIN ... ON`.
5. `ForeignExpr` / `Exists` / `ExistsRelated` `EXISTS` subqueries also apply the target table's own `ConstFilter` before combining the relation condition and your `InnerExpr`.
6. `UPDATE` / `DELETE` continue to carry the same fixed rule.

It fits:

- **Model-level invariant rules** such as enabled rows, published rows, or compatibility slices
- **Compile-time fixed** partitions such as a fixed tenant kind or source type
- **Table-level fixed conditions** expressed by numeric, boolean, or string markers that do not vary per request

It does **not** fit:

- the current logged-in user
- the current request tenant
- any value coming from request arguments, tokens, or runtime context

If you maintain a custom metadata provider, you can also assign `ConstFilter` directly while creating `TableDefinition`; the semantic rule is still the same: it should represent a fixed model rule, not a request-scoped variable.

That also means: if you filter users with `ExistsRelated<Department>(...)`, and `Department` itself declares a fixed rule such as `State == Enabled`, that rule is automatically injected into the `EXISTS` subquery. You do not need to repeat it manually in `InnerExpr`.

### 2.3 Wrapping "read from user context" filters with `GenericSqlExpr`

When you want to reuse a "current user scope" rule but do not want to pass `currentUser.Id` through every call layer, `GenericSqlExpr` can fetch the value directly from user context:

```csharp
using static LiteOrm.Common.Expr;

// UserContext.Current is illustrative here; replace it with your own user-context accessor
GenericSqlExpr.Register("CurrentUserFilter", (context, sqlBuilder, outputParams, _) =>
{
    var currentUser = UserContext.Current
        ?? throw new InvalidOperationException("Current user not found.");

    string paramName = outputParams.Count.ToString();
    outputParams.Add(new(sqlBuilder.ToParamName(paramName), currentUser.Id));
    return $"{sqlBuilder.ToSqlName(nameof(Order.CreatedByUserId))} = {sqlBuilder.ToSqlParam(paramName)}";
});

var filter = BuildBusinessFilter(request)
    & (Prop(nameof(Order.IsDeleted)) == false)
    & Expr.Sql("CurrentUserFilter");
```

This approach is useful because:

- you can reuse "current user data scope" as a shared building block
- the current-user value comes from user context instead of caller-provided arguments
- it still composes with normal Expr, soft-delete rules, and statistics queries
- it remains parameterized through `outputParams`, rather than concatenating user values into SQL

For the security boundary, see the `GenericSqlExpr` section in [Security](./08-security.en.md).

## 3. Multi-Tenancy Patterns

Multi-tenancy is not one single pattern; it depends on where the isolation boundary lives. In LiteOrm, the most common options are the following three.

### 3.1 Shared-table multi-tenancy: build `Expr` in application code

If all tenants share one table, the most direct option is to append both `TenantId` and `IsDeleted` in query construction:

```csharp
using static LiteOrm.Common.Expr;

var tenantFilter = Prop(nameof(Order.TenantId)) == currentTenantId;
var filter = BuildBusinessFilter(request)
    & (Prop(nameof(Order.IsDeleted)) == false)
    & tenantFilter;

var result = await orderService.SearchAsync(
    From<OrderView>()
        .Where(filter)
        .OrderBy(Prop(nameof(Order.CreatedTime)).Desc())
        .Section(0, 20)
);
```

This is the most common pattern, and it combines naturally with current-user filtering.

### 3.2 Fixed-tenant models: carry invariant rules with `ConstFilter`

If a model always represents one fixed tenant slice, the rule can be pushed down into `ConstFilter`. Typical examples include:

- platform-only tenant data
- archived internal-tenant data
- legacy compatibility models with a built-in fixed filter

For example:

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

This means "this model only sees platform tenant rows". It does **not** mean "switch dynamically per current tenant".  
As soon as the tenant value comes from the current request, go back to runtime Expr or `GenericSqlExpr`.

### 3.3 Physical tenant sharding: override `CreateSqlBuildContext`

If the tenant is part of the physical table name, such as `[Table("Orders_{0}")]`, it is often better to control `TableArgs` in the SQL build context than to append `WHERE TenantId = ...`:

```csharp
[Table("Orders_{0}")]
public class TenantOrder : ObjectBase
{
    [Column("Id", IsPrimaryKey = true)]
    public long Id { get; set; }
}

public class TenantOrderViewDAO : ObjectViewDAO<TenantOrder>
{
    private readonly ITenantProvider _tenantProvider;

    public TenantOrderViewDAO(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    public override SqlBuildContext CreateSqlBuildContext(bool initTable = false)
    {
        var context = base.CreateSqlBuildContext(initTable);
        context.TableArgs = new[] { _tenantProvider.CurrentTenantCode };
        return context;
    }
}
```

When SQL is generated, the current tenant code is injected into the real table name, for example `Orders_tenant_a`.

This fits scenarios where:

- different tenants live in different physical tables
- you want Expr, ExprString, and DAO queries to inherit the same route automatically
- tenant isolation is a **table routing** concern rather than a **row filtering** concern

This works because DAO and `ExprString` both create SQL contexts through `CreateSqlBuildContext(...)`; once you override that method and populate `TableArgs`, downstream SQL generation automatically reuses the same route parameters. For more details, see [Sharding and TableArgs](./02-sharding-and-tableargs.en.md).

Also note that if a lower-level `TableExpr` explicitly sets its own `TableArgs`, that value overrides the inherited context value.  
In multi-tenant or scoped queries, this means you can unintentionally leave the original tenant / shard boundary, so explicit overrides should be reviewed carefully.

### 3.4 How to choose

| Pattern | Best for | Advantage | Limitation |
|------|----------|------|------|
| Runtime Expr | current user, current tenant, request-driven filters | simple, flexible, universal | must be applied consistently at query entry points |
| `ConstFilter` | fixed status, fixed business slice, fixed tenant type | auto-injected into SQL; works for main and joined tables | not suitable for request-scoped context |
| `CreateSqlBuildContext` + `TableArgs` | physical tenant sharding / routing | hits the real table directly | solves routing, not row-level authorization |

## 4. Frontend Guidance

- Clearly indicate to regular users that "query results are automatically filtered to the current account or tenant scope."
- When encountering `403`, display "the current user does not have access to this data" rather than incorrectly reporting "record not found."
- Do not rely on hidden buttons in the frontend for permission control; button hiding is a UX optimization, not a security boundary.

## 5. Common Mistakes

### 5.1 Permission control only in the frontend

The frontend can hide buttons, but this cannot serve as the final authorization basis. The true permission boundary must be on the backend.

### 5.2 Restricting only lists, not detail and delete

As long as detail, update, and delete endpoints lack verification, users can still directly access objects they do not own.

### 5.3 Using `ConstFilter` for the current user or current tenant

`ConstFilter` expresses a fixed rule, not "who this request belongs to". If the value comes from login state, a token, a header, or tenant context, switch to Expr, `GenericSqlExpr`, or table routing.

### 5.4 Confusing row filters with physical sharding

`TenantId == currentTenantId` solves **row isolation** in a shared table; `TableArgs` / `CreateSqlBuildContext` solves **real table routing**. They can coexist, but they should not replace each other.

## Related Links

- [Back to docs hub](../README.md)
- [Associations](../02-core-usage/06-associations.en.md)
- [Sharding and TableArgs](./02-sharding-and-tableargs.en.md)
- [Security](./08-security.en.md)
- [Lambda & Expr Mixing](../02-core-usage/07-lambda-expr-mixing.en.md)

