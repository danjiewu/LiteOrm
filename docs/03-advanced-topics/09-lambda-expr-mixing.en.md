# Mixing Lambda and Expr

LiteOrm supports multiple ways to combine Lambda expressions and `Expr` objects. This document introduces two core patterns:

- **`To<T>()`**: Embeds a pre-built `Expr` object into a Lambda expression
- **`Expr.Lambda<T>()`**: Converts a Lambda expression to `LogicExpr`, then combines with `Expr` dynamically

## 1. To<T>() Extension Method

### 1.1 Core Mechanism

`Expr.To<T>()` is a bridging method designed for Lambda expression parsing:

```csharp
public static T To<T>(this Expr expr)
{
    throw new NotSupportedException("Only supported in Lambda expression parsing scenarios.");
}
```

It throws an exception during normal execution but is recognized and replaced by `LambdaExprConverter` during Lambda parsing.

### 1.2 Simple Combination

Combine `Expr`-built conditions with Lambda conditions:

```csharp
var condition = u => u.Age >= 18 && Expr.Prop("UserName").Contains("John").To<bool>();
var users = await userService.SearchAsync(condition);
```

### 1.3 Dynamic Condition Encapsulation

Encapsulate complex dynamic conditions as `LogicExpr` for reuse in Lambdas:

```csharp
LogicExpr filter = null;

if (!string.IsNullOrEmpty(keyword))
    filter &= Expr.Prop("UserName").Contains(keyword);

if (minAge.HasValue)
    filter &= Expr.Prop("Age") >= minAge.Value;

if (filter != null)
{
    var users = await userService.SearchAsync(
        u => filter.To<bool>()
    );
}
```

### 1.4 Association Queries with Exists

Use `Expr.ExistsRelated<T>()` to build association Exists conditions:

```csharp
var hasRelatedOrders = Expr.ExistsRelated<Order>(
    Expr.Prop("T0", "Id") == Expr.Prop("UserId")
    && Expr.Prop("Status") != "Completed"
);

var activeUsers = await userService.SearchAsync(
    u => u.IsActive == true && hasRelatedOrders.To<bool>()
);
```

## 2. Expr.Lambda<T>() Method

### 2.1 Core Mechanism

`Expr.Lambda<T>()` converts a Lambda expression to `LogicExpr`, used for embedding complex Lambda conditions in `Expr` dynamic construction:

```csharp
// Convert Lambda expression to LogicExpr
var lambdaExpr = Expr.Lambda<User>(u => u.Age > 18 && u.IsActive);
```

### 2.2 Combining with Expr

Combine the `LogicExpr` returned by `Expr.Lambda<T>()` with other `Expr` dynamically:

```csharp
// Lambda-converted condition
var lambdaCondition = Expr.Lambda<User>(u => u.Age > 18);

// Dynamically build additional conditions
LogicExpr filter = null;
if (!string.IsNullOrEmpty(keyword))
    filter &= Expr.Prop("UserName").Contains(keyword);

// Combine: lambdaCondition AND filter
var combined = lambdaCondition & filter;

var users = await userService.SearchAsync(
    u => combined.To<bool>()
);
```

## 3. Practical Scenarios

### 3.1 Dynamic Filter Combination

```csharp
LogicExpr filter = null;

if (!string.IsNullOrEmpty(keyword))
    filter &= Expr.Prop("UserName").Contains(keyword);

if (minAge.HasValue)
    filter &= Expr.Prop("Age") >= minAge.Value;

if (isActive.HasValue)
    filter &= Expr.Prop("IsActive") == isActive.Value;

var users = await userService.SearchAsync(
    u => u.IsActive == true && filter.To<bool>()
);
```

### 3.2 Combining Predefined and Dynamic Conditions

```csharp
// Predefined base condition
var baseCondition = Expr.Lambda<User>(u => u.IsActive == true);

// Dynamic additional conditions
LogicExpr extraFilter = null;
if (minAge.HasValue)
    extraFilter &= Expr.Prop("Age") >= minAge.Value;

// Combined usage
var combined = baseCondition & extraFilter;

var users = await userService.SearchAsync(
    u => combined.To<bool>()
);
```

## 4. Important Notes

### 4.1 Type Consistency for To<T>()

The generic parameter `T` in `To<T>()` should match the return type of the Lambda expression:

```csharp
// Condition expression returns bool
u => u.Age >= 18 && expr.To<bool>()
```

### 4.2 Avoid Calling in Non-Parsing Scenarios

`To<T>()` throws `NotSupportedException` during normal execution and can only be used in Lambda parameters of query methods.

### 4.3 Performance Considerations

Hybrid usage does not introduce additional performance overhead. `To<T>()` is replaced during parsing, and actual SQL is generated from the optimized expression tree.

## 5. Comparison with Pure Expr Query

| Feature | Pure Lambda | Pure Expr | Hybrid |
|---------|------------|-----------|--------|
| Type Safety | ✅ Compile-time | ❌ Runtime | ✅ Compile-time |
| IntelliSense | ✅ IDE support | ❌ None | ✅ IDE support |
| Dynamic Construction | ❌ Difficult | ✅ Flexible | ✅ Flexible |
| Applicable Scenarios | Fixed conditions | Complex dynamic logic | Variable conditions |

## 6. Related Links

- [Back to docs hub](../README.md)
- [Query Guide](../02-core-usage/03-query-guide.en.md)
- [Expression Extension](../04-extensibility/01-expression-extension.en.md)
