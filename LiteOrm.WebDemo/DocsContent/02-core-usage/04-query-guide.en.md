# Query Guide

LiteOrm supports three main query styles: Lambda, `Expr`, and `ExprString`.  
Lambda is also converted into `Expr` first and then translated into SQL through the same pipeline.  
This page focuses on **how to choose between them** and the most common query entry points. If you want the full `Expr` construction model, static methods, extension methods, and composition semantics, start with the [Expr Guide](./03-expr-guide.en.md).

## 1. Comparing the three query styles

| Style | Syntax | Best for | Type safety |
|------|------|--------|----------|
| Lambda | `u => u.Age > 18` | Fixed conditions and clear business intent | ✅ Strong |
| `Expr` | `Expr.Prop("Age") > 18` | Dynamic composition, query builders, admin filtering | ✅ Compile-time |
| `ExprString` | `$"WHERE {expr}"` | DAO-side condition fragments or full SQL | ❌ Runtime |

### 1.1 Practical guidance

- **Use Lambda by default**: it is the clearest choice for most business queries.
- **Use `Expr` when conditions must be accumulated dynamically**: admin filters, frontend query builders, reusable cross-layer filters.
- **Use `ExprString` when the DAO layer needs handwritten SQL**: it can represent either a `Search` condition fragment or a full SQL statement, but Service APIs do not expose this entry point.

## 2. Lambda query entry points

### 2.1 Basic filters

```csharp
var users = await userService.SearchAsync(u => u.Age >= 18);
var users = await userService.SearchAsync(u => u.UserName.Contains("admin"));
var users = await userService.SearchAsync(u => new[] { 1, 2, 3 }.Contains(u.Id));
```

### 2.2 Sorting

Lambda queries support sorting through `OrderBy` / `OrderByDescending` / `ThenBy` / `ThenByDescending` chain calls.

**Single-column sorting:**

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

**Multi-column sorting (ThenBy):**

```csharp
// Sort by department ascending, then by creation time descending within the same department
var users = await userService.SearchAsync(
    q => q.OrderBy(u => u.DeptId)
          .ThenByDescending(u => u.CreateTime)
);
```

`ThenBy` / `ThenByDescending` must follow `OrderBy` / `OrderByDescending`. You can chain multiple calls.

**Sorting with paging:**

```csharp
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderByDescending(u => u.CreateTime)
          .Skip(0)
          .Take(20)
);
```

**Lambda sorting with computed expressions:**

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

### 2.3 Variable capture and parameterization

```csharp
var keyword = "admin";
var users = await userService.SearchAsync(u => u.UserName.Contains(keyword));
```

Variables declared outside the Lambda are parameterized.  
For values such as `DateTime.Now`, assign them to a variable first if you want them parameterized.

### 2.4 The conditional operator becomes `CASE`

```csharp
var users = await userService.SearchAsync(
    u => (u.Age >= 18 ? "Adult" : "Minor") == "Adult"
);
```

This kind of Lambda is first converted into `Expr.If(...)`, then rendered as SQL `CASE WHEN ... THEN ... ELSE ... END`.

## 3. `Exists` and `ExistsRelated`

### 3.1 Explicit `Exists`

Lambda style:

```csharp
using static LiteOrm.Common.Expr;

var users = await userService.SearchAsync(
    u => Exists<Department>(d => d.Id == u.DeptId && d.Name == "R&D")
);
```

Expr style:

```csharp
using static LiteOrm.Common.Expr;

var expr = Exists<Department>(
    Prop("Id") == Prop("T0", "DeptId") & Prop("Name") == "R&D"
);
var users = await userService.SearchAsync(expr);
```

Use this when you want to control the correlation condition yourself.

### 3.2 Auto-related `ExistsRelated`

Lambda style:

```csharp
using static LiteOrm.Common.Expr;

var users = await userService.SearchAsync(
    u => ExistsRelated<DepartmentView>(d => d.Name == "R&D")
);
```

Expr style:

```csharp
using static LiteOrm.Common.Expr;

var expr = ExistsRelated<DepartmentView>(Prop("Name") == "R&D");
var users = await userService.SearchAsync(expr);
```

Use this when relationships are already declared in the model and you only want to filter the main table by related-table conditions.  
For matching rules, inheritance behavior, and `ConstFilter` interaction, see [Associations](./06-associations.en.md).

## 4. `Expr` query entry point

```csharp
using static LiteOrm.Common.Expr;

LogicExpr condition = null;

if (minAge.HasValue)
    condition &= Prop("Age") >= minAge.Value;

if (!string.IsNullOrWhiteSpace(keyword))
    condition &= Prop("UserName").Contains(keyword);

var users = await userService.SearchAsync(condition);
```

