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

### 1.1 Logical combinations (`&&`, `||`, `!`)

Use C# logical operators directly inside a Lambda:

```csharp
// AND + OR
var result = await userService.SearchAsync(
    u => u.Age > 18 && u.Status == 1 || u.IsVip
);

// NOT
var active = await userService.SearchAsync(
    u => !u.IsDeleted && u.IsActive
);

// Parentheses change precedence
var complex = await userService.SearchAsync(
    u => u.Age > 18 && (u.Status == 1 || u.IsVip)
);
```

`&&` → `AND`, `||` → `OR`, `!` → `NOT`. Operator precedence follows C# semantics: `!` > `&&` > `||`. Add parentheses to override.

### 1.2 Supported string and method handlers

At startup, `LiteOrmLambdaHandlerInitializer` registers a batch of Lambda method handlers. The strings it currently supports (see `LiteOrm.Common/Converter/LiteOrmLambdaHandlerInitializer.cs`):

| Method | SQL semantic | Example |
|--------|--------------|---------|
| `string.Contains(text)` | `LIKE '%text%'` | `u => u.Name.Contains("admin")` |
| `string.StartsWith(text)` | `LIKE 'text%'` | `u => u.Name.StartsWith("admin")` |
| `string.EndsWith(text)` | `LIKE '%text'` | `u => u.Name.EndsWith("admin")` |
| `string.Concat(...)` | `CONCAT(...)` | `u => string.Concat(u.FirstName, " ", u.LastName)` |
| `string.ToUpper()` | `UPPER` | `u => u.Name.ToUpper() == "ADMIN"` |
| `string.ToLower()` | `LOWER` | `u => u.Name.ToLower() == "admin"` |
| `string.Trim()` / `TrimStart()` / `TrimEnd()` | `TRIM` / `LTRIM` / `RTRIM` | `u => u.Name.Trim() == "admin"` |
| `string.Remove(startIndex)` | `LEFT` | `u => u.Code.Remove(3) == "ABC"` |
| `string.Length` (property) | `CHAR_LENGTH` / `LEN` | `u => u.Name.Length > 5` |
| `Equals(obj)` | `=` | `u => u.Name.Equals("admin")` |
| `ToString()` / `ToString(format)` | raw / `Format` | `u => u.CreateTime.ToString("yyyy-MM-dd")` |

Collection methods:

| Method | SQL semantic | Example |
|--------|--------------|---------|
| `IList.Contains(item)` / `Enumerable.Contains(collection, item)` | `IN` | `u => new[] { 1, 2, 3 }.Contains(u.Id)` |

In addition, the following members/methods are supported:

- **DateTime**: `DateTime.Now` (`CURRENT_TIMESTAMP`), `DateTime.Today` (`CURRENT_DATE`), `AddYears/AddMonths/AddDays/AddHours/AddMinutes/AddSeconds` (`DATE_ADD` / `DATEADD`)
- **Math**: `Abs`, `Max`, `Min`, `Floor`, `Ceiling`, `Round`, `Pow`, `Sqrt`, `Truncate`, etc. (mapped to SQL math functions)
- **TimeSpan**: `TotalSeconds` / `TotalDays` / `TotalHours` / `TotalMinutes` / `TotalMilliseconds`. Subtracting two dates (e.g. `(DateTime.Now - u.CreateTime).TotalDays`) auto-translates to `DateDiffDays` and similar functions.

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

### 2.5 Skip/Take paging semantics

Paging is expressed via the query builder's `.Skip(skip)` and `.Take(take)` chain calls:

```csharp
// First page (10 per page)
var paged = await userService.SearchAsync(
    q => q.Where(u => u.Age > 18)
          .OrderBy(u => u.UserName)
          .Skip(0)
          .Take(10)
);
```

