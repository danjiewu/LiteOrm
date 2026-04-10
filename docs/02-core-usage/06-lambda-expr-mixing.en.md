# Mixing Lambda and Expr

This is not just an advanced trick. In LiteOrm, it is a practical day-to-day pattern: **use Lambda to keep business intent readable, and use `Expr` where the filter needs to be built dynamically.**

The two main patterns are:

- **`expr.To<bool>()`**: keep Lambda on the outside and embed a dynamic `Expr`.
- **`Expr.Lambda<T>()`**: write stable conditions as Lambda first, then convert them into `LogicExpr` and keep composing.

## 1. When to mix them

| Scenario | Recommended pattern | Why |
|------|----------|------|
| Most conditions are fixed, only a few are runtime-driven | Lambda + `expr.To<bool>()` | Keeps the query readable and strongly typed |
| A stable base condition must be reused with optional filters | `Expr.Lambda<T>()` | Turns Lambda into a composable `LogicExpr` |
| Nearly everything is dynamic | Pure `Expr` | Simpler than wrapping everything back into Lambda |

## 2. Embed an existing Expr into Lambda

`To<T>()` is the bridge between Lambda and `Expr`. It only works during LiteOrm expression parsing, so **use it only inside the Lambda passed to a LiteOrm query API**.

### 2.1 Smallest useful example

```csharp
var users = await userService.SearchAsync(
    u => u.Age >= 18 && Expr.Prop("UserName").Contains("John").To<bool>()
);
```

This style works well for "stable main condition + dynamic extra filter" queries.

### 2.2 Build dynamically first, then plug back into Lambda

```csharp
LogicExpr filter = null;

if (!string.IsNullOrWhiteSpace(keyword))
    filter &= Expr.Prop("UserName").Contains(keyword);

if (minAge.HasValue)
    filter &= Expr.Prop("Age") >= minAge.Value;

var users = await userService.SearchAsync(
    u => u.IsActive == true && filter.To<bool>()
);
```

You keep the dynamic part reusable while still letting the final query read like business logic.

### 2.3 Combine with related `EXISTS`

```csharp
var hasOpenOrder = Expr.ExistsRelated<Order>(
    Expr.Prop("Status") != "Completed"
);

var activeUsers = await userService.SearchAsync(
    u => u.IsActive == true && hasOpenOrder.To<bool>()
);
```

If relationships are already declared in the model, combining `ExistsRelated(...)` with Lambda is usually clearer than hand-writing the whole subquery.

## 3. Start with Lambda, then convert to Expr

When the base business rule is naturally expressed as Lambda but still needs optional runtime filters, convert it first with `Expr.Lambda<T>()`.

```csharp
var baseCondition = Expr.Lambda<User>(u => u.IsActive == true && u.Age >= 18);

LogicExpr extraFilter = null;

if (!string.IsNullOrWhiteSpace(keyword))
    extraFilter &= Expr.Prop("UserName").Contains(keyword);

if (deptId.HasValue)
    extraFilter &= Expr.Prop("DeptId") == deptId.Value;

var combined = baseCondition & extraFilter;

var users = await userService.SearchAsync(
    u => combined.To<bool>()
);
```

This is a good fit for "stable business baseline + optional filter set" query builders.

## 4. Practical notes

### 4.1 `To<T>()` should match the Lambda return type

In query predicates that normally means `bool`:

```csharp
u => u.Age >= 18 && expr.To<bool>()
```

### 4.2 Do not call `To<T>()` in normal runtime code

It is not a general conversion API. It exists for LiteOrm's Lambda parser.

### 4.3 Mixing does not add extra SQL execution cost

`To<T>()` is replaced during parsing and still becomes part of the same SQL expression tree.

## 5. Which style to choose

| Style | Strength | Best for |
|---------|----------|----------|
| Pure Lambda | Most readable, best IDE experience | Fixed-condition queries |
| Pure `Expr` | Most flexible for runtime composition | Query builders and admin-style filters |
| Lambda + `Expr` | Keeps intent clear while staying dynamic | Stable business rules with optional filters |

## Related Links

- [Back to docs hub](../README.md)
- [Query Guide](./03-query-guide.en.md)
- [Expression Extension](../04-extensibility/01-expression-extension.en.md)
