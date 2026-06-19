# Expr Guide

`Expr` is LiteOrm's core object-expression model,and this article mainly explains how to construct, compose, reuse, and understand its semantics.  
If your question is when to choose Lambda, `Expr`, or `ExprString`, continue with the [Query Guide](./04-query-guide.en.md). 
## 1. Creating basic expressions

### 1.1 Properties, values, and constants

```csharp
using static LiteOrm.Common.Expr;

var age = Prop("Age");
var userName = Prop("U", "UserName");

var paramValue = Value(18);         // parameterized
var constValue = Const("Enabled");  // inlined
```

- `Prop(name)`: create a property expression
- `Prop(alias, name)`: create a property expression with a table alias
- `Value(obj)`: parameterized runtime value
- `Const(obj)`: inline SQL constant

### 1.2 Comparison, strings, and sets

```csharp
using static LiteOrm.Common.Expr;

var expr1 = Prop("Age") >= 18;
var expr2 = Prop("DeptId").In(1, 2, 3);
var expr3 = Prop("Age").Between(18, 30);
var expr4 = Prop("UserName").Contains("admin");
var expr5 = Prop("UserName").Like("%root%");
```

All of these return `LogicExpr`, so they can keep composing.

### 1.3 Functions, aggregates, and dynamic SQL

```csharp
using static LiteOrm.Common.Expr;

var absAge = Func("ABS", Prop("Age"));
var countExpr = Aggregate("COUNT", Prop("Id"), isDistinct: true);
var currentUserFilter = Sql("CurrentUserFilter");
```

- `Func(name, args)`: regular SQL function
- `Aggregate(name, expr, isDistinct)`: aggregate wrapper
- `Sql(key, arg)`: registered dynamic SQL fragment for runtime-context filters

### 1.4 Type conversion and conditional values

```csharp
using static LiteOrm.Common.Expr;
using System.Data;

var ageText = Prop("Age").Cast(DbType.String);
var levelExpr = If(Prop("Age") >= 18, Const("Adult"), Const("Minor"));
```

- `.Cast(DbType)`: converts a value expression to a target database type, rendered as SQL `CAST(...)`
- `Expr.If(condition, then, else)`: builds a simple conditional expression, equivalent to `CASE WHEN condition THEN then ELSE else END`
- `Expr.Case(...)`: builds a multi-condition CASE expression with the following overloads:
  - `Case((LogicExpr, ValueTypeExpr)[] cases, ValueTypeExpr elseExpr)` - condition-result tuple array + ELSE
  - `Case(params (LogicExpr, ValueTypeExpr)[] cases)` - condition-result tuple array (no ELSE)
  - `Case(params Expr[] cases)` - alternating condition and result expressions; the last argument is ELSE when the count is odd
- In **Lambda** queries, the conditional operator `a ? b : c` is automatically parsed into `Expr.If(...)`, then rendered as `CASE WHEN ... THEN ... ELSE ... END`

Examples:

```csharp
using static LiteOrm.Common.Expr;

// Build CASE expression using tuple array (recommended)
var ageGroup = Case(
    new[] {
        (Prop("Age") < 18, (ValueTypeExpr)Const("Minor")),
        (Prop("Age") < 30, (ValueTypeExpr)Const("Young")),
        (Prop("Age") < 50, (ValueTypeExpr)Const("Adult"))
    },
    Const("Senior")  // ELSE clause
);

// Without ELSE clause
var ageGroupNoElse = Case(
    (Prop("Age") < 18, Const("Minor")),
    (Prop("Age") < 30, Const("Young"))
);

// Using alternating argument form
var levelExpr = Case(
    Prop("Score") >= 90, Const("A"),
    Prop("Score") >= 80, Const("B"),
    Prop("Score") >= 60, Const("C"),
    Const("D")  // ELSE clause (last argument is ELSE when count is odd)
);
```

## 2. Subqueries and relation filters

### 2.1 Explicit `Exists`

Lambda style:

```csharp
var users = await userService.SearchAsync(
    u => Exists<Department>(d => d.Id == u.DeptId && d.Name == "R&D")
);
```

Expr style:

```csharp
using static LiteOrm.Common.Expr;

var expr = Exists<Department>(
    Prop("Id") == Prop("T0", "DeptId")
    & Prop("Name") == "R&D"
);
```

Use this when you want to **write the correlation condition explicitly**.

### 2.2 Auto-related `ExistsRelated`

Lambda style:

```csharp
var users = await userService.SearchAsync(
    u => ExistsRelated<DepartmentView>(d => d.Name == "R&D")
);
```

Expr style:

```csharp
using static LiteOrm.Common.Expr;

var expr = ExistsRelated<DepartmentView>(
    Prop("Name") == "R&D"
);
```

`ExistsRelated` fills in the relation condition from metadata such as `ForeignType` and `TableJoin`.  
For the detailed matching rules, see [Associations](./06-associations.en.md).

## 3. Building Expr dynamically

### 3.1 Accumulate conditions by parameters

```csharp
using static LiteOrm.Common.Expr;

LogicExpr condition = null;

if (minAge.HasValue)
    condition &= Prop("Age") >= minAge.Value;

if (deptId.HasValue)
    condition &= Prop("DeptId") == deptId.Value;

if (!string.IsNullOrWhiteSpace(keyword))
    condition &= Prop("UserName").Contains(keyword);
```

`&` and `|` are null-friendly, which makes them ideal for admin search filters.

> The old `AndIf`, `OrIf`, `WhereIf`, and `SetIf` helpers have been removed. Prefer normal `if` statements together with the null-friendly `&` / `|` composition rules, or conditionally append chained clauses.

### 3.2 Build from QueryString / Dictionary

```csharp
using static LiteOrm.Common.Expr;

public static LogicExpr BuildUserSearch(IReadOnlyDictionary<string, string?> query)
{
    LogicExpr condition = null;

    if (query.TryGetValue("minAge", out var minAgeText) && int.TryParse(minAgeText, out var minAge))
        condition &= Prop("Age") >= minAge;

    if (query.TryGetValue("keyword", out var keyword) && !string.IsNullOrWhiteSpace(keyword))
        condition &= Prop("UserName").Contains(keyword);

    return condition;
}
```

This works well for open query endpoints, gateway forwarding, and frontend query builders.

Additional note: in string-based entry points like these, property names and `orderby` field names are typically matched **case-insensitively** against the model.

### 3.3 Mix with Lambda

```csharp
using static LiteOrm.Common.Expr;

LogicExpr extra = null;
extra &= Prop("UserName").Contains("John");

var users = await userService.SearchAsync(
    u => u.IsActive == true && extra.To<bool>()
);
```

If you want Lambda readability outside and dynamic Expr reuse inside, continue with [Mixing Lambda and Expr](./07-lambda-expr-mixing.en.md).

## 4. Build chained queries with `Expr.From<T>()`

```csharp
using static LiteOrm.Common.Expr;

var query = From<User>()
    .Where(Prop("Age") > 18)
    .GroupBy(Prop("DeptId"))
    .Having(Prop("Id").Count() > 5)
    .Select(
        Prop("DeptId"),
        Prop("Id").Count().As("UserCount")
    )
    .OrderBy(Prop("UserCount").Desc())
    .Section(0, 20);
```

This is the most complete `Expr` style: start from `FROM`, then build `WHERE / GROUP BY / HAVING / SELECT / ORDER BY / paging`.

## 5. Expr type map

You can think of LiteOrm's `Expr` model as four layers:

| Layer | Representative types | Purpose |
|------|----------|------|
| Root | `Expr` | The common base type for all expression objects |
| Value expressions | `ValueTypeExpr` | Values that can appear in columns, functions, comparisons, and `SELECT` items |
| Logical expressions | `LogicExpr` | Boolean expressions that can appear in `WHERE`, `HAVING`, or `EXISTS` |
| SQL segments | `SqlSegment` | Chainable SQL nodes such as `FROM / SELECT / WHERE / ORDER BY` |

### 5.1 ValueTypeExpr family

- `ValueExpr`: literal or parameter value
- `PropertyExpr`: column reference
- `FunctionExpr`: function call
- `ValueBinaryExpr`: value arithmetic such as `a + b`
- `UnaryExpr`: unary operations such as `-a` or `DISTINCT a`
- `ValueSet`: a value set such as `IN (...)`
- `SelectItemExpr`: `SELECT xxx AS Alias`
- `OrderByItemExpr`: `ORDER BY xxx ASC/DESC`

### 5.2 LogicExpr family

- `LogicBinaryExpr`: comparisons such as `Age >= 18`
- `AndExpr`: AND composition
- `OrExpr`: OR composition
- `NotExpr`: NOT composition
- `ForeignExpr`: the EXISTS expression used by `Exists` and `ExistsRelated`
- `LambdaExpr`: a wrapper used during Lambda conversion; usually not written by hand

### 5.3 SqlSegment family

- `SourceExpr`: abstract base for SQL segments that can act as a data source
- `TableExpr`: table
- `CommonTableExpr`: CTE
- `TableJoinExpr`: JOIN
- `FromExpr`: FROM
- `SelectExpr`: SELECT
- `WhereExpr`: WHERE
- `GroupByExpr`: GROUP BY
- `HavingExpr`: HAVING
- `OrderByExpr`: ORDER BY
- `SectionExpr`: paging

### 5.4 Statement expressions directly under Expr

- `UpdateExpr`: UPDATE
- `DeleteExpr`: DELETE

In day-to-day query code, the most common ones are usually:

- `PropertyExpr` / `ValueExpr`
- `LogicBinaryExpr` / `AndExpr` / `OrExpr`
- `ForeignExpr`
- `SelectExpr` / `WhereExpr` / `OrderByExpr`

## 6. Expr static method quick reference

| Method | Description | Example |
|------|------|------|
| `Expr.Prop(name)` | Create a property expression | `Expr.Prop("Age")` |
| `Expr.Prop(alias, name)` | Create a property expression with alias | `Expr.Prop("U", "UserName")` |
| `Expr.Value(value)` | Create a parameterized value | `Expr.Value(18)` |
| `Expr.Const(value)` | Create an inline constant | `Expr.Const("Enabled")` |
| `Expr.Null` | SQL NULL | `Expr.Null` |
| `Expr.From<T>()` | Create a chained-query starting point | `Expr.From<User>()` |
| `Expr.Update<T>()` | Create an UPDATE expression | `Expr.Update<User>()` |
| `Expr.Delete<T>()` | Create a DELETE expression | `Expr.Delete<User>()` |
| `Expr.Exists<T>(innerExpr)` | Create an EXISTS subquery | `Expr.Exists<Department>(...)` |
| `Expr.ExistsRelated<T>(innerExpr)` | Create an auto-related EXISTS subquery | `Expr.ExistsRelated<DepartmentView>(...)` |
| `Expr.Lambda<T>(expr)` | Convert Lambda into `LogicExpr` | `Expr.Lambda<User>(u => u.Age > 18)` |
| `Expr.Func(name, args)` | Create a function expression | `Expr.Func("COUNT", Expr.Prop("Id"))` |
| `Expr.Aggregate(name, expr, isDistinct)` | Create an aggregate expression | `Expr.Aggregate("COUNT", Expr.Prop("Id"), true)` |
| `Expr.If(condition, then, else)` | IF / CASE WHEN form | `Expr.If(... )` |
| `Expr.Case(cases, elseExpr)` | CASE expression | `Expr.Case(... )` |
| `Expr.Now()` | Current timestamp | `Expr.Now()` |
| `Expr.Today()` | Current date | `Expr.Today()` |
| `Expr.Sql(key, arg)` | Dynamic SQL fragment | `Expr.Sql("CurrentUserFilter")` |
| `Expr.Query<T>(expression)` | Convert IQueryable Lambda to Expr | `Expr.Query<User>(...)` |
| `Expr.Query<T, TResult>(expression)` | Convert IQueryable Lambda with scalar result to Expr | `Expr.Query<User, int>(...)` |

