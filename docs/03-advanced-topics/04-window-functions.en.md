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
| Pure Expr | Directly construct `FunctionExpr`, suitable for one-time use |

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

    // Other window function examples
    public static int RowNumber<T>(this int rowNum,
        params Expression<Func<T, object>>[] partitionBy) => rowNum;

    public static int Rank<T>(this int rank,
        params Expression<Func<T, object>>[] partitionBy) => rank;
}
```

### 3.3 Register Method Handler

```csharp
LambdaExprConverter.RegisterMethodHandler("SumOver", (node, converter) =>
{
    var amountExpr = converter.Convert(node.Arguments[0]) as ValueTypeExpr;

    var partitionExprs = new List<ValueTypeExpr>();
    var orderExprs = new List<ValueTypeExpr>();

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

    return new FunctionExpr("SUM_OVER",
        amountExpr,
        new ValueSet(partitionExprs),
        new ValueSet(orderExprs));
});
```

### 3.4 Register SQL Handler

```csharp
SqlBuilder.Instance.RegisterFunctionSqlHandler("SUM_OVER",
    (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context,
     ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
{
    outSql.Append("SUM(");
    expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(") OVER (");

    if (expr.Args.Count > 1 && expr.Args[1] is ValueSet partitionSet && partitionSet.Items.Count > 0)
    {
        outSql.Append("PARTITION BY ");
        for (int i = 0; i < partitionSet.Items.Count; i++)
        {
            if (i > 0) outSql.Append(", ");
            partitionSet.Items[i].ToSql(ref outSql, context, sqlBuilder, outputParams);
        }
    }

    if (expr.Args.Count > 2 && expr.Args[2] is ValueSet orderSet && orderSet.Items.Count > 0)
    {
        if (expr.Args.Count > 1 && expr.Args[1] is ValueSet part && part.Items.Count > 0)
            outSql.Append(' ');
        outSql.Append("ORDER BY ");
        for (int i = 0; i < orderSet.Items.Count; i++)
        {
            if (i > 0) outSql.Append(", ");
            orderSet.Items[i].ToSql(ref outSql, context, sqlBuilder, outputParams);
        }
    }

    outSql.Append(')');
});
```

### 3.5 Usage Example

```csharp
var sales = await saleService.SearchAsync<SaleView>(s => s.Select(
    x => new SaleView
    {
        Id = x.Id,
        ProductId = x.ProductId,
        Amount = x.Amount,
        SaleDate = x.SaleDate,

        // Partition by year and quarter, no order (cumulative quarterly total)
        QuarterlyTotal = x.Amount.SumOver<Sale>(
            p => p.Year, p => p.Quarter
        ),

        // Partition by year, order by sale date ascending (cumulative yearly running total)
        YearlyRunning = x.Amount.SumOver<Sale>(
            partitionBy: new Expression<Func<Sale, object>>[] { p => p.Year },
            orderBy: new SumOverOrderBy<Sale>[] {
                new SumOverOrderBy<Sale>(p => p.SaleDate, true)
            }
        )
    }
));
```

### 3.6 Registration and Query Flow from Demo

The following is organized from `LiteOrm.Demo\Demos\WindowFunctionDemo.cs`:

```csharp
// Register handlers at application startup
WindowFunctionDemo.RegisterHandlers();

// Use window function extension directly in projection during query
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

Looking at the current API design, it's also advisable to use the `FunctionSqlHandler` new overload to write SQL output directly, rather than concatenating intermediate strings first.

**Generated SQL**:

```sql
SELECT
    s.Id,
    s.ProductId,
    s.Amount,
    s.SaleDate,
    SUM(s.Amount) OVER (PARTITION BY s.Year, s.Quarter) AS QuarterlyTotal,
    SUM(s.Amount) OVER (PARTITION BY s.Year ORDER BY s.SaleDate ASC) AS YearlyRunning
FROM Sale s
```

## 4. Pure Expr Approach

The pure Expr approach doesn't require defining extension methods or registering `RegisterMethodHandler`. You construct expressions directly.

### 4.1 Construct Expr Directly

```csharp
// Cumulative quarterly total
var productTotalExpr = new FunctionExpr("SUM_OVER",
    Expr.Prop("Amount"),
    new ValueSet(Expr.Prop("ProductId")),
    new ValueSet());

// Cumulative yearly running total
var runningTotalExpr = new FunctionExpr("SUM_OVER",
    Expr.Prop("Amount"),
    new ValueSet(Expr.Prop("ProductId")),
    new ValueSet(new OrderByItemExpr(Expr.Prop("SaleDate"), ascending: true)));
```

### 4.2 Embed in Query

**Approach 1: Lambda closure variable**

```csharp
var results = await saleDAO
    .WithArgs([tableMonth])
    .Search<SaleView>(q => q
        .OrderBy(s => s.ProductId)
        .Select(s => new SaleView
        {
            Id = s.Id,
            ProductId = s.ProductId,
            Amount = s.Amount,
            SaleDate = s.SaleDate,
            ProductTotal = (int)productTotalExpr,
            RunningTotal = (int)runningTotalExpr
        })
    ).ToListAsync();
```

**Approach 2: SelectExpr chain building**

```csharp
var selectExpr = new FromExpr(typeof(SalesRecord))
    .OrderBy(new OrderByItemExpr(Expr.Prop("ProductId"), ascending: true))
    .Select(
        new SelectItemExpr(Expr.Prop("Id"), "Id"),
        new SelectItemExpr(Expr.Prop("ProductId"), "ProductId"),
        new SelectItemExpr(Expr.Prop("Amount"), "Amount"),
        new SelectItemExpr(Expr.Prop("SaleDate"), "SaleDate"),
        new SelectItemExpr(productTotalExpr, "ProductTotal"),
        new SelectItemExpr(runningTotalExpr, "RunningTotal"));

var results = await saleDAO
    .WithArgs([tableMonth])
    .SearchAs<SaleView>(selectExpr)
    .ToListAsync();
```

## 5. Comparison of Both Approaches

| Item | Lambda Extension Method | Pure Expr |
|------|------------------------|-----------|
| Requires extension method definition | ✅ Yes | ❌ No |
| Requires RegisterMethodHandler | ✅ Yes | ❌ No |
| Requires RegisterFunctionSqlHandler | ✅ Yes | ✅ Yes |
| Code hints | ✅ High | ⚠️ Medium |
| Applicable scenarios | General, high reusability | One-time, rapid prototyping |

## 6. More Window Function Examples

### 6.1 ROW_NUMBER - Row Number

```csharp
LambdaExprConverter.RegisterMethodHandler("RowNumber", (node, converter) =>
{
    var partitionExprs = new List<ValueTypeExpr>();
    if (node.Arguments.Count > 0 && node.Arguments[0] is NewArrayExpression arr)
    {
        foreach (var elem in arr.Expressions)
            if (converter.Convert(elem) is ValueTypeExpr vte)
                partitionExprs.Add(vte);
    }
    return new FunctionExpr("ROW_NUMBER_OVER", new ValueSet(partitionExprs), new ValueSet());
});

SqlBuilder.Instance.RegisterFunctionSqlHandler("ROW_NUMBER_OVER", (ref outSql, expr, context, sqlBuilder, outputParams) =>
{
    outSql.Append("ROW_NUMBER() OVER (");
    if (expr.Args.Count > 0 && expr.Args[0] is ValueSet partitionSet && partitionSet.Items.Count > 0)
    {
        outSql.Append("PARTITION BY ");
        for (int i = 0; i < partitionSet.Items.Count; i++)
        {
            if (i > 0) outSql.Append(", ");
            partitionSet.Items[i].ToSql(ref outSql, context, sqlBuilder, outputParams);
        }
    }
    outSql.Append(')');
});
```

### 6.2 LAG - Get Previous Row

```csharp
LambdaExprConverter.RegisterMethodHandler("Lag", (node, converter) =>
{
    var expr = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
    return new FunctionExpr("LAG_OVER", expr);
});

SqlBuilder.Instance.RegisterFunctionSqlHandler("LAG_OVER", (ref outSql, expr, context, sqlBuilder, outputParams) =>
{
    outSql.Append("LAG(");
    expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(')');
});
```

## 7. Caveats

1. **Database support**: Window functions are part of the SQL standard, but some older databases may not support them
2. **Partition key selection**: Choosing high-selectivity columns can improve window function performance
3. **ORDER BY**: Sorting within the window affects results of functions like `LAG/LEAD/RANK`

## Related Links

- [Back to docs hub](../README.md)
- [Associations](../02-core-usage/05-associations.en.md)
- [Expression Extension](../04-extensibility/01-expression-extension.en.md)
- [Function Validator](../04-extensibility/02-function-validator.en.md)
