# Data Permissions

Filtering query results by the current user is only half of data permissions. The other half is deciding, when a caller skips the list and goes straight at a primary key, who judges whether that access is allowed. The five scenarios below cover query filtering, scoped writes, object-level checks and interface-level enforcement.

Three places can hold the rule, and the only criterion is whether the value is known at compile time:

| Where | What it carries | When to use |
| --- | --- | --- |
| Runtime `Expr` | Conditions from the current user, tenant or request parameters | The default; covers lists, counts and exports |
| `GenericSqlExpr` fragment | A rule that must be reused without threading parameters | The same rule appears at several entry points |
| `TableDefinition.ConstFilter` | Fixed status, fixed partition, fixed tenant class | Rules that never change for the model |

Writing the current user into `ConstFilter` makes every caller see the same user's data. For fixed slices see [Tenant Isolation](./tenant-isolation.en.md).

## Scenario 1: lists, counts and exports only show what the caller may see

**Requirement**: non-admins only see their own orders, and the same rule must apply to the list, the total, aggregates and the export.

**Approach**: write one condition-assembling function and call it from every entry point:

```csharp
using static LiteOrm.Common.Expr;

public static class OrderScopes
{
    public static LogicExpr For(OrderQueryRequest request, ICurrentUser user)
    {
        var filter = (Prop(nameof(Order.Id)) > 0)
            & (Prop(nameof(Order.IsDeleted)) == false);

        if (!string.IsNullOrEmpty(request.Keyword))
            filter &= Prop(nameof(Order.Title)).Like($"%{request.Keyword}%");

        if (!user.IsAdmin)
            filter &= Prop(nameof(Order.OwnerId)) == user.Id;

        return filter;
    }
}
```

```csharp
var filter = OrderScopes.For(request, currentUser);

var page = await orderViewService.SearchAsync(
    From<OrderView>().Where(filter).OrderBy(Prop(nameof(Order.CreateTime)).Desc()).Section(0, 20));

var total = await orderViewService.CountAsync(filter);
var exists = await orderViewService.ExistsAsync(filter);
```

Notes:

- Do not query by business conditions and then filter in memory. Page totals become wrong, aggregate and export endpoints bypass the filter, and rows the caller may not read already reached the process.
- Export endpoints are the ones most often forgotten because they usually have their own query method. Wire them to the same function instead of duplicating the conditions.
- Do not create a separate "no filter" overload for admins and reuse it globally; ordinary requests can reach it too. Assemble conditions by role in one function so the filtering entry point stays unique.

## Scenario 2: bulk update and bulk delete need the scope too

**Requirement**: the admin console offers "cancel this batch of orders", and the scope limit must not live only in the list query.

**Approach**: `UpdateAll` takes an `UpdateExpr`, `DeleteAll` takes a condition expression. Put the scope in the same `Where` as the business condition:

```csharp
using static LiteOrm.Common.Expr;

var updateExpr = new UpdateExpr
{
    Table = new TableExpr(typeof(Order)),
    Sets = new List<SetItem> { new(Prop(nameof(Order.State)), Const(OrderState.Cancelled)) },
    Where = Expr.Lambda<Order>(o => o.OwnerId == userId && o.State == OrderState.Pending)
};

var affected = orderService.UpdateAll(updateExpr);

var deleted = orderService.DeleteAll(Expr.Lambda<Order>(o => o.OwnerId == userId && o.CreateTime < deadline));
```

Notes:

- A `DeleteAll` condition without a scope deletes outside the caller's data and nothing reports an error.
- On the query path an empty `GenericSqlExpr` fragment is ignored, and `WHERE` is not even emitted. Write paths give no such guarantee: `UPDATE` / `DELETE` always emit the `WHERE` keyword, so an empty fragment leaves a dangling `WHERE` and the statement fails. Conditions passed to write entry points must always exist.
- One sentence to remember: query conditions may be empty, write conditions may not.
- Single-entity `Update(entity)` / `Delete(entity)` do not go through condition assembly at all; they belong to the next scenario.

## Scenario 3: object-level checks when the call arrives with a primary key

**Requirement**: the list is already filtered, but the detail endpoint receives nothing but an id, so any signed-in user can read someone else's row by editing a URL parameter.

**Approach**: check ownership on the loaded object:

```csharp
public async Task<Order> GetOrderAsync(long id, ICurrentUser user, CancellationToken cancellationToken = default)
{
    var order = await orderViewService.GetObjectAsync(id, cancellationToken: cancellationToken);
    if (order is null) throw new NotFoundException();
    if (!user.IsAdmin && order.OwnerId != user.Id) throw new ForbiddenException();
    return order;
}
```

Notes:

- Keep "does not exist" and "not allowed" distinct (`404` and `403`) so the client can show the right message. If the business must not reveal existence, return `404` everywhere, consistently.
- Update and delete need the same check. Pushing the scope into the write statement instead (scenario 2) is more robust because the check happens in SQL.
- The check must read from the master. A read replica returns lagging data and misjudges ownership; see [Concurrency and Read/Write Splitting](./concurrency-and-read-write-splitting.en.md).

## Scenario 4: turn the scope rule into a reusable fragment

**Requirement**: the same scope rule now appears at more than three entry points and parameter plumbing starts to break.

