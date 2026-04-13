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
LambdaExprConverter.RegisterMemberHandler(typeof(string), null, handler);  // Handle all methods of this type
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
    ISqlBuilder sqlBuilder,
    ICollection<KeyValuePair<string, object>> outputParams);
```

```csharp
MySqlBuilder.Instance.RegisterFunctionSqlHandler("DATE_FORMAT",
    (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context,
     ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
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
    var dateExpr = converter.ConvertInternal(node.Object) as ValueTypeExpr;
    var formatExpr = converter.ConvertInternal(node.Arguments[0]) as ValueTypeExpr;
    return new FunctionExpr("DATE_FORMAT", dateExpr, formatExpr);
});
```

### 4.3 Register SQL Handler

```csharp
MySqlBuilder.Instance.RegisterFunctionSqlHandler("DATE_FORMAT",
    (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context,
     ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
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

### 4.5 Real Formatting Example from Demo

In `LiteOrm.Demo\Demos\DateFormatDemo.cs`, using `DateTime.ToString(format)` directly is preferred. Both manually constructing `FunctionExpr` and using `FunctionExpr("Format", ...)` ultimately fall to the database dialect's formatting function:

```csharp
// Method 1: Directly construct FunctionExpr
var formatExpr = new FunctionExpr("Format", Expr.Prop("CreateTime"), new ValueExpr("yyyy-MM-dd"));
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
    var userExpr = converter.ConvertInternal(node.Expression) as ValueTypeExpr;
    return new FunctionExpr("YEAR", new FunctionExpr("CURRENT_DATE")) -
           new FunctionExpr("YEAR", new PropertyExpr("BirthDate"));
});
```

### 5.3 Register SQL Handler

```csharp
SqlBuilder.Instance.RegisterFunctionSqlHandler("YEAR",
    (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context,
     ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
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
    var strExpr = converter.ConvertInternal(node.Arguments[0]) as ValueTypeExpr;
    return new FunctionExpr("CUSTOM_PROCESS", strExpr);
});
```

### 6.2 Register SQL Handler

```csharp
SqlServerBuilder.Instance.RegisterFunctionSqlHandler("CUSTOM_PROCESS",
    (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context,
     ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
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
// MySQL
MySqlBuilder.Instance.RegisterFunctionSqlHandler("CUSTOM_FUNC", (ref outSql, expr, context, sqlBuilder, outputParams) => {
    outSql.Append("MYSQL_CUSTOM(");
    expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(')');
});

// SQL Server
SqlServerBuilder.Instance.RegisterFunctionSqlHandler("CUSTOM_FUNC", (ref outSql, expr, context, sqlBuilder, outputParams) => {
    outSql.Append("dbo.CustomFunc(");
    expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(')');
});

// Oracle
OracleBuilder.Instance.RegisterFunctionSqlHandler("CUSTOM_FUNC", (ref outSql, expr, context, sqlBuilder, outputParams) => {
    outSql.Append("CUSTOM_FUNC(");
    expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(')');
});
```

### 7.2 Global Registration (Same for All Databases)

```csharp
// Global registration (SqlBuilder.Instance corresponds to default database)
SqlBuilder.Instance.RegisterFunctionSqlHandler("CUSTOM_FUNC", (ref outSql, expr, context, sqlBuilder, outputParams) => {
    outSql.Append("CUSTOM_FUNC(");
    expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(')');
});
```

## 8. Advanced Usage

### 8.1 Handling Complex Parameters

```csharp
LambdaExprConverter.RegisterMethodHandler("InRange", (node, converter) => {
    var valueExpr = converter.ConvertInternal(node.Arguments[0]) as ValueTypeExpr;
    var minExpr = converter.ConvertInternal(node.Arguments[1]) as ValueTypeExpr;
    var maxExpr = converter.ConvertInternal(node.Arguments[2]) as ValueTypeExpr;

    var greaterOrEqual = new LogicBinaryExpr(valueExpr, LogicOperator.GreaterThanOrEqual, minExpr);
    var lessOrEqual = new LogicBinaryExpr(valueExpr, LogicOperator.LessThanOrEqual, maxExpr);
    return greaterOrEqual.And(lessOrEqual);
});
```

### 8.2 Returning Logic Expressions

```csharp
LambdaExprConverter.RegisterMethodHandler("IsValid", (node, converter) => {
    var propExpr = converter.ConvertInternal(node.Object) as ValueTypeExpr;
    return propExpr.IsNotNull() & (propExpr != "");
});
```

## 9. Default Registered Lambda Methods

LiteOrm automatically registers many default methods at startup through `LiteOrmLambdaHandlerInitializer` and `LiteOrmSqlFunctionInitializer`:

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
| `IList` | `.Contains()` | Collection contains | SQL `IN` |
| `TimeSpan` | `.TotalSeconds` / `.TotalDays` etc. | Time difference calculation | Database DateDiff function |
| `Equals()` | Instance/static Equals | Equality comparison | SQL `=` |
| `ExprExtensions.To()` | Convert object to Expr | Type conversion | - |

```csharp
// The following Lambda expressions are automatically converted to corresponding SQL functions
var users = await userService.SearchAsync(u => u.CreateTime > DateTime.Now);
var users = await userService.SearchAsync(u => u.UserName.StartsWith("A"));
var users = await userService.SearchAsync(u => u.UserName.Contains("test"));
var users = await userService.SearchAsync(u => u.Tags.Contains(1));
var users = await userService.SearchAsync(u => u.CreateTime.AddDays(7) > DateTime.Now);
```

## 10. Default Registered SqlFunctions (Cross-Database)

LiteOrm automatically registers the following cross-database SqlFunctions at startup through `LiteOrmSqlFunctionInitializer`:

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

**Database-Specific Functions:**

**MySQL**: `LENGTH` → `CHAR_LENGTH()`

**SQL Server**: `Length` → `LEN()`, `IndexOf` → `CHARINDEX(..., ...+1)-1`

**SQLite**: Date functions use `julianday()` for calculation

**Oracle / PostgreSQL**: Use `EXTRACT()` for time intervals, `IfNull` → `NVL` / `COALESCE`

## 11. Best Practices

- When creating custom expressions, prefer reusing existing base expression types like `FunctionExpr`, `LogicBinaryExpr`, `PropertyExpr` to avoid reinventing the wheel.
- If the same function needs to adapt to multiple databases, keep the differences in different `SqlBuilder` handlers rather than scattering branch logic throughout business code.
- For function extensions that can be affected by external input, combining with the [Function Validator](./02-function-validator.en.md) is recommended.
- For extensions targeting legacy or private databases, writing examples and generated SQL samples simultaneously is recommended for regression verification.

## 12. Related Links

- [Back to docs hub](../README.md)
- [Associations](../02-core-usage/05-associations.en.md)
- [Window Functions](../03-advanced-topics/04-window-functions.en.md)
- [Function Validator](./02-function-validator.en.md)
