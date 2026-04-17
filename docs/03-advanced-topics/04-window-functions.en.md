# Window Functions

LiteOrm supports window functions (Window Functions) through its expression extension mechanism, enabling data analysis scenarios such as running sums, grouped rankings, and more.

## 1. Window Functions Overview

Window functions perform aggregate or analytical calculations on a **set of rows** while returning both the detail data for each row.

### 1.1 Common Window Functions

| Function | Description |
|----------|-------------|
| `SUM() OVER()` | Running sum |
| `AVG() OVER()` | Moving average |
| `ROW_NUMBER() OVER()` | Row number |
| `RANK() OVER()` | Rank (with gaps) |
| `DENSE_RANK() OVER()` | Rank (without gaps) |
| `LAG() OVER()` | Previous row data |
| `LEAD() OVER()` | Next row data |

## 2. Implementation Approaches

LiteOrm provides two window function implementation approaches:

| Approach | Description |
|----------|-------------|
| Lambda extension methods | Define C# extension methods, declarative invocation |
| Pure Expr | Directly use the `Over` extension on `FunctionExpr`, suitable for one-time or reusable flexible usage |

## 3. Lambda Extension Method Approach

### 3.1 Define Sort Helper Class

```csharp
/// <summary>
/// Window function order-by item, used to specify sort field and direction.
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
public class SumOverOrderBy<T>
{
    public SumOverOrderBy(Expression<Func<T, object>> field, bool ascending = true)
    {
        Field = field;
        Ascending = ascending;
    }

    public Expression<Func<T, object>> Field { get; }
    public bool Ascending { get; }
}
```

### 3.2 Define Window Function Extension Methods

```csharp
public static class WindowFunctionExtensions
{
    // Partition fields only (params overload)
    public static int SumOver<T>(this int amount,
        params Expression<Func<T, object>>[] partitionBy) => amount;

    // Partition + Order by (explicit array overload)
    public static int SumOver<T>(this int amount,
        Expression<Func<T, object>>[] partitionBy,
        SumOverOrderBy<T>[] orderBy) => amount;

    // decimal overloads
    public static decimal SumOver<T>(this decimal amount,
        params Expression<Func<T, object>>[] partitionBy) => amount;

    public static decimal SumOver<T>(this decimal amount,
        Expression<Func<T, object>>[] partitionBy,
        SumOverOrderBy<T>[] orderBy) => amount;
}
```

### 3.3 Register Method Handler

```csharp
LambdaExprConverter.RegisterMethodHandler("SumOver", (node, converter) =>
{
    var amountExpr = converter.Convert(node.Arguments[0]) as ValueTypeExpr;

    var partitionExprs = new List<ValueTypeExpr>();
    var orderExprs = new List<OrderByItemExpr>();

    // Partition fields: NewArrayExpression, elements are Quote(Lambda)
    if (node.Arguments.Count > 1 && node.Arguments[1] is NewArrayExpression partArray)
    {
        foreach (var elem in partArray.Expressions)
        {
            if (converter.Convert(elem) is ValueTypeExpr vte)
                partitionExprs.Add(vte);
        }
    }

    // Order by fields: NewArrayExpression, elements are SumOverOrderBy<T> construction expressions
    if (node.Arguments.Count > 2 && node.Arguments[2] is NewArrayExpression orderArray)
    {
        foreach (var elem in orderArray.Expressions)
        {
            if (elem is NewExpression ctorNew && ctorNew.Arguments.Count == 2)
            {
                var field = converter.Convert(ctorNew.Arguments[0]) as ValueTypeExpr;
                bool isAsc = ctorNew.Arguments[1] is ConstantExpression { Value: bool b } && b;
                if (field is not null)
                    orderExprs.Add(new OrderByItemExpr(field, isAsc));
            }
        }
    }

    return Func("SUM", amountExpr)
        .Over(partitionExprs.ToArray(), orderExprs.ToArray());
});
```

### 3.4 About SQL handling for `Over`

```csharp
// Note: SqlBuilder already registers a handler for "Over" by default (it emits the inner function
// first and then appends the OVER (...) clause), so you usually do not need to call
// RegisterFunctionSqlHandler for "Over" yourself.
// Use `Func("SUM", ...).Over(...)` directly to get the correct SQL output.
```

### 3.5 Usage Example

```csharp
var sales = await saleService.SearchAs<SalesWindowView>(q => q
    .OrderBy(s => s.ProductId)
    .Select(s => new SalesWindowView
    {
        Id = s.Id,
        ProductId = s.ProductId,
        ProductName = s.ProductName,
        Amount = s.Amount,
        SaleTime = s.SaleTime,
        ProductTotal = s.Amount.SumOver<SalesRecord>(p => p.ProductId),
        RunningTotal = s.Amount.SumOver<SalesRecord>(
            partitionBy: new Expression<Func<SalesRecord, object>>[] { p => p.ProductId },
            orderBy: new SumOverOrderBy<SalesRecord>[]
            {
                new SumOverOrderBy<SalesRecord>(p => p.SaleTime, true)
            })
    })
);
```

