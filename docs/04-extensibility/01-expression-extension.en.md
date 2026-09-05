# Expression Extension

LiteOrm provides a powerful expression extension mechanism that allows registering custom method handlers and member handlers to translate C# methods/properties into database SQL functions.

## 1. Core Concepts

Expression extension relies on the collaboration of two key components:

| Component | Responsibility |
|----------|---------------|
| `LambdaExprConverter` | Converts C# Lambda expression method/property calls into `Expr` objects |
| `SqlBuilder` | Converts `Expr` objects into SQL strings for specific databases |

### 1.1 Processing Pipeline

```
C# Lambda Expression
    │
    ▼
LambdaExprConverter.RegisterMethodHandler()
    │  Convert to FunctionExpr / other Expr
    ▼
SqlBuilder.RegisterFunctionSqlHandler()
    │  Convert to database-specific SQL function
    ▼
Final SQL
```

### 1.2 Minimal Complete Workflow

When first implementing expression extension, follow these 4 steps:

1. Define a business-readable C# method or property.
2. Use `LambdaExprConverter` to convert it into an `Expr`.
3. Use `SqlBuilder.RegisterFunctionSqlHandler` to convert the `Expr` into target database SQL.
4. Use it in queries like a normal method.

```csharp
var users = await userService.SearchAsync(
    u => u.CreateTime.Format("yyyy-MM-dd") == "2026-03-31"
);
```

If this query executes successfully, your extension chain is working.

## 2. LambdaExprConverter Methods

### 2.1 RegisterMethodHandler - Register Method Handler

```csharp
// Register global method handler (matched by method name)
LambdaExprConverter.RegisterMethodHandler("Format", handler);

// Register type-specific method handler
LambdaExprConverter.RegisterMethodHandler(typeof(DateTime), "Format", handler);
LambdaExprConverter.RegisterMethodHandler(typeof(string), null, handler);  // Handle all methods of this type
```

`"Format"` is just an example method name. In real projects, prefer `nameof(SomeType.SomeMethod)` so refactoring stays safer.

**Parameter Description:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `methodName` | string | Method name |
| `handler` | `Func<MethodCallExpression, LambdaExprConverter, Expr>` | Handler logic |

**Handler Return Value:**

- Return `Expr` subclass object: `FunctionExpr`, `LogicBinaryExpr`, etc.
- Return `null`: Use default handling

### 2.2 RegisterMemberHandler - Register Member Handler

```csharp
// Register global member handler
LambdaExprConverter.RegisterMemberHandler("Length", handler);

// Register type-specific member handler
LambdaExprConverter.RegisterMemberHandler(typeof(User), "Age", handler);
```

`"Length"` is also just an example member name. For your own members, prefer `nameof(SomeType.SomeProperty)` when possible.

## 3. SqlBuilder Methods

### 3.1 RegisterFunctionSqlHandler - Register Function SQL Handler

The new lower-level overload is recommended:

```csharp
public delegate void FunctionSqlHandler(
    ref ValueStringBuilder outSql,
    FunctionExpr expr,
    SqlBuildContext context,
    SqlBuilder sqlBuilder,
    ICollection<Param> outputParams);
```

```csharp
using static LiteOrm.Common.Expr;
MySqlBuilder.Instance.RegisterFunctionSqlHandler("DATE_FORMAT",
    (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context,
     SqlBuilder sqlBuilder, ICollection<Param> outputParams) =>
{
    outSql.Append("DATE_FORMAT(");
    expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(", ");
    expr.Args[1].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(')');
});
```

This overload is better suited for:

- Needing fine-grained control over SQL output
- Needing to directly reuse `Expr.ToSql(...)`
- Needing to handle parameter output and different database dialects simultaneously

For very simple string format concatenation, the simplified overload can still be used; but documentation examples prioritize the new `FunctionSqlHandler` form.

## 4. Example 1: Date Formatting

### 4.1 Define Extension Method

```csharp
public static class DateTimeExtensions
{
    public static string Format(this DateTime date, string format)
    {
        return date.ToString(format);
    }
}
```

### 4.2 Register Method Handler

```csharp
LambdaExprConverter.RegisterMethodHandler("Format", (node, converter) => {
    var dateExpr = converter.Convert(node.Object) as ValueTypeExpr;
    var formatExpr = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
    return new FunctionExpr("DATE_FORMAT", dateExpr, formatExpr);
});
```

