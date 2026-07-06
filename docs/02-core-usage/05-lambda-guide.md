# Lambda 查询指南

Lambda 是 LiteOrm 最直观的查询方式，强类型、可读性最好，适合大部分业务查询。本文聚焦 Lambda 查询的过滤、排序、参数化、三目运算和子查询用法。

三种查询方式的对比与选型见[查询总览](./04-query-overview.md)；动态拼装条件请看 [Expr 使用指南](./06-expr-guide.md)；DAO 层手写 SQL 请看 [ExprString 使用指南](./07-exprstring-guide.md)。

## 1. 基础过滤

```csharp
var users = await userService.SearchAsync(u => u.Age >= 18);
var users = await userService.SearchAsync(u => u.UserName.Contains("admin"));
var users = await userService.SearchAsync(u => new[] { 1, 2, 3 }.Contains(u.Id));
```

Lambda 中的属性访问会被解析成 `PropertyExpr`，比较/字符串/集合操作会被解析成 `LogicExpr`，最终统一走 `Expr` → SQL 的生成管道。

## 2. 排序

Lambda 查询中，排序通过 `OrderBy` / `OrderByDescending` / `ThenBy` / `ThenByDescending` 链式调用实现。

### 2.1 单列排序

```csharp
// 按创建时间升序
var users = await userService.SearchAsync(
    q => q.OrderBy(u => u.CreateTime)
);

// 按年龄降序
var users = await userService.SearchAsync(
    q => q.OrderByDescending(u => u.Age)
);
```

### 2.2 多列排序（ThenBy）

```csharp
// 先按部门升序，同部门内按创建时间降序
var users = await userService.SearchAsync(
    q => q.OrderBy(u => u.DeptId)
          .ThenByDescending(u => u.CreateTime)
);
```

`ThenBy` / `ThenByDescending` 必须在 `OrderBy` / `OrderByDescending` 之后使用，可以级联多个。

### 2.3 排序与分页组合

```csharp
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderByDescending(u => u.CreateTime)
          .Skip(0)
          .Take(20)
);
```

### 2.4 按计算表达式排序

```csharp
// 按计算字段排序
var users = await userService.SearchAsync(
    q => q.OrderBy(u => u.FirstName + " " + u.LastName)
);

// 按时间差排序
var users = await userService.SearchAsync(
    q => q.OrderByDescending(u => (DateTime.Now - u.CreateTime).TotalMilliseconds)
);
```

> Lambda 中的字符串 `+` 会在解析阶段被转换为 concat，最终通过 `SqlBuilder.BuildConcatSql` 按方言输出 `CONCAT(a,b,...)` 或 `a || b`。手写 `Expr` 时则需要显式使用 `.Concat(...)`，见 [Expr 使用指南](./06-expr-guide.md#字符串拼接不要用--用-concat)。

## 3. 变量捕获与参数化

```csharp
var keyword = "admin";
var users = await userService.SearchAsync(u => u.UserName.Contains(keyword));
```

Lambda 外定义的变量会被参数化。如果是 `DateTime.Now` 这类值，希望参数化时应先赋给变量：

```csharp
var now = DateTime.Now;
var users = await userService.SearchAsync(u => u.CreateTime <= now);
```

## 4. 三目运算符会转成 `CASE`

```csharp
var users = await userService.SearchAsync(
    u => (u.Age >= 18 ? "Adult" : "Minor") == "Adult"
);
```

这类 Lambda 会先转成 `Expr.If(...)`，最终生成 SQL `CASE WHEN ... THEN ... ELSE ... END`。多条件 `CASE` 可通过 `Expr.Case(...)` 构造，见 [Expr 使用指南](./06-expr-guide.md#14-类型转换与条件值)。

## 5. `Exists` 与 `ExistsRelated`

### 5.1 显式 `Exists`

```csharp
using static LiteOrm.Common.Expr;

var users = await userService.SearchAsync(
    u => Exists<Department>(d => d.Id == u.DeptId && d.Name == "研发中心")
);
```

适合你想**自己明确写出关联条件**的场景。`Exists<T>` 是 `Expr` 的静态方法，等价的 `Expr` 写法见 [Expr 使用指南](./06-expr-guide.md#21-显式-exists)。

### 5.2 自动关联 `ExistsRelated`

```csharp
using static LiteOrm.Common.Expr;

var users = await userService.SearchAsync(
    u => ExistsRelated<DepartmentView>(d => d.Name == "研发中心")
);
```

`ExistsRelated` 会根据 `ForeignType` / `TableJoin` 等元数据自动补关联条件。适合模型里已经声明好关联路径，只想"按关联表条件过滤主表"的场景。匹配逻辑、继承链规则和 `ConstFilter` 行为请看[关联查询](./08-associations.md)。

## 6. 相关链接

- [查询总览](./04-query-overview.md)
- [Expr 使用指南](./06-expr-guide.md)
- [ExprString 使用指南](./07-exprstring-guide.md)
- [增删改查](./03-crud-guide.md)
- [关联查询](./08-associations.md)
- [Lambda 与 Expr 组合使用](./09-lambda-expr-mixing.md)
- [CTE 指南](./10-cte-guide.md)
- [表达式扩展](../04-extensibility/01-expression-extension.md)
