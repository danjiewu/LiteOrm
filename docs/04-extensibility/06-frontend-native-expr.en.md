# Frontend Native Expr Querying

This is also an **integration pattern**. Once a page outgrows "a few fixed filters and one sort order", the frontend can send LiteOrm native `Expr` JSON directly.

The recommended approach is to follow **the actual serialized shape produced by `JsonSerializer.Serialize<Expr>(...)`** instead of inventing a separate frontend-only DSL.

## Scenario guide

| Scenario | Recommended approach | Why |
|------|----------|------|
| Dynamic multi-condition querying | Native Expr | Fields, operators, and values can be combined at runtime |
| Switchable AND / OR logic | Native Expr | Composite logic stays explicit |
| Multi-column sorting | Native Expr | `OrderBys` supports it directly |
| Custom paging | Native Expr | `Skip` / `Take` are native |

## 1. Integration rules

If the frontend sends Expr directly, align on these rules first:

- frontend and backend share LiteOrm's expression model
- the frontend sends LiteOrm's real serialized shape
- the backend still injects permission and safety checks after deserialization

## 2. Actual JSON shape

LiteOrm serializes `SectionExpr -> OrderByExpr -> WhereExpr` into a shape like this:

```json
{
  "$section": {
    "$orderby": {
      "$where": null,
      "Where": {
        "$": "and",
        "Items": [
          {
            "$": "==",
            "Left": { "#": "Status" },
            "Right": { "@": "Pending" }
          },
          {
            "$": ">=",
            "Left": { "#": "TotalAmount" },
            "Right": { "@": 300 }
          }
        ]
      }
    },
    "OrderBys": [
      {
        "Field": { "#": "CreatedTime" },
        "Asc": false
      }
    ]
  },
  "Skip": 0,
  "Take": 5
}
```

The important rule is: `$section`, `$orderby`, and `$where` hold each segment's `Source`, while segment-specific properties stay at the same object level.

## 3. Frontend construction steps

1. Build the business logic expression.
2. Build the serialized `WhereExpr` shape.
3. Wrap it in the serialized `OrderByExpr` shape.
4. Wrap the whole result in the serialized `SectionExpr` shape.

## 4. JavaScript example

```javascript
const payload = {
    "$section": {
        "$orderby": {
            "$where": null,
            "Where": {
                "$": "contains",
                "Left": { "#": "CustomerName" },
                "Right": { "@": "Contoso" }
            }
        },
        "OrderBys": [
            { "Field": { "#": "CreatedTime" }, "Asc": false }
        ]
    },
    "Skip": 0,
    "Take": 5
};

const result = await demoApp.apiFetch("/api/orders/query/expr", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
});
```

## 5. Backend behavior

The backend typically accepts this JSON directly as `Expr`, then extracts:

- filters
- sort items
- paging settings

After that, it injects permission filtering. Non-admin users are automatically limited to their own orders.

### 5.1 Count caching

Native Expr queries also often need to return `total`, so the `Count` call is a good place for short-lived caching.

One practical pattern for a demo-style project is:

- build the final effective filter as an `Expr`
- use that `Expr` directly as the count cache key
- make the user-scope filter part of that effective filter
- invalidate old count entries by bumping a cache version after successful create, update, or delete operations

Because LiteOrm `Expr` already has structural `Equals/GetHashCode`, repeated paging requests with the same effective filter can reuse the same count result without converting the filter into JSON first.

### 5.2 Things to watch

- when total count is unaffected, `OrderBy`, `Skip`, and `Take` do not need to be part of the count cache key
- if user scope is injected dynamically, build the cache key only after that permission filter is applied
- this is meant to reduce repeated paging overhead, not to replace true aggregate caching
- in-memory cache is fine for the demo app; multi-instance deployments should move to a shared cache

## 6. Common mistakes

1. Using the `"$": "section"` / `Source` shape instead of LiteOrm's actual serialized output.
2. Putting `Skip` / `Take` inside the `$section` value instead of beside it.
3. Putting `OrderBys` inside the `$orderby` value instead of beside it.

## Related Links

- [Back to index](../README.md)
- [Permission filtering](../03-advanced-topics/06-permission-filtering.en.md)
- [Query guide](../02-core-usage/03-query-guide.en.md)
