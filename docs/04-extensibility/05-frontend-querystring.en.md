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

An endpoint such as `GET /api/orders/query` commonly uses:

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

### 4.1 Count caching

If a list API always returns `total`, the backend usually needs an extra `Count` query. When the same filters and pages are requested repeatedly, that count query is a good candidate for short-lived caching.

One practical approach for a demo-style project is:

- cache only the `Count` result, not the page data
- use the **final effective `Expr` object itself** as the cache key, together with the current cache version for validity checks
- bump the cache version after successful create, update, or delete operations so old count entries become invalid
- keep the TTL short so demo data does not stay stale for long

### 4.2 Implementation approach

A practical pattern looks like this:

1. build the full filter in the service layer
2. use the final `Expr` directly as the `IMemoryCache` key
3. rely on LiteOrm's structural `Equals/GetHashCode` implementation for cache hits
4. try `IMemoryCache` first
5. on a miss, run `CountAsync(...)` and store the result
6. invalidate by version after successful writes

This keeps count caching scoped to "the same user + the same effective filter" and avoids cross-user reuse without converting the filter into JSON first.

### 4.3 Things to watch

- cache `total`, not the actual page items
- make sure the user-scope filter is part of the cache key
- in-process memory cache is fine for a demo app; multi-instance deployments should switch to distributed cache or skip this optimization
- version-based invalidation is simple and safe, but it expires all count entries together, which is acceptable for demos and small admin apps, not ideal for very high write rates

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
- [LiteOrm.WebDemo](../../LiteOrm.WebDemo/)