### 4.3 Register SQL Handler

```csharp
using static LiteOrm.Common.Expr;
MySqlBuilder.Instance.RegisterFunctionSqlHandler("DATE_FORMAT",
    (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context,
     SqlBuilder sqlBuilder, ICollection<Param> outputParams) =>
{
    if (expr.Args.Count != 2)
        throw new ArgumentException("DATE_FORMAT requires 2 arguments");

    outSql.Append("DATE_FORMAT(");
    expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(", ");
    expr.Args[1].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(')');
});
```

### 4.4 Usage

```csharp
var users = await userService.SearchAsync(
    u => u.CreateTime.Format("yyyy-MM-dd") == "2026-03-31"
);
```

### 4.5 Real Formatting Example

Using `DateTime.ToString(format)` directly is preferred. Both manually constructing `FunctionExpr` and using `FunctionExpr("Format", ...)` ultimately fall to the database dialect's formatting function:

```csharp
using static LiteOrm.Common.Expr;
// Method 1: Directly construct FunctionExpr
var formatExpr = new FunctionExpr("Format", Prop("CreateTime"), new ValueExpr("yyyy-MM-dd"));
var results1 = await userService.SearchAsync(formatExpr == "2024-06-15");

// Method 2: Use ToString(format) directly in Lambda
Expression<Func<UserView, bool>> where =
    u => u.CreateTime.ToString("yyyy-MM-dd") == "2024-12-25";
var results2 = await userService.SearchAsync(where);
```

This example validates that both "manually constructing function expressions" and "writing Lambda directly" paths can fall to the database's native formatting function.
If your project doesn't need a custom `DateTime.Format(...)` business alias, using `ToString(format)` directly is more natural.

## 5. Example 2: Computed Properties

### 5.1 Define Computed Property

```csharp
public class User
{
    public DateTime BirthDate { get; set; }

    // Age is a computed property, not stored in database
    public int Age => DateTime.Now.Year - BirthDate.Year;
}
```

### 5.2 Register Member Handler

```csharp
LambdaExprConverter.RegisterMemberHandler(typeof(User), "Age", (node, converter) => {
    var userExpr = converter.Convert(node.Expression) as ValueTypeExpr;
    return new FunctionExpr("YEAR", new FunctionExpr("CURRENT_DATE")) -
           new FunctionExpr("YEAR", new PropertyExpr("BirthDate"));
});
```

### 5.3 Register SQL Handler

```csharp
using static LiteOrm.Common.Expr;
SqlBuilder.Instance.RegisterFunctionSqlHandler("YEAR",
    (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context,
     SqlBuilder sqlBuilder, ICollection<Param> outputParams) =>
{
    outSql.Append("YEAR(");
    expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(')');
});
```

### 5.4 Usage

```csharp
var adults = await userService.SearchAsync(u => u.Age >= 18);
```

## 6. Example 3: Custom String Functions

### 6.1 Register Method Handler

```csharp
LambdaExprConverter.RegisterMethodHandler("CustomProcess", (node, converter) => {
    var strExpr = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
    return new FunctionExpr("CUSTOM_PROCESS", strExpr);
});
```

### 6.2 Register SQL Handler

```csharp
using static LiteOrm.Common.Expr;
SqlServerBuilder.Instance.RegisterFunctionSqlHandler("CUSTOM_PROCESS",
    (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context,
     SqlBuilder sqlBuilder, ICollection<Param> outputParams) =>
{
    if (expr.Args.Count != 1)
        throw new ArgumentException("CUSTOM_PROCESS requires 1 argument");

    outSql.Append("dbo.CustomProcess(");
    expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(')');
});
```

### 6.3 Extension Method Definition

```csharp
public static class StringExtensions
{
    public static string CustomProcess(this string value)
    {
        return value.ToUpper();  // Local implementation
    }
}
```

### 6.4 Usage

```csharp
var users = await userService.SearchAsync(
    u => u.UserName.CustomProcess() == "ADMIN"
);
```

## 7. Example 4: Multi-Database Adaptation

### 7.1 Register for Different Databases Separately

```csharp
using static LiteOrm.Common.Expr;
// MySQL
MySqlBuilder.Instance.RegisterFunctionSqlHandler("CUSTOM_FUNC", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, SqlBuilder sqlBuilder, ICollection<Param> outputParams) => {
    outSql.Append("MYSQL_CUSTOM(");
    expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(')');
});

// SQL Server
SqlServerBuilder.Instance.RegisterFunctionSqlHandler("CUSTOM_FUNC", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, SqlBuilder sqlBuilder, ICollection<Param> outputParams) => {
    outSql.Append("dbo.CustomFunc(");
    expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(')');
});

// Oracle
OracleBuilder.Instance.RegisterFunctionSqlHandler("CUSTOM_FUNC", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, SqlBuilder sqlBuilder, ICollection<Param> outputParams) => {
    outSql.Append("CUSTOM_FUNC(");
    expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(')');
});
```

### 7.2 Global Registration (Same for All Databases)

```csharp
using static LiteOrm.Common.Expr;
// Global registration (SqlBuilder.Instance corresponds to default database)
SqlBuilder.Instance.RegisterFunctionSqlHandler("CUSTOM_FUNC", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, SqlBuilder sqlBuilder, ICollection<Param> outputParams) => {
    outSql.Append("CUSTOM_FUNC(");
    expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(')');
});
```

## 8. Advanced Usage

### 8.1 Handling Complex Parameters

```csharp
LambdaExprConverter.RegisterMethodHandler("InRange", (node, converter) => {
    var valueExpr = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
    var minExpr = converter.Convert(node.Arguments[1]) as ValueTypeExpr;
    var maxExpr = converter.Convert(node.Arguments[2]) as ValueTypeExpr;

    var greaterOrEqual = new LogicBinaryExpr(valueExpr, LogicOperator.GreaterThanOrEqual, minExpr);
    var lessOrEqual = new LogicBinaryExpr(valueExpr, LogicOperator.LessThanOrEqual, maxExpr);
    return greaterOrEqual.And(lessOrEqual);
});
```

### 8.2 Returning Logic Expressions

```csharp
using static LiteOrm.Common.Expr;
LambdaExprConverter.RegisterMethodHandler("IsValid", (node, converter) => {
    var propExpr = converter.Convert(node.Object) as ValueTypeExpr;
    return propExpr.IsNotNull() & (propExpr != "");
});
```

## 9. JSON Function Extensions

LiteOrm includes a set of generic JSON functions (`JsonExprExtensions`, namespace `LiteOrm.Common`) that can be used directly via `Expr` or Lambda, with each dialect's SqlBuilder mapping them to the corresponding database's native JSON functions.

> For using `JsonNode` indexers and `GetValue<T>()` in Lambda, see the [Lambda Guide](../02-core-usage/05-lambda-guide.en.md#7-jsonnode-queries). For auto-mapping and serialization of JsonNode properties, see [Data Mapping & Value Conversion](../03-advanced-topics/11-data-mapping.en.md#33-jsonnode-mapping-navigation).

### 9.1 Generic JSON Function Overview

| Generic Function | Purpose | Return Type |
|-----------------|---------|-------------|
| `JsonExtract(expr, path)` | Extract JSON fragment (object/array) | JSON |
| `JsonValue(expr, path)` | Extract scalar value (string/number/boolean) | scalar |
| `JsonQuery(expr, path)` | Extract JSON fragment (same as JsonExtract, dialect differences) | JSON |
| `JsonContains(expr, candidate)` | Check if JSON contains the specified element | bool |
| `JsonObject(key1, val1, key2, val2, ...)` | Construct JSON object | JSON |
| `JsonArray(val1, val2, ...)` | Construct JSON array | JSON |
| `IsJson(expr)` | Check if value is valid JSON | bool |

### 9.2 Using in Expr

```csharp
using static LiteOrm.Common.Expr;

// Extract JSON field (returns JSON fragment, can be further nested)
var expr = Prop("Settings").JsonExtract("$.name");

// Extract scalar value
var nameExpr = Prop("Settings").JsonValue("$.profile.name");

// Used in WHERE condition
var adults = await userDAO.SearchAsync(
    Prop("Settings").JsonValue("$.age").As<int>() >= 18
);

// Nested JSON path
var firstTag = Prop("Settings").JsonExtract("$.tags[0]");

// Construct JSON object
var objExpr = JsonObject(
    Const("name"), Prop("UserName"),
    Const("age"), Prop("Age")
);

// Construct JSON array
var arrExpr = JsonArray(Prop("Id"), Prop("UserName"));

// Check if valid JSON
var valid = await userDAO.SearchAsync(Prop("Settings").IsJson());
```

### 9.3 JSON Function Mapping by Database

| Generic Function | SQL Server | MySQL | PostgreSQL | SQLite | Oracle |
|-----------------|-----------|-------|------------|--------|--------|
| `JsonExtract` | `JSON_QUERY` | `JSON_EXTRACT` | `->` operator | `json_extract` | `JSON_QUERY` |
| `JsonValue` | `JSON_VALUE` | `JSON_UNQUOTE(JSON_EXTRACT(...))` | `->>` operator | `json_extract` | `JSON_VALUE` |
| `JsonQuery` | `JSON_QUERY` | `JSON_EXTRACT` | `->` operator | `json_extract` | `JSON_QUERY` |
| `JsonContains` | - | `JSON_CONTAINS` | `@>` operator | - | - |
| `JsonObject` | `JSON_OBJECT` | `JSON_OBJECT` | `jsonb_build_object` | `json_object` | `JSON_OBJECT` |
| `JsonArray` | `JSON_ARRAY` | `JSON_ARRAY` | `jsonb_build_array` | `json_array` | `JSON_ARRAY` |
| `IsJson` | `ISJSON` | `JSON_VALID` | - | `json_valid` | `IS JSON` |

> Different databases have varying levels of JSON function support. Entries marked with `-` indicate no native mapping is currently available. PostgreSQL's `JsonContains` uses the `@>` operator on `jsonb` types, so the column type must be jsonb.

### 9.4 PostgreSQL-Specific JSON/JSONB Extensions

The PostgreSQL dialect provides richer jsonb-specific extensions (namespace `LiteOrm.Pgsql`):

| Function | Description |
|----------|-------------|
| `JsonbExtractPath(expr, path...)` | Extract jsonb path (equivalent to `#>`) |
| `JsonbExtractPathText(expr, path...)` | Extract path text (equivalent to `#>>`) |
| `JsonbContains(jsonbExpr, candidateExpr)` | jsonb containment check (`@>` operator) |
| `JsonbBuildObject(key, val, ...)` | Construct jsonb object |
| `JsonbBuildArray(val, ...)` | Construct jsonb array |

### 9.5 How JsonNode Indexers Map in Lambda

When using indexers (`entity.JsonProp["key"]`) or `GetValue<T>()` on `JsonNode` in Lambda expressions, handlers registered in `LiteOrmLambdaHandlerInitializer` convert them into `FunctionExpr("JsonExtract", ...)` or `FunctionExpr("JsonValue", ...)`, which SqlBuilder then renders into dialect-specific native SQL.

Conversion chain:

```
entity.Settings["profile"]["age"].GetValue<int>()
        ↓  Lambda parsing
BuildJsonAccess() recursively resolves index chain, building path "$.profile.age"
        ↓
new FunctionExpr("JsonValue", PropertyExpr("Settings"), ValueExpr("$.profile.age"))
        ↓  SqlBuilder dialect mapping
JSON_UNQUOTE(JSON_EXTRACT(Settings, '$.profile.age'))   (MySQL)
Settings ->> '$.profile.age'                              (PostgreSQL)
json_extract(Settings, '$.profile.age')                   (SQLite)
JSON_VALUE(Settings, '$.profile.age')                     (SQL Server / Oracle)
```

### 9.6 Custom JSON Functions

To extend with custom JSON functions, register them the same way as regular functions:

```csharp
// Register Lambda method handler
LambdaExprConverter.RegisterMethodHandler(typeof(JsonNode), "GetArrayLength", (node, converter) =>
{
    var baseExpr = converter.Convert(node.Object!) as ValueTypeExpr;
    return new FunctionExpr("JSON_LENGTH", baseExpr);
});

// Register SQL handler for MySQL dialect
MySqlBuilder.Instance.RegisterFunctionSqlHandler("JSON_LENGTH",
    (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context,
     SqlBuilder sqlBuilder, ICollection<Param> outputParams) =>
{
    outSql.Append("JSON_LENGTH(");
    expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(')');
});
```

## 10. Default Registered Lambda Methods

LiteOrm automatically registers many default methods on first access through `LiteOrmLambdaHandlerInitializer` and `LiteOrmSqlFunctionInitializer` (triggered by the static constructors of `LambdaExprConverter` and `SqlBuilder` respectively):

| Type | Method/Member | Description | Corresponding SqlFunction |
|------|--------------|-------------|--------------------------|
| `DateTime` | `.Now` | Current time | `CURRENT_TIMESTAMP` |
| `DateTime` | `.Today` | Today's date | `CURRENT_DATE` |
| `DateTime` | `.AddSeconds()` / `.AddMinutes()` etc. | Date arithmetic | Database DATE_ADD function |
| `string` | `.StartsWith()` | Prefix match | SQL `LIKE 'xxx%'` |
| `string` | `.EndsWith()` | Suffix match | SQL `LIKE '%xxx'` |
| `string` | `.Contains()` | Contains | SQL `LIKE '%xxx%'` |
| `string` | `.Length` | String length | Database LENGTH function |
| `string` | `.Concat()` | String concatenation | Database `+` or `||` or CONCAT |
| `string` | `.IndexOf()` | Substring position | Database INSTR / CHARINDEX |
| `string` | `.Substring()` | Substring extraction | Database SUBSTR / SUBSTRING |
| `string` | `.Trim()` / `.TrimStart()` / `.TrimEnd()` | Trim whitespace | SQL TRIM / LTRIM / RTRIM |
| `string` | `.Replace()` | String replacement | SQL REPLACE |
| `string` | `.Insert()` | Insert string | SQL INSERT |
| `string` | `.Remove()` | Remove characters | SQL LEFT |
| `string` | `.ToString(format)` | Formatting | SQL Format |
| `Math` | `.Abs()` / `.Max()` / `.Min()` etc. | Math functions | Directly converted to SQL |
| `Regex` | `Regex.IsMatch(input, pattern)` | Regex match predicate | `REGEXP_LIKE` (MySQL: `REGEXP`, PostgreSQL: `~`) |
| `Regex` | `Regex.Replace(input, pattern, replacement)` | Regex replace | `REGEXP_REPLACE` |
| `Regex` | `Regex.Match(input, pattern).Value` | Extract match substring | `REGEXP_SUBSTR` |
| `Regex` | `Regex.Match(input, pattern).Index` | Match start position | `REGEXP_INSTR - 1` |
| `Regex` | `Regex.Match(input, pattern).Success` | Whether matched | `REGEXP_LIKE` |
| `IList` | `.Contains()` | Collection contains | SQL `IN` |
| `TimeSpan` | `.TotalSeconds` / `.TotalDays` etc. | Time difference calculation | Database DateDiff function |
| `Equals()` | Instance/static Equals | Equality comparison | SQL `=` |
| C# `?:` | Conditional operator | Automatically converted to `Expr.If(...)` | SQL `CASE WHEN` |
| `ExprExtensions.To()` | Convert object to Expr | Type conversion | - |

```csharp
// The following Lambda expressions are automatically converted to corresponding SQL functions
var users = await userService.SearchAsync(u => u.CreateTime > DateTime.Now);
var users = await userService.SearchAsync(u => u.UserName.StartsWith("A"));
var users = await userService.SearchAsync(u => u.UserName.Contains("test"));
var users = await userService.SearchAsync(u => u.Tags.Contains(1));
var users = await userService.SearchAsync(u => u.CreateTime.AddDays(7) > DateTime.Now);
// Regex match (REGEXP_LIKE)
var users = await userService.SearchAsync(u => Regex.IsMatch(u.UserName!, @"\d+"));
// Regex replace (REGEXP_REPLACE)
var dt = await dao.Search(Expr.Query<TestUser, IQueryable<object>>(q => q
    .Select(u => new { Replaced = Regex.Replace(u.Name!, @"\d+", "#") })));
```

## 11. Default Registered SqlFunctions (Cross-Database)

LiteOrm automatically registers the following cross-database SqlFunctions on first access to `SqlBuilder` through `LiteOrmSqlFunctionInitializer`:

| SqlFunction | Description | Database Implementations |
|-------------|-------------|-------------------------|
| `Now` | Current timestamp | MySQL: `NOW()`, SQLite: `datetime('now')` |
| `Today` | Current date | MySQL: `CURDATE()`, SQLite: `date('now')` |
| `CASE` | Conditional expression | Standard SQL CASE WHEN |
| `Over` | Window function OVER clause | Standard SQL OVER |
| `RowsBetween` / `RangeBetween` | Window function frame definition | Standard ROWS/RANGE BETWEEN |
| `IndexOf` | String position (0-based) | MySQL: `INSTR()-1`, SQL Server: `CHARINDEX()-1` |
| `Substring` | String extraction (0-based) | MySQL: `SUBSTR(..., pos+1, len)` |
| `Trim` | Trim leading/trailing spaces/characters | `TRIM(str)` or `TRIM(BOTH char FROM str)` |
| `TrimStart` | Trim leading whitespace/characters | `LTRIM(str)` |
| `TrimEnd` | Trim trailing whitespace/characters | `RTRIM(str)` |
| `Remove` | Remove characters from position to end | SQL `LEFT(str, count)` |
| `IfNull` | Null value replacement | MySQL: `IFNULL`, SQL Server: `ISNULL`, Oracle: `NVL` |
| `Format` | Date formatting | Database-native FORMAT function |
| `AddSeconds` / `AddMinutes` etc. | Date arithmetic | Database DATE_ADD / DATEADD |
| `DateDiffSeconds` / `DateDiffDays` etc. | Date difference calculation | Database-specific functions |
| `TotalSeconds` / `TotalDays` etc. | Time value to number | Database-specific functions |
| `REGEXP_LIKE` | Regex match predicate | MySQL: `REGEXP`, PostgreSQL: `~`, others: `REGEXP_LIKE(a, b)` |
| `REGEXP_REPLACE` | Regex replace | Oracle/MySQL8+/PostgreSQL: `REGEXP_REPLACE` |
| `REGEXP_SUBSTR` | Extract match substring | Oracle/MySQL8+/PostgreSQL: `REGEXP_SUBSTR` |
| `REGEXP_INSTR` | Match position | Oracle/MySQL8+: `REGEXP_INSTR` (1-based; `Match().Index` mapping auto-subtracts 1) |
| `REGEXP_COUNT` | Match count | Oracle/MySQL8+: `REGEXP_COUNT` |

**Database-Specific Functions:**

**MySQL**: `LENGTH` → `CHAR_LENGTH()`

**SQL Server**: `Length` → `LEN()`, `IndexOf` → `CHARINDEX(..., ...+1)-1`

**SQLite**: Date functions use `julianday()` for calculation

**Oracle / PostgreSQL**: Use `EXTRACT()` for time intervals, `IfNull` → `NVL` / `COALESCE`

**Array and JSON functions**:

- PostgreSQL arrays: `array_to_string(array, delimiter[, null_string])`, `array_append(array, element)`, `ANY(array)` (`value = ANY(array)`, the array is bound as a single parameter).
- Common JSON functions (`JsonExprExtensions`, namespace `LiteOrm.Common`): `JsonExtract` / `JsonValue` / `JsonQuery` / `JsonContains` / `JsonObject` / `JsonArray` / `IsJson`, mapped to native functions per dialect (MySQL `JSON_EXTRACT` and friends, SQLite `json_extract` and friends, SQL Server `JSON_VALUE`/`JSON_QUERY`, Oracle `JSON_VALUE`/`JSON_QUERY`, PostgreSQL `->`/`->>`/`@>`).
- PgSQL-specific extensions (namespace `LiteOrm.Pgsql`): `ArrayToString`, `ArrayAppend`, `Any`, `Contains`, `JsonbExtractPath`, `JsonbExtractPathText`, `JsonbContains`, `JsonbBuildObject`, `JsonbBuildArray`.

## 12. Best Practices

- When creating custom expressions, prefer reusing existing base expression types like `FunctionExpr`, `LogicBinaryExpr`, `PropertyExpr` to avoid reinventing the wheel.
- If the same function needs to adapt to multiple databases, keep the differences in different `SqlBuilder` handlers rather than scattering branch logic throughout business code.
- For function extensions that can be affected by external input, combining with the [Function Validator](./02-function-validator.en.md) is recommended.
- For extensions targeting legacy or private databases, writing examples and generated SQL samples simultaneously is recommended for regression verification.

## 13. Related Links

- [Back to docs hub](../README.md)
- [Associations](../02-core-usage/08-associations.en.md)
- [Window Functions](../03-advanced-topics/04-window-functions.en.md)
- [Function Validator](./02-function-validator.en.md)
