# Function Expression Validator

`FunctionExprValidator` validates whether function expressions in queries comply with established security policies.

## 1. Overview

When using expression extension features to register custom functions, you may need to restrict the usage scope of these functions to prevent abuse or security risks. `FunctionExprValidator` provides a mechanism to control which function expressions are allowed in queries.

## 2. FunctionPolicy Enum

```csharp
public enum FunctionPolicy
{
    AllowAll,       // Allow all function expressions
    AllowRegisted, // Only allow pre-registered function expressions
    Disallow        // Disallow any function expressions
}
```

### 2.1 Policy Descriptions

| Policy | Description |
|--------|-------------|
| `AllowAll` | Allow any function expression, including unregistered ones |
| `AllowRegisted` | Only allow functions registered via `SqlBuilder.RegisterFunctionSqlHandler` |
| `Disallow` | Disallow any function expressions |

## 3. Usage

### 3.1 Preconfigured Validators

`FunctionExprValidator` provides three preconfigured static instances:

```csharp
// Allow all functions
var validatorAllowAll = FunctionExprValidator.AllowAll;

// Only allow registered functions
var validatorAllowRegisted = FunctionExprValidator.AllowRegisted;

// Disallow any functions
var validatorDisallow = FunctionExprValidator.Disallow;
```

### 3.2 Custom Validator

```csharp
var validator = new FunctionExprValidator(FunctionPolicy.AllowRegisted);
```

### 3.3 Recommended Policy Templates

| Environment or Role | Recommended Policy |
|--------------------|-------------------|
| Local development / Internal tools | `AllowAll` |
| Production environment business queries | `AllowRegisted` |
| Restricted scenarios prohibiting custom functions | `Disallow` |

## 4. Validation Interface

`FunctionExprValidator` inherits from `ExprValidator`. Single node validation uses `Validate(node)`, while full expression tree validation more commonly uses `VisitAll(expr)`:

```csharp
public override bool Validate(Expr node)
{
    if (node == null) return true;
    if (node is FunctionExpr funcExpr)
    {
        switch (FunctionPolicy)
        {
            case FunctionPolicy.AllowAll:
                return true;
            case FunctionPolicy.AllowRegisted:
                return SqlBuilder.Instance.TryGetFunctionSqlHandler<SqlBuilder>(
                    funcExpr.FunctionName, out _);
            case FunctionPolicy.Disallow:
                return false;
        }
    }
    return true;
}
```

## 5. Usage Scenarios

### 5.1 Restrict User Queries

In multi-tenant scenarios, restrict which functions different tenants can use:

```csharp
public class UserQueryService
{
    private readonly FunctionExprValidator _validator;

    public UserQueryService(UserRole role)
    {
        _validator = role == UserRole.Admin
            ? FunctionExprValidator.AllowAll
            : FunctionExprValidator.AllowRegisted;
    }

    public async Task<List<User>> SearchAsync(Expr query)
    {
        // Validate query expression
        if (!_validator.VisitAll(query))
            throw new InvalidOperationException("Query contains disallowed function expressions");

        return await _userViewDAO.Search(query).ToListAsync();
    }
}
```

### 5.2 Custom DAO Validation

```csharp
public class SafeUserDAO : ObjectViewDAO<User>
{
    private static readonly FunctionExprValidator Validator =
        FunctionExprValidator.AllowRegisted;

    public async Task<List<User>> SafeSearchAsync(Expr expr)
    {
        if (!Validator.VisitAll(expr))
            throw new SecurityException("Expression validation failed");

        return await Search(expr).ToListAsync();
    }
}
```

### 5.3 Global Query Interception

Perform global validation before query execution:

```csharp
public class QueryInterceptor
{
    private readonly FunctionExprValidator _validator;

    public QueryInterceptor(FunctionPolicy policy)
    {
        _validator = new FunctionExprValidator(policy);
    }

    public void Intercept(Expr query)
    {
        if (!_validator.VisitAll(query))
        {
            throw new UnauthorizedAccessException(
                "Query contains unauthorized function expressions");
        }
    }
}
```

### 5.4 Integration with Expression Extension

A typical flow: first register allowed extension functions, then use the validator as a final check before query execution.

```csharp
MySqlBuilder.Instance.RegisterFunctionSqlHandler("DATE_FORMAT", ...);

var validator = FunctionExprValidator.AllowRegisted;
var expr = new FunctionExpr("DATE_FORMAT", ...);

if (!validator.VisitAll(expr))
    throw new InvalidOperationException("Function not registered, execution disallowed");
```

## 6. Working with SqlBuilder

`FunctionExprValidator.AllowRegisted` depends on functions registered via `SqlBuilder.RegisterFunctionSqlHandler`:

```csharp
// Register function handlers
MySqlBuilder.Instance.RegisterFunctionSqlHandler("DATE_FORMAT", ...);
MySqlBuilder.Instance.RegisterFunctionSqlHandler("CONCAT", ...);

// AllowRegisted validator only allows these registered functions
var validator = FunctionExprValidator.AllowRegisted;

// DATE_FORMAT - allowed (registered)
var expr1 = new FunctionExpr("DATE_FORMAT", ...);
validator.VisitAll(expr1);  // true

// CUSTOM_UNREGISTERED - rejected (not registered)
var expr2 = new FunctionExpr("CUSTOM_UNREGISTERED", ...);
validator.VisitAll(expr2);  // false
```

## 7. Caveats

1. **Validation timing**: It is recommended to validate the entire Expr tree before query execution, not just the root node.
2. **Invocation method**: Prefer using `validator.VisitAll(expr)` in business scenarios; `Validate(node)` is more suitable for overriding when implementing validators.
3. **Performance impact**: The validation process traverses the expression tree, which has some performance overhead.
4. **Security consideration**: It is recommended to use `AllowRegisted` policy in production environments.

## Related Links

- [Back to docs hub](../README.md)
- [Associations](../02-core-usage/05-associations.en.md)
- [Expression Extension](./01-expression-extension.en.md)
- [Window Functions](../03-advanced-topics/04-window-functions.en.md)
