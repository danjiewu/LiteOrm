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
using static LiteOrm.Common.Expr;
var cteDef = new SelectExpr(
    From(typeof(User)),
    Prop("Id").As("Id"),
    Prop("UserName").As("Name"),
    Prop("Age").As("Age")
);

var query = cteDef.With("ActiveUsers")
    .Where(Prop("Age") >= 18)
    .OrderBy(Prop("Name").Asc())
    .Select(Prop("Name"), Prop("Age"));
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
using static LiteOrm.Common.Expr;
var cteDef = From<User>()
    .Where(Prop("Age") >= 25)
    .GroupBy(Prop("DeptId"))
    .Select(
        Prop("DeptId"),
        Prop("Id").Count().As("UserCount"),
        Prop("Age").Avg().As("AvgAge")
    );

var query = cteDef.With("DeptAdultStats")
    .Where(Prop("UserCount") >= 2)
    .OrderBy(Prop("UserCount").Desc())
    .Select(Prop("DeptId"), Prop("UserCount"), Prop("AvgAge"));
```

## 4. Reusing the same CTE in a UNION

CTE can also be reused on both sides of a `UNION` / `UNION ALL` query:

```csharp
using static LiteOrm.Common.Expr;
var adultUsers = From<User>()
    .Where(Prop("Age") >= 18)
    .Select(
        Prop("UserName").As("Name"),
        Prop("Age").As("Age"))
    .With("AdultUsers");

var query = adultUsers
    .Where(Prop("Age") < 30)
    .Select(Prop("Name"), Prop("Age"), Const("18-29").As("AgeGroup"))
    .UnionAll(
        adultUsers
            .Where(Prop("Age") >= 30)
            .Select(Prop("Name"), Prop("Age"), Const("30+").As("AgeGroup")));
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

## 5.1 Recursive CTE

Recursive CTEs are used for hierarchical / tree-structured data (org charts, category trees, path finding, etc.). LiteOrm decides whether to emit the `RECURSIVE` keyword based on the database's `ExplicitRecursive` property.

### Writing a recursive CTE

To write a recursive CTE, reference the CTE's own alias within its definition. Create an alias-only `CommonTableExpr` for the self-reference:

```csharp
using static LiteOrm.Common.Expr;

// Anchor part: query root nodes
var anchor = From<Category>()
    .Where(Prop("ParentId") == 0)
    .Select(
        Prop("Id").As("Id"),
        Prop("ParentId").As("ParentId"),
        Prop("Name").As("Name"),
        Const(1).As("Level"));

// Recursive part: join children via self-referencing alias
var selfRef = new CommonTableExpr { Alias = "CategoryTree" };
var recursive = From<Category>()
    .Join(selfRef, Prop("ParentId") == Prop("CategoryTree", "Id"))
    .Select(
        Prop("Id").As("Id"),
        Prop("ParentId").As("ParentId"),
        Prop("Name").As("Name"),
        Prop("CategoryTree", "Level") + 1);

// Combine anchor and recursive parts, wrap as CTE
var cteDef = anchor.UnionAll(recursive);
var query = cteDef.With("CategoryTree")
    .Select(Prop("Id"), Prop("Name"), Prop("Level"));
```

### SQL generation behaviour

Whether the `RECURSIVE` keyword is appended after `WITH` depends solely on the target database's `SqlBuilder.ExplicitRecursive` property:

- `ExplicitRecursive` is `true`: all CTEs use `WITH RECURSIVE` (whether recursive or not)
- `ExplicitRecursive` is `false`: all CTEs use `WITH` (no `RECURSIVE`)

Default `ExplicitRecursive` values by database:

| Database | `ExplicitRecursive` | Notes |
|----------|---------------------|-------|
| MySQL / TiDB / OceanBase / GreatDB | `true` | uses `WITH RECURSIVE` |
| PostgreSQL / KingbaseES / GaussDB | `true` | uses `WITH RECURSIVE` |
| SQLite | `true` | uses `WITH RECURSIVE` |
| SQL Server | `false` | no `RECURSIVE` keyword needed |
| Oracle / DM (Dameng) | `false` | no `RECURSIVE` keyword needed |

For databases where `ExplicitRecursive` is `true`, the generated SQL looks like:

```sql
WITH RECURSIVE [CategoryTree] AS (
    SELECT [Id], [ParentId], [Name], 1 AS [Level]
    FROM [Categories]
    WHERE [ParentId] = 0
    UNION ALL
    SELECT ... FROM [Categories] JOIN [CategoryTree] ON ...
)
SELECT [Id], [Name], [Level]
FROM [CategoryTree]
```

For databases where `ExplicitRecursive` is `false` (e.g., SQL Server), only `WITH` is emitted (no `RECURSIVE`).

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

### 7.2 Supported approach: write WITH fragment manually

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

Here the `WITH ...` part is handwritten SQL, LiteOrm only continues handling interpolated parameters.

It can also be constructed by inserting a `SelectExpr`:

```csharp
using static LiteOrm.Expr;

Expr cteDef = From(typeof(User))
    .Select(
    Prop("Id"),
    Prop("UserName"),
    Prop("Age")
    ).Where(Prop("Age") >= 18);

var result = await dataViewDAO.Search(
    $"""
    WITH ActiveUsers AS (
        {cteDef}
    )
    SELECT Id, UserName, Age
    FROM ActiveUsers
    """,
    isFull: true
).GetResultAsync();
```

## 8. Related reading

- [Query Overview](./04-query-overview.en.md)
- [Lambda Guide](./05-lambda-guide.en.md)
- [Expr Guide](./06-expr-guide.en.md)
- [ExprString Guide](./07-exprstring-guide.en.md)
- [Lambda & Expr Mixing](./09-lambda-expr-mixing.en.md)
- [AI Guide](../05-reference/05-ai-guide.en.md)
- [Back to docs hub](../README.md)

