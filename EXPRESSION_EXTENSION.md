# 表达式扩展指南

本指南详细介绍如何使用 LiteOrm 的表达式扩展功能，通过结合 `LambdaExprConverter` 和 `SqlBuilder` 的方法来实现自定义表达式处理。

## 1. 核心方法介绍

### 1.1 LambdaExprConverter 方法

#### 1.1.1 RegisterMethodHandler

```csharp
// 注册全局方法处理器
public static void RegisterMethodHandler(string methodName, Func<MethodCallExpression, LambdaExprConverter, Expr> handler = null)

// 注册特定类型的方法处理器
public static void RegisterMethodHandler(Type type, string methodName = null, Func<MethodCallExpression, LambdaExprConverter, Expr> handler = null)
```

- **参数说明**：
  - `type`：目标类型
  - `methodName`：要处理的方法名称，若为 null 则处理所有方法（`ToString`、`Equals` 等常用方法除外）
  - `handler`：处理逻辑，若为 null 则使用默认处理器

#### 1.1.2 RegisterMemberHandler

```csharp
// 注册全局成员处理器
public static void RegisterMemberHandler(string memberName, Func<MemberExpression, LambdaExprConverter, Expr> handler = null)

// 注册特定类型的成员处理器
public static void RegisterMemberHandler(Type type, string memberName, Func<MemberExpression, LambdaExprConverter, Expr> handler = null)
```

- **参数说明**：
  - `type`：目标类型
  - `memberName`：要处理的成员名称（属性/字段），若为 null 则处理所有成员
  - `handler`：处理逻辑，若为 null 则使用默认处理器

### 1.2 SqlBuilder 方法

#### 1.2.1 RegisterFunctionSqlHandler

通过 SqlBuilder 实例注册函数 SQL 处理器：

```csharp
// 为特定 SqlBuilder 类型注册函数 SQL 处理器
MySqlBuilder.Instance.RegisterFunctionSqlHandler("MyCustomFunction", (functionName, args) => {
    // 构建自定义 SQL 片段
    return $"CUSTOM_FUNCTION({string.Join(", ", args.Select(arg => arg.Key))})";
});
```

- **参数说明**：
  - `functionName`：函数名称
  - `handler`：构建 SQL 片段的委托

## 2. 实现表达式扩展的步骤

### 步骤 1：注册方法/成员处理器

首先，使用 `LambdaExprConverter` 注册自定义的方法或成员处理器，将 C# 方法/属性调用转换为 `Expr` 对象。

### 步骤 2：注册函数 SQL 处理器

然后，使用 `SqlBuilder` 注册对应的函数 SQL 处理器，将 `Expr` 对象转换为数据库特定的 SQL 语句。

### 步骤 3：使用自定义表达式

最后，在 LINQ 查询中使用自定义方法/属性，框架会自动将其转换为对应的 SQL。

## 3. 示例：实现日期处理扩展

### 3.1 示例 1：添加日期格式化方法

#### 步骤 1：定义扩展方法

```csharp
public static class DateTimeExtensions
{
    public static string Format(this DateTime date, string format)
    {
        return date.ToString(format);
    }
}
```

#### 步骤 2：注册方法处理器

```csharp
// 注册 Format 方法处理器
LambdaExprConverter.RegisterMethodHandler("Format", (node, converter) => {
    // 转换方法调用为 FunctionExpr
    var dateExpr = converter.ConvertInternal(node.Object) as ValueTypeExpr;
    var formatExpr = converter.ConvertInternal(node.Arguments[0]) as ValueTypeExpr;
    return new FunctionExpr("DATE_FORMAT", dateExpr, formatExpr);
});
```

#### 步骤 3：注册 SQL 处理器（针对 MySQL）

```csharp
// 为 MySQL 注册 DATE_FORMAT 函数处理器
MySqlBuilder.Instance.RegisterFunctionSqlHandler("DATE_FORMAT", (functionName, args) => {
    if (args.Count != 2) {
        throw new ArgumentException("DATE_FORMAT requires exactly 2 arguments");
    }
    return $"DATE_FORMAT({args[0].Key}, {args[1].Key})";
});
```

#### 步骤 4：使用自定义表达式

```csharp
// 使用自定义 Format 方法
var users = await userService.SearchAsync(
    u => u.CreateTime.Format("%Y-%m-%d") == "2026-03-12"
);
```

