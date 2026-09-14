# Data Permissions: What Comes After Query Filtering

Filtering query results by the current user is only half of data permissions. The other half is deciding who rejects a caller that skips the list endpoint and addresses a row by primary key. This page ties together where the rules live, how to reuse them, and how to fail closed.

## 1. Three places to put the rule

| Location | Carries | When to use |
| --- | --- | --- |
| Runtime `Expr` | Conditions driven by the current user, tenant or request | The default, covering lists, counts and exports |
| A `GenericSqlExpr` building block | A rule reused by several entry points | The same rule appears in more than one place |
| `TableDefinition.ConstFilter` | Fixed states, fixed partitions, fixed tenant kinds | Rules that never change at runtime |

The only test that matters: can the value be determined at compile time? If yes, `ConstFilter`. If no, a runtime condition. Writing the current login into `ConstFilter` makes every caller see the same user's data.

## 2. Assemble the condition at the query entry point

Keep "business conditions + soft delete + data scope" in one function shared by lists, counts and exports:

```csharp
using static LiteOrm.Common.Expr;

private Expr BuildOrderFilter(OrderQueryRequest request, ICurrentUser user)
{
    var filter = (Prop(nameof(Order.Id)) > 0)
        & (Prop(nameof(Order.IsDeleted)) == false);

    if (!string.IsNullOrEmpty(request.Keyword))
        filter &= Prop(nameof(Order.Title)).Like($"%{request.Keyword}%");

    if (!user.IsAdmin)
        filter &= Prop(nameof(Order.OwnerId)) == user.Id;

    return filter;
}
```

The pattern to avoid is querying first and filtering in memory afterwards. It breaks three things: `Count` and paging totals disagree with the page contents; aggregation, statistics and export endpoints bypass the filter; and rows the caller may not read have already entered the process.

The same rule has to cover range-based writes. `UpdateAll` and `DeleteAll` take a `LogicExpr` or an `UpdateExpr`, and an entry point that only carries a primary key or a business condition has dropped the scope:

```csharp
var updateExpr = new UpdateExpr
{
    Table = new TableExpr(typeof(Order)),
    Sets = new List<SetItem> { new(Expr.Prop(nameof(Order.State)), Expr.Const(OrderState.Cancelled)) },
    Where = Expr.Lambda<Order>(o => o.OwnerId == user.Id && o.State == OrderState.Pending)
};

orderService.UpdateAll(updateExpr);
```

Note that the scope condition lives in the same `Where` as the business condition. `DeleteAll(Expr.Lambda<Order>(...))` works the same way: drop the scope and the delete goes out of bounds.

## 3. Single-object access needs its own check

List filtering is not object-level access control. Detail, update and delete all start from a primary key:

```csharp
public async Task<Order> GetOrderAsync(long id, ICurrentUser user)
{
    var order = await _orderViewService.GetObjectAsync(id);
    if (order is null) throw new NotFoundException();
    if (!user.IsAdmin && order.OwnerId != user.Id) throw new ForbiddenException();
    return order;
}
```

Keeping "not found" (`404`) distinct from "not allowed" (`403`) lets the front end react correctly. If the business does not want to reveal whether a resource exists, return `404` in both cases, but do it consistently across every entry point.

## 4. Reuse: promote the scope condition

Once a rule shows up in three or more entry points, promote it to a `GenericSqlExpr`:

```csharp
using static LiteOrm.Common.Expr;

GenericSqlExpr.Register("OwnerScope", (context, sqlBuilder, outputParams, _) =>
{
    var user = UserContext.Current ?? throw new InvalidOperationException("User not resolved.");
    if (user.IsAdmin) return null;   // an empty fragment is dropped, no stray AND / WHERE

    string paramName = outputParams.Count.ToString();
    outputParams.Add(new Param(sqlBuilder.ToParamName(paramName), user.Id));
    return $"{sqlBuilder.ToSqlName(nameof(Order.OwnerId))} = {sqlBuilder.ToSqlParam(paramName)}";
});
```

Keep parameterization rather than string concatenation; the safety boundary is described in [Security](../03-advanced-topics/08-security.en.md). For a fuller comparison of the options, see [Permission filtering and user scope](../06-di/02-permission-filtering.en.md).

### Empty fragments are dropped, on read paths only

