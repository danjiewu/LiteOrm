# Query Guide

LiteOrm supports three main query styles: Lambda, `Expr`, and `ExprString`.

## 1. Which style should I choose?

| Style | Best for |
|------|----------|
| Lambda | Most day-to-day queries |
| `Expr` | Dynamic conditions, query builders, admin search screens |
| `ExprString` | Small custom SQL fragments only |

## 2. Lambda queries

```csharp
var adults = await userService.SearchAsync(u => u.Age >= 18);

var page = await userService.SearchAsync(
    q => q.Where(u => u.Status == 1)
          .OrderByDescending(u => u.CreateTime)
          .Skip(0).Take(20)
);
```

### EXISTS

```csharp
var users = await userService.SearchAsync(
    u => Expr.Exists<Order>(o => o.UserId == u.Id && o.Status == 1)
);
```

### Mixing `Expr` into Lambda

When you already have a dynamically built `Expr` but still want to keep a Lambda outside, use `To<T>()` to satisfy type checking.

```csharp
var users = await userService.SearchAsync(
    u => u.Age >= 18 && Expr.Prop("Name").Contains("John").To<bool>()
);
```

## 3. `Expr` queries

```csharp
LogicExpr condition = null;

if (minAge.HasValue)
    condition &= Expr.Prop("Age") >= minAge.Value;

if (!string.IsNullOrWhiteSpace(keyword))
    condition &= Expr.Prop("UserName").Contains(keyword);

var users = await userViewDAO.Search(condition).ToListAsync();
```

### Build from `QueryString` or `Dictionary`

```csharp
public static LogicExpr BuildUserSearch(IReadOnlyDictionary<string, string?> query)
{
    LogicExpr condition = null;

    if (query.TryGetValue("minAge", out var minAgeText) && int.TryParse(minAgeText, out var minAge))
        condition &= Expr.Prop("Age") >= minAge;

    if (query.TryGetValue("keyword", out var keyword) && !string.IsNullOrWhiteSpace(keyword))
        condition &= Expr.Prop("UserName").Contains(keyword);

    return condition;
}
```

## 4. `Expr.ExistsRelated(...)`

`Expr.ExistsRelated(...)` does not define a relationship. It uses an existing relationship to build an `EXISTS` filter.

```csharp
var expr = Expr.ExistsRelated<DepartmentView>(Expr.Prop("Name") == "R&D");
var users = await userService.SearchAsync(expr);
```

## 5. `ExprString`

Use `ExprString` only for small custom SQL fragments.

```csharp
var result = await userViewDAO.Search(
    $"WHERE {Expr.Prop("Status") == 1} ORDER BY CreateTime DESC"
).ToListAsync();
```

## Related Links

- [Back to English docs hub](../SUMMARY.en.md)
- [Associations Guide](./05-associations.en.md)
- [SQL Examples](../05-reference/07-sql-examples.en.md)