The main value of `Expr` is that you can **build first, then compose, then reuse**.
Lambda queries also go through `Expr` before SQL generation, so they are not a separate feature stack.

And for the full `Expr` construction model, static methods, extension methods, and composition semantics, see [Expr Guide](./03-expr-guide.en.md).

### 4.1 Sorting in Expr

In Expr queries, sorting uses `.Asc()` / `.Desc()` to mark direction, combined with `.OrderBy()` chain calls.

**Single-column sorting:**

```csharp
using static LiteOrm.Common.Expr;

var query = From<User>()
    .Where(Prop("Age") >= 18)
    .OrderBy(Prop("CreateTime").Desc())
    .Section(0, 20);

var users = await userService.SearchAsync(query);
```

**Multi-column sorting:**

```csharp
using static LiteOrm.Common.Expr;

var query = From<User>()
    .Where(Prop("Age") >= 18)
    .OrderBy(
        Prop("DeptId").Asc(),
        Prop("CreateTime").Desc()
    )
    .Section(0, 20);

var users = await userService.SearchAsync(query);
```

`OrderBy` accepts multiple `OrderByItemExpr` parameters, generating `ORDER BY col1 ASC, col2 DESC, ...` in the order passed.

**Sorting with aggregation:**

```csharp
using static LiteOrm.Common.Expr;

var query = From<User>()
    .GroupBy(Prop("DeptId"))
    .Select(
        Prop("DeptId"),
        Prop("Id").Count().As("UserCount")
    )
    .OrderBy(Prop("UserCount").Desc())
    .Section(0, 10);

var users = await userService.SearchAsync(query);
```

In aggregation queries, sort fields must use the aliases defined in `Select` (e.g., `UserCount`).

**Dynamic sorting:**

```csharp
using static LiteOrm.Common.Expr;

// Dynamically build sorting from request parameters
var sortField = "CreateTime";    // from frontend parameters
var sortDesc = true;             // from frontend parameters

var orderByItem = sortDesc
    ? Prop(sortField).Desc()
    : Prop(sortField).Asc();

var query = From<User>()
    .Where(Prop("Age") >= 18)
    .OrderBy(orderByItem)
    .Section(0, 20);

var users = await userService.SearchAsync(query);
```

**Multi-condition dynamic sorting:**

```csharp
using static LiteOrm.Common.Expr;

// Parse multiple sort fields, e.g., "DeptId,CreateTime desc"
var sortFields = new[] { "DeptId", "CreateTime desc" };
var orderByItems = new List<OrderByItemExpr>();

foreach (var field in sortFields)
{
    var parts = field.Trim().Split(' ');
    var prop = parts[0];
    var desc = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
    orderByItems.Add(desc ? Prop(prop).Desc() : Prop(prop).Asc());
}

var query = From<User>()
    .Where(Prop("Age") >= 18)
    .OrderBy(orderByItems.ToArray())
    .Section(0, 20);

var users = await userService.SearchAsync(query);
```

**Direct chaining from LogicExpr:**

```csharp
using static LiteOrm.Common.Expr;

var condition = Prop("Age") >= 18;
var query = condition
    .OrderBy(Prop("CreateTime").Desc())
    .Section(0, 20);

var users = await userService.SearchAsync(query);
```

`LogicExpr` also supports `.OrderBy(...)` and `.Section(...)`, suitable for scenarios where the condition is already built and you just need to add sorting and paging.

## 5. `ExprString` Interpolated Strings

`ExprString` lets you embed `Expr` objects and parameter values directly inside interpolated strings. It is suitable when the DAO layer needs to build a `Search` condition fragment or a full SQL statement manually. Service APIs do not expose a public `ExprString` query overload.

### 5.1 Basic usage

```csharp
using static LiteOrm.Common.Expr;

var condition = Prop("Age") >= 18;
var users = await userViewDAO.Search(
    $"WHERE {condition} ORDER BY CreateTime DESC"
).ToListAsync();
```

### 5.2 Parameterization and safety

```csharp
using static LiteOrm.Common.Expr;

int minAge = 18;
var result = await userViewDAO.Search(
    $"WHERE {Prop("Age")} >= {minAge}"
).ToListAsync();
```

Regular interpolated values are still parameterized. Embedded `Expr` objects are rendered as SQL fragments before they are inserted into the final command text.  
That is why it is better to interpolate structured objects such as `Expr.Prop(...)`, `Expr.Value(...)`, and `LogicExpr` instead of handwriting large amounts of column/value text.

### 5.3 Usage boundaries and recommended style

Recommendations:

