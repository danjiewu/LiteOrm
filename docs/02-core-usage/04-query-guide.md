# 查询指南

LiteOrm 支持三种主要查询方式：Lambda、`Expr`、`ExprString`。  
其中 Lambda 最终也会先转换成 `Expr`，再统一生成 SQL。  
本文档聚焦“**如何选型**”和“**常见查询入口**”；如果你要系统学习 `Expr` 的构造方式、静态方法、扩展方法和组合语义，请优先阅读[Expr 使用指南](./03-expr-guide.md)。

## 1. 三种查询方式对比

| 方式 | 语法 | 更适合 | 类型安全 |
|------|------|--------|----------|
| Lambda | `u => u.Age > 18` | 固定条件、业务语义清晰 | ✅ 强 |
| `Expr` | `Expr.Prop("Age") > 18` | 动态拼装、查询构造器、多条件后台筛选 | ✅ 编译期 |
| `ExprString` | `$"WHERE {expr}"` | DAO 中的条件片段或完整 SQL | ❌ 运行时 |

### 1.1 经验性选择

- **优先用 Lambda**：大部分业务查询最直观。
- **条件需要动态累加时用 `Expr`**：例如管理后台筛选、前端构造器、跨层传递过滤条件。
- **只在 DAO 需要手写 SQL 时用 `ExprString`**：它既能补 `Search` 的条件片段，也能传完整 SQL；Service 层不提供这个入口。

## 2. Lambda 查询入口

### 2.1 基础过滤

```csharp
var users = await userService.SearchAsync(u => u.Age >= 18);
var users = await userService.SearchAsync(u => u.UserName.Contains("admin"));
var users = await userService.SearchAsync(u => new[] { 1, 2, 3 }.Contains(u.Id));
```

### 2.2 排序

Lambda 查询中，排序通过 `OrderBy` / `OrderByDescending` / `ThenBy` / `ThenByDescending` 链式调用实现。

**单列排序：**

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

**多列排序（ThenBy）：**

```csharp
// 先按部门升序，同部门内按创建时间降序
var users = await userService.SearchAsync(
    q => q.OrderBy(u => u.DeptId)
          .ThenByDescending(u => u.CreateTime)
);
```

`ThenBy` / `ThenByDescending` 必须在 `OrderBy` / `OrderByDescending` 之后使用，可以级联多个。

**排序与分页组合：**

```csharp
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderByDescending(u => u.CreateTime)
          .Skip(0)
          .Take(20)
);
```

**Lambda 排序的表达式支持：**

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

### 2.3 变量捕获与参数化

```csharp
var keyword = "admin";
var users = await userService.SearchAsync(u => u.UserName.Contains(keyword));
```

Lambda 外定义的变量会被参数化。  
如果是 `DateTime.Now` 这类值，希望参数化时应先赋给变量。

### 2.4 三目运算符会转成 `CASE`

```csharp
var users = await userService.SearchAsync(
    u => (u.Age >= 18 ? "Adult" : "Minor") == "Adult"
);
```

这类 Lambda 会先转成 `Expr.If(...)`，最终生成 SQL `CASE WHEN ... THEN ... ELSE ... END`。

## 3. `Exists` 与 `ExistsRelated`

### 3.1 显式 `Exists`

Lambda 写法：

```csharp
using static LiteOrm.Common.Expr;

var users = await userService.SearchAsync(
    u => Exists<Department>(d => d.Id == u.DeptId && d.Name == "研发中心")
);
```

Expr 写法：

```csharp
using static LiteOrm.Common.Expr;

var expr = Exists<Department>(
    Prop("Id") == Prop("T0", "DeptId") & Prop("Name") == "研发中心"
);
var users = await userService.SearchAsync(expr);
```

适合你想自己控制关联条件的场景。

### 3.2 自动关联 `ExistsRelated`

Lambda 写法：

```csharp
using static LiteOrm.Common.Expr;

var users = await userService.SearchAsync(
    u => ExistsRelated<DepartmentView>(d => d.Name == "研发中心")
);
```

Expr 写法：

```csharp
using static LiteOrm.Common.Expr;

var expr = ExistsRelated<DepartmentView>(Prop("Name") == "研发中心");
var users = await userService.SearchAsync(expr);
```

适合模型里已经声明好关联路径，只想“按关联表条件过滤主表”的场景。  
匹配逻辑、继承链规则和 `ConstFilter` 行为请看[关联查询](./06-associations.md)。

## 4. `Expr` 查询入口

```csharp
using static LiteOrm.Common.Expr;

LogicExpr condition = null;

if (minAge.HasValue)
    condition &= Prop("Age") >= minAge.Value;

if (!string.IsNullOrWhiteSpace(keyword))
    condition &= Prop("UserName").Contains(keyword);

var users = await userService.SearchAsync(condition);
```

`Expr` 最大的价值在于"**先构造，再组合，再复用**"。  
而 Lambda 查询也会先转成 `Expr` 再继续生成 SQL，所以两者并不是两套互相隔离的能力体系。  
有关 `Expr` 的详细说明，请转到：[Expr 使用指南](./03-expr-guide.md)

### 4.1 Expr 中的排序

Expr 查询中，排序通过 `.Asc()` / `.Desc()` 标记方向，配合 `.OrderBy()` 链式调用。

**单列排序：**

```csharp
using static LiteOrm.Common.Expr;

var query = From<User>()
    .Where(Prop("Age") >= 18)
    .OrderBy(Prop("CreateTime").Desc())
    .Section(0, 20);

var users = await userService.SearchAsync(query);
```

**多列排序：**

```csharp
using static LiteOrm.Common.Expr;

var query = From<User>()
    .Where(Prop("Age") >= 18)
    .OrderBy(
        Prop("DeptId").Asc(),
        Prop("CreateTime").Desc()
    )
    .Section(0, 20);

var users = await userService.SearchAsync(query);
```

`OrderBy` 接受多个 `OrderByItemExpr` 参数，按传入顺序生成 `ORDER BY col1 ASC, col2 DESC, ...`。

**排序与聚合组合：**

```csharp
using static LiteOrm.Common.Expr;

var query = From<User>()
    .GroupBy(Prop("DeptId"))
    .Select(
        Prop("DeptId"),
        Prop("Id").Count().As("UserCount")
    )
    .OrderBy(Prop("UserCount").Desc())
    .Section(0, 10);

var users = await userService.SearchAsync(query);
```

聚合查询中，排序字段必须使用 `Select` 中定义的别名（如 `UserCount`）。

**动态排序：**

```csharp
using static LiteOrm.Common.Expr;

// 从请求参数中动态构建排序
var sortField = "CreateTime";    // 来自前端参数
var sortDesc = true;             // 来自前端参数

var orderByItem = sortDesc
    ? Prop(sortField).Desc()
    : Prop(sortField).Asc();

var query = From<User>()
    .Where(Prop("Age") >= 18)
    .OrderBy(orderByItem)
    .Section(0, 20);

var users = await userService.SearchAsync(query);
```

**多条件动态排序：**

```csharp
using static LiteOrm.Common.Expr;

// 解析多个排序字段，例如 "DeptId,CreateTime desc"
var sortFields = new[] { "DeptId", "CreateTime desc" };
var orderByItems = new List<OrderByItemExpr>();

foreach (var field in sortFields)
{
    var parts = field.Trim().Split(' ');
    var prop = parts[0];
    var desc = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
    orderByItems.Add(desc ? Prop(prop).Desc() : Prop(prop).Asc());
}

var query = From<User>()
    .Where(Prop("Age") >= 18)
    .OrderBy(orderByItems.ToArray())
    .Section(0, 20);

var users = await userService.SearchAsync(query);
```

**从 LogicExpr 直接链式排序：**

```csharp
using static LiteOrm.Common.Expr;

var condition = Prop("Age") >= 18;
var query = condition
    .OrderBy(Prop("CreateTime").Desc())
    .Section(0, 20);

var users = await userService.SearchAsync(query);
```

`LogicExpr` 也支持 `.OrderBy(...)` 和 `.Section(...)`，适合条件已构造好、只需追加排序和分页的场景。

## 5. `ExprString` 插值字符串

`ExprString` 允许你在插值字符串中直接嵌入 `Expr` 对象和参数，适合 DAO 层里需要手动构造 `Search` 条件片段或完整 SQL 的场景。Service 层没有对应的公开查询重载。

### 5.1 基本用法

```csharp
using static LiteOrm.Common.Expr;

var condition = Prop("Age") >= 18;
var users = await userViewDAO.Search(
    $"WHERE {condition} ORDER BY CreateTime DESC"
).ToListAsync();
```

### 5.2 参数化与安全性

```csharp
using static LiteOrm.Common.Expr;

int minAge = 18;
var result = await userViewDAO.Search(
    $"WHERE {Prop("Age")} >= {minAge}"
).ToListAsync();
```

插值里的普通值仍会按参数处理；插值里的 `Expr` 对象会先转成对应 SQL 片段再拼进去。  
所以推荐优先插入 `Expr.Prop(...)`、`Expr.Value(...)`、`LogicExpr` 这类结构化对象，而不是在字符串里手写大量列名和值。

### 5.3 使用边界与推荐写法

建议：

- 把它当作 DAO 层的手写 SQL 入口：既可以补 `Search` 条件片段，也可以在需要时配合 `isFull: true` 传完整 SQL。
- 能用 `Expr` 表达的过滤条件，建议先构造 `Expr` 后再插入 `ExprString`，而不是写死在字符串里。
- `ExprString` 按照 `Expr` 插入的顺序进行解析，参数生成的顺序以及上下文逻辑与完整 `Expr` 解析存在差异，例如 `ExprString` 中 `SelectExpr` 早于 `FromExpr` 解析，若 `SelectExpr` 中未指定表别名的列可能不能正确匹配默认表（主查询已通过预先创建默认主表的上下文解决，但子查询会存在问题），使用时需注意。
- 手写标识符时，可以把 `[` 和 `]` 当作通用引用符占位，执行前 LiteOrm 会按当前数据库方言把它们替换成真实的标识符引用符。

```csharp
var result = await dataViewDAO.Search(
    $"SELECT [Id], [UserName] FROM [Users] WHERE [Age] >= {minAge}",
    isFull: true
).GetResultAsync();
```

- `ExprString` 不支持自动展开 `CommonTableExpr`，需要直接写完整 `WITH ... SELECT ...` SQL 或通过 ` SelectExpr` 构造 With 块。

```csharp
var result = await dataViewDAO.Search(
    $"""
    WITH ActiveUsers AS (
        SELECT Id, UserName, Age
        FROM Users
        WHERE Age >= {minAge}
    )
    SELECT Id, UserName, Age
    FROM ActiveUsers
    """,
    isFull: true
).GetResultAsync();
```

### 5.4 ExprString 中的排序

ExprString 中的排序直接写在 SQL 片段的 `ORDER BY` 子句中，支持两种方式：直接写列名或嵌入 `Expr` 排序表达式。

**基本排序：**

```csharp
using static LiteOrm.Common.Expr;

var condition = Prop("Age") >= 18;
var users = await userViewDAO.Search(
    $"WHERE {condition} ORDER BY CreateTime DESC"
).ToListAsync();
```

**多列排序：**

```csharp
var users = await userViewDAO.Search(
    $"WHERE {Prop("Age")} >= {minAge} ORDER BY DeptId ASC, CreateTime DESC"
).ToListAsync();
```

**嵌入 Expr 排序表达式：**

```csharp
using static LiteOrm.Common.Expr;

var orderBy = new OrderByExpr(
    null,
    Prop("DeptId").Asc(),
    Prop("CreateTime").Desc()
);

var users = await userViewDAO.Search(
    $"WHERE {Prop("Age")} >= {minAge} {orderBy}"
).ToListAsync();
```

**完整 SQL 中的排序：**