### 3.2 示例 2：添加年龄计算属性

#### 步骤 1：定义计算属性

```csharp
public class User
{
    public DateTime BirthDate { get; set; }
    
    // 计算属性，不存储在数据库中
    public int Age
    {
        get { return DateTime.Now.Year - BirthDate.Year; }
    }
}
```

#### 步骤 2：注册成员处理器

```csharp
// 注册 Age 属性处理器
LambdaExprConverter.RegisterMemberHandler(typeof(User), "Age", (node, converter) => {
    // 转换 Age 属性访问为 SQL 表达式
    var userExpr = converter.ConvertInternal(node.Expression) as ValueTypeExpr;
    return new FunctionExpr("YEAR", new FunctionExpr("CURRENT_DATE")) - 
           new FunctionExpr("YEAR", new PropertyExpr("BirthDate"));
});
```

#### 步骤 3：使用自定义属性

```csharp
// 使用自定义 Age 属性
var adultUsers = await userService.SearchAsync(
    u => u.Age >= 18
);
```

### 3.3 示例 3：实现自定义字符串处理函数

#### 步骤 1：注册方法处理器

```csharp
// 注册自定义字符串处理方法
LambdaExprConverter.RegisterMethodHandler("CustomStringProcess", (node, converter) => {
    var stringExpr = converter.ConvertInternal(node.Arguments[0]) as ValueTypeExpr;
    return new FunctionExpr("CUSTOM_STRING_PROCESS", stringExpr);
});
```

#### 步骤 2：注册 SQL 处理器

```csharp
// 注册 CUSTOM_STRING_PROCESS 函数处理器
SqlServerBuilder.Instance.RegisterFunctionSqlHandler("CUSTOM_STRING_PROCESS", (functionName, args) => {
    if (args.Count != 1) {
        throw new ArgumentException("CUSTOM_STRING_PROCESS requires exactly 1 argument");
    }
    // 实现自定义字符串处理逻辑
    return $"dbo.CustomStringProcess({args[0].Key})";
});
```

#### 步骤 3：创建自定义方法

```csharp
public static class StringExtensions
{
    public static string CustomStringProcess(this string value)
    {
        // 本地实现（仅用于客户端计算）
        return value.ToUpper();
    }
}
```

#### 步骤 4：使用自定义函数

```csharp
// 使用自定义字符串处理函数
var users = await userService.SearchAsync(
    u => u.UserName.CustomStringProcess() == "ADMIN"
);
```

### 3.4 示例 4：实现窗口函数

#### 步骤 1：定义扩展方法

提供两个重载：仅分区字段的 `params` 重载，以及同时支持分区和排序的显式数组重载。
`SumOverOrderBy<T>` 用于替代元组，避免表达式树不支持元组字面量（CS8143/CS8144）的限制。
两个重载均只需返回原值，实际转换由处理器完成。

```csharp
/// <summary>窗口函数排序项，指定排序字段和方向。用于替代元组，避免表达式树不支持元组字面量的限制。</summary>
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

public static class WindowFunctionExtensions
{
    // 仅分区字段的重载（params 可逐一传入）
    public static int SumOver<T>(this int amount,
        params Expression<Func<T, object>>[] partitionBy) => amount;

    // 分区字段 + 排序字段的重载
    public static int SumOver<T>(this int amount,
        Expression<Func<T, object>>[] partitionBy,
        SumOverOrderBy<T>[] orderBy) => amount;
}
```

#### 步骤 2：注册方法处理器

在表达式树中，内部 lambda（如 `p => p.Year`）被编译为 `Quote(LambdaExpression)` 节点，
order-by 每个元素被编译为 `SumOverOrderBy<T>` 的 `NewExpression`。
必须使用现有 `converter.Convert()` 处理这些节点，才能携带正确的表别名；
排序项直接构造为 `OrderByItemExpr`，其字段 SQL 由 `ExprSqlConverter` 正确渲染，方向由 `Ascending` 属性控制。

