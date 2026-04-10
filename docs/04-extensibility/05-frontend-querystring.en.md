# Frontend QueryString Querying

This is not a built-in LiteOrm query syntax. It is an **integration pattern**: the frontend sends filters through the query string, and the backend converts those parameters into LiteOrm `Expr` before running the query.

It works best when filters are relatively stable and the UI benefits from shareable, refreshable, back-button-friendly URLs.

## Scenario guide

| Scenario | Recommended approach | Why |
|------|----------|------|
| Simple list filtering | QueryString | Easy to debug and share |
| Back-office list pages with fixed filters | QueryString | Lower frontend/backend complexity |
| Complex nested logic and dynamic condition groups | Native Expr | QueryString becomes too limited |
| Refresh/back navigation should keep filters | QueryString | State is visible in the URL |

## 1. Integration rules

Before exposing this kind of endpoint, align on three rules:

- the frontend only sends simple, serializable filter parameters
- the backend owns the conversion from parameters to `Expr`
- permission rules, sort-field whitelists, and paging guardrails stay on the backend

If the UI already needs grouped AND / OR logic, multi-column dynamic sorting, or a visual condition builder, switch to frontend native Expr instead.

## 2. Supported parameters

`GET /api/orders/query` in `LiteOrm.WebDemo` commonly uses:

| Parameter | Purpose |
|------|------|
| `keyword` | Matches order number, customer name, and product name |
| `status` | Order status |
| `departmentName` | Department name contains |
| `createdByUserName` | Creator display name contains |
| `minTotalAmount` / `maxTotalAmount` | Amount range |
| `createdFrom` / `createdTo` | Created time range |
| `sortBy` | Sort field |
| `desc` | Descending flag |
| `page` / `pageSize` | Paging parameters |
| `onlyMine` | Force “my orders only” |

## 3. Frontend flow

### 3.1 Build parameters with `URLSearchParams`

```javascript
const params = new URLSearchParams();
params.set("keyword", "Contoso");
params.set("status", "Pending");
params.set("sortBy", "CreatedTime");
params.set("desc", "true");
params.set("page", "1");
params.set("pageSize", "5");
```

### 3.2 Send the request

```javascript
const result = await demoApp.apiFetch(`/api/orders/query?${params.toString()}`);
```

To load summary data, you can reuse the same filter set with:

```javascript
const stats = await demoApp.apiFetch(`/api/orders/stats?${params.toString()}`);
```

## 4. What the backend should own

The frontend only transports parameters. The backend should still centralize the actual query rules, including:

- converting `keyword`, ranges, and sorting parameters into `Expr`
- validating sort fields against a whitelist
- injecting permission filters
- applying default paging and maximum page size rules

That keeps list, stats, and export endpoints aligned on the same query behavior.

## 5. Response shape

The query API returns:

| Field | Meaning |
|------|------|
| `page` / `pageSize` | Current page and page size |
| `total` | Total matching record count |
| `items` | Current page items |
| `sql` | Latest executed SQL |

The stats API returns aggregate values plus SQL.

## 6. Interaction with permission filtering

QueryString is only a transport format. Authorization is still enforced on the backend:

- `admin` can view all data
- non-admin users are automatically scoped to their own orders
- `onlyMine=true` lets an admin intentionally narrow to self-owned data

## 7. Common mistakes

1. Manually concatenating strings instead of using `URLSearchParams`.
2. Ignoring `total` and therefore breaking paging UX.
3. Forcing complex grouped conditions into QueryString instead of switching to native Expr.

## Related Links

- [Back to index](../README.md)
- [Permission filtering](../03-advanced-topics/06-permission-filtering.en.md)
- [Frontend native Expr querying](./06-frontend-native-expr.en.md)
- [Query guide](../02-core-usage/03-query-guide.en.md)
