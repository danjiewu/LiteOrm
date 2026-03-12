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
  - `methodName`：要处理的方法名称
  - `type`：目标类型
  - `handler`：处理逻辑，若为 null 则使用默认处理器

#### 1.1.2 RegisterMemberHandler

```csharp
// 注册全局成员处理器
public static void RegisterMemberHandler(string memberName, Func<MemberExpression, LambdaExprConverter, Expr> handler = null)

// 注册特定类型的成员处理器
public static void RegisterMemberHandler(Type type, string memberName, Func<MemberExpression, LambdaExprConverter, Expr> handler = null)
```

- **参数说明**：
  - `memberName`：要处理的成员名称（属性/字段）
  - `type`：目标类型
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

### 3.4 示例 4：实现窗口函数

#### 步骤 1：定义扩展方法

```csharp
public static class WindowFunctionExtensions
{
    public static decimal SumOver<T>(this decimal amount, params Expression<Func<T, object>>[] partitionBy)
    {
        // 本地实现（仅用于客户端计算）
        return amount;
    }
    
    public static decimal SumOver<T>(this decimal amount, IEnumerable<Expression<Func<T, object>>> partitionBy, IEnumerable<(Expression<Func<T, object>>, bool)> orderBy)
    {
        // 本地实现（仅用于客户端计算）
        return amount;
    }
}
```

#### 步骤 2：注册方法处理器

```csharp
// 注册 SumOver 方法处理器
LambdaExprConverter.RegisterMethodHandler("SumOver", (node, converter) => {
    // 获取金额表达式
    var amountExpr = converter.ConvertInternal(node.Arguments[0]) as ValueTypeExpr;
    
    // 处理分区字段
    List<ValueTypeExpr> partitionByExprs = new List<ValueTypeExpr>();
    List<(ValueTypeExpr, bool)> orderByExprs = new List<(ValueTypeExpr, bool)>();
    
    if (node.Arguments.Count == 2) {
        // 只有分区字段的情况
        var partitionByArg = node.Arguments[1];
        if (partitionByArg is NewArrayExpression arrayExpr) {
            foreach (var expr in arrayExpr.Expressions) {
                if (expr is LambdaExpression lambda) {
                    var lambdaConverter = new LambdaExprConverter(lambda);
                    partitionByExprs.Add(lambdaConverter.ToValueExpr());
                }
            }
        }
    } else if (node.Arguments.Count == 3) {
        // 分区字段和排序字段的情况
        var partitionByArg = node.Arguments[1];
        var orderByArg = node.Arguments[2];
        
        // 处理分区字段
        if (partitionByArg is NewArrayExpression arrayExpr) {
            foreach (var expr in arrayExpr.Expressions) {
                if (expr is LambdaExpression lambda) {
                    var lambdaConverter = new LambdaExprConverter(lambda);
                    partitionByExprs.Add(lambdaConverter.ToValueExpr());
                }
            }
        }
        
        // 处理排序字段
        if (orderByArg is NewArrayExpression orderArrayExpr) {
            foreach (var expr in orderArrayExpr.Expressions) {
                if (expr is NewExpression newExpr && newExpr.Arguments.Count == 2) {
                    var fieldExpr = newExpr.Arguments[0];
                    var ascExpr = newExpr.Arguments[1];
                    
                    if (fieldExpr is LambdaExpression fieldLambda && ascExpr is ConstantExpression ascConst) {
                        var lambdaConverter = new LambdaExprConverter(fieldLambda);
                        bool isAscending = ascConst.Value is bool b && b;
                        orderByExprs.Add((lambdaConverter.ToValueExpr(), isAscending));
                    }
                }
            }
        }
    }
    
    // 创建窗口函数表达式
    return new FunctionExpr("SUM_OVER", amountExpr, 
        new ValueSet(ValueJoinType.List, partitionByExprs.ToArray()),
        new ValueSet(ValueJoinType.List, orderByExprs.Select(o => new ValueSet(ValueJoinType.List, o.Item1, new ValueExpr(o.Item2))).ToArray())
    );
});
```

#### 步骤 3：注册 SQL 处理器

