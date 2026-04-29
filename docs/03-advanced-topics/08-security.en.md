# Security

LiteOrm incorporates multiple layers of SQL injection prevention at the architectural level. This document comprehensively covers the framework's security strategy, the principles behind each component, potential risk points, and best practices.

## 1. Defense Overview

LiteOrm's SQL injection prevention uses a **multi-layered defense-in-depth** strategy:

| Layer | Mechanism | Description |
|-------|-----------|-------------|
| Parameterized SQL | `outputParams` + placeholders | All user values are passed as parameters, no exceptions |
| LIKE escaping + parameterization | Wildcard escaping + `ESCAPE` clause | Dual protection against LIKE injection |
| ExprString auto-parameterization | Non-Expr values auto-converted to named params | User values in interpolated strings are automatically parameterized |
| Expression type whitelist | `ExprTypeValidator` | Controls allowed expression types |
| Function policy control | `FunctionExprValidator` | Controls the range of executable SQL functions |
| Custom SQL pre-registration | `GenericSqlExpr` | Forbids dynamic creation of unregistered SQL fragments |

---

## 2. Parameterized SQL — Core Defense Line

### 2.1 Parameter Passing Mechanism

All SQL value passing in LiteOrm goes through the `outputParams` collection:

```csharp
public static string ToSql(this Expr expr, SqlBuildContext context, ISqlBuilder sqlBuilder,
    ICollection<KeyValuePair<string, object>> outputParams)
```

The generated SQL uses parameter placeholders (e.g., `@0`, `@1`), and values are passed independently through `outputParams` — **user input is never directly concatenated into the SQL string**.

**Example**:

```csharp
var users = await userService.SearchAsync(u => u.UserName == "John");
// Generated SQL: SELECT * FROM Users WHERE UserName = @0
// Parameters: @0 = "John"
```

### 2.2 Tiered Value Handling

| Value Type | Handling | Security Analysis |
|-----------|----------|-------------------|
| `null` | Output `NULL` literal | Safe, not injectable |
| `bool` | Output `1` / `0` | Safe |
| Primitive numerics (int/long/float etc.) | Inline `.ToString()` | Safe, numeric types cannot contain SQL special chars |
| Collections (IN clause) | Each element individually parameterized | Safe |
| string, DateTime, etc. | Parameterized | Safe |

```csharp
// Numeric values are inlined (only for IsConst + primitive numeric types)
var users = await userService.SearchAsync(u => u.Age >= 18);
// SQL: SELECT * FROM Users WHERE Age >= 18

// Strings are always parameterized
var users = await userService.SearchAsync(u => u.UserName == "O'Brien");
// SQL: SELECT * FROM Users WHERE UserName = @0
// Parameters: @0 = "O'Brien"  (single quote safely handled)
```

### 2.3 Dual Protection for LIKE Queries

LIKE queries use both **parameterization** and **wildcard escaping**:

```csharp
var users = await userService.SearchAsync(u => u.UserName.Contains("100%"));
// SQL: SELECT * FROM Users WHERE UserName LIKE @0 ESCAPE '/'
// Parameters: @0 = "%100/%%"  (% is escaped as /%)
```

Escaping rules: wildcards `_`, `%`, `[`, `]` are escaped with the `/` prefix (declared via `ESCAPE '/'`):

```
User input: "100%_test"
Escaped param value: "100/%/_test"  → LIKE @0 ESCAPE '/'
```

### 2.4 NULL Safety

`Column = NULL` is automatically converted to `IS NULL`, avoiding the semantic error of `= NULL` in standard SQL:

```csharp
var results = await viewService.SearchAsync(u => u.UserName == null);
// SQL: SELECT * FROM Users WHERE UserName IS NULL
```

### 2.5 Database-Specific Parameter Placeholders

| Database | Placeholder Format | Example |
|----------|-------------------|---------|
| SQL Server / MySQL / PostgreSQL / SQLite | `@n` | `@0`, `@1` |
| Oracle | `:n` | `:0`, `:1` |

Placeholder generation is abstracted by the `ISqlBuilder` interface, with each database dialect implementing `ToSqlParam(name)` and `ToParamName(name)`.

---

## 3. ExprString — Safe Interpolated String Parsing

### 3.1 Design Principle

`ExprString` is a `ref struct` marked with `[InterpolatedStringHandler]`, leveraging C# compiler support to parse interpolated strings at compile time and automatically distinguish between Expr and plain values:

```csharp
var sql = $"WHERE {Expr.Prop("Age")} > {minAge}";
// Expr.Prop("Age")  → goes through Expr.ToSql(), fully parameterized
// minAge (int)      → auto-generated parameter placeholder @0, value added to outputParams
```

### 3.2 Processing Paths

```
Interpolated string $"..."
    │
    ├─ Format item is an Expr object → expr.ToSql() → full expression tree processing
    │
    ├─ Format item is a plain value  → auto-generate @N placeholder + add to param list
    │
    └─ Literal string                → appended directly (developer-hardcoded SQL keywords/structure)
```

**Code example**:

```csharp
string keyword = "John";
int minAge = 18;

// ExprString auto-handles:
// - "WHERE " is a literal, appended directly
// - {keyword} is a plain value, auto-parameterized as @0
// - {Expr.Prop("Age")} is an Expr, goes through ToSql parameterization
// - " >= " is a literal
// - {minAge} is a plain value, auto-parameterized as @1
var sql = $"WHERE {Expr.Prop("UserName")} LIKE {'%' + keyword + '%'} AND {Expr.Prop("Age")} >= {minAge}";
```

### 3.3 Key Points

- **Literal strings** (`AppendLiteral`) are developer-hardcoded SQL keywords and structure, **must not be controllable by users**
- **Plain values in format items** (non-Expr) are **automatically parameterized**, no manual handling needed

---

## 4. ExprValidator — Expression Validator

### 4.1 Architecture

LiteOrm uses the **Visitor pattern** + **Strategy pattern** for expression validation:

```
ExprValidator (abstract base)
├── ExprTypeValidator      ── expression type whitelist
├── FunctionExprValidator  ── function policy
└── ExprValidatorGroup     ── composite validators
```

### 4.2 Type Whitelist Validation (ExprTypeValidator)

Controls allowed expression types via an `ExprType` whitelist:

```csharp
// Minimum: allows basic query conditions (12 types)
// Value, Property, Unary, ValueSet, LogicBinary, And, Or, Not,
// Where, OrderBy, OrderByItem, Section
// Forbidden: SelectItem, From, Table, Function, Update, Delete

// QueryOnly: allows full SELECT queries (20 types)
// Includes all Minimum types + SelectItem, From, GroupBy, TableJoin
// Explicitly forbidden: Update, Delete
```

```csharp
var validator = ExprValidator.CreateQueryOnly();

if (validator.VisitAll(expr))
{
    var results = await userService.SearchAsync(expr);
}
else
{
    // validator.FailedExpr contains the rejected node
    throw new UnauthorizedAccessException("Query contains disallowed expressions");
}
```

### 4.3 Function Policy Validation (FunctionExprValidator)

Controls the execution scope of `FunctionExpr`:

| Policy | Value | Description | Use Case |
|--------|-------|-------------|----------|
| `AllowAll` | 0 | Allow all functions | Local dev / internal tools |
| `AllowRegisted` | 1 | Only allow registered functions | **Recommended for production** |
| `Disallow` | 2 | Forbid all functions | Fully restricted environments |

```csharp
// Production recommendation: only allow registered functions
var validator = ExprValidatorGroup.Create(
    ExprValidator.CreateQueryOnly(),
    FunctionExprValidator.AllowRegisted
);

// Validate before Search
if (!validator.VisitAll(expr))
{
    throw new UnauthorizedAccessException(
        $"Blacklisted expression found: {validator.FailedExpr}"
    );
}
```

`AllowRegisted` checks whether the function has been registered in `SqlBuilder`:

```csharp
case FunctionPolicy.AllowRegisted:
    return SqlBuilder.Instance.TryGetFunctionSqlHandler<SqlBuilder>(
        funcExpr.FunctionName, out _);
```

### 4.4 Composite Validators

```csharp
var validator = ExprValidatorGroup.Create(
    ExprValidator.CreateQueryOnly(),      // only allow query types
    FunctionExprValidator.AllowRegisted   // only allow registered functions
);

if (!validator.VisitAll(expr))
{
    // validator.FailedExpr     — node that failed
    // validator.FailedVisitor  — validator that failed
}
```

