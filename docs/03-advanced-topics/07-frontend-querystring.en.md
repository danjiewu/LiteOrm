# Frontend QueryString Query Integration

When query conditions are relatively regular and fit naturally into a URL, QueryString-based querying is the lightest way to connect a frontend list page to LiteOrm. `LiteOrm.WebDemo` uses this approach on its QueryString page: **the frontend builds URL parameters, and the backend converts them into LiteOrm Expr filters before executing the query.**

## Scenario guide

| Scenario | Recommended approach | Why |
|------|----------|------|
| Simple list filtering | QueryString | Easy to debug and share |
| Back-office list pages with fixed filters | QueryString | Lower frontend/backend complexity |
| Complex nested logic and dynamic condition groups | Native Expr | QueryString becomes too limited |
| Refresh/back navigation should keep filters | QueryString | State is visible in the URL |

## 1. Supported parameters

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

## 2. Frontend flow

### 2.1 Build parameters with `URLSearchParams`

```javascript
const params = new URLSearchParams();
params.set("keyword", "Contoso");
params.set("status", "Pending");
params.set("sortBy", "CreatedTime");
params.set("desc", "true");
params.set("page", "1");
params.set("pageSize", "5");
```

### 2.2 Send the request

```javascript
const result = await demoApp.apiFetch(`/api/orders/query?${params.toString()}`);
```

To load summary data, you can reuse the same filter set with:

```javascript
const stats = await demoApp.apiFetch(`/api/orders/stats?${params.toString()}`);
```

## 3. Response shape

The query API returns:

| Field | Meaning |
|------|------|
| `page` / `pageSize` | Current page and page size |
| `total` | Total matching record count |
| `items` | Current page items |
| `sql` | Latest executed SQL |

The stats API returns aggregate values plus SQL.

## 4. Interaction with permission filtering

QueryString is only a transport format. Authorization is still enforced on the backend:

- `admin` can view all data
- non-admin users are automatically scoped to their own orders
- `onlyMine=true` lets an admin intentionally narrow to self-owned data

## 5. Common mistakes

1. Manually concatenating strings instead of using `URLSearchParams`.
2. Ignoring `total` and therefore breaking paging UX.
3. Forcing complex grouped conditions into QueryString instead of switching to native Expr.

## 6. Next steps

- [Back to index](../README.md)
- [Permission filtering](./06-permission-filtering.en.md)
- [Frontend native Expr querying](./08-frontend-native-expr.en.md)
- [Query guide](../02-core-usage/03-query-guide.en.md)