**Approach**: register it as a `GenericSqlExpr` that reads the context itself:

```csharp
using static LiteOrm.Common.Expr;

GenericSqlExpr.Register("OwnerScope", (context, sqlBuilder, outputParams, _) =>
{
    var user = UserContext.Current ?? throw new InvalidOperationException("User not resolved.");
    if (user.IsAdmin) return null;   // an empty fragment is ignored, leaving no stray AND / WHERE

    string paramName = outputParams.Count.ToString();
    outputParams.Add(new Param(sqlBuilder.ToParamName(paramName), user.Id));
    return $"{sqlBuilder.ToSqlName(nameof(Order.OwnerId))} = {sqlBuilder.ToSqlParam(paramName)}";
});
```

```csharp
var filter = OrderScopes.For(request) & Expr.Sql("OwnerScope");
```

Notes:

- The generated SQL is parameterized; the value never lands in the text. See [Security](../advanced-topics/security.en.md).
- The admin branch returns `null`, not `"1 = 1"`. The composition rolls back the `" AND "` it already wrote, and a `SELECT` with no conditions emits no `WHERE` at all.
- Declare the assembling function as returning `LogicExpr` so `&` composes with `Expr.Sql(...)`. An `Expr` left operand will not compile.
- Do not rely on the empty-fragment semantics in write statements; see scenario 2.

## Scenario 5: interface-level role control

**Requirement**: certain service methods may only be called by specific roles, declared on the method like `[Authorize]`.

**Approach**: `[ServicePermission]` declares metadata, you implement the check. The declaration:

```csharp
public class OrderService : EntityService<Order>, IOrderService
{
    public OrderService(IServiceProvider serviceProvider) : base(serviceProvider) { }

    [ServicePermission(AllowRoles = "Admin,Operator")]
    public void CancelAll(long tenantId) { /* ... */ }

    [ServicePermission(AllowAnonymous = true)]
    public decimal GetPublicPrice(long id) { /* ... */ }

    [ServicePermission(AllowRoles = "Admin")]
    public void ResetOwner(long orderId, [Log(false)] string reason) { /* ... */ }
}
```

The check hangs off the service invoking event:

```csharp
public sealed class RoleCheckEvent : IServiceInvokingEvent
{
    private readonly ICurrentUser _currentUser;

    public RoleCheckEvent(ICurrentUser currentUser) => _currentUser = currentUser;

    public void OnInvoking(ServiceInvokeContext context)
    {
        var description = new ServiceDescription();
        description.LoadFrom(context.Method);

        if (description.AllowAnonymous) return;

        var roles = description.AllowRoles;
        if (roles is null || roles.Length == 0) return;

        foreach (var role in roles)
        {
            if (_currentUser.IsInRole(role)) return;
        }

        throw new UnauthorizedAccessException($"Role required: {string.Join(",", roles)}.");
    }
}

services.AddScoped<IServiceInvokingEvent, RoleCheckEvent>();
```

Notes:

- The attribute is metadata only. The framework reads it into `ServiceDescription` (`ServiceExt.LoadFrom`) but never enforces it; `ServiceInvokeInterceptor` uses the description for transaction switches, log level and call context.
- Nested calls do not raise events. `ServiceInvokeInterceptor` keeps an "invocation in progress" flag to avoid double handling, so a service method calling another service method only reuses the outer transaction and `OnInvoking` / `OnInvoked` do not fire again. Checks still belong at the business entry point.
- `ServiceInvokeContext.Arguments` holds raw arguments with no masking. Logging `Arguments` writes passwords and identity numbers to disk. To control framework logging, mark parameters with `[Log(false)]`; they appear as `*`.
- An exception thrown by an event propagates up the call chain to the hosting framework or to `IServiceExceptionEvent`. LiteOrm only logs it.

## Scenario 6: confirm which entry points skip the filter

**Requirement**: before going live, review the ways a call can avoid the data scope.

| Path | Goes through the data scope? | What to do |
| --- | --- | --- |
| `SearchAsync` / `CountAsync` / `ExistsAsync` | Yes, if assembly is centralised | Keep one assembling function |
| `GetObjectAsync(id)` | No | Add an object-level check |
| `Update` / `Delete(entity)` | No | Read then write, or use `UpdateAll` / `DeleteAll` with a condition |
| `UpdateAll` / `DeleteAll(expr)` | Depends on the `expr` passed in | Put the scope inside the expression |
| Writing through an injected `IObjectDAO<T>` | No | Route business code through services; keep DAOs for infrastructure |
| Remote server proxy | Decided by the server implementation | Implement the same rules on the server |

`LiteOrm.Remote` only forwards service calls to the server, so the data permission decision happens inside the server implementation. The client's job is to not send what it should not send; the server's job is to never assume that a request is trustworthy just because it came from an internal system.

## Related links

- [Back to index](../README.md)
- [Permission Filtering and User Scopes](../di/permission-filtering.en.md)
- [Tenant Isolation](./tenant-isolation.en.md)
- [Soft Deletes and Historical Data](./soft-delete-and-archive.en.md)
- [Audit and Change Tracking](./audit-and-change-tracking.en.md)
- [Security](../advanced-topics/security.en.md)
