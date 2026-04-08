# Lambda 与 Expr 组合使用

LiteOrm 支持多种方式组合 Lambda 表达式和 `Expr` 对象。本文档介绍两种核心模式：

- **`To<T>()`**：将已构建的 `Expr` 对象嵌入 Lambda 表达式
- **`Expr.Lambda<T>()`**：将 Lambda 表达式转换为 `LogicExpr`，然后与 `Expr` 动态组合

## 1. To<T>() 扩展方法

### 1.1 核心机制

`Expr.To<T>()` 是专为 Lambda 表达式解析设计的桥接方法：

```csharp
public static T To<T>(this Expr expr)
{
    throw new NotSupportedException("Only supported in Lambda expression parsing scenarios.");
}
```

在正常执行时会抛出异常，但在 Lambda 解析时（通过 `LambdaExprConverter`）会被识别并替换为底层 `Expr` 对象。

### 1.2 简单组合

将 `Expr` 构建的条件与 Lambda 条件组合：

```csharp
var condition = u => u.Age >= 18 && Expr.Prop("UserName").Contains("John").To<bool>();
var users = await userService.SearchAsync(condition);
```

### 1.3 动态条件封装

将复杂的动态条件封装为 `LogicExpr`，然后在 Lambda 中复用：

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

### 1.4 关联查询与Exists

使用 `Expr.ExistsRelated<T>()` 构建关联 Exists 条件：

```csharp
var hasRelatedOrders = Expr.ExistsRelated<Order>(
    Expr.Prop("T0", "Id") == Expr.Prop("UserId")
    && Expr.Prop("Status") != "Completed"
);

var activeUsers = await userService.SearchAsync(
    u => u.IsActive == true && hasRelatedOrders.To<bool>()
);
```

## 2. Expr.Lambda<T>() 方法

### 2.1 核心机制

`Expr.Lambda<T>()` 将 Lambda 表达式转换为 `LogicExpr`，用于在 `Expr` 动态构建中嵌入复杂的 Lambda 条件：

```csharp
// 将 Lambda 表达式转换为 LogicExpr
var lambdaExpr = Expr.Lambda<User>(u => u.Age > 18 && u.IsActive);
```

### 2.2 与 Expr 组合使用

将 `Expr.Lambda<T>()` 返回的 `LogicExpr` 与其他 `Expr` 动态组合：

```csharp
// 使用 Lambda 转换后的条件
var lambdaCondition = Expr.Lambda<User>(u => u.Age > 18);

// 动态构建附加条件
LogicExpr filter = null;
if (!string.IsNullOrEmpty(keyword))
    filter &= Expr.Prop("UserName").Contains(keyword);

// 组合：lambdaCondition AND filter
var combined = lambdaCondition & filter;

var users = await userService.SearchAsync(
    u => combined.To<bool>()
);
```

## 3. 实用场景

### 3.1 动态筛选条件组合

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

### 3.2 预定义条件与动态条件组合

```csharp
// 预定义的基础条件
var baseCondition = Expr.Lambda<User>(u => u.IsActive == true);

// 动态附加条件
LogicExpr extraFilter = null;
if (minAge.HasValue)
    extraFilter &= Expr.Prop("Age") >= minAge.Value;

// 组合使用
var combined = baseCondition & extraFilter;

var users = await userService.SearchAsync(
    u => combined.To<bool>()
);
```

## 4. 注意事项

### 4.1 To<T>() 的类型一致性

`To<T>()` 的泛型参数 `T` 应与 Lambda 表达式的返回类型匹配：

```csharp
// 条件表达式返回 bool
u => u.Age >= 18 && expr.To<bool>()
```

### 4.2 避免在非解析场景调用

`To<T>()` 方法在正常执行时会抛出 `NotSupportedException`，只能在查询方法的 Lambda 参数中使用。

### 4.3 性能考量

组合使用不会引入额外性能开销，`To<T>()` 在解析阶段被替换，实际 SQL 由优化后的表达式树生成。

## 5. 与纯 Expr 查询的对比

| 特性 | 纯 Lambda | 纯 Expr | 组合使用 |
|------|----------|---------|---------|
| 类型安全 | ✅ 编译时检查 | ❌ 运行时检查 | ✅ 编译时检查 |
| 智能提示 | ✅ IDE 支持 | ❌ 无 | ✅ IDE 支持 |
| 动态构造 | ❌ 困难 | ✅ 灵活 | ✅ 灵活 |
| 适用场景 | 固定条件 | 复杂动态逻辑 | 条件可变的业务场景 |

## 6. 相关链接

- [返回目录](../README.md)
- [查询指南](../02-core-usage/03-query-guide.md)
- [表达式扩展](../04-extensibility/01-expression-extension.md)