## 7. Operator overloads and implicit conversions

### 7.1 Operator overload quick view

LiteOrm overloads common C# operators on `ValueTypeExpr` and `LogicExpr`, so many expressions read very close to ordinary code:

| Operator | Applies to | Returns | Example |
|------|------|------|------|
| `==` `!=` `>` `<` `>=` `<=` | `ValueTypeExpr` | `LogicExpr` | `Prop("Age") >= 18` |
| `+` `-` `*` `/` `%` | `ValueTypeExpr` | `ValueTypeExpr` | `Prop("Amount") * 0.9m` |
| unary `-` / `~` | `ValueTypeExpr` | `ValueTypeExpr` | `-Prop("Balance")`, `~Prop("Flags")` |
| `&` `\|` | `LogicExpr` | `LogicExpr` | `(Prop("Age") >= 18) & (Prop("Status") == 1)` |
| `!` | `LogicExpr` | `LogicExpr` | `!(Prop("IsDeleted") == true)` |

For example:

```csharp
using static LiteOrm.Common.Expr;

var scoreExpr = (Prop("MathScore") + Prop("ExtraScore")) / 2;
var filter = (Prop("Age") >= 18 & Prop("Status") == 1)
           | Prop("UserName").Contains("admin");
```

#### String concatenation: do NOT use `+`, use `.Concat(...)`

When hand-writing `Expr`, `ValueTypeExpr`'s `+` operator has **arithmetic-add** semantics, and may render SQL `+`, which is not portable for string concatenation across databases.

Use concat explicitly:

```csharp
using static LiteOrm.Common.Expr;

var fullName = Prop("FirstName")
    .Concat(" ")
    .Concat(Prop("LastName"));
```

`Concat(...)` is rendered via `SqlBuilder.BuildConcatSql`, so providers can output `CONCAT(a,b,...)` or `a || b` as appropriate.

> Note: In **Lambda** expressions (e.g. `SearchAsync(u => u.FirstName + " " + u.LastName == "..." )`), C# string `+` is typically converted to concat during parsing; but when hand-writing `Expr`, always use `.Concat(...)` explicitly.

### 7.2 Null-friendly composition on `LogicExpr`

`&` and `|` on `LogicExpr` are especially useful for dynamic filters because they are null-friendly:

```csharp
using static LiteOrm.Common.Expr;

LogicExpr condition = null;
condition &= Prop("Age") >= 18;
condition &= Prop("Status") == 1;
condition |= Prop("IsVip") == true;
```

The rules are:

- `null & expr` => `expr`
- `expr & null` => `expr`
- `null | expr` => `expr`
- `expr | null` => `expr`

This removes the need to manually guard every composition step with a null check.

### 7.3 Implicit conversion for scalar values

`ValueTypeExpr` / `ValueExpr` support implicit conversion from:

- `string`
- `int`
- `long`
- `bool`
- `DateTime`
- `double`
- `decimal`

So you can write:

```csharp
using static LiteOrm.Common.Expr;

var expr1 = Prop("Age") >= 18;
var expr2 = Prop("CreateTime") >= DateTime.Today;
var expr3 = Prop("IsEnabled") == true;
var expr4 = Prop("Amount") + 12.5m;
```

Those literals are automatically wrapped as `ValueExpr`, which usually means the same as:

```csharp
Prop("Age") >= Value(18)
Prop("CreateTime") >= Value(DateTime.Today)
```

### 7.4 These implicit values are parameterized by default

Ordinary literals used through operator overloads follow `Expr.Value(...)` semantics, not `Expr.Const(...)` semantics:

```csharp
using static LiteOrm.Common.Expr;

var expr = Prop("Status") == 1;            // parameterized
var constExpr = Prop("Status") == Const(1); // inlined constant
```

Rule of thumb:

- **Runtime value**: prefer a normal literal, or explicitly use `Value(...)`
- **Must be inlined into SQL text**: explicitly use `Const(...)`

### 7.5 `null` does not have an implicit conversion

`null` is not implicitly converted into `ValueTypeExpr`, so null checks should be written explicitly:

```csharp
using static LiteOrm.Common.Expr;

var expr1 = Prop("DeletedTime").IsNull();
var expr2 = Prop("DeletedTime") == Expr.Null;
```

For database semantics, `.IsNull()` / `.IsNotNull()` is usually clearer.

### 7.6 Other convenient implicit conversions

LiteOrm also includes a few ergonomic conversions for chained APIs:

```csharp
using static LiteOrm.Common.Expr;

var query = From<User>()
    .OrderBy(("Age", false)); // (string property, bool ascending) -> OrderByItemExpr

var update = Update<User>()
    .Set((Prop("Age"), Prop("Age") + 1)); // (PropertyExpr, ValueTypeExpr) -> SetItem
```

These are mainly about reducing ceremony so `OrderBy(...)`, `Set(...)`, and similar APIs stay concise.

> **Tip**: when mixing comparison, arithmetic, and logical operators, add parentheses instead of relying on C# operator precedence to make the final expression obvious.

### 7.7 Lambda conditional operator

You can use the C# conditional operator directly inside Lambda queries:

```csharp
var users = await userService.SearchAsync(
    u => (u.Age >= 18 ? "Adult" : "Minor") == "Adult"
);
```

LiteOrm parses this into `Expr.If(...)`, which is then rendered as a SQL `CASE` expression.

## 8. ExprExtensions quick reference

### 8.1 Logic composition

| Method | Description | Example |
|------|------|------|
| `&` / `.And(right)` | AND | `Prop("Age") > 18 & Prop("DeptId") == 2` |
| `|` / `.Or(right)` | OR | `condition1 | condition2` |
| `!` / `.Not()` | NOT | `!Prop("IsDeleted").Equal(true)` |

### 8.2 Comparison and set operations

| Method | Description |
|------|------|
| `.Equal(v)` `.NotEqual(v)` | equals / not equals |
| `.GreaterThan(v)` `.LessThan(v)` | greater / less |
| `.GreaterThanOrEqual(v)` `.LessThanOrEqual(v)` | greater-or-equal / less-or-equal |
| `.In(params items)` `.In(IEnumerable)` `.In(Expr)` | IN set / subquery |
| `.Between(low, high)` | BETWEEN |

### 8.3 String and NULL helpers

| Method | Description |
|------|------|
| `.Like(pattern)` | LIKE |
| `.Contains(text)` `.StartsWith(text)` `.EndsWith(text)` | common string predicates |
| `.RegexpLike(pattern)` | regex predicate |
| `.IsNull()` `.IsNotNull()` | NULL checks |
| `.IfNull(defaultValue)` | null replacement |
| `.Cast(DbType)` | convert to a target database type |

Additional note: `Contains` / `StartsWith` / `EndsWith` / `Like` still use parameterization and wildcard escaping, but the `ESCAPE` fragment is emitted only when the pattern actually contains characters that need escaping.

### 8.4 Alias, aggregate, and ordering helpers

| Method | Description |
|------|------|
| `.As(name)` | create `SelectItemExpr` |
| `.Distinct()` | DISTINCT |
| `.Count()` `.Sum()` `.Avg()` `.Max()` `.Min()` | aggregates |
| `.Asc()` `.Desc()` | ordering |
| `.Over(partitionBy)` | window function |

### 8.5 Chained SQL building

| Method | Description |
|------|------|
| `.Where(condition)` | WHERE |
| `.SelectAll()` | SELECT * |
| `.GroupBy(props)` | GROUP BY |
| `.Having(condition)` | HAVING |
| `.Select(props)` | SELECT |
| `.OrderBy(props)` | ORDER BY |
| `.Section(skip, take)` | paging |
| `.Set(assignments)` | UPDATE SET |

## 9. Equals and composition semantics

