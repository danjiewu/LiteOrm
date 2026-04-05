# Frontend Native Expr Querying

When queries move beyond a few fixed fields and a single sort order, sending LiteOrm native Expr JSON from the frontend is much more flexible. `LiteOrm.WebDemo` now follows **the actual output shape produced by `JsonSerializer.Serialize<Expr>(...)`** instead of inventing a separate frontend-only format.

## Scenario guide

| Scenario | Recommended approach | Why |
|------|----------|------|
| Dynamic multi-condition querying | Native Expr | Fields, operators, and values can be combined at runtime |
| Switchable AND / OR logic | Native Expr | Composite logic stays explicit |
| Multi-column sorting | Native Expr | `OrderBys` supports it directly |
| Custom paging | Native Expr | `Skip` / `Take` are native |

## 1. Actual JSON shape

LiteOrm serializes `SectionExpr -> OrderByExpr -> WhereExpr` into a shape like this:

```json
{
  "$section": {
    "$order": {
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

The important rule is: `$section`, `$order`, and `$where` hold each segment's `Source`, while segment-specific properties stay at the same object level.

## 2. Frontend construction steps

1. Build the business logic expression.
2. Build the serialized `WhereExpr` shape.
3. Wrap it in the serialized `OrderByExpr` shape.
4. Wrap the whole result in the serialized `SectionExpr` shape.

## 3. JavaScript example

```javascript
const payload = {
    "$section": {
        "$order": {
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

## 4. Backend behavior

`LiteOrm.WebDemo` accepts this JSON directly as `Expr`, then extracts:

- filters
- sort items
- paging settings

After that, it injects permission filtering. Non-admin users are automatically limited to their own orders.

## 5. Common mistakes

1. Using the `"$": "section"` / `Source` shape instead of LiteOrm's actual serialized output.
2. Putting `Skip` / `Take` inside the `$section` value instead of beside it.
3. Putting `OrderBys` inside the `$order` value instead of beside it.

## 6. Next steps

- [Back to index](../README.md)
- [Permission filtering](./06-permission-filtering.en.md)
- [Frontend QueryString querying](./07-frontend-querystring.en.md)
- [Query guide](../02-core-usage/03-query-guide.en.md)
