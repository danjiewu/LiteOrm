# Lambda Guide

Lambda is the most intuitive query style in LiteOrm — strongly typed, best readability, and well-suited to most business queries. This page covers filtering, sorting, parameterization, the conditional operator, and subqueries in Lambda queries.

For a comparison of the three query styles and selection guidance, see [Query Overview](./04-query-overview.en.md). For dynamic condition assembly, see the [Expr Guide](./06-expr-guide.en.md). For handwritten SQL in the DAO, see the [ExprString Guide](./07-exprstring-guide.en.md).

## 1. Basic filters

```csharp
var users = await userService.SearchAsync(u => u.Age >= 18);
var users = await userService.SearchAsync(u => u.UserName.Contains("admin"));
var users = await userService.SearchAsync(u => new[] { 1, 2, 3 }.Contains(u.Id));
```

Property access inside a Lambda is parsed into `PropertyExpr`; comparison/string/set operations are parsed into `LogicExpr`; everything then goes through the unified `Expr` → SQL pipeline.

## 2. Sorting

Lambda queries support sorting through `OrderBy` / `OrderByDescending` / `ThenBy` / `ThenByDescending` chain calls.

### 2.1 Single-column sorting

```csharp
// Sort by creation time ascending
var users = await userService.SearchAsync(
    q => q.OrderBy(u => u.CreateTime)
);

// Sort by age descending
var users = await userService.SearchAsync(
    q => q.OrderByDescending(u => u.Age)
);
```

### 2.2 Multi-column sorting (ThenBy)

```csharp
// Sort by department ascending, then by creation time descending within the same department
var users = await userService.SearchAsync(
    q => q.OrderBy(u => u.DeptId)
          .ThenByDescending(u => u.CreateTime)
);
```

`ThenBy` / `ThenByDescending` must follow `OrderBy` / `OrderByDescending`. You can chain multiple calls.

### 2.3 Sorting with paging

```csharp
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderByDescending(u => u.CreateTime)
          .Skip(0)
          .Take(20)
);
```

### 2.4 Sorting by computed expressions

```csharp
// Sort by computed field
var users = await userService.SearchAsync(
    q => q.OrderBy(u => u.FirstName + " " + u.LastName)
);

// Sort by time difference
var users = await userService.SearchAsync(
    q => q.OrderByDescending(u => (DateTime.Now - u.CreateTime).TotalMilliseconds)
);
```

> String `+` inside a Lambda is converted to concat during parsing, and ultimately rendered via `SqlBuilder.BuildConcatSql` as `CONCAT(a,b,...)` or `a || b` per dialect. When handwriting `Expr`, you must use `.Concat(...)` explicitly — see the [Expr Guide](./06-expr-guide.en.md#string-concatenation-do-not-use--use-concat).

## 3. Variable capture and parameterization

```csharp
var keyword = "admin";
var users = await userService.SearchAsync(u => u.UserName.Contains(keyword));
```

Variables declared outside the Lambda are parameterized. For values such as `DateTime.Now`, assign them to a variable first if you want them parameterized:

```csharp
var now = DateTime.Now;
var users = await userService.SearchAsync(u => u.CreateTime <= now);
```

## 4. The conditional operator becomes `CASE`

```csharp
var users = await userService.SearchAsync(
    u => (u.Age >= 18 ? "Adult" : "Minor") == "Adult"
);
```

This kind of Lambda is first converted into `Expr.If(...)`, then rendered as SQL `CASE WHEN ... THEN ... ELSE ... END`. Multi-branch `CASE` can be built with `Expr.Case(...)` — see the [Expr Guide](./06-expr-guide.en.md#14-type-conversion-and-conditional-values).

## 5. `Exists` and `ExistsRelated`

### 5.1 Explicit `Exists`

```csharp
using static LiteOrm.Common.Expr;

var users = await userService.SearchAsync(
    u => Exists<Department>(d => d.Id == u.DeptId && d.Name == "R&D")
);
```

Use this when you want to control the correlation condition yourself. `Exists<T>` is an `Expr` static method; the equivalent `Expr` form is shown in the [Expr Guide](./06-expr-guide.en.md#21-explicit-exists).

### 5.2 Auto-related `ExistsRelated`

```csharp
using static LiteOrm.Common.Expr;

var users = await userService.SearchAsync(
    u => ExistsRelated<DepartmentView>(d => d.Name == "R&D")
);
```

`ExistsRelated` fills in the relation condition from metadata such as `ForeignType` and `TableJoin`. Use this when relationships are already declared in the model and you only want to filter the main table by related-table conditions. For matching rules, inheritance behavior, and `ConstFilter` interaction, see [Associations](./08-associations.en.md).

## 6. Related links

- [Query Overview](./04-query-overview.en.md)
- [Expr Guide](./06-expr-guide.en.md)
- [ExprString Guide](./07-exprstring-guide.en.md)
- [CRUD Guide](./03-crud-guide.en.md)
- [Associations](./08-associations.en.md)
- [Mixing Lambda and Expr](./09-lambda-expr-mixing.en.md)
- [CTE Guide](./10-cte-guide.en.md)
- [Expression Extension](../04-extensibility/01-expression-extension.en.md)