```csharp
var result = await dataViewDAO.Search(
    $"""
    SELECT [Id], [UserName], [Age]
    FROM [Users]
    WHERE [Age] >= {minAge}
    ORDER BY [Age] DESC, [UserName] ASC
    """,
    isFull: true
).GetResultAsync();
```

## 6. `Search` vs `SearchAs`

`Search` 和 `SearchAs` 都是查询入口，但职责不同：

| 维度 | `Search` / `SearchAsync` | `SearchAs<TResult>` / `SearchAsAsync<TResult>` |
|------|--------------------------|------------------------------------------------|
| 返回类型 | 实体类型 `T`（或视图 `TView`） | **任意** `TResult`：实体、匿名类型、标量、自定义投影类 |
| 查询构造 | `Expr` / Lambda / `ExprString`（仅 DAO） | `SelectExpr`（Service+DAO）/ Lambda 投影 / `ExprString`（仅 DAO） |
| 字段映射 | 按 `TableInfoProvider` 注册的 `SelectColumns` 位置映射 | 见下方注意事项 |
| 典型场景 | 「查这张表的数据」 | 「跨表投影、聚合、计算列、换类型」 |

### 6.1 基本用法对比

```csharp
using static LiteOrm.Common.Expr;

// Search：返回 User 实体列表
List<User> users = await viewService.SearchAsync(Prop("Age") >= 18);

// SearchAs：投影到匿名类型
var summaries = await viewService.SearchAsAsync<dynamic>(
    From<UserView>()
        .Where(Prop("Age") >= 18)
        .Select(Prop("UserName"), Prop("Age"))
);

// SearchAs：投影到自定义类型
var summaries = await viewService.SearchAsAsync<UserSummary>(
    From<UserView>()
        .Where(Prop("Age") >= 18)
        .Select(
            Prop("Id"),
            Prop("UserName"),
            Expr.If(Prop("IsVip") == true, "VIP", "Normal").As("Level")
        )
);
```

### 6.2 `SearchAs` 使用的注意事项