```csharp
// 为 MySQL 注册 SUM_OVER 函数处理器
MySqlBuilder.Instance.RegisterFunctionSqlHandler("SUM_OVER", (functionName, args) => {
    if (args.Count < 1) {
        throw new ArgumentException("SUM_OVER requires at least 1 argument");
    }
    
    var amount = args[0].Key;
    
    // 构建 PARTITION BY 子句
    string partitionBy = string.Empty;
    if (args.Count > 1) {
        var partitionBySet = args[1].Key;
        // 移除 ValueSet 包装，提取实际的分区字段
        partitionBy = partitionBySet.Replace("LIST(", "").Replace(")", "");
    }
    
    // 构建 ORDER BY 子句
    string orderBy = string.Empty;
    if (args.Count > 2) {
        var orderBySet = args[2].Key;
        // 移除 ValueSet 包装，提取实际的排序字段
        var orderByItems = orderBySet.Replace("LIST(", "").Replace(")", "").Split(", ");
        List<string> orderByClauses = new List<string>();
        
        for (int i = 0; i < orderByItems.Length; i += 2) {
            if (i + 1 < orderByItems.Length) {
                var field = orderByItems[i];
                var isAsc = orderByItems[i + 1] == "True";
                orderByClauses.Add($"{field} {(isAsc ? "ASC" : "DESC")}");
            }
        }
        orderBy = string.Join(", ", orderByClauses);
    }
    
    string partitionByClause = string.IsNullOrEmpty(partitionBy) ? "" : $"PARTITION BY {partitionBy}";
    string orderByClause = string.IsNullOrEmpty(orderBy) ? "" : $"ORDER BY {orderBy}";
    
    // 组合所有子句
    List<string> clauses = new List<string>();
    if (!string.IsNullOrEmpty(partitionByClause)) clauses.Add(partitionByClause);
    if (!string.IsNullOrEmpty(orderByClause)) clauses.Add(orderByClause);
    
    string overClause = string.Join(" ", clauses);
    
    return $"SUM({amount}) OVER ({overClause})";
});

// 为 SQL Server 注册 SUM_OVER 函数处理器
SqlServerBuilder.Instance.RegisterFunctionSqlHandler("SUM_OVER", (functionName, args) => {
    // 与 MySQL 处理器相同的实现
    if (args.Count < 1) {
        throw new ArgumentException("SUM_OVER requires at least 1 argument");
    }
    
    var amount = args[0].Key;
    
    // 构建 PARTITION BY 子句
    string partitionBy = string.Empty;
    if (args.Count > 1) {
        var partitionBySet = args[1].Key;
        partitionBy = partitionBySet.Replace("LIST(", "").Replace(")", "");
    }
    
    // 构建 ORDER BY 子句
    string orderBy = string.Empty;
    if (args.Count > 2) {
        var orderBySet = args[2].Key;
        var orderByItems = orderBySet.Replace("LIST(", "").Replace(")", "").Split(", ");
        List<string> orderByClauses = new List<string>();
        
        for (int i = 0; i < orderByItems.Length; i += 2) {
            if (i + 1 < orderByItems.Length) {
                var field = orderByItems[i];
                var isAsc = orderByItems[i + 1] == "True";
                orderByClauses.Add($"{field} {(isAsc ? "ASC" : "DESC")}");
            }
        }
        orderBy = string.Join(", ", orderByClauses);
    }
    
    string partitionByClause = string.IsNullOrEmpty(partitionBy) ? "" : $"PARTITION BY {partitionBy}";
    string orderByClause = string.IsNullOrEmpty(orderBy) ? "" : $"ORDER BY {orderBy}";
    
    List<string> clauses = new List<string>();
    if (!string.IsNullOrEmpty(partitionByClause)) clauses.Add(partitionByClause);
    if (!string.IsNullOrEmpty(orderByClause)) clauses.Add(orderByClause);
    
    string overClause = string.Join(" ", clauses);
    
    return $"SUM({amount}) OVER ({overClause})";
});
```

#### 步骤 4：使用窗口函数

```csharp
// 定义排序方向的辅助方法
public static (Expression<Func<T, object>>, bool) Asc<T>(Expression<Func<T, object>> expr) => (expr, true);
public static (Expression<Func<T, object>>, bool) Desc<T>(Expression<Func<T, object>> expr) => (expr, false);

// 使用窗口函数计算季度销售总额
var sales = await saleService.SearchAsync<SaleView>(
    q => q.Select(
        s => new SaleView {
            Id = s.Id,
            Amount = s.Amount,
            SaleDate = s.SaleDate,
            // 计算季度累计销售额
            QuarterlyTotal = s.Amount.SumOver<Sale>(
                // 分区字段：按年份和季度
                s => Expr.Func("YEAR", s.SaleDate),
                s => Expr.Func("QUARTER", s.SaleDate),
                // 排序字段：按销售日期升序
                orderBy: new[] {
                    Asc<Sale>(s => s.SaleDate)
                }
            )
        }
    )
);
```

## 6. 总结

通过结合使用 `LambdaExprConverter.RegisterMethodHandler`、`RegisterMemberHandler` 和 `SqlBuilder.RegisterFunctionSqlHandler`，可以：

- 扩展 LiteOrm 的表达式处理能力
- 实现自定义方法和属性的 SQL 转换
- 支持复杂的业务逻辑表达式
- 提高查询代码的可读性和可维护性

这种扩展机制使得 LiteOrm 能够适应各种复杂的业务场景，同时保持代码的简洁性和类型安全性。