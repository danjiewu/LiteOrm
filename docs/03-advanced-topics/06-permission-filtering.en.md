# Permission Filtering and User Scope Control

When a system needs to demonstrate rich querying while preventing regular users from reading or writing data they do not own, permission filtering cannot stop at the frontend UI layer. This document explains the recommended LiteOrm approach: **inject user-scope conditions into `Expr` before `Search` / `Count`, and add explicit access checks for detail, update, and delete operations.**

## Scenario Matrix

| Scenario | Recommended approach | Why |
|------|----------|------|
| Admin views all orders | No user-scope filter attached | Preserves full operational/audit perspective |
| Regular user queries lists and counts | Auto-append `CreatedByUserId == currentUser.Id` | Consistently limits visible scope |
| Regular user reads detail, updates, or deletes | Explicit access check at the endpoint layer | Prevents bypassing list filtering |
| Frontend UI hints | Hints only, not final authorization | Backend authorization is authoritative |

## 1. Filtering Behavior in WebDemo

### 1.1 QueryString queries and counts

`GET /api/orders/query` and `GET /api/orders/stats` build business filters first, then append scope conditions based on the current user role:

```csharp
if (request.OnlyMine == true || !IsAdmin(currentUser))
{
    filter &= Expr.Prop(nameof(DemoOrder.CreatedByUserId)) == currentUser.Id;
}
```

The key point: **permission conditions are part of the query itself**, not an in-memory trim applied after results return.

### 1.2 Expr queries

`POST /api/orders/query/expr` follows the same pattern, injecting the current user's scope into the native Expr before `SearchAsync` / `CountAsync`:

```csharp
filter ??= Expr.Prop(nameof(DemoOrder.Id)) > 0;

if (!IsAdmin(currentUser))
{
    filter &= Expr.Prop(nameof(DemoOrder.CreatedByUserId)) == currentUser.Id;
}
```

This ensures that whether the frontend uses a visual builder or submits a native `Source` chain Expr JSON directly, the backend permission boundary stays consistent.

### 1.3 Detail, update, and delete

List filtering does not replace object-level access control. Explicit access checks are still required for:

- `GET /api/orders/{id}`
- `PUT /api/orders/{id}`
- `DELETE /api/orders/{id}`

Returning a clear `403` is recommended so the frontend can distinguish "forbidden" from "not found".

## 2. Recommended Implementation

### 2.1 Inject permission into Expr, not post-query trimming

**Recommended:**

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

**Avoid:**

```csharp
var items = await orderService.SearchAsync(expr);
var myItems = items.Where(x => x.CreatedByUserId == currentUser.Id).ToList();
```

The second approach creates three problems:

1. `Count` and pagination totals become inaccurate.
2. Unfiltered aggregations, statistics, or exports remain possible.
3. The query layer has already read data that should not have been accessed.

### 2.2 List filtering and detail checks must coexist

A common misconception: since the list only returns "my items", the detail endpoint does not need to verify access. As long as callers can manually construct a URL to request `GET /api/orders/1`, the detail endpoint must re-check whether the current user has permission to access that object.

## 3. Frontend Guidance

- Clearly indicate to regular users that "query results are automatically filtered to the current account scope."
- When encountering `403`, display "the current user does not have access to this data" rather than incorrectly reporting "record not found."
- Do not rely on hidden buttons in the frontend for permission control; button hiding is a UX optimization, not a security boundary.

## 4. Common Mistakes

### 4.1 Permission control only in the frontend

The frontend can hide buttons, but this cannot serve as the final authorization basis. The true permission boundary must be on the backend.

### 4.2 Restricting only lists, not detail and delete

As long as detail, update, and delete endpoints lack verification, users can still directly access objects they do not own.

### 4.3 Hard-coding permission filters in controllers

It is better to consolidate "user-scope conditions" into the service layer's query-building logic. This allows QueryString, Expr, statistics, and exports to share the same rules.

## Related Links

- [Back to index](../README.md)
- [Associations](../02-core-usage/05-associations.en.md)
- [Lambda & Expr Mixing](../02-core-usage/06-lambda-expr-mixing.en.md)
