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

### 1.1 逻辑组合（`&&`、`||`、`!`）

Lambda 中可以直接使用 C# 逻辑运算符组合条件：

```csharp
// AND + OR 组合
var result = await userService.SearchAsync(
    u => u.Age > 18 && u.Status == 1 || u.IsVip
);

// NOT 取反
var active = await userService.SearchAsync(
    u => !u.IsDeleted && u.IsActive
);

// 加括号改变优先级
var complex = await userService.SearchAsync(
    u => u.Age > 18 && (u.Status == 1 || u.IsVip)
);
```

`&&` 转换为 `AND`，`||` 转换为 `OR`，`!` 转换为 `NOT`。运算符优先级遵循 C# 语义：`!` 优先于 `&&`，`&&` 优先于 `||`。如需改变优先级，请加括号。

### 1.2 支持的字符串与方法

LiteOrm 在启动时通过 `LiteOrmLambdaHandlerInitializer` 注册了一批 Lambda 方法处理器，以下是实际支持的字符串方法（对照源码 `LiteOrm.Common/Converter/LiteOrmLambdaHandlerInitializer.cs`）：

| 方法 | SQL 语义 | 示例 |
|------|----------|------|
| `string.Contains(text)` | `LIKE '%text%'` | `u => u.Name.Contains("admin")` |
| `string.StartsWith(text)` | `LIKE 'text%'` | `u => u.Name.StartsWith("admin")` |
| `string.EndsWith(text)` | `LIKE '%text'` | `u => u.Name.EndsWith("admin")` |
| `string.Concat(...)` | `CONCAT(...)` | `u => string.Concat(u.FirstName, " ", u.LastName)` |
| `string.ToUpper()` | `UPPER` | `u => u.Name.ToUpper() == "ADMIN"` |
| `string.ToLower()` | `LOWER` | `u => u.Name.ToLower() == "admin"` |
| `string.Trim()` / `TrimStart()` / `TrimEnd()` | `TRIM` / `LTRIM` / `RTRIM` | `u => u.Name.Trim() == "admin"` |
| `string.Remove(startIndex)` | `LEFT` | `u => u.Code.Remove(3) == "ABC"` |
| `string.Length`（属性） | `CHAR_LENGTH` / `LEN` | `u => u.Name.Length > 5` |
| `Equals(obj)` | `=` | `u => u.Name.Equals("admin")` |
| `ToString()` / `ToString(format)` | 原值 / `Format` | `u => u.CreateTime.ToString("yyyy-MM-dd")` |

集合方法：

| 方法 | SQL 语义 | 示例 |
|------|----------|------|
| `IList.Contains(item)` / `Enumerable.Contains(collection, item)` | `IN` | `u => new[] { 1, 2, 3 }.Contains(u.Id)` |

此外还支持以下类型的成员和方法：

- **DateTime**：`DateTime.Now`（`CURRENT_TIMESTAMP`）、`DateTime.Today`（`CURRENT_DATE`）、`AddYears/AddMonths/AddDays/AddHours/AddMinutes/AddSeconds`（`DATE_ADD` / `DATEADD`）
- **Math**：`Abs`、`Max`、`Min`、`Floor`、`Ceiling`、`Round`、`Pow`、`Sqrt`、`Truncate` 等（直接映射为 SQL 数学函数）
- **TimeSpan**：`TotalSeconds` / `TotalDays` / `TotalHours` / `TotalMinutes` / `TotalMilliseconds`。当两个日期相减时（如 `(DateTime.Now - u.CreateTime).TotalDays`），自动转换为 `DateDiffDays` 等日期差函数

> Lambda 中的字符串 `+` 会在解析阶段被转换为 `concat`，最终通过 `SqlBuilder.BuildConcatSql` 按方言输出 `CONCAT(a,b,...)` 或 `a || b`。

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

### 2.5 Skip/Take 分页语义

Lambda 查询中，分页通过查询构建器的 `.Skip(skip)` 和 `.Take(take)` 链式方法实现：

```csharp
// 取第 1 页（每页 10 条）
var paged = await userService.SearchAsync(
    q => q.Where(u => u.Age > 18)
          .OrderBy(u => u.Name)
          .Skip(0)
          .Take(10)
);
```

- `Skip(n)`：跳过前 `n` 条记录，对应 SQL 的 `OFFSET n`（部分数据库为 `LIMIT n, ...` 或 `ROWNUM` 方言）。
- `Take(n)`：取 `n` 条记录，对应 SQL 的 `LIMIT n`（或 `FETCH FIRST n ROWS ONLY`）。
- `Skip` 和 `Take` 可以单独使用，也可以组合使用；分页时通常先 `OrderBy` 再 `Skip`/`Take`。
- 大偏移量分页（如 `Skip(10000)`）性能较差，建议改用基于 ID 的游标分页，见[性能优化](../03-advanced-topics/03-performance.md#33-分页优化)。

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

## 6. 常见问题

### 6.1 Lambda 中不支持的方法会怎样？

如果 Lambda 中调用了未注册处理器的方法（即 `LiteOrmLambdaHandlerInitializer` 未覆盖的方法），LiteOrm 会在解析阶段抛出异常，提示该方法不被支持。这是设计上的安全保障——不会静默忽略未知方法，而是让开发者尽早发现问题。

如需支持自定义方法，可以参考 `LiteOrmLambdaHandlerInitializer` 中的注册方式，通过 `LambdaExprConverter.RegisterMethodHandler` 注册自定义处理器。

### 6.2 Lambda 与 Expr 如何互转？

- **Lambda → Expr**：Lambda 表达式在解析阶段会自动转换为 `Expr` 树。也可以通过 `Expr.Lambda<T>(u => ...)` 手动将 Lambda 转成 `LogicExpr`，便于与手写 Expr 组合使用。
- **Expr → Lambda**：在 Lambda 中可以通过 `ExprExtensions.To()` 方法嵌入已有的 `Expr` 对象，例如 `u => u.IsActive && extra.To<bool>()`，其中 `extra` 是外部构建的 `LogicExpr`。

详细用法见 [Lambda 与 Expr 组合使用](./09-lambda-expr-mixing.md)。

### 6.3 Lambda 性能是否有额外开销？

Lambda 查询在**解析阶段**会将表达式树转换为 `Expr` 对象，这个转换只发生一次（每次查询调用时）。一旦转换为 `Expr`，后续的 SQL 生成流程与手写 `Expr` 完全一致，**没有额外的运行时开销**。

也就是说，Lambda 的开销仅体现在表达式解析阶段，生成的 SQL 和执行路径与等价的手写 `Expr` 相同。在绝大多数业务场景中，解析开销可以忽略不计。

## 7. 相关链接

- [查询总览](./04-query-overview.md)
- [Expr 使用指南](./06-expr-guide.md)
- [ExprString 使用指南](./07-exprstring-guide.md)
- [增删改查](./03-crud-guide.md)
- [关联查询](./08-associations.md)
- [Lambda 与 Expr 组合使用](./09-lambda-expr-mixing.md)
- [CTE 指南](./10-cte-guide.md)
- [表达式扩展](../04-extensibility/01-expression-extension.md)