- treat it as the DAO-side handwritten SQL entry: it can append `Search` condition fragments, or carry full SQL together with `isFull: true`
- when a filter can be expressed with `Expr`, build the `Expr` first and then interpolate it into `ExprString` rather than hardcoding the condition in the string
- `ExprString` is parsed in the insertion order of the embedded `Expr` objects, so parameter generation order and context behavior can differ from full `Expr` parsing. For example, inside `ExprString`, `SelectExpr` is resolved before `FromExpr`; if the `SelectExpr` contains columns without an explicit table alias, they may not bind to the default table correctly. The main query already works around this by creating the default main-table context early, but subqueries still require extra care.
- when hand-writing identifiers, you can use `[` and `]` as provider-agnostic quote placeholders; LiteOrm rewrites them to the real identifier quotes of the current database dialect before execution

When you hand-write identifiers, you can use `[` and `]` as provider-agnostic quote placeholders:

```csharp
var result = await dataViewDAO.Search(
    $"SELECT [Id], [UserName] FROM [Users] WHERE [Age] >= {minAge}",
    isFull: true
).GetResultAsync();
```

`ExprString` does not automatically expand `CommonTableExpr`. If you need CTE, write the full `WITH ... SELECT ...` SQL directly, or build the `WITH` block through `SelectExpr`.

```csharp
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

### 5.4 Sorting in ExprString

Sorting in ExprString is written directly in the `ORDER BY` clause of the SQL fragment. You can either write column names directly or embed `Expr` sort expressions.

**Basic sorting:**

```csharp
using static LiteOrm.Common.Expr;

var condition = Prop("Age") >= 18;
var users = await userViewDAO.Search(
    $"WHERE {condition} ORDER BY CreateTime DESC"
).ToListAsync();
```

**Multi-column sorting:**

```csharp
var users = await userViewDAO.Search(
    $"WHERE {Prop("Age")} >= {minAge} ORDER BY DeptId ASC, CreateTime DESC"
).ToListAsync();
```

**Embedding Expr sort expressions:**

```csharp
using static LiteOrm.Common.Expr;

var orderBy = new OrderByExpr(
    null,
    Prop("DeptId").Asc(),
    Prop("CreateTime").Desc()
);

var users = await userViewDAO.Search(
    $"WHERE {Prop("Age")} >= {minAge} {orderBy}"
).ToListAsync();
```

**Sorting in full SQL:**

```csharp
var result = await dataViewDAO.Search(
    $"""
    SELECT [Id], [UserName], [Age]
    FROM [Users]
    WHERE [Age] >= {minAge}
    ORDER BY [Age] DESC, [UserName] ASC
    """,
    isFull: true
).GetResultAsync();
```

## 6. Service vs DAO queries

### 6.1 Service

```csharp
using static LiteOrm.Common.Expr;

var users1 = await userService.SearchAsync(u => u.Age >= 18);
var users2 = await userService.SearchAsync(Prop("Age") >= 18);
var users3 = await userService.SearchAsAsync<UserSummary>(
    From<UserView>()
        .Where(Prop("Age") >= 18)
        .Select(
            Prop("Id"),
            Prop("UserName"),
            Expr.If(Prop("IsVip") == true, "VIP", "Normal").As("Level")
        )
);
```

- Service query APIs mainly target Lambda and `Expr`, and also support projection queries through `SearchAs(...)` / `SearchAsAsync(...)` with `SelectExpr`, which fits business-facing query code, transactions, and AOP-backed service encapsulation.
- Service does not provide an `ExprString` query overload; once the need becomes "handwritten SQL", switch to DAO.

### 6.2 DAO

```csharp
using static LiteOrm.Common.Expr;

var users1 = await userViewDAO.Search(u => u.Age >= 18).ToListAsync();
var users2 = await userViewDAO.Search(Prop("Age") >= 18).ToListAsync();
var users3 = await userViewDAO.Search($"WHERE {Prop("Age")} > {minAge}").ToListAsync();
```

- DAO supports Lambda and `Expr`, and also adds `ExprString`, so it is the right layer for custom SQL fragments, full SQL, richer projection queries, and DataTable-oriented queries.
- If you need lower-level entry points such as IQueryable-based `SearchAs(...)`, `ExprString`-based `SearchAs(...)`, `Query(...)`, `Execute(...)`, or `GetValue(...)`, go directly through DAO.

## 7. Related links

- [Expr Guide](./03-expr-guide.en.md)
- [CRUD Guide](./05-crud-guide.en.md)
- [Associations](./06-associations.en.md)
- [Mixing Lambda and Expr](./07-lambda-expr-mixing.en.md)
- [CTE Guide](./08-cte-guide.en.md)
- [Expression Extension](../04-extensibility/01-expression-extension.en.md)
