# CTE Guide

LiteOrm supports Common Table Expressions (CTEs, `WITH` clauses) through `SelectExpr.With(name)`. This page explains when to use CTEs, how to build them, and where the boundary is between CTE expressions and `ExprString`.

## 1. When to use CTE

CTE works well when:

- the same subquery result needs to be referenced multiple times
- a complex query is easier to understand when split into named steps
- you want to stay in the structured `Expr` / `SelectExpr` model instead of writing the whole SQL manually

For one-off subqueries or simple filtering/paging, plain `Expr` / `SelectExpr` is usually enough.

## 2. Basic usage

Define a `SelectExpr`, then wrap it with `.With(name)`:

```csharp
var cteDef = new SelectExpr(
    Expr.From(typeof(User)),
    Expr.Prop("Id").As("Id"),
    Expr.Prop("UserName").As("Name"),
    Expr.Prop("Age").As("Age")
);

var query = cteDef.With("ActiveUsers")
    .Where(Expr.Prop("Age") >= 18)
    .OrderBy(Expr.Prop("Name").Asc())
    .Select(Expr.Prop("Name"), Expr.Prop("Age"));
```

Generated SQL shape:

```sql
WITH [ActiveUsers] AS (
    SELECT [Id] AS [Id], [UserName] AS [Name], [Age] AS [Age]
    FROM [Users]
)
SELECT [Name], [Age]
FROM [ActiveUsers]
WHERE [Age] >= 18
ORDER BY [Name]
```

## 3. Aggregate CTE

CTE is a good fit for "aggregate first, filter later":

```csharp
var cteDef = Expr.From<User>()
    .Where(Expr.Prop("Age") >= 25)
    .GroupBy(Expr.Prop("DeptId"))
    .Select(
        Expr.Prop("DeptId"),
        Expr.Prop("Id").Count().As("UserCount"),
        Expr.Prop("Age").Avg().As("AvgAge")
    );

var query = cteDef.With("DeptAdultStats")
    .Where(Expr.Prop("UserCount") >= 2)
    .OrderBy(Expr.Prop("UserCount").Desc())
    .Select(Expr.Prop("DeptId"), Expr.Prop("UserCount"), Expr.Prop("AvgAge"));
```

## 4. Reusing the same CTE in a UNION

CTE can also be reused on both sides of a `UNION` / `UNION ALL` query:

```csharp
var adultUsers = Expr.From<User>()
    .Where(Expr.Prop("Age") >= 18)
    .Select(
        Expr.Prop("UserName").As("Name"),
        Expr.Prop("Age").As("Age"))
    .With("AdultUsers");

var query = adultUsers
    .Where(Expr.Prop("Age") < 30)
    .Select(Expr.Prop("Name"), Expr.Prop("Age"), Expr.Const("18-29").As("AgeGroup"))
    .UnionAll(
        adultUsers
            .Where(Expr.Prop("Age") >= 30)
            .Select(Expr.Prop("Name"), Expr.Prop("Age"), Expr.Const("30+").As("AgeGroup")));
```

The important part is:

- store the result of `With("AdultUsers")`
- keep building multiple branches from the same `CommonTableExpr`
- SQL generation still keeps only one `WITH AdultUsers AS (...)` definition

## 5. Validation rules for duplicate CTE aliases

LiteOrm now collects all CTEs in the expression tree and validates them by alias:

- Same alias with **equal definitions**: deduplicated automatically, only the first definition is kept in `WITH`
- Same alias with **different definitions**: throws `InvalidOperationException`
- Alias-only reference without a prior full definition: throws

So you can safely reuse the same CTE expression multiple times, or reuse the same alias across a large expression tree, as long as the definition stays identical.

## 6. CTE serialization rules

When an `Expr` / `SelectExpr` tree is serialized to JSON:

- the first CTE with a given alias is serialized in full
- later equivalent references serialize as alias-only

Example of a later compressed reference:

```json
{"$cte":"ActiveUsers"}
```

LiteOrm restores it back to the first full definition during deserialization.

## 7. `ExprString` boundary for CTE

`ExprString` **does not support expanding a CTE structure from Expr objects automatically**. In other words:

- `SelectExpr.With(name)` / `CommonTableExpr` belongs to the structured `Expr` / `SelectExpr` model
- `ExprString` is for regular `Expr` fragments or handwritten SQL fragments
- if you need `WITH` while using `ExprString`, you must **write the WITH part manually**

### 7.1 Unsupported idea

This does not work as a "CTE Expr fragment" pattern:

```csharp
var cteQuery = cteDef.With("ActiveUsers");
// Not supported: cteQuery cannot be auto-expanded into WITH SQL inside ExprString
```

### 7.2 Supported approach: write full SQL manually

If your scenario must use raw DAO SQL with `ExprString`, write the `WITH` clause yourself:

```csharp
int minAge = 18;

var result = await dataViewDAO.Search(
    $"""
    WITH ActiveUsers AS (
        SELECT Id, UserName, Age
        FROM Users
        WHERE Age >= {minAge}
    )
    SELECT Id, UserName, Age
    FROM ActiveUsers
    """,
    isFull: true
).GetResultAsync();
```

Here the `WITH ...` part is handwritten SQL; LiteOrm only continues handling interpolated parameters.

## 8. Related reading

- [Query Guide](./03-query-guide.en.md)
- [Lambda & Expr Mixing](./06-lambda-expr-mixing.en.md)
- [AI Guide](../05-reference/05-ai-guide.en.md)
- [Back to docs hub](../README.md)
