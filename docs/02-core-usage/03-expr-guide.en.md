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

## 10. Related links

- [Query Guide](./04-query-guide.en.md)
- [CRUD Guide](./05-crud-guide.en.md)
- [Associations](./06-associations.en.md)
- [Mixing Lambda and Expr](./07-lambda-expr-mixing.en.md)
- [CTE Guide](./08-cte-guide.en.md)
- [Expression Extension](../04-extensibility/01-expression-extension.en.md)