### 3.6 Registration and Query Flow from Demo

The following is organized from `LiteOrm.Demo\Demos\WindowFunctionDemo.cs`:

```csharp
// Register handlers at application startup
WindowFunctionDemo.RegisterHandlers();

// Use the window function extension directly in the projection
var results = await factory.SalesDAO
    .WithArgs([tableMonth])
    .SearchAs(q => q
        .OrderBy(s => s.ProductId)
        .Select(s => new SalesWindowView
        {
            Id = s.Id,
            ProductId = s.ProductId,
            ProductName = s.ProductName,
            Amount = s.Amount,
            SaleTime = s.SaleTime,
            ProductTotal = s.Amount.SumOver<SalesRecord>(p => p.ProductId)
        })
    ).ToListAsync();
```

If you plan to provide window function capabilities for long-term reuse in business logic, this "register at startup + call directly during query" pattern is recommended.

**Generated SQL**:

```sql
SELECT
    s.Id,
    s.ProductId,
    s.ProductName,
    s.Amount,
    s.SaleTime,
    SUM(s.Amount) OVER (PARTITION BY s.ProductId) AS ProductTotal
FROM Sales_yyyyMM s
```

## 4. Pure Expr Approach

The pure Expr approach doesn't require defining extension methods or registering `RegisterMethodHandler`. You construct expressions directly.

### 4.1 Construct Expr Directly

```csharp
// Cumulative total using the built-in Over form (no custom SUM_OVER needed)
var productTotalExpr = Func("SUM", Prop(nameof(SalesRecord.Amount)))
    .Over(new[] { Prop(nameof(SalesRecord.ProductId)) });

// Running total ordered by SaleTime ascending
var runningTotalExpr = Func("SUM", Prop(nameof(SalesRecord.Amount)))
    .Over(new[] { Prop(nameof(SalesRecord.ProductId)) }, new[] { Prop(nameof(SalesRecord.SaleTime)).Asc() });
```

### 4.2 Embed in Query

**Approach 1: Lambda projection**

```csharp
var results = await saleDAO
    .WithArgs([tableMonth])
    .SearchAs<SalesWindowView>(q => q
        .OrderBy(s => s.ProductId)
        .Select(s => new SalesWindowView
        {
            Id = s.Id,
            ProductId = s.ProductId,
            ProductName = s.ProductName,
            Amount = s.Amount,
            SaleTime = s.SaleTime,
            ProductTotal = productTotalExpr.To<int>(),
            RunningTotal = runningTotalExpr.To<int>()
        })
    ).ToListAsync();
```

**Approach 2: SelectExpr chain building**

```csharp
var selectExpr = new FromExpr(typeof(SalesRecord))
    .OrderBy(new OrderByItemExpr(Prop(nameof(SalesRecord.ProductId)), ascending: true))
    .Select(
        new SelectItemExpr(Prop(nameof(SalesRecord.Id)), nameof(SalesWindowView.Id)),
        new SelectItemExpr(Prop(nameof(SalesRecord.ProductId)), nameof(SalesWindowView.ProductId)),
        new SelectItemExpr(Prop(nameof(SalesRecord.ProductName)), nameof(SalesWindowView.ProductName)),
        new SelectItemExpr(Prop(nameof(SalesRecord.Amount)), nameof(SalesWindowView.Amount)),
        new SelectItemExpr(Prop(nameof(SalesRecord.SaleTime)), nameof(SalesWindowView.SaleTime)),
        new SelectItemExpr(productTotalExpr, nameof(SalesWindowView.ProductTotal)),
        new SelectItemExpr(runningTotalExpr, nameof(SalesWindowView.RunningTotal)));

var results = await saleDAO
    .WithArgs([tableMonth])
    .SearchAs<SalesWindowView>(selectExpr)
    .ToListAsync();
```

## 5. Comparison of Both Approaches

| Item | Lambda Extension Method | Pure Expr |
|------|------------------------|-----------|
| Requires extension method definition | ✅ Yes | ❌ No |
| Requires RegisterMethodHandler | ✅ Yes | ❌ No |
| Requires RegisterFunctionSqlHandler | ❌ No (unless overriding `Over` output) | ❌ No |
| Code hints | ✅ High | ⚠️ Medium |
| Applicable scenarios | General, high reusability | One-time, rapid prototyping |

## 6. Notes

The examples in this repository focus on `SumOver` and rely on `FunctionExpr.Over(...)` underneath. If you need `ROW_NUMBER`, `RANK`, `LAG`, or similar functions later, you can extend them with the same registration pattern.

## 7. Caveats

1. **Database support**: Window functions are part of the SQL standard, but some older databases may not support them
2. **Partition key selection**: Choosing high-selectivity columns can improve window function performance
3. **ORDER BY**: Sorting within the window affects results of functions like `LAG/LEAD/RANK`

## Related Links

- [Back to docs hub](../README.md)
- [Associations](../02-core-usage/05-associations.en.md)
- [Expression Extension](../04-extensibility/01-expression-extension.en.md)
- [Function Validator](../04-extensibility/02-function-validator.en.md)