```csharp
LambdaExprConverter.RegisterMethodHandler("SumOver", (node, converter) => {
    // node.Arguments[0] = this（扩展方法的 amount 参数）
    var amountExpr = converter.Convert(node.Arguments[0]) as ValueTypeExpr;

    var partitionExprs = new List<ValueTypeExpr>();
    var orderExprs     = new List<ValueTypeExpr>();

    // node.Arguments[1] = partitionBy（NewArrayExpression，元素为 Quote(Lambda)）
    if (node.Arguments.Count > 1 && node.Arguments[1] is NewArrayExpression partArray) {
        foreach (var elem in partArray.Expressions) {
            // converter.Convert 自动处理 Quote 节点，将内部 Lambda 转为 PropertyExpr
            if (converter.Convert(elem) is ValueTypeExpr vte)
                partitionExprs.Add(vte);
        }
    }

    // node.Arguments[2] = orderBy（NewArrayExpression，元素为 SumOverOrderBy<T> 构造表达式）
    if (node.Arguments.Count > 2 && node.Arguments[2] is NewArrayExpression orderArray) {
        foreach (var elem in orderArray.Expressions) {
            // 每个元素是 SumOverOrderBy<T> 的构造表达式
            // Arguments[0] = Field（Quote(Lambda)），Arguments[1] = Ascending（ConstantExpression(bool)）
            if (elem is NewExpression ctorNew && ctorNew.Arguments.Count == 2) {
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

#### 步骤 3：注册 SQL 处理器

`ValueSet` 渲染为 `(col1,col2)` 格式（含首尾括号）；其中的 `OrderByItemExpr` 元素由
`ExprSqlConverter` 渲染为 `"field"` 或 `"field DESC"`，排序方向已内置，无需额外处理器。
为 `SUM_OVER` 注册统一处理器，通过 `Substring` 去除 `ValueSet` 外层括号提取分区和排序子句。
`SUM_OVER` 语法在主流数据库中一致，只需注册一次。

```csharp
// 注册 SUM_OVER 全局处理器（所有数据库通用）
// args[0].Key = 金额列 SQL（如 "t.Amount"）
// args[1].Key = 分区 ValueSet SQL，格式为 "(t.Year,t.Quarter)"，去除首尾括号即可
// args[2].Key = 排序 ValueSet SQL，格式为 "(t.SaleDate,t.Name DESC)"，去除首尾括号即可
SqlBuilder.Instance.RegisterFunctionSqlHandler("SUM_OVER", (_, args) => {
    string amount = args[0].Key;

    // ValueSet 为空时渲染为 ""，非空时渲染为 "(col1,col2)"，用 Substring 去除首尾各一个括号
    string partitionSql = args.Count > 1 && args[1].Key.Length > 2
        ? args[1].Key.Substring(1, args[1].Key.Length - 2)
        : string.Empty;

    string orderSql = args.Count > 2 && args[2].Key.Length > 2
        ? args[2].Key.Substring(1, args[2].Key.Length - 2)
        : string.Empty;

    var clauses = new List<string>();
    if (!string.IsNullOrEmpty(partitionSql)) clauses.Add($"PARTITION BY {partitionSql}");
    if (!string.IsNullOrEmpty(orderSql))     clauses.Add($"ORDER BY {orderSql}");

    return $"SUM({amount}) OVER ({string.Join(" ", clauses)})";
});
```

#### 步骤 4：使用窗口函数

```csharp
var sales = await saleService.SearchAsync<SaleView>(
    s => s.Select(
        x => new SaleView {
            Id             = x.Id,
            Amount         = x.Amount,
            SaleDate       = x.SaleDate,
            // params 重载：按年、季度分区，无排序
            QuarterlyTotal = x.Amount.SumOver<Sale>(p => p.Year, p => p.Quarter),
            // 显式数组重载：按年分区，按销售日期升序排列
            YearlyRunning  = x.Amount.SumOver<Sale>(
                partitionBy: new Expression<Func<Sale, object>>[] { p => p.Year },
                orderBy: new SumOverOrderBy<Sale>[] { new SumOverOrderBy<Sale>(p => p.SaleDate, true) }
            )
        }
    )
);
```

### 3.5 示例 5：纯 Expr 方式实现窗口函数

相比示例 4 的 Lambda 扩展方法，纯 Expr 方式**无需定义扩展方法，也无需注册 `RegisterMethodHandler`**，
直接通过 `FunctionExpr`、`ValueSet`、`OrderByItemExpr` 等 Expr 对象构造窗口函数表达式，
再借助**闭包变量**嵌入 Lambda 表达式树中。

**工作原理**：`LambdaExprConverter` 的 `EvaluateToExpr` 方法会对表达式树中不含 Lambda 参数的节点
进行求值；若求值结果本身是一个 `Expr` 实例，则直接将该 `Expr` 作为列值送入 SQL 转换管线，
完全绕过方法注册机制。Lambda 中的 `(int)exprVar` 类型转换同样透明——
`ConvertUnary(Convert)` 直接穿透到 `EvaluateToExpr`，不影响 `Expr` 的传递。

#### 步骤 1：注册 SQL 处理器

与 Lambda 方式完全相同，只注册一次即可（若两种方式共用，无需重复注册）：

```csharp
SqlBuilder.Instance.RegisterFunctionSqlHandler("SUM_OVER", (_, args) =>
{
    string amount = args[0].Key;

    string partitionSql = args.Count > 1 && args[1].Key.Length > 2
        ? args[1].Key.Substring(1, args[1].Key.Length - 2)
        : string.Empty;

    string orderSql = args.Count > 2 && args[2].Key.Length > 2
        ? args[2].Key.Substring(1, args[2].Key.Length - 2)
        : string.Empty;

    var clauses = new List<string>();
    if (!string.IsNullOrEmpty(partitionSql)) clauses.Add($"PARTITION BY {partitionSql}");
    if (!string.IsNullOrEmpty(orderSql))     clauses.Add($"ORDER BY {orderSql}");

    return $"SUM({amount}) OVER ({string.Join(" ", clauses)})";
});
```

#### 步骤 2：直接构造 Expr 对象

使用 `Expr.Prop()`、`ValueSet`、`OrderByItemExpr` 组装窗口函数表达式，无需任何扩展方法：

```csharp
// 仅分区：SUM(Amount) OVER (PARTITION BY ProductId)
var productTotalExpr = new FunctionExpr("SUM_OVER",
    Expr.Prop("Amount"),
    new ValueSet(Expr.Prop("ProductId")),   // PARTITION BY ProductId
    new ValueSet());                         // 无排序