When the building block returns `string.Empty` or `null`, it contributes no SQL at all. The condition combiners roll back the `" AND "` / `" OR "` they had already written, and a `SELECT` statement omits the `WHERE` keyword entirely when nothing remains. That is why query conditions need no `"1 = 1"` placeholder to keep the syntax valid; the administrator branch can simply return an empty string.

Write paths give no such guarantee. The `UPDATE` and `DELETE` statements generated for `UpdateAll` / `DeleteAll` write the `WHERE` keyword unconditionally, so an empty fragment leaves a dangling `WHERE` and the statement fails to execute. A range condition passed to those entry points must always produce SQL, or the caller has to decide up front whether to attach it at all (the approach in section 2).

In one line: query conditions may be empty, write conditions may not.

## 5. Service-level backstop: `[ServicePermission]` supplies metadata, you supply the check

LiteOrm provides the `[ServicePermission]` attribute:

```csharp
public class OrderService : EntityService<Order>, IOrderService
{
    [ServicePermission(AllowRoles = "Admin,Operator")]
    public void CancelAll(long tenantId) { /* ... */ }

    [ServicePermission(AllowAnonymous = true)]
    public decimal GetPublicPrice(long id) { /* ... */ }
}
```

Its two properties are `AllowAnonymous` and `AllowRoles` (comma-separated role names). The framework reads them into a `ServiceDescription` (`ServiceExt.LoadFrom`), but it does **not** perform the check for you. `ServiceInvokeInterceptor` uses the description for the transaction flag, the log level and the invocation context; the permission fields remain descriptive data.

To actually block a call, add a layer yourself. The least invasive place is `IServiceInvokingEvent`, which fires before the method executes, so throwing from it aborts the call:

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

Three things to keep in mind:

- **Nested calls do not raise the events.** `ServiceInvokeInterceptor` keeps an internal "invocation in progress" flag to avoid double processing, so a service method calling another service method reuses the outer transaction and `OnInvoking` / `OnInvoked` do not fire again. Authorization cannot live only in the event; the business entry point still needs its own checks.
- **`ServiceInvokeContext.Arguments` holds the raw arguments with no masking.** Writing `Arguments` to a log from a subscriber persists passwords and identity numbers along with everything else. To control what appears in framework logs, mark parameters with `[Log(false)]`; such arguments show up as `*`:

  ```csharp
  [ServicePermission(AllowRoles = "Admin")]
  public void ResetPassword(long userId, [Log(false)] string newPassword) { /* ... */ }
  ```
- **Exceptions propagate as-is.** An exception raised by the permission event travels up the call chain to your application or to `IServiceExceptionEvent`. LiteOrm only logs it.

## 6. Ways to bypass the filter

| Path | Goes through the scope filter | Handling |
| --- | --- | --- |
| `SearchAsync` / `CountAsync` | Yes, if conditions are assembled in one place | Keep the single builder |
| `GetObjectAsync(id)` | No | Add an ownership check |
| `Update` / `Delete(entity)` | No | Read first, or switch to a scoped `UpdateAll` / `DeleteAll` |
| `UpdateAll` / `DeleteAll(expr)` | Depends on the `expr` you pass | Include the scope in the expression |
| Injecting `IObjectDAO<T>` directly | No | Route business code through the service interfaces |
| A Remote server proxy | Decided by the server-side implementation | Apply the same rules on the server |

The last row deserves a note: `LiteOrm.Remote` forwards the call to the server, and the data permission decision happens inside the server-side implementation. The client's job is not to send privilege information it should not send; the server's job is to stop assuming that a request is trustworthy because it came from an internal system.

## 7. Common mistakes

| Mistake | Consequence |
| --- | --- |
| Filtering lists only | A primary key is enough to reach other users' rows |
| Assuming `[ServicePermission]` blocks calls | The attribute is metadata; the call runs |
| Putting authorization only in `IServiceInvokingEvent` | Nested calls skip the event |
| Logging `Arguments` from a subscriber | Sensitive parameters land in the log |
| Giving admins an "unfiltered" overload and reusing it globally | Ordinary requests can reach it too |

## Related

- [Back to index](../README.md)
- [Permission filtering and user scope](../06-di/02-permission-filtering.en.md)
- [Three levels of multi-tenant isolation](./01-multi-tenancy.en.md)
- [Audit trails and change tracking](./03-audit-trail.en.md)
- [Logging and diagnostics](../06-di/03-logging.en.md)
- [Security](../03-advanced-topics/08-security.en.md)
