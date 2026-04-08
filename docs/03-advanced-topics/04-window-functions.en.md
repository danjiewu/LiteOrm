# Window Functions

LiteOrm can support window functions through expression extension handlers.

## 1. Two implementation styles

| Style | Best for |
|------|----------|
| Lambda extension methods | Reusable business-facing syntax |
| Pure `Expr` | One-off advanced queries |

## 2. Recommended SQL handler style

Use the newer `FunctionSqlHandler` overload when registering SQL generation logic:

```csharp
SqlBuilder.Instance.RegisterFunctionSqlHandler("SUM_OVER",
    (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context,
     ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
{
    outSql.Append("SUM(");
    expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(") OVER (...)");
});
```

## 3. Lambda-style usage

```csharp
var results = await factory.SalesDAO
    .WithArgs([tableMonth])
    .SearchAs(q => q
        .OrderBy(s => s.ProductId)
        .Select(s => new SalesWindowView
        {
            Id = s.Id,
            ProductId = s.ProductId,
            Amount = s.Amount,
            ProductTotal = s.Amount.SumOver<SalesRecord>(p => p.ProductId)
        })
    ).ToListAsync();
```

## 4. Pure `Expr` usage

```csharp
var runningTotalExpr =
    Func("SUM", Prop(nameof(SalesRecord.Amount)))
    .Over(
        [Prop(nameof(SalesRecord.ProductId))],
        [Prop(nameof(SalesRecord.SaleTime)).Asc()]);
```

## 5. Notes

- Make sure the target database version supports the window function you want.
- Prefer the low-level SQL handler delegate for precise output control.
- Keep the extension method surface small and business-readable.

## Related Links

- [Back to English docs hub](../README.md)
- [Expression Extension Guide](../04-extensibility/01-expression-extension.en.md)
- [SQL Examples](../05-reference/07-sql-examples.en.md)
