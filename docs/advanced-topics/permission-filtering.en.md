# Permission Filtering and User Scope Control

Permissions have two layers. One decides "may this method be called at all", enforced at the service layer by `[ServicePermission]`; see [Service Authorization](../di/service-authorization.en.md). The other decides "which rows are visible once called", which is the data filtering this article covers. The two are independent and usually used together.

When a system needs rich querying while preventing regular users from reading or writing data they do not own, permission filtering cannot stop at the frontend UI layer. In LiteOrm, scope rules usually live at one of two layers:

1. **Runtime Expr**: append conditions from the current user, current tenant, or request arguments.
2. **Model-level `ConstFilter`**: carry fixed rules such as status, partition flags, or compatibility slices; when the value comes from runtime context, `GenericSqlExpr` supplies it.

The key rule is: **current user / current tenant belongs to runtime context, so prefer Expr; when those conditions must also cover associations and write paths automatically, put them in `TableDefinition.ConstFilter` with `GenericSqlExpr` supplying the value; the `Constant` attribute argument is only for rules that never change.**

For the four multi-tenancy layouts split by isolation layer (building `Expr`, sharding with `TableArgs`, slicing with `ConstFilter`, and routing databases by overriding `DataSource`), see [Tenant Isolation](../typical-applications/tenant-isolation.en.md).

In real projects, a query usually combines more than just a "permission condition". It often stacks:

- business conditions
- soft-delete conditions (for example `IsDeleted == false`)
- current-user / current-tenant scope conditions

This article focuses on the **mechanics** of each mechanism. For a business-oriented list of landed scenarios, see [Data Permissions](../typical-applications/data-permission.en.md).

## Scenario Matrix

| Scenario | Recommended approach | Why |
|------|----------|------|
| Admin views all orders | No user-scope filter attached | Preserves full operational/audit perspective |
| Regular user queries lists and counts | Append runtime `Expr` | The current user is request-scoped |
| Regular user reads detail, updates, or deletes | Explicit access check at the endpoint layer | Prevents bypassing list filtering |
| Model always represents one fixed state / slice | `[Column(Constant = ...)]` / `TableDefinition.ConstFilter` | The rule is invariant at the model level |

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

The `Constant` argument on `[Column]` is a **global fixed filter for the table itself**; in association queries (`From<...>()`, `TableJoinExpr`) it also becomes part of `JOIN ... ON`. At the metadata layer, it is consolidated into `TableDefinition.ConstFilter`:

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
4. In association queries, joined-table fixed filters go into `JOIN ... ON`.
5. `ForeignExpr` / `Exists` / `ExistsRelated` `EXISTS` subqueries also apply the target table's own `ConstFilter` before combining the relation condition and your `InnerExpr`.
6. `UPDATE` / `DELETE` statements carry the same rule, including the DAO key-based read and write paths (`GetObject`, `ExistsKey`, `Update`, `DeleteByKeys`, and the batch update/delete methods). A row the model cannot see cannot be read back, updated, or deleted. Conditional updates and deletes (`Update(UpdateExpr)`, `Delete(LogicExpr)`) behave the same way.
7. A joined table's fixed filter only reaches association statements produced by expression queries. The DAO key-based reads (`GetObject`, `ExistsKey`) use the model's own `From` fragment, which only emits the join keys and does not carry joined-table fixed filters, so reading joined columns through that path can still surface a related row outside the filter; use an expression query such as `Search(...)` when the joined table must be constrained as well.

Tables that declare a fixed filter do not reuse the command cache. The cache keeps the SQL and parameters generated on the first call, while the slice condition comes from table metadata and can be replaced at runtime (both the value and the shape of the condition may change), so reuse would freeze the old content; such tables build a fresh command on every call, and the new command is not written into the cache, so it never takes a cache slot away from a regular command. Replacing `TableDefinition.ConstFilter` at runtime therefore takes effect on the next call, at the cost of one extra SQL build per operation for such tables; tables without a fixed filter keep using the command cache (the cache holds the underlying command itself and every call creates a proxy that does not own it, so releasing the proxy never affects the cache).

It fits:

- **Model-level invariant rules** such as enabled rows, published rows, or compatibility slices
- **Compile-time fixed** partitions such as a fixed tenant kind or source type
- **Table-level fixed conditions** expressed by numeric, boolean, or string markers that do not vary per request
- **Row slices that must apply on every path** — a tenant or an organization — where the value comes from runtime context and `GenericSqlExpr` supplies it. See the end of this section

It does **not** fit:

- writing one concrete current user or current tenant into the model with `Column.Constant`. Attribute arguments are compile-time constants, so the value is identical for every request and every caller ends up seeing the same tenant's data

If you maintain a custom metadata provider, you can also assign `ConstFilter` directly while creating `TableDefinition`. `TableDefinition.ConstFilter` is a public read-write property, and there is a more direct route: fetch the table definition and assign it, with `GenericSqlExpr` supplying the value.

```csharp
var tableDefinition = TableInfoProvider.Instance.GetTableDefinition(typeof(Order))!;
tableDefinition.ConstFilter = Expr.Sql("TenantFilter");   // the fragment reads the current tenant itself
```

The condition is then no longer bound to compile-time constants, while keeping the property that makes `ConstFilter` useful: it applies automatically to every query, association and write path. The trade-off is semantic and has a cost: once `ConstFilter` resolves its value at runtime, tables that declare it stop reusing the prepared-command cache and rebuild their SQL and command on every operation. The complete layout, including the tenant interface, the fragment registration and the scope it covers, is in example 3 of [Tenant Isolation](../typical-applications/tenant-isolation.en.md).

That also means: if you filter users with `ExistsRelated<Department>(...)`, and `Department` itself declares a fixed rule such as `State == Enabled`, that rule is automatically injected into the `EXISTS` subquery. You do not need to repeat it manually in `InnerExpr`.

### 2.3 Wrapping "read from user context" filters with `GenericSqlExpr`

When you want to reuse a "current user scope" rule but do not want to pass `currentUser.Id` through every call layer, `GenericSqlExpr` can fetch the value directly from user context:

```csharp
using static LiteOrm.Common.Expr;

// UserContext.Current is illustrative here; replace it with your own user-context accessor
GenericSqlExpr.Register("CurrentUserFilter", (context, _) =>
{
    var currentUser = UserContext.Current
        ?? throw new InvalidOperationException("Current user not found.");

    string paramName = context.OutputParams.Count.ToString();
    context.OutputParams.Add(new(context.SqlBuilder.ToParamName(paramName), currentUser.Id));
    return $"{context.SqlBuilder.ToSqlName(nameof(Order.CreatedByUserId))} = {context.SqlBuilder.ToSqlParam(paramName)}";
});

var filter = BuildBusinessFilter(request)
    & (Prop(nameof(Order.IsDeleted)) == false)
    & Expr.Sql("CurrentUserFilter");
```

This approach is useful because:

- you can reuse "current user data scope" as a shared building block
- the current-user value comes from user context instead of caller-provided arguments
- it still composes with normal Expr, soft-delete rules, and statistics queries
- it remains parameterized through `context.OutputParams`, rather than concatenating user values into SQL

For the security boundary, see the `GenericSqlExpr` section in [Security](../advanced-topics/security.en.md).

## 4. Frontend Guidance

- Clearly indicate to regular users that "query results are automatically filtered to the current account or tenant scope."
- When encountering `403`, display "the current user does not have access to this data" rather than incorrectly reporting "record not found."
- Do not rely on hidden buttons in the frontend for permission control; button hiding is a UX optimization, not a security boundary.

## 5. Common Mistakes

### 5.1 Permission control only in the frontend

The frontend can hide buttons, but this cannot serve as the final authorization basis. The true permission boundary must be on the backend.

### 5.2 Restricting only lists, not detail and delete

As long as detail, update, and delete endpoints lack verification, users can still directly access objects they do not own.

### 5.3 Using `Column.Constant` for the current user or current tenant

The argument of `[Column(Constant = ...)]` is a compile-time constant. Writing a login state, token or header value into it means the same concrete value applies to every request, so every caller sees one user's or one tenant's data.

When "the value varies per request but must still apply on every path" is the requirement, assign `ConstFilter` on the table definition and let `GenericSqlExpr` supply the value, as shown at the end of 2.2. When the value is only needed at the query entry point and does not have to cover associations and write paths, a runtime `Expr` or `GenericSqlExpr` fragment is lighter.

### 5.4 Confusing row filters with physical sharding

`TenantId == currentTenantId` solves **row isolation** in a shared table; `TableArgs` / `CreateSqlBuildContext` solves **real table routing**. They can coexist, but they should not replace each other.

## Related Links

- [Back to docs hub](../README.md)
- [Service Authorization](../di/service-authorization.en.md)
- [Associations](../core-usage/associations.en.md)
- [Sharding and TableArgs](../advanced-topics/sharding-and-tableargs.en.md)
- [Security](../advanced-topics/security.en.md)
- [Lambda & Expr Mixing](../core-usage/lambda-expr-mixing.en.md)

