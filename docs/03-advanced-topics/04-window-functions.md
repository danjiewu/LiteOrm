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
| 纯 Expr | 直接使用 `FunctionExpr` 的 `Over` 扩展 |

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

    // decimal 版本
    public static decimal SumOver<T>(this decimal amount,
        params Expression<Func<T, object>>[] partitionBy) => amount;

    public static decimal SumOver<T>(this decimal amount,
        Expression<Func<T, object>>[] partitionBy,
        SumOverOrderBy<T>[] orderBy) => amount;
}
```

### 3.3 注册方法处理器

```csharp
LambdaExprConverter.RegisterMethodHandler("SumOver", (node, converter) =>
{
    var amountExpr = converter.Convert(node.Arguments[0]) as ValueTypeExpr;

    var partitionExprs = new List<ValueTypeExpr>();
    var orderExprs = new List<OrderByItemExpr>();

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

    return Func("SUM", amountExpr)
        .Over(partitionExprs.ToArray(), orderExprs.ToArray());
});
```

### 3.4 关于 `Over` 的 SQL 处理

```csharp
// 注意：SqlBuilder 已默认注册对 "Over" 的处理器（先输出内部函数，再追加 OVER (...) 语义），
// 因此通常无需自行调用 RegisterFunctionSqlHandler 来注册 "Over"。
// 直接使用 `Func("SUM", ...).Over(...)` 即可得到正确的 SQL 输出。
```

### 3.5 使用示例

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

**生成的 SQL**：

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

## 4. 纯 Expr 方式

纯 Expr 方式无需定义扩展方法和注册 `RegisterMethodHandler`，直接构造表达式。

### 4.1 直接构造 Expr

```csharp
// 累计总，使用内置 Over 函数
var productTotalExpr = Func("SUM", Prop(nameof(SalesRecord.Amount)))
    .Over(new[] { Prop(nameof(SalesRecord.ProductId)) });

// 按 SaleTime 升序的累计值
var runningTotalExpr = Func("SUM", Prop(nameof(SalesRecord.Amount)))
    .Over(new[] { Prop(nameof(SalesRecord.ProductId)) }, new[] { Prop(nameof(SalesRecord.SaleTime)).Asc() });
```

### 4.2 嵌入查询

**方式一：嵌入 Lambda**

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

**方式二：SelectExpr 链式构建**

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

## 5. 两种方式对比

| 对比项 | Lambda 扩展方法 | 纯 Expr |
|--------|----------------|---------|
| 需要扩展方法定义 | ✅ 是 | ❌ 否 |
| 需要 RegisterMethodHandler | ✅ 是 | ❌ 否 |
| 需要 RegisterFunctionSqlHandler | ❌ 否（除非自定义 Over 输出） | ❌ 否 |
| 代码提示 | ✅ 高 | ⚠️ 中 |
| 适用场景 | 通用、高复用 | 快速原型 |

## 6. 说明

当前仓库中的示例主要覆盖 `SumOver`，底层仍然依赖 `FunctionExpr.Over(...)`。如果后续需要扩展 `ROW_NUMBER`、`RANK`、`LAG` 等函数，可以沿用同样的注册模式。

## 7. 注意事项

1. **数据库支持**：窗口函数是 SQL 标准，但部分老旧数据库可能不支持
2. **分区键选择**：选择高选择性的列可以提高窗口函数性能
3. **ORDER BY**：窗口内的排序影响 `LAG/LEAD/RANK` 等函数的结果

## 相关链接

- [返回目录](../README.md)
- [关联查询](../02-core-usage/05-associations.md)
- [表达式扩展](../04-extensibility/01-expression-extension.md)
- [函数验证器](../04-extensibility/02-function-validator.md)