- `Skip(n)` skips the first `n` records, rendered as SQL `OFFSET n` (or `LIMIT n, ...` / `ROWNUM` per dialect).
- `Take(n)` takes `n` records, rendered as `LIMIT n` (or `FETCH FIRST n ROWS ONLY`).
- `Skip` and `Take` can be used independently or together; usually combine with `OrderBy` first.
- Large-offset paging (e.g. `Skip(10000)`) suffers poor performance; prefer ID-based cursor paging — see [Performance Optimization](../03-advanced-topics/03-performance.en.md#33-pagination-optimization).

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

## 6. Projection Queries (`SearchAs` / `SearchOneAs`)

`SearchAs` / `SearchOneAs` accept IQueryable-style lambdas and project the result into a target type (custom classes or anonymous types), which is convenient for "select only some columns" or "assemble view / summary objects":

```csharp
// Project to a custom class
List<UserSummary> summaries = userService.SearchAs(
    q => q.Where(u => u.Age >= 18)
          .Select(u => new UserSummary { UserName = u.UserName, Age = u.Age })
);

// Project to an anonymous type (column aliases match the anonymous member names)
var anon = userService.SearchAs(
    q => q.Where(u => u.Age >= 18).Select(u => new { u.UserName, u.Age })
);

// Filter without projection: TResult is inferred as the entity type
List<User> adults = userService.SearchAs(q => q.Where(u => u.Age >= 18));

// Single record
UserSummary? one = userService.SearchOneAs(
    q => q.Where(u => u.Id == 1).Select(u => new UserSummary { UserName = u.UserName, Age = u.Age })
);
```

Async versions are `SearchAsAsync` / `SearchOneAsAsync`:

```csharp
var list = await userService.SearchAsAsync(
    q => q.Where(u => u.Age >= 18).Select(u => new { u.UserName, u.Age })
);
```

> The `SearchAs` lambda is converted into a `SelectExpr` before execution (equivalent to the `SelectExpr` usage in [Query Overview](./04-query-overview.en.md#3-search-vs-searchas)). Result mapping is handled by `DataReaderConverter`: unregistered plain classes / anonymous types match by "member name ↔ column alias", while registered entity types match positionally — prefer DTO / anonymous-type projections. For sharded tables, pass the shard name via the `tableArgs` parameter.

## 7. JsonNode Queries

LiteOrm natively supports `System.Text.Json.Nodes.JsonNode` (including `JsonObject`, `JsonArray`, `JsonValue`) properties — they are automatically mapped to JSON columns (stored as strings), and you can query JSON fields directly via indexers and `GetValue<T>()` in Lambda expressions.

> For auto-mapping and serialization of JsonNode properties, see [Data Mapping & Value Conversion](../03-advanced-topics/11-data-mapping.en.md#33-jsonnode-mapping-navigation).

### 7.1 Basic usage: indexer access

`JsonNode` indexers (`this[string]` / `this[int]`) are translated into the `JsonExtract` function, returning a JSON fragment:

```csharp
// Query configs where name in Settings is "admin"
var result = await configDAO.SearchAsync(
    c => c.Settings!["name"].GetValue<string>() == "admin"
);

// Nested access: Settings.profile.age > 18
var adults = await configDAO.SearchAsync(
    c => c.Settings!["profile"]["age"].GetValue<int>() > 18
);

// Array access: Settings.tags[0] == "vip"
var vips = await configDAO.SearchAsync(
    c => c.Settings!["tags"][0].GetValue<string>() == "vip"
);
```

### 7.2 Scalar value extraction: `GetValue<T>()`

`GetValue<T>()` is translated into the `JsonValue` function, used to extract scalar values (string, number, boolean, etc.):

| C# Expression | Mapped Function | Description |
|---------------|-----------------|-------------|
| `json["key"].GetValue<string>()` | `JsonValue` | Extract string scalar |
| `json["key"].GetValue<int>()` | `JsonValue` | Extract integer scalar |
| `json["key"].GetValue<bool>()` | `JsonValue` | Extract boolean scalar |
| `json["key"]` (no GetValue) | `JsonExtract` | Returns JSON fragment, for further indexing |

```csharp
// Extract numeric value and compare
var result = await configDAO.SearchAsync(
    c => c.Settings!["score"].GetValue<int>() >= 90
);

// Extract boolean value
var enabled = await configDAO.SearchAsync(
    c => c.Settings!["enabled"].GetValue<bool>() == true
);
```

### 7.3 Index chains and path rules

LiteOrm automatically parses consecutive indexer calls and concatenates them into the correct JSON path:

- **String keys** → dot notation path (`$.name`)
- **Integer indices** → bracket notation path (`$[0]`)
- **Mixed nesting** → combined path (`$.items[0].name`)

```csharp
// Equivalent to JSON path $.items[2].price
var price = await configDAO.SearchAsync(
    c => c.Settings!["items"][2]["price"].GetValue<decimal>() > 100
);
```

> If the index key is a **constant** (string or integer), the path is fully concatenated at compile time; if it's a dynamic variable, it's concatenated at runtime. Both are supported.

### 7.4 JSON function mapping by database

| Generic Function | SQL Server | MySQL | PostgreSQL | SQLite | Oracle |
|-----------------|-----------|-------|------------|--------|--------|
| **JsonExtract** | `JSON_QUERY` | `JSON_EXTRACT` | `->` operator | `json_extract` | `JSON_QUERY` |
| **JsonValue** | `JSON_VALUE` | `JSON_UNQUOTE(JSON_EXTRACT(...))` | `->>` operator | `json_extract` | `JSON_VALUE` |

Different databases have varying levels of JSON function support — please verify against your target database. To build JSON queries manually in `Expr`, see [Expression Extension](../04-extensibility/01-expression-extension.en.md#9-json-function-extensions).

## 8. FAQ

### 8.1 What if a method is not supported in a Lambda?

If the Lambda calls a method without a registered handler (i.e. one not covered by `LiteOrmLambdaHandlerInitializer`), LiteOrm throws an exception during parsing to flag the unsupported method. This is by design — unsupported methods are not silently ignored; you'll see the problem early.

To support custom methods, follow the registration pattern in `LiteOrmLambdaHandlerInitializer` and call `LambdaExprConverter.RegisterMethodHandler`.

### 8.2 How do Lambda and Expr interoperate?

- **Lambda → Expr**: A Lambda expression is automatically converted to an `Expr` tree during parsing. You can also call `Expr.Lambda<T>(u => ...)` manually to produce a `LogicExpr`, useful for combining with handwritten `Expr`.
- **Expr → Lambda**: Embed an existing `Expr` into a Lambda via the `ExprExtensions.To()` extension, e.g. `u => u.IsActive && extra.To<bool>()`, where `extra` is an externally built `LogicExpr`.

For details, see [Combining Lambda and Expr](./09-lambda-expr-mixing.en.md).

### 8.3 Does Lambda add extra runtime overhead?

At **parse time** Lambda queries are converted to `Expr` objects; this happens once per query call. Once converted to `Expr`, the subsequent SQL generation is identical to handwritten `Expr`, with **no extra runtime overhead**.

In other words, the cost is confined to parse time; the generated SQL and execution path match an equivalent handwritten `Expr`. For the vast majority of business scenarios, parse-time cost is negligible.

## 9. Related links

- [Query Overview](./04-query-overview.en.md)
- [Expr Guide](./06-expr-guide.en.md)
- [ExprString Guide](./07-exprstring-guide.en.md)
- [CRUD Guide](./03-crud-guide.en.md)
- [Associations](./08-associations.en.md)
- [Mixing Lambda and Expr](./09-lambda-expr-mixing.en.md)
- [CTE Guide](./10-cte-guide.en.md)
- [Expression Extension](../04-extensibility/01-expression-extension.en.md)
