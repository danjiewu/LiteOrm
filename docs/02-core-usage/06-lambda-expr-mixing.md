# Lambda 与 Expr 组合使用

这不是“高级技巧”，而是 LiteOrm 日常查询里非常常见的一种写法：**用 Lambda 保持业务语义清晰，用 `Expr` 承担动态拼装能力。**

最常见的组合方式有两种：

- **`expr.To<bool>()`**：外层继续写 Lambda，把动态 `Expr` 嵌进去。
- **`Expr.Lambda<T>()`**：先把稳定条件写成 Lambda，再转成 `LogicExpr` 和其他动态条件拼接。

## 1. 什么时候该组合使用

| 场景 | 推荐方式 | 原因 |
|------|----------|------|
| 大部分条件固定，少量条件运行时决定 | Lambda + `expr.To<bool>()` | 保留强类型和可读性 |
| 有一组稳定基础条件，要和动态筛选器复用 | `Expr.Lambda<T>()` | 方便把 Lambda 转成可拼装的 `LogicExpr` |
| 条件几乎全部动态生成 | 纯 `Expr` | 更直接，不必为了保留 Lambda 硬做包裹 |

## 2. 在 Lambda 中嵌入已有 Expr

`To<T>()` 是连接 Lambda 与 `Expr` 的桥接方法。它只在表达式解析阶段生效，正常直接调用会抛出异常，因此**只能放在 LiteOrm 查询方法接收的 Lambda 参数里使用**。

### 2.1 最简单的组合

```csharp
var users = await userService.SearchAsync(
    u => u.Age >= 18 && Expr.Prop("UserName").Contains("John").To<bool>()
);
```

这类写法很适合“固定主条件 + 动态补充条件”的查询。

### 2.2 先动态拼好，再放回 Lambda

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

这样写的好处是：动态部分仍然可以在多个入口复用，而最终查询依然保留 Lambda 的业务语义。

### 2.3 和关联 Exists 一起用

```csharp
var hasOpenOrder = Expr.ExistsRelated<Order>(
    Expr.Prop("Status") != "Completed"
);

var activeUsers = await userService.SearchAsync(
    u => u.IsActive == true && hasOpenOrder.To<bool>()
);
```

如果关联路径已经在模型里声明好，`ExistsRelated(...)` 和 Lambda 组合通常比纯手写子查询更直观。

## 3. 先写 Lambda，再转成可拼装的 Expr

当“基础条件”本身非常适合用 Lambda 表达，但后面还要继续按参数追加条件时，可以先用 `Expr.Lambda<T>()` 把 Lambda 转成 `LogicExpr`。

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

这一招很适合做“固定业务基线 + 可选筛选器”的查询构建。

## 4. 使用建议

### 4.1 `To<T>()` 的泛型参数要与 Lambda 返回类型一致

在查询条件里通常就是 `bool`：

```csharp
u => u.Age >= 18 && expr.To<bool>()
```

### 4.2 不要在普通运行路径里直接执行 `To<T>()`

它不是运行时转换方法，而是给 LiteOrm 的 Lambda 解析器识别用的。

### 4.3 组合使用不会额外增加 SQL 执行成本

`To<T>()` 会在解析阶段被替换为底层 `Expr`，最终还是统一生成一棵 SQL 表达式树。

## 5. 该怎么选

| 方式 | 优点 | 更适合 |
|------|------|--------|
| 纯 Lambda | 最直观、IDE 体验最好 | 固定条件查询 |
| 纯 Expr | 动态拼装最自由 | 查询构建器、复杂后台筛选 |
| Lambda + Expr | 同时兼顾语义清晰和动态扩展 | 业务条件稳定但筛选项可变 |

## 6. 相关链接

- [返回目录](../README.md)
- [查询指南](./03-query-guide.md)
- [表达式扩展](../04-extensibility/01-expression-extension.md)