Validator groups use **short-circuit evaluation**: execution stops at the first failed validator, and the failing validator is recorded.

---

## 5. GenericSqlExpr — Custom SQL Fragments

### 5.1 Design Purpose

`GenericSqlExpr` provides a safe mechanism for embedding custom SQL fragments, controlling SQL generation through **pre-registration + callback delegate**:

```csharp
public delegate string SqlGenerateHandler(
    SqlBuildContext context, ISqlBuilder sqlBuilder,
    ICollection<KeyValuePair<string, object>> outputParams, object arg);

public sealed class GenericSqlExpr : LogicExpr
{
    public string Key { get; set; }   // unique key for registry lookup
    public object Arg { get; set; }   // extra argument passed to callback
}
```

### 5.2 Registration Mechanism

```csharp
// Register a custom SQL generator
GenericSqlExpr.Register("CustomCheck", (context, sqlBuilder, outputParams, arg) =>
{
    // Parameterize: use outputParams to pass user values
    string paramName = outputParams.Count.ToString();
    outputParams.Add(new(sqlBuilder.ToParamName(paramName), arg));
    return $"dbo.CustomCheck({sqlBuilder.ToSqlParam(paramName)})";
});

// Use in queries
var expr = Expr.Prop("IsActive") == true
    & new GenericSqlExpr("CustomCheck") { Arg = "someValue" };
var users = await userService.SearchAsync(expr);
```

### 5.3 Security Features

1. **Must be pre-registered**: Maintains a global registry via `ConcurrentDictionary`; unregistered keys throw exceptions
2. **Supports parameterization**: The delegate signature includes `outputParams`, allowing safe passing of user values
3. **Parameter passing**: Business parameters are passed via the `Arg` property, not concatenated into SQL

---

## 6. Expr Risk Points and Precautions

### 6.1 ExprString Usage Limitations

`ExprString` is a `ref struct` marked with `[InterpolatedStringHandler]`. It is **only generated by the compiler when calling methods that accept `ExprString` type parameters** (e.g., `dao.Search(...)`, `SqlGen.ToSql(...)`, etc.). **Regular interpolated strings produce plain `string` values and do NOT auto-parameterize**:

```csharp
string userInput = request.Query["name"];

// ❌ Wrong: regular interpolated strings produce plain string, not ExprString, no auto-parameterization
var badSql1 = $"SELECT * FROM Users WHERE Name = '{userInput}'";  // Dangerous: value inlined as literal
var badSql2 = $"SELECT * FROM Users WHERE Name = {userInput}";    // Wrong: still just a plain string, not parameterized

// ✅ Correct: use ExprString in DAO methods (method accepts ExprString type parameter)
var result = await dao.Search($"WHERE {Expr.Prop("Name")} == {userInput}").ToListAsync();
// Generated SQL: WHERE Name = @0
// Parameters: @0 = value of userInput

// ✅ Correct: use Expr expressions to build queries
var expr = Expr.Prop("Name") == userInput;
var users = await userService.SearchAsync(expr);
```

Within `ExprString`, **literal strings** (`AppendLiteral`) are developer-hardcoded SQL keywords and structure, **must not be controllable by users**; plain values in format items (non-Expr) are automatically parameterized.

### 6.2 GenericSqlExpr Freedom

The `SqlGenerateHandler` delegate can return any string. If `outputParams` is not carefully used within the callback, injection points can be introduced in custom SQL:

```csharp
// ❌ Dangerous: user input concatenated directly in delegate
GenericSqlExpr.Register("UnsafeLookup", (ctx, sb, outputParams, arg) =>
{
    return $"SELECT * FROM Users WHERE Code = '{arg}'";
});

// ✅ Safe: use outputParams for parameterization
GenericSqlExpr.Register("SafeLookup", (ctx, sb, outputParams, arg) =>
{
    string paramName = outputParams.Count.ToString();
    outputParams.Add(new(sb.ToParamName(paramName), arg));
    return $"SELECT * FROM Users WHERE Code = {sb.ToSqlParam(paramName)}";
});
```

### 6.3 Expr.Prop Property Name Source

`Expr.Prop` already validates property names and table aliases internally (rejecting special characters like `@`, `-`, spaces, etc.). Passing an invalid name will throw an `ArgumentException`, so additional validation is generally unnecessary:

```csharp
// ✅ Safe: Prop internally validates names
var propName = request.Query["field"];  // user-controllable
// new PropertyExpr("Name@123")  → throws ArgumentException
// new PropertyExpr("Name-Column") → throws ArgumentException
var expr = Expr.Prop(propName) == "value";
```

If you need to restrict the allowed field range (e.g., only allow querying specific columns), use a whitelist for **business-level restrictions**:

```csharp
// ✅ Recommended: use a whitelist when field range restrictions are needed
var allowedFields = new HashSet<string> { "UserName", "Age", "Email" };
if (!allowedFields.Contains(propName))
    throw new ArgumentException("Invalid field");
var expr = Expr.Prop(propName) == "value";
```

### 6.4 Risk of Frontend-Submitted Expr JSON

When allowing frontends to construct Expr via JSON (see [Frontend Native Expr](../04-extensibility/06-frontend-native-expr.en.md)), always use validators:

```csharp
var expr = ExprJsonConvert.Deserialize(json);
var validator = ExprValidatorGroup.Create(
    ExprValidator.CreateQueryOnly(),
    FunctionExprValidator.AllowRegisted
);

if (!validator.VisitAll(expr))
{
    throw new UnauthorizedAccessException("Query rejected by security validator");
}

// Additional recommendation: restrict to specific tables and columns
var propValidator = new PropertyNameValidator(new[] { "UserName", "Age", "CreateTime" });
if (!propValidator.VisitAll(expr))
{
    throw new UnauthorizedAccessException("Field access denied");
}
```

### 6.5 Coordination with Permission Filtering

Security filtering should be used in conjunction with [Permission Filtering](./06-permission-filtering.en.md):

```csharp
// Before entering Search, append user scope conditions
LogicExpr permissionFilter = GetCurrentUserPermissionExpr();
LogicExpr finalExpr = expr & permissionFilter;

// Then pass through security validator
if (!securityValidator.VisitAll(finalExpr))
    throw new UnauthorizedAccessException();

var results = await userService.SearchAsync(finalExpr);
```

### 6.6 Expr Flexibility and Precautions

While the Expr expression system can eliminate SQL injection at the architectural level, it is very powerful and flexible — use with care:

- **Powerful expression capabilities**: Expr supports subqueries, function calls, cross-table joins, and other complex operations. Improper use may lead to performance issues or unexpected behavior
- **Validators are not enabled by default**: `ExprValidator` is optional. Without configuring validators, Expr can generate arbitrary SQL structures (including UPDATE, DELETE, etc.)
- **Always configure validators in production**: Use `ExprValidator.CreateQueryOnly()` + `FunctionExprValidator.AllowRegisted` to limit expression capabilities
- **Always validate frontend-submitted Expr**: If allowing frontends to construct Expr JSON, always use `ExprValidator` + `PropertyNameValidator` for dual validation
- **Avoid excessive dynamism**: Try to avoid dynamically constructing overly complex expression trees based on user input; keep business logic predictable

---

## 7. Security Checklist

When using LiteOrm in production, confirm each item:

| Check Item | Description |
|------------|-------------|
| ✅ Enable `AllowRegisted` function policy | Prevent execution of unregistered SQL functions |
| ✅ Use validators before frontend Expr queries | Restrict expression types and field access |
| ✅ Use `outputParams` in custom SQL | Parameterize within GenericSqlExpr callbacks |
| ✅ Expr.Prop has built-in name validation | Invalid names throw exceptions; use whitelist only for field range restrictions |
| ✅ Use ExprString through DAO methods | Regular interpolated strings do not produce ExprString; use `dao.Search(...)` etc. |
| ✅ Coordinate with permission filtering | Layer user scope filtering on top of validators |
| ✅ Don't accept raw wildcards in LIKE | Consider escaping/forbidding wildcards in frontend LIKE values |
| ✅ Be aware of Expr flexibility | Expr is powerful; always configure validators in production to limit capabilities |

---

## 8. Related Links

- [Back to docs hub](../README.md)
- [Function Validator](../04-extensibility/02-function-validator.en.md)
- [Permission Filtering](./06-permission-filtering.en.md)
- [Frontend Native Expr](../04-extensibility/06-frontend-native-expr.en.md)
- [Expression Extension](../04-extensibility/01-expression-extension.en.md)
