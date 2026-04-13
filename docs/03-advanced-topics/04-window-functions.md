# 窗口函数

LiteOrm 支持通过表达式扩展机制实现窗口函数（Window Functions），用于数据分析场景如累计求和、分组排名等。

## 1. 窗口函数概述

窗口函数在数据的**一组行上执行聚合或分析计算**，同时返回每一行的明细数据。

### 1.1 常见窗口函数

| 函数 | 说明 |
|------|------|
| `SUM() OVER()` | 累计求和 |
| `AVG() OVER()` | 移动平均 |
| `ROW_NUMBER() OVER()` | 行号 |
| `RANK() OVER()` | 排名（跳跃） |
| `DENSE_RANK() OVER()` | 排名（连续） |
| `LAG() OVER()` | 前一行数据 |
| `LEAD() OVER()` | 后一行数据 |

## 2. 实现方式

LiteOrm 提供两种窗口函数实现方式：

| 方式 | 说明 |
|------|------|
| Lambda 扩展方法 | 定义 C# 扩展方法，声明式调用 |
| 纯 Expr | 直接构造 `FunctionExpr`，适合一次性使用 |

## 3. Lambda 扩展方法方式

### 3.1 定义排序辅助类

```csharp
/// <summary>
/// 窗口函数排序项，用于指定排序字段和方向。
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
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

### 3.2 定义窗口函数扩展方法

```csharp
public static class WindowFunctionExtensions
{
    // 仅分区字段（params 重载）
    public static int SumOver<T>(this int amount,
        params Expression<Func<T, object>>[] partitionBy) => amount;

    // 分区 + 排序（显式数组重载）
    public static int SumOver<T>(this int amount,
        Expression<Func<T, object>>[] partitionBy,
        SumOverOrderBy<T>[] orderBy) => amount;

    // 其他窗口函数示例
    public static int RowNumber<T>(this int rowNum,
        params Expression<Func<T, object>>[] partitionBy) => rowNum;

    public static int Rank<T>(this int rank,
        params Expression<Func<T, object>>[] partitionBy) => rank;
}
```

### 3.3 注册方法处理器

```csharp
LambdaExprConverter.RegisterMethodHandler("SumOver", (node, converter) =>
{
    var amountExpr = converter.Convert(node.Arguments[0]) as ValueTypeExpr;

    var partitionExprs = new List<ValueTypeExpr>();
    var orderExprs = new List<ValueTypeExpr>();

    // 分区字段：NewArrayExpression，元素为 Quote(Lambda)
    if (node.Arguments.Count > 1 && node.Arguments[1] is NewArrayExpression partArray)
    {
        foreach (var elem in partArray.Expressions)
        {
            if (converter.Convert(elem) is ValueTypeExpr vte)
                partitionExprs.Add(vte);
        }
    }

    // 排序字段：NewArrayExpression，元素为 SumOverOrderBy<T> 构造表达式
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

### 3.4 注册 SQL 处理器

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

### 3.5 使用示例

```csharp
var sales = await saleService.SearchAsync<SaleView>(s => s.Select(
    x => new SaleView
    {
        Id = x.Id,
        ProductId = x.ProductId,
        Amount = x.Amount,
        SaleDate = x.SaleDate,

        // 按年、季度分区，无排序（累计季度总）
        QuarterlyTotal = x.Amount.SumOver<Sale>(
            p => p.Year, p => p.Quarter
        ),

        // 按年分区，按销售日期升序（累计年度 running total）
        YearlyRunning = x.Amount.SumOver<Sale>(
            partitionBy: new Expression<Func<Sale, object>>[] { p => p.Year },
            orderBy: new SumOverOrderBy<Sale>[] {
                new SumOverOrderBy<Sale>(p => p.SaleDate, true)
            }
        )
    }
));
```

### 3.6 来自 Demo 的注册与查询流程

下面这段整理自 `LiteOrm.Demo\Demos\WindowFunctionDemo.cs`：

```csharp
// 应用启动时先注册处理器
WindowFunctionDemo.RegisterHandlers();

// 查询时直接在投影里使用窗口函数扩展
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

如果你准备把窗口函数能力提供给业务层长期复用，推荐采用这种“启动期注册 + 查询期直接调用”的模式。  
而且从当前 API 设计看，也更建议使用 `FunctionSqlHandler` 新重载直接写 SQL 输出，而不是先拼接中间字符串再返回。

**生成的 SQL**：

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

## 4. 纯 Expr 方式

纯 Expr 方式无需定义扩展方法和注册 `RegisterMethodHandler`，直接构造表达式。

### 4.1 直接构造 Expr

```csharp
// 累计季度总
var productTotalExpr = new FunctionExpr("SUM_OVER",
    Expr.Prop("Amount"),
    new ValueSet(Expr.Prop("ProductId")),
    new ValueSet());

// 累计年度 running total
var runningTotalExpr = new FunctionExpr("SUM_OVER",
    Expr.Prop("Amount"),
    new ValueSet(Expr.Prop("ProductId")),
    new ValueSet(new OrderByItemExpr(Expr.Prop("SaleDate"), ascending: true)));
```

### 4.2 嵌入查询

**方式一：Lambda 闭包变量**

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

**方式二：SelectExpr 链式构建**

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

## 5. 两种方式对比

| 对比项 | Lambda 扩展方法 | 纯 Expr |
|--------|----------------|---------|
| 需要扩展方法定义 | ✅ 是 | ❌ 否 |
| 需要 RegisterMethodHandler | ✅ 是 | ❌ 否 |
| 需要 RegisterFunctionSqlHandler | ✅ 是 | ✅ 是 |
| 代码提示 | ✅ 高 | ⚠️ 中 |
| 适用场景 | 通用、高复用 | 一次性、快速原型 |

## 6. 更多窗口函数示例

### 6.1 ROW_NUMBER - 行号

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

### 6.2 LAG - 取前一行

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

## 7. 注意事项

1. **数据库支持**：窗口函数是 SQL 标准，但部分老旧数据库可能不支持
2. **分区键选择**：选择高选择性的列可以提高窗口函数性能
3. **ORDER BY**：窗口内的排序影响 `LAG/LEAD/RANK` 等函数的结果

## 相关链接

- [返回目录](../README.md)
- [关联查询](../02-core-usage/05-associations.md)
- [表达式扩展](../04-extensibility/01-expression-extension.md)
- [函数验证器](../04-extensibility/02-function-validator.md)

