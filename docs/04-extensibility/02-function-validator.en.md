# Function Expression Validator

`FunctionExprValidator` checks whether a query is allowed to use function expressions under the current policy.

## 1. Why it exists

When you expose custom functions through expression extension or dynamic query building, you may want a guardrail that allows only approved functions.

## 2. `FunctionPolicy`

```csharp
public enum FunctionPolicy
{
    AllowAll,
    AllowRegisted,
    Disallow
}
```

| Policy | Meaning |
|------|---------|
| `AllowAll` | allow any function expression |
| `AllowRegisted` | allow only functions registered through `SqlBuilder.RegisterFunctionSqlHandler` |
| `Disallow` | reject all function expressions |

The enum member name is intentionally `AllowRegisted` because that is the code-level identifier used by the library.

## 3. Built-in validator instances

```csharp
var allowAll = FunctionExprValidator.AllowAll;
var allowRegisted = FunctionExprValidator.AllowRegisted;
var disallow = FunctionExprValidator.Disallow;
```

For custom setup:

```csharp
var validator = new FunctionExprValidator(FunctionPolicy.AllowRegisted);
```

## 4. Typical usage

```csharp
if (!validator.Validate(expr))
    throw new InvalidOperationException("The query uses an unauthorized function expression.");
```

This fits:

- user-driven query builders
- DAO wrappers with extra safety checks
- global interception before query execution

## 5. Relationship to `SqlBuilder`

`AllowRegisted` depends on functions already registered through `SqlBuilder.RegisterFunctionSqlHandler`. That makes it a good production default when only known SQL translations should be available.

## 6. Recommended policies

- internal development or trusted tooling: `AllowAll`
- ordinary production queries: `AllowRegisted`
- restricted environments: `Disallow`

## Related Links

- [Back to English docs hub](../README.md)
- [Expression Extension](./01-expression-extension.en.md)
- [Window Functions](../03-advanced-topics/04-window-functions.en.md)
- [API Index](../05-reference/02-api-index.en.md)