// 分区 + 排序：SUM(Amount) OVER (PARTITION BY ProductId ORDER BY SaleTime)
var runningTotalExpr = new FunctionExpr("SUM_OVER",
    Expr.Prop("Amount"),
    new ValueSet(Expr.Prop("ProductId")),
    new ValueSet(new OrderByItemExpr(Expr.Prop("SaleTime"), ascending: true)));
```

#### 步骤 3：嵌入查询

构造好的 `FunctionExpr` 有两种方式嵌入查询中：

**方式一：Lambda 闭包变量**

将 `FunctionExpr` 作为闭包变量，在 Lambda 中通过强制类型转换引用（`EvaluateToExpr` 识别
`Expr` 实例，透明穿透 `(int)` 转换后直接送入 SQL 管线）：

```csharp
var results = await saleDAO
    .WithArgs([tableMonth])
    .Search<SaleView>(q => q
        .OrderBy(s => s.ProductId)
        .Select(s => new SaleView
        {
            Id           = s.Id,
            ProductId    = s.ProductId,
            Amount       = s.Amount,
            SaleDate     = s.SaleDate,
            ProductTotal = (int)productTotalExpr,   // 闭包变量透明传递到 FunctionExpr
            RunningTotal = (int)runningTotalExpr
        })
    ).ToListAsync();
```

> 也可直接在 Lambda 内内联构造——因其不含 Lambda 参数，`EvaluateToExpr` 同样会编译执行
> `new FunctionExpr(...)` 并将结果送入管线：
> ```csharp
> ProductTotal = (int)(new FunctionExpr("SUM_OVER",
>     Expr.Prop("Amount"),
>     new ValueSet(Expr.Prop("ProductId")),
>     new ValueSet()))
> ```

**方式二：直接构造 `SelectExpr`（纯 Expr，无需任何 Lambda）**

使用 `FromExpr` 作为数据源，链式调用 `OrderBy` / `Select` 构造完整查询表达式，
传入 `SearchAs<TResult>(SelectExpr)` 直接执行——整个流程不涉及任何 Lambda 表达式树：

```csharp
// 构造完整的 Expr 查询链，无需 Lambda
var source = new FromExpr(typeof(SalesRecord));   // 数据源类型对应 DAO 的 ObjectType
var selectExpr = source
    .OrderBy(new OrderByItemExpr(Expr.Prop("ProductId"), ascending: true))
    .Select(
        new SelectItemExpr(Expr.Prop("Id"),          "Id"),
        new SelectItemExpr(Expr.Prop("ProductId"),   "ProductId"),
        new SelectItemExpr(Expr.Prop("Amount"),      "Amount"),
        new SelectItemExpr(Expr.Prop("SaleDate"),    "SaleDate"),
        new SelectItemExpr(productTotalExpr,         "ProductTotal"),   // 直接放入 SelectItemExpr
        new SelectItemExpr(runningTotalExpr,         "RunningTotal"));