### 9.1 Names and aliases are case-insensitive

Expression objects such as `PropertyExpr`, `TableExpr`, `ForeignExpr`, `FunctionExpr`, `SelectExpr`, `SelectItemExpr`, `CommonTableExpr`, and `GenericSqlExpr` treat **names and aliases as case-insensitive** in `Equals` / `GetHashCode`.

For example:

```csharp
Expr.Prop("User", "Name")
Expr.Prop("user", "name")
```

are treated as equal expressions.

### 9.2 `AndExpr` / `OrExpr` use set semantics

`AndExpr.Items` and `OrExpr.Items` now use set semantics:

- duplicate conditions are removed
- `Equals` / `GetHashCode` no longer depend on duplicate distribution
- insertion order is still preserved internally for iteration, output, and serialization

So:

```csharp
new AndExpr(a, a, b)
new AndExpr(a, b)
```

are equivalent in composition semantics.

## 10. Detecting circular references

When dynamically building `Expr` trees, accidentally setting a node's `Source` property to itself or forming a loop can cause stack overflows during traversal/conversion. `CycleDetector` uses `ExprVisitor` to detect such circular references.

### 10.1 Basic usage

```csharp
using LiteOrm.Common;

var expr = Expr.Prop("Age") > 18;
bool hasCycle = CycleDetector.HasCycle(expr);   // false
```

### 10.2 API

| Method | Return type | Description |
|--------|-------------|-------------|
| `CycleDetector.HasCycle(Expr root)` | `bool` | Whether a circular reference exists |
| `CycleDetector.FindCycle(Expr root)` | `Expr` | Returns the node causing the cycle, or `null` |
| `CycleDetector.Detect(Expr root)` | `CycleResult` | Returns detailed result including `CycleNode` and `Path` |

### 10.3 Using Detect for detailed path information

```csharp
var result = CycleDetector.Detect(someExpr);
if (result.HasCycle)
{
    Console.WriteLine($"Circular reference detected, trigger node: {result.CycleNode.ExprType}");
    Console.WriteLine("Path from root to cycle node:");
    foreach (var node in result.Path)
    {
        Console.WriteLine($"  → {node.ExprType}");
    }
}
```

`CycleResult.Path` records the complete path from the root node to the cycle node (second occurrence), with the duplicate node at the end, making it easy to locate the cycle.

### 10.4 Common cycle scenarios

```csharp
// Scenario 1: Direct self-reference (Source points to itself)
var where = new WhereExpr();
where.Source = where;                 // self-reference
where.Where = Expr.Prop("Age") > 18;
// CycleDetector.HasCycle(where) → true

// Scenario 2: Indirect loop (A → B → A)
var whereA = new WhereExpr();
var whereB = new WhereExpr();
whereA.Source = whereB;
whereB.Source = whereA;
// CycleDetector.HasCycle(whereA) → true

// Scenario 3: Normal chain structure (no cycle)
var query = Expr.From(typeof(User))
    .Where(Expr.Prop("Age") > 18)
    .OrderBy(Expr.Prop("Name").Asc());
// CycleDetector.HasCycle(query) → false
```

### 10.5 Implementation principle

`CycleDetector` implements the `IExprNodeVisitor` interface. During `BeginVisit`, it adds nodes to a path set (using reference equality). During `EndVisit`, it removes them from the path. When the same node appears in the path set again, a circular reference is detected and traversal is interrupted via `CancellationTokenSource.Cancel()`.

> **Note**: Detection uses reference equality (`ReferenceEquals`) rather than value equality. Two nodes with identical content but different references will not be falsely reported as a cycle.

## 11. Related links

- [Query Guide](./04-query-guide.en.md)
- [CRUD Guide](./05-crud-guide.en.md)
- [Associations](./06-associations.en.md)
- [Mixing Lambda and Expr](./07-lambda-expr-mixing.en.md)
- [CTE Guide](./08-cte-guide.en.md)
- [Expression Extension](../04-extensibility/01-expression-extension.en.md)