`SearchAs<TResult>` 的结果映射依赖 [DataReaderConverter](file:///d:/Repos/LiteOrm/LiteOrm/Converter/DataReaderConverter.cs#L97-L117)，按 `TResult` 是否注册到 `TableInfoProvider` 走不同路径，**初学者最容易在以下几处翻车**：

#### 6.2.1 `TResult` 的三种映射路径

| `TResult` 类型 | 映射方式 | 是否需要注册 |
|----------------|---------|-------------|
| **标量类型**（`int` / `string` / `DateTime` 等） | 直接读第 0 列 | 否 |
| **匿名类型**（`new { UserName = ..., Age = ... }`） | 按构造函数参数名匹配列名（不区分大小写） | 否 |
| **注册到 `TableInfoProvider` 的类型** | 按 `SelectColumns` 位置映射 | 是 |
| **未注册的普通类型** | 取**第一个公开构造函数**，按其参数名匹配列名（不区分大小写） | 否 |

> 关键差别：注册过的类型走「位置映射」——要求 `Select` 列顺序与 `TableInfoProvider` 注册顺序一致；未注册的普通类型 / 匿名类型走「列名匹配」——要求 `Select` 列的别名与构造函数参数名一致。

#### 6.2.2 列别名必须与目标成员名对应

对于「按名称匹配」的类型（匿名类型、未注册普通类），`Select` 列的别名（`.As("xxx")`）必须与目标类型的成员名（构造函数参数名）一致，否则字段会落到默认值：

```csharp
public class UserSummary
{
    public string UserName { get; set; }
    public string Level { get; set; }

    // 注意：DataReaderConverter 取 GetConstructors()[0]
    // 参数名必须与列别名一致
    public UserSummary(string userName, string level)
    {
        UserName = userName;
        Level = level;
    }
}

// ✅ 列别名 "Level" 与构造函数参数名 level 匹配
await viewService.SearchAsAsync<UserSummary>(
    From<UserView>().Select(
        Prop("UserName"),
        Expr.If(Prop("IsVip") == true, "VIP", "Normal").As("Level")
    )
);

// ❌ 没有用 .As("Level")，列名是 "Expr_If_..." 之类的自动名，Level 会变成 null
```

#### 6.2.3 标量结果用 `SearchAs<T>`，别用 `Search`

`COUNT` / `SUM` / `MAX` 等聚合查询只返回一个标量值，应该用 `SearchAs<int>` / `SearchAs<long>` 直接读第 0 列：

```csharp
// ✅ 标量投影
var count = await viewService.SearchAsAsync<int>(
    From<UserView>().Select(Expr.Func("COUNT", Prop("Id")))
);
```

#### 6.2.4 注册过的 `TResult` 走位置映射，列顺序要对

如果 `TResult` 是注册到 `TableInfoProvider` 的实体类型（比如直接用 `SearchAs<User>`），按 `SelectColumns` 位置映射——`Select` 列的**顺序**必须与注册的列顺序一致，列名不参与匹配。建议**优先用未注册的投影类型**（DTO / 匿名类型）来避免顺序耦合。

#### 6.2.5 `TResult` 必须可实例化

- 需要有公开构造函数
- 匿名类型天然满足（编译器生成）
- DTO / record 需要有公共构造函数
- 接口 / 抽象类 / 没有公开构造函数的类型会失败

#### 6.2.6 DAO 比 Service 多两个重载

| 重载 | Service | DAO |
|------|---------|-----|
| `SearchAs<TResult>(SelectExpr)` | ✅ 返回 `List<TResult>` | ✅ 返回 `EnumerableResult<TResult>` |
| `SearchAs<TResult>(Expression<Func<IQueryable<T>, IQueryable<TResult>>>)` | ❌ | ✅ Lambda 投影 |
| `SearchAs<TResult>(ref ExprString sqlBody)` | ❌ | ✅ 原生 SQL 投影 |

需要 Lambda 投影或原生 SQL 投影时，切到 DAO（见 [7.2 DAO](#72-dao)）。

## 7. Service 与 DAO 查询

### 7.1 Service

```csharp
using static LiteOrm.Common.Expr;

var users1 = await userService.SearchAsync(u => u.Age >= 18);
var users2 = await userService.SearchAsync(Prop("Age") >= 18);
var users3 = await userService.SearchAsAsync<UserSummary>(
    From<UserView>()
        .Where(Prop("Age") >= 18)
        .Select(
            Prop("Id"),
            Prop("UserName"),
            Expr.If(Prop("IsVip") == true, "VIP", "Normal").As("Level")
        )
);
```

- Service 查询入口以 Lambda / `Expr` 为主，同时支持基于 `SelectExpr` 的 `SearchAs(...)` / `SearchAsAsync(...)` 投影查询，适合业务语义清晰、需要事务/AOP/服务封装的场景。
- Service 不提供 `ExprString` 查询重载；如果需求已经变成“手写 SQL”，就应该切到 DAO。

### 7.2 DAO

```csharp
using static LiteOrm.Common.Expr;

var users1 = await userViewDAO.Search(u => u.Age >= 18).ToListAsync();
var users2 = await userViewDAO.Search(Prop("Age") >= 18).ToListAsync();
var users3 = await userViewDAO.Search($"WHERE {Prop("Age")} > {minAge}").ToListAsync();
```

- DAO 除了支持 Lambda / `Expr`，还支持 `ExprString`，因此更适合自定义 SQL 片段、完整 SQL、复杂投影查询和 DataTable 查询。
- 需要 IQueryable 投影版 `SearchAs(...)`、`ExprString` 版 `SearchAs(...)`、`Query(...)`、`Execute(...)`、`GetValue(...)` 这类更底层能力时，也应该直接使用 DAO。

## 8. 相关链接

- [Expr 使用指南](./03-expr-guide.md)
- [增删改查](./05-crud-guide.md)
- [关联查询](./06-associations.md)
- [Lambda 与 Expr 组合使用](./07-lambda-expr-mixing.md)
- [CTE 指南](./08-cte-guide.md)
- [表达式扩展](../04-extensibility/01-expression-extension.md)