// SearchAs<TResult>(SelectExpr) 直接执行 SelectExpr，WithArgs 的表名参数由上下文自动传入
var results = await saleDAO
    .WithArgs([tableMonth])
    .SearchAs<SaleView>(selectExpr)
    .ToListAsync();
```

#### 两种方式对比

| 对比项 | Lambda 扩展方法（示例 4） | 纯 Expr（示例 5） |
|--------|--------------------------|-------------------|
| 需要扩展方法定义 | ✅ 是 | ❌ 否 |
| 需要 RegisterMethodHandler | ✅ 是 | ❌ 否 |
| 需要 RegisterFunctionSqlHandler | ✅ 是 | ✅ 是 |
| 代码提示友好性 | ✅ 高（方法调用形式） | ⚠️ 中（Expr 对象构造） |
| 支持多表查询列别名 | ✅ 自动（converter.Convert 携带别名） | ⚠️ 需手动设置 PropertyExpr.TableAlias |
| 适用场景 | 通用、高复用 | 一次性、快速原型 |

## 4. 高级技巧

### 4.1 处理不同数据库的差异

针对不同的数据库，可以为每种数据库类型注册不同的 SQL 处理器：

```csharp
// 为 MySQL 注册
MySqlBuilder.Instance.RegisterFunctionSqlHandler("CustomFunction", (name, args) => {
    return $"MYSQL_CUSTOM({string.Join(", ", args.Select(arg => arg.Key))})";
});

// 为 SQL Server 注册
SqlServerBuilder.Instance.RegisterFunctionSqlHandler("CustomFunction", (name, args) => {
    return $"SQLSERVER_CUSTOM({string.Join(", ", args.Select(arg => arg.Key))})";
});
```

### 4.2 处理复杂参数

对于包含复杂参数的方法，可以在处理器中进行特殊处理：

```csharp
LambdaExprConverter.RegisterMethodHandler("InRange", (node, converter) => {
    var valueExpr = converter.ConvertInternal(node.Arguments[0]) as ValueTypeExpr;
    var minExpr = converter.ConvertInternal(node.Arguments[1]) as ValueTypeExpr;
    var maxExpr = converter.ConvertInternal(node.Arguments[2]) as ValueTypeExpr;
    
    // 创建范围条件表达式
    var greaterThanMin = new LogicBinaryExpr(valueExpr, LogicOperator.GreaterThanOrEqual, minExpr);
    var lessThanMax = new LogicBinaryExpr(valueExpr, LogicOperator.LessThanOrEqual, maxExpr);
    return greaterThanMin.And(lessThanMax);
});
```

## 5. 注意事项

1. **性能考虑**：注册的处理器会在每次表达式转换时被调用，应确保处理逻辑高效。

2. **参数验证**：在处理器中应验证参数数量和类型，确保生成的表达式正确。

3. **数据库兼容性**：不同数据库的 SQL 语法可能不同，应针对目标数据库编写相应的 SQL 处理器。

4. **错误处理**：在处理器中添加适当的错误处理，确保在表达式转换失败时能给出明确的错误信息。

5. **测试**：在使用自定义表达式前，应充分测试其在不同场景下的行为。

## 6. 总结

通过结合使用 `LambdaExprConverter.RegisterMethodHandler`、`RegisterMemberHandler` 和 `SqlBuilder.RegisterFunctionSqlHandler`，可以：

- 扩展 LiteOrm 的表达式处理能力
- 实现自定义方法和属性的 SQL 转换
- 支持复杂的业务逻辑表达式
- 提高查询代码的可读性和可维护性

这种扩展机制使得 LiteOrm 能够适应各种复杂的业务场景，同时保持代码的简洁性和类型安全性。