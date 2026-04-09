# Expression Extension

LiteOrm lets you translate C# methods and members into SQL by combining `LambdaExprConverter` and `SqlBuilder`.

## 1. The pipeline

1. Register a Lambda method or member handler
2. Convert the Lambda node into an `Expr`
3. Register SQL output with `RegisterFunctionSqlHandler`
4. Use the method in a normal query

## 2. Recommended SQL handler signature

```csharp
public delegate void FunctionSqlHandler(
    ref ValueStringBuilder outSql,
    FunctionExpr expr,
    SqlBuildContext context,
    ISqlBuilder sqlBuilder,
    ICollection<KeyValuePair<string, object>> outputParams);
```

This overload is recommended because it gives precise control over SQL output and parameter generation.

## 3. Date formatting example

```csharp
LambdaExprConverter.RegisterMethodHandler("Format", (node, converter) =>
{
    var dateExpr = converter.ConvertInternal(node.Object) as ValueTypeExpr;
    var formatExpr = converter.ConvertInternal(node.Arguments[0]) as ValueTypeExpr;
    return new FunctionExpr("DATE_FORMAT", dateExpr, formatExpr);
});

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

`"Format"` is just an example method name. In real projects, prefer `nameof(SomeType.SomeMethod)` when possible so refactoring stays safer.

## 4. Practical note

If the framework already supports `DateTime.ToString(format)` for your target database, you usually do not need to create a custom wrapper method such as `DateTime.Format(...)`.

## 5. Member handler example

```csharp
LambdaExprConverter.RegisterMemberHandler("Length", handler);
LambdaExprConverter.RegisterMemberHandler(typeof(User), "Age", handler);
```

`"Length"` is also just an example member name. For your own members, prefer `nameof(SomeType.SomeProperty)` when possible.

## 6. Tip: mix `Expr` into Lambda

When you dynamically build an `Expr` but still want to combine it with a Lambda, use `To<T>()`:

```csharp
 u => u.Age >= 18 && Expr.Prop("UserName").Contains("John").To<bool>()
```

## Related Links

- [Back to English docs hub](../README.md)
- [Window Functions](../03-advanced-topics/04-window-functions.en.md)
- [API Index](../05-reference/02-api-index.en.md)
