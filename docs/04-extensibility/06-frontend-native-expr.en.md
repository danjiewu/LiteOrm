# Frontend Native Expr Query

This document is part of the **Extension Integration Guide**: when the frontend no longer needs "just a few fixed filter options" but needs to dynamically combine fields, operators, sorting, and pagination, you can submit LiteOrm's native `Expr` JSON directly.

The recommended approach is: **the frontend constructs JSON according to the actual output shape of `JsonSerializer.Serialize<Expr>(...)`**, rather than inventing a separate DSL just for frontend use.

## Choosing the Right Approach

| Scenario | Recommended Approach | Reason |
|----------|---------------------|--------|
| Dynamic multi-condition query | Native Expr | Fields, operators, and values can all be combined at runtime |
| Switchable AND / OR | Native Expr | Easier to express complex logic |
| Multi-column sorting | Native Expr | `OrderBys` directly supports multiple columns |
| Custom pagination | Native Expr | `Skip` / `Take` natively available |

## 1. Integration Principles

When submitting Expr directly from the frontend, it is recommended to establish these three conventions first:

- Frontend and backend share LiteOrm's expression semantics
- Frontend constructs JSON according to LiteOrm's actual serialization output
- Backend must still supplement permission and security validation after receiving

## 2. Current Actual JSON Shape

When LiteOrm serializes `SectionExpr -> OrderByExpr -> WhereExpr`, the output shape is approximately:

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

Key points:

1. The value of `$section` represents its `Source`
2. `Skip` / `Take` are at the same level
3. The value of `$orderby` represents its `Source`
4. `OrderBys` are at the same level
5. The value of `$where` represents its `Source`; can be `null` if no upstream segment
6. `Where` is at the same level

## 3. Frontend Construction Steps

### 3.1 First Generate Logic Expression

```javascript
const logicExpr = {
    "$": "and",
    "Items": [
        { "$": "==", "Left": { "#": "Status" }, "Right": { "@": "Pending" } },
        { "$": ">=", "Left": { "#": "TotalAmount" }, "Right": { "@": 300 } }
    ]
};
```

### 3.2 Construct `WhereExpr` Serialization Result

```javascript
let expr = {
    "$where": null,
    "Where": logicExpr
};
```

### 3.3 Construct `OrderByExpr` Serialization Result

```javascript
expr = {
    "$orderby": expr,
    "OrderBys": [
        { "Field": { "#": "CreatedTime" }, "Asc": false },
        { "Field": { "#": "TotalAmount" }, "Asc": true }
    ]
};
```

### 3.4 Wrap as `SectionExpr` Serialization Result

```javascript
expr = {
    "$section": expr,
    "Skip": 0,
    "Take": 5
};
```

## 4. JavaScript Call Example

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

## 5. What the Backend Does

The backend typically receives this JSON as `Expr`, then parses out:

- Filter conditions
- Sort items
- Pagination parameters

Then supplements permission filtering. For non-`Admin` users, the backend automatically appends:

```csharp
Expr.Prop(nameof(DemoOrder.CreatedByUserId)) == currentUser.Id
```

### 5.1 Count Caching

Native Expr queries also frequently need to return `total`, so it's also suitable for adding short-term caching to `Count`.

A suitable approach for the demo project:

- First organize the final effective filter conditions into `Expr`
- Use that `Expr` directly as the count cache key
- Include the converged filter conditions after user permission into the cache key semantics
- After successful create, update, or delete, uniformly invalidate old caches by bumping the cache version number

LiteOrm's `Expr` already implements structured `Equals/GetHashCode`, so native Expr filter conditions with the same structure can reuse the same count result during continuous pagination without needing to convert to JSON first for key generation.

### 5.2 Caveats

- `OrderBy`, `Skip`, `Take` do not affect the total count, so you can cache count based only on the final filter conditions
- If your native Expr allows injecting user scope, be sure to generate the cache key only after supplementing permission filtering
- This type of cache is more suitable for reducing database pressure during repeated pagination, not for replacing real statistical aggregation caching
- In-memory cache is sufficient for the demo; production environments with multiple instances need to switch to a shared caching solution

## 6. Common Mistakes

### 6.1 Writing `"$": "section"` / `Source` Directly

It is recommended to stay consistent with LiteOrm's actual serialization output rather than wrapping in an extra custom structure.

### 6.2 Putting `Skip` / `Take` Inside the `$section` Object

They should be at the same level as `$section`, not inside `$section`'s value.

### 6.3 Putting `OrderBys` Inside `$orderby`'s Value

`OrderBys` should be at the same level as `$orderby`; `$orderby`'s value only represents its `Source`.

## 7. Related Links

- [Back to docs hub](../README.md)
- [Permission Filtering](../03-advanced-topics/06-permission-filtering.en.md)
- [Query Guide](../02-core-usage/03-query-guide.en.md)
