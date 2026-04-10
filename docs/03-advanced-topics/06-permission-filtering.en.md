# Permission Filtering and User Scope Control

When a system needs to demonstrate rich querying while preventing regular users from reading or modifying data they do not own, permission filtering must live on the backend rather than in the frontend alone. This document shows a practical LiteOrm pattern: **inject the user-scope condition before `Search` / `Count`, and add explicit checks for detail, update, and delete operations.**

## Scenario Matrix

| Scenario | Recommended approach | Why |
|------|----------|------|
| Admin views all orders | Do not inject a user-scope filter | Keeps the full operational view |
| Regular user queries lists and stats | Automatically append `CreatedByUserId == currentUser.Id` | Ensures consistent visibility boundaries |
| Regular user reads detail, updates, deletes | Add an explicit access check at the endpoint layer | Prevents direct URL access from bypassing list filtering |
| Frontend hints | Use hints only, not final authorization | Backend authorization must remain authoritative |

## 1. Filtering behavior in WebDemo

### 1.1 QueryString queries and stats

`GET /api/orders/query` and `GET /api/orders/stats` first build business filters, then append the current-user scope:

```csharp
if (request.OnlyMine == true || !IsAdmin(currentUser))
{
    filter &= Expr.Prop(nameof(DemoOrder.CreatedByUserId)) == currentUser.Id;
}
```

The important part is that the permission rule becomes part of the query itself rather than an in-memory post-filter.

### 1.2 Expr queries

`POST /api/orders/query/expr` applies the same idea to LiteOrm Expr JSON built with the native `Source` chain:

```csharp
filter ??= Expr.Prop(nameof(DemoOrder.Id)) > 0;

if (!IsAdmin(currentUser))
{
    filter &= Expr.Prop(nameof(DemoOrder.CreatedByUserId)) == currentUser.Id;
}
```

This keeps QueryString and native Expr flows aligned on the same permission boundary.

### 1.3 Detail, update, and delete

List filtering is not enough. You should also check access for:

- `GET /api/orders/{id}`
- `PUT /api/orders/{id}`
- `DELETE /api/orders/{id}`

Returning a clear `403` is recommended so the frontend can distinguish “forbidden” from “not found”.

## 2. Recommended implementation pattern

### 2.1 Inject permission into Expr before execution

Recommended:

```csharp
var filter = BuildBusinessFilter(request);

if (!IsAdmin(currentUser))
{
    filter &= Expr.Prop(nameof(Order.CreatedByUserId)) == currentUser.Id;
}

var result = await orderService.SearchAsync(
    Expr.From<OrderView>()
        .Where(filter)
        .OrderBy(Expr.Prop(nameof(Order.CreatedTime)).Desc())
        .Section(0, 20)
);
```

Avoid loading broad results and then trimming them in memory, because that breaks totals, paging, and data boundaries.

### 2.2 Combine list filtering with object-level checks

Even if a list only returns “my items”, direct requests to `/api/orders/{id}` still need explicit authorization checks.

## 3. Frontend guidance

- Tell regular users that results are automatically scoped to the current account.
- Handle `403` as “you do not have access to this order”.
- Do not rely on hidden buttons as the actual permission boundary.

## 4. Common mistakes

1. Enforcing permissions only in the frontend.
2. Filtering lists but not detail/update/delete endpoints.
3. Scattering permission rules across controllers instead of centralizing them in query-building logic.

## 5. Next steps

- [Back to index](../README.md)
- [Frontend QueryString querying](../04-extensibility/05-frontend-querystring.en.md)
- [Frontend native Expr querying](../04-extensibility/06-frontend-native-expr.en.md)
- [Associations](../02-core-usage/05-associations.en.md)
- [LiteOrm.WebDemo](../../LiteOrm.WebDemo/)
