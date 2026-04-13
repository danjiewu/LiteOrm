# 查询指南

LiteOrm 支持三种查询方式：Lambda 表达式、Expr 对象、ExprString 插值字符串。本文档重点说明每种方式适合什么场景、怎样组合使用，以及如何避免常见误区。

## 1. 三种查询方式对比

| 方式 | 语法 | 适用场景 | 类型安全 |
|------|------|----------|----------|
| Lambda 表达式 | `u => u.Age > 18` | 简单条件，编译时验证 | ✅ 强类型 |
| Expr 对象 | `Expr.Prop("Age") > 18` | 复杂条件、动态拼接 | ✅ 编译时 |
| ExprString | `$"WHERE {expr}"` | 自定义 SQL 片段 | ❌ 运行时 |

### 1.1 如何选型

- 优先用 Lambda：适合绝大多数日常查询，代码最直观。
- 条件需要动态拼装时用 Expr：适合后台筛选、多条件组合、查询构造器。
- 只在必须插入自定义 SQL 片段时使用 ExprString：例如复杂 Select、数据库特定语法、临时拼装片段。

### 1.2 常见场景建议

| 场景 | 推荐方式 | 原因 |
|------|----------|------|
| 列表页简单筛选 | Lambda | 可读性最好，改动成本低 |
| 管理后台多条件组合 | Expr | 可按参数动态累加条件 |
| 报表或特殊 SQL 片段 | ExprString | 允许局部手写 SQL |
| 需要和扩展函数配合 | Lambda + Expr | 既保持类型安全，也保留扩展性 |

> 💡 如果你希望在保持 Lambda 可读性的同时复用动态条件，请继续阅读：[Lambda 与 Expr 组合使用](./06-lambda-expr-mixing.md)

## 2. Lambda 表达式查询

Lambda 查询是最简洁的方式，编译时进行类型检查。

### 2.1 基础查询

```csharp
// 等于
var users = await userService.SearchAsync(u => u.UserName == "admin");

// 不等于
var users = await userService.SearchAsync(u => u.UserName != "admin");

// 大于/小于
var users = await userService.SearchAsync(u => u.Age >= 18);

// 模糊查询
var users = await userService.SearchAsync(u => u.UserName.Contains("admin"));

// IN 查询
var users = await userService.SearchAsync(u => new[] { 1, 2, 3 }.Contains(u.Id));
```

### 2.2 排序

```csharp
// 升序
var users = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18).OrderBy(u => u.CreateTime)
);

// 降序
var users = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18).OrderByDescending(u => u.CreateTime)
);

// 多字段排序
var users = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderBy(u => u.DeptId)
          .ThenByDescending(u => u.CreateTime)
);
```

### 2.3 分页

```csharp
// Skip/Take 分页
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderByDescending(u => u.CreateTime)
          .Skip(10).Take(20)
);
```

### 2.4 EXISTS 子查询

```csharp
// 检查关联数据存在性
var users = await userService.SearchAsync(
    u => Expr.Exists<Department>(d => d.Id == u.DeptId)
);

// 带条件的 EXISTS
var users = await userService.SearchAsync(
    u => Expr.Exists<Department>(d => d.Id == u.DeptId && d.Name == "研发中心")
);
```

### 2.5 来自测试的 EXISTS 示例

下面的写法提炼自 `LiteOrm.Tests\ServiceTests.cs` 和 `LiteOrm.Tests\LambdaQueryTests.cs`：

```csharp
// 查询存在关联部门的用户
var users = await viewService.SearchAsync(
    Expr.Lambda<TestUser>(u => Expr.Exists<TestDepartment>(d => d.Id == u.DeptId))
);

// 查询属于指定部门的用户
var usersWithSpecificDept = await viewService.SearchAsync(
    Expr.Lambda<TestUser>(u => Expr.Exists<TestDepartment>(d => d.Id == u.DeptId && d.Name == "ExistsTestDept"))
);
```

这种写法适合“当前表字段和子查询表字段存在明确对应关系”的场景，优点是条件表达非常直接。

### 2.6 ExistsRelated 自动关联示例

`ExistsRelated` 与 `Exists` 的区别在于：它会根据 `[ForeignType]` 或关联元数据自动推断关联条件。

下面的例子整理自 `LiteOrm.Demo\Demos\ExistsRelatedDemo.cs`：

```csharp
// 查询属于“研发中心”的用户
var expr = Expr.ExistsRelated<DepartmentView>(Expr.Prop("Name") == "研发中心");
var users = await userService.SearchAsync(expr);

// 查询不属于任何“研”字开头部门的用户
var notResearch = await userService.SearchAsync(
    Expr.ExistsRelated<DepartmentView>(Expr.Prop("Name").StartsWith("研")).Not()
);

// 与普通字段条件组合
var marketAdults = await userService.SearchAsync(
    Expr.ExistsRelated<DepartmentView>(Expr.Prop("Name") == "市场部")
    & (Expr.Prop("Age") > 25)
);
```

如果关联路径已经在模型层定义好，`ExistsRelated` 通常比手写 `Exists` 更省心。

### 2.7 表达式解析机制

**常量处理**：

表达式中的基础值类型常量（如 `int`）会直接拼接进 SQL，不会被参数化。`string` 类型不论常量还是变量，都会自动被参数化。

```csharp
var users = await userService.SearchAsync(u => u.Age >= 18);
// 生成 SQL: SELECT * FROM Users WHERE Age >= 18

var users = await userService.SearchAsync(u => u.UserName == "admin");
// 生成 SQL: SELECT * FROM Users WHERE UserName = @0（参数化）
```

**变量捕获与参数化**：

在 Lambda 外部定义的变量在 Lambda 中引用时，会被参数化传入 SQL：

```csharp
var age = 18;  // 在 Lambda 外定义
var users = await userService.SearchAsync(u => u.Age > age);
// 生成 SQL: SELECT * FROM Users WHERE Age > @0（参数化）
```

`DateTime` 没有常量表达式，在 Lambda 中直接写 `DateTime.Now` 会被解析为 `NOW()` SQL 函数，而非参数化。如果希望 DateTime 值被参数化，需要先定义变量：

```csharp
var users = await userService.SearchAsync(u => u.CreateTime > DateTime.Now);
// 生成 SQL: SELECT * FROM Users WHERE CreateTime > NOW()

var now = DateTime.Now;  // 在 Lambda 外定义
var users = await userService.SearchAsync(u => u.CreateTime > now);
// 生成 SQL: SELECT * FROM Users WHERE CreateTime > @0（参数化）
```

**Expr 与 Lambda 组合**：

Lambda 中 `Expr` 类型的值可以与 Lambda 表达式组合拼接，但需要通过 `To<T>()` 扩展方法满足 Lambda 的类型检查：

```csharp
var condition = u => u.Age >= 18 && Expr.Prop("UserName").Contains("John").To<bool>();
var users = await userService.SearchAsync(condition);
// 生成 SQL: SELECT * FROM Users WHERE Age >= 18 AND UserName LIKE @0（参数化）
```

> 小技巧：当你已经用 `Expr.Prop(...)` 动态拼好了一个条件，但外层又想继续用 Lambda 组合时，可以把它写成 `expr.To<bool>()`。  
> 这样既能复用 `Expr` 的动态构造能力，又不会卡在 Lambda 的类型检查上。

详细内容请参阅：[表达式扩展 - 默认注册的 Lambda 方法](../04-extensibility/01-expression-extension.md#默认注册的-lambda-方法)

## 3. Expr 对象查询

Expr 查询提供更灵活的动态查询能力，适合复杂条件拼接。

### 3.1 创建属性表达式

```csharp
// 基本属性
var prop = Expr.Prop("Age");

// 带别名的属性（用于多表查询）
var prop = Expr.Prop("U", "UserName");
```

### 3.2 比较运算

```csharp
// 使用运算符重载
var expr = Expr.Prop("Age") > 18;
var expr = Expr.Prop("DeptId") == 2;
var expr = Expr.Prop("UserName") != "admin";

// 字符串比较
var expr = Expr.Prop("UserName").Contains("admin");
var expr = Expr.Prop("UserName").StartsWith("a");
var expr = Expr.Prop("UserName").EndsWith("z");
var expr = Expr.Prop("UserName").Like("%admin%");
```

### 3.3 集合运算

```csharp
// IN 查询
var expr = Expr.Prop("Id").In(1, 2, 3, 4, 5);

// IN 子查询
var subQuery = Expr.From<Department>()
    .Where(Expr.Prop("Name") == "IT")
    .Select("Id");
var expr = Expr.Prop("DeptId").In(subQuery);

// BETWEEN
var expr = Expr.Prop("Age").Between(18, 30);
```

### 3.4 子查询示例

下面是几个更贴近实际业务的子查询模式：

```csharp
// 1. IN 子查询：查询属于 IT 部门的用户
var deptSubQuery = Expr.From<Department>()
    .Where(Expr.Prop("Name") == "IT")
    .Select("Id");

var users = await userService.SearchAsync(
    Expr.Prop("DeptId").In(deptSubQuery)
);

// 2. EXISTS 子查询：查询至少有一个关联部门记录的用户
var usersWithDept = await userService.SearchAsync(
    Expr.Exists<Department>(Expr.Prop("Id") == Expr.Prop("T0", "DeptId"))
);
```

这些写法在 `LiteOrm.Tests\PracticalQueryTests.cs` 和 `ServiceTests.cs` 中都有对应验证。

### 3.5 逻辑运算

```csharp
// AND 运算
var expr = Expr.Prop("Age") >= 18 & Expr.Prop("DeptId") == 2;

// OR 运算
var expr = Expr.Prop("DeptId") == 2 | Expr.Prop("DeptId") == 3;

// NOT 运算
var expr = !Expr.Prop("UserName").StartsWith("Temp");

// 组合示例
var expr = (Expr.Prop("Age") >= 18) & (Expr.Prop("DeptId") == 2 | Expr.Prop("DeptId") == 3);
```

### 3.6 动态条件累加

`&` 运算符对 null 安全，适合动态拼接条件：

```csharp
LogicExpr condition = null;

if (minAge.HasValue)
    condition &= Expr.Prop("Age") >= minAge.Value;

if (deptId.HasValue)
    condition &= Expr.Prop("DeptId") == deptId.Value;

if (!string.IsNullOrEmpty(name))
    condition &= Expr.Prop("UserName").Contains(name);

var users = await dao.Search(condition).ToListAsync();
```

### 3.7 从 QueryString 或 Dictionary 动态构造

很多管理后台、开放查询接口或网关转发场景，最终拿到的并不是强类型 DTO，而是 `QueryString`、`Dictionary` 这类键值对。

```csharp
public static LogicExpr BuildUserSearch(IReadOnlyDictionary<string, string?> query)
{
    LogicExpr condition = null;

    if (query.TryGetValue("minAge", out var minAgeText) && int.TryParse(minAgeText, out var minAge))
        condition &= Expr.Prop("Age") >= minAge;

    if (query.TryGetValue("maxAge", out var maxAgeText) && int.TryParse(maxAgeText, out var maxAge))
        condition &= Expr.Prop("Age") <= maxAge;

    if (query.TryGetValue("deptId", out var deptIdText) && int.TryParse(deptIdText, out var deptId))
        condition &= Expr.Prop("DeptId") == deptId;

    if (query.TryGetValue("keyword", out var keyword) && !string.IsNullOrWhiteSpace(keyword))
    {
        condition &= Expr.Prop("UserName").Contains(keyword)
                  |  Expr.Prop("DeptName").Contains(keyword);
    }

    if (query.TryGetValue("withDept", out var withDept) && withDept == "true")
        condition &= Expr.Prop("DeptId").IsNotNull();

    return condition;
}
```

```csharp
var filters = new Dictionary<string, string?>
{
    ["minAge"] = "18",
    ["keyword"] = "demo",
    ["withDept"] = "true"
};

var condition = BuildUserSearch(filters);
var users = await userViewDAO.Search(condition).ToListAsync();
```

如果你在 ASP.NET Core 中直接读取 `Request.Query`，通常只需要先把 `StringValues` 转成普通字符串，再复用同一套构造函数。

### 3.8 链式查询构建

使用 `Expr.From<T>()` 起点链式构建完整查询：

```csharp
var query = Expr.From<User>()
    .Where(Expr.Prop("Age") > 18)
    .GroupBy(Expr.Prop("DeptId"))
    .Having(Expr.Prop("Id").Count() > 5)
    .Select(
        Expr.Prop("DeptId"),
        Expr.Prop("Id").Count().As("UserCount")
    )
    .OrderBy(Expr.Prop("UserCount").Desc())
    .Section(0, 20);

var result = await userService.SearchAsync(query);
```

### 3.9 聚合函数

```csharp
// Count
var expr = Expr.Prop("Id").Count();

// Sum/Avg/Max/Min
var expr = Expr.Prop("Amount").Sum();
var expr = Expr.Prop("Amount").Avg();
var expr = Expr.Prop("Amount").Max();
var expr = Expr.Prop("Amount").Min();

// 去重聚合
var expr = Expr.Prop("DeptId").Count(isDistinct: true);
```

### 3.10 Expr 静态方法

`Expr` 类提供以下静态方法用于构建表达式：

| 方法 | 说明 | 示例 |
|------|------|------|
| `Expr.Prop(name)` | 创建属性表达式 | `Expr.Prop("Age")` |
| `Expr.Prop(alias, name)` | 创建带别名的属性表达式 | `Expr.Prop("U", "UserName")` |
| `Expr.Value(value)` | 创建参数化变量表达式 | `Expr.Value(18)` |
| `Expr.Const(value)` | 创建常量表达式（直接内嵌 SQL） | `Expr.Const("test")` |
| `Expr.Null` | SQL NULL 值 | `Expr.Null` |
| `Expr.From<T>()` | 创建 FROM 查询起点 | `Expr.From<User>()` |
| `Expr.Update<T>()` | 创建 UPDATE 表达式 | `Expr.Update<User>()` |
| `Expr.Delete<T>()` | 创建 DELETE 表达式 | `Expr.Delete<User>()` |
| `Expr.Exists<T>(innerExpr)` | 创建 EXISTS 子查询 | `Expr.Exists<Department>(Expr.Prop("Id") == Expr.Prop("T0", "DeptId"))` |
| `Expr.ExistsRelated<T>(innerExpr)` | 自动关联 EXISTS 查询 | `Expr.ExistsRelated<DepartmentView>(...)` |
| `Expr.Lambda<T>(expr)` | 从 Lambda 表达式创建 LogicExpr | `Expr.Lambda<User>(u => u.Age > 18)` |
| `Expr.Func(name, args)` | 创建函数调用表达式 | `Expr.Func("COUNT", Expr.Prop("Id"))` |
| `Expr.If(condition, then, else)` | 条件表达式 CASE WHEN | `Expr.If(Expr.Prop("Age") > 18, Expr.Value("成年"), Expr.Value("未成年"))` |
| `Expr.Now()` | 当前时间戳 | `Expr.Now()` |
| `Expr.Today()` | 当前日期 | `Expr.Today()` |
| `Expr.Sql(key, arg)` | 动态 SQL 片段 | `Expr.Sql("@0", value)` |

### 3.11 ExprExtensions 扩展方法

`ExprExtensions` 为 `ValueTypeExpr` 和 `LogicExpr` 提供链式扩展方法：

**逻辑表达式组合**：

| 方法 | 说明 | 示例 |
|------|------|------|
| `.And(right)` | AND 连接 | `Expr.Prop("Age") > 18 .And(Expr.Prop("DeptId") == 2)` |
| `.Or(right)` | OR 连接 | `condition1.Or(condition2)` |
| `.Not()` | 取反 | `Expr.Prop("UserName").StartsWith("Temp").Not()` |

**比较运算**：

| 方法 | 说明 | 示例 |
|------|------|------|
| `.Equal(right)` | 等于 | `Expr.Prop("DeptId").Equal(2)` |
| `.GreaterThan(right)` | 大于 | `Expr.Prop("Age").GreaterThan(18)` |
| `.LessThan(right)` | 小于 | `Expr.Prop("Age").LessThan(65)` |

**集合运算**：

| 方法 | 说明 | 示例 |
|------|------|------|
| `.In(items)` | IN 集合 | `Expr.Prop("Id").In(1, 2, 3)` |
| `.Between(low, high)` | BETWEEN 范围 | `Expr.Prop("Age").Between(18, 65)` |

**字符串匹配**：

| 方法 | 说明 | 示例 |
|------|------|------|
| `.Like(pattern)` | LIKE 模式匹配 | `Expr.Prop("Name").Like("J%")` |
| `.Contains(text)` | 包含 | `Expr.Prop("UserName").Contains("admin")` |
| `.StartsWith(text)` | 前缀 | `Expr.Prop("UserName").StartsWith("admin")` |

**NULL 检查**：

| 方法 | 说明 | 示例 |
|------|------|------|
| `.IsNull()` | IS NULL | `Expr.Prop("DeletedAt").IsNull()` |
| `.IsNotNull()` | IS NOT NULL | `Expr.Prop("DeptId").IsNotNull()` |
| `.IfNull(defaultValue)` | 空值替换 | `Expr.Prop("NickName").IfNull(Expr.Prop("UserName"))` |

**聚合函数**：

| 方法 | 说明 | 示例 |
|------|------|------|
| `.Count()` | COUNT 聚合 | `Expr.Prop("Id").Count()` |
| `.Sum()` | SUM 聚合 | `Expr.Prop("Salary").Sum()` |
| `.Avg()` | AVG 聚合 | `Expr.Prop("Score").Avg()` |
| `.Max()` | MAX 聚合 | `Expr.Prop("Price").Max()` |
| `.Min()` | MIN 聚合 | `Expr.Prop("Price").Min()` |

**窗口函数**：

| 方法 | 说明 | 示例 |
|------|------|------|
| `.Over(partitionBy)` | OVER PARTITION BY | `Expr.Prop("Salary").Sum().Over(Expr.Prop("DeptId"))` |

**SQL 构建（链式）**：

| 方法 | 说明 | 示例 |
|------|------|------|
| `.Where(condition)` | WHERE 子句 | `fromExpr.Where(Expr.Prop("Age") > 18)` |
| `.GroupBy(props)` | GROUP BY 子句 | `fromExpr.GroupBy("DeptId")` |
| `.Having(condition)` | HAVING 子句 | `groupExpr.Having(Expr.Prop("Count").Count() > 5)` |
| `.Select(props)` | SELECT 子句 | `fromExpr.Select("Id", "UserName")` |
| `.OrderBy(props)` | ORDER BY 子句 | `fromExpr.OrderBy("CreateTime".Desc())` |
| `.Section(skip, take)` | 分页子句 | `fromExpr.Section(0, 20)` |
| `.Set(assignments)` | UPDATE SET 子句 | `updateExpr.Set(("Age", Expr.Value(18)))` |

## 4. ExprString 插值字符串

ExprString 允许在字符串中直接嵌入 Expr 对象，适合自定义 DAO 场景。

### 4.1 基本用法

```csharp
var expr = Expr.Prop("Age") > 18;
var result = dao.Search($"WHERE {expr}").ToListAsync();
```

### 4.2 参数化安全

```csharp
int minAge = 18;
var expr = Expr.Prop("Age") > 25;

// 自动参数化，防止 SQL 注入
var result = dao.Search($"WHERE {expr} AND Age > {minAge}").ToListAsync();
```

### 4.3 DataViewDAO 用法

```csharp
var dataTable = await dataViewDAO.Search(
    $"SELECT Id, UserName FROM Users WHERE {Expr.Prop("Age")} > {minAge}"
).GetResultAsync();
```

### 4.4 使用边界与推荐写法

- ExprString 更适合“局部 SQL 自定义”，不要把整条复杂业务 SQL 都塞进插值字符串。
- 能用 `Expr.Prop(...)`、`Expr.Value(...)` 表达的条件，优先不要手写列名和值。
- 如果某段 SQL 会反复复用，优先抽到 DAO 或扩展方法，而不是在业务代码里到处复制。

```csharp
// 推荐：只在必要片段上使用 ExprString
var condition = Expr.Prop("Age") >= 18;
var result = await userViewDAO.Search(
    $"WHERE {condition} ORDER BY CreateTime DESC"
).ToListAsync();
```

## 5. Service vs DAO 查询

### 5.1 Service 查询

Service 层支持 Lambda 和 Expr 两种查询方式：

```csharp
// Lambda 表达式查询
var users = await userService.SearchAsync(u => u.Age >= 18);

// Expr 对象查询
var users = await userService.SearchAsync(Expr.Prop("Age") >= 18);
```

### 5.2 DAO 查询

DAO 层同时支持 Lambda、Expr、ExprString 三种查询方式：

```csharp
// Lambda 表达式查询
var users = await userViewDAO.Search(u => u.Age >= 18).ToListAsync();

// Expr 对象查询
var users = await userViewDAO.Search(Expr.Prop("Age") >= 18).ToListAsync();

// ExprString 插值字符串查询
var users = await userViewDAO.Search($"WHERE {Expr.Prop("Age")} > {minAge}").ToListAsync();
```

## 6. 查询结果处理

### 6.1 ObjectViewDAO 返回类型

`EnumerableResult<T>` 支持多种结果获取方式：

```csharp
var result = userViewDAO.Search(Expr.Prop("Age") >= 18);

// 同步方式
var list = result.ToList();
var first = result.FirstOrDefault();

// 异步方式
var listAsync = await result.ToListAsync();
var firstAsync = await result.FirstOrDefaultAsync();

// 枚举方式
await foreach (var user in result)
{
    Console.WriteLine(user.UserName);
}
```

### 6.2 DataViewDAO 返回类型

`DataTableResult` 返回 DataTable：

```csharp
var result = dataViewDAO.Search(Expr.Prop("Age") >= 18);
var dataTable = result.GetResult();

// 异步
var dataTableAsync = await result.GetResultAsync();
```

## 7. 高级查询模式

### 7.1 多表关联查询

有关多表关联查询的详细说明、示例与最佳实践，请参阅： [关联查询](./05-associations.md)。

简要：通过 ForeignType/TableJoin + ForeignColumn 在类型层面定义关联，LiteOrm 会自动生成 JOIN。生成复杂视图前建议审查最终 SQL 与执行计划以确保性能。

### 7.2 SelectAs 选择部分字段

```csharp
// 只查询部分字段
var result = await userService.SearchAs<UserView>(
    Expr.From<UserView>()
        .Where(Expr.Prop("Age") > 18)
        .Select("Id", "UserName", "DeptName")
);
```

### 7.3 列表页组合示例

```csharp
var now = DateTime.Now;
var query = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18 && u.CreateTime <= now)
          .OrderByDescending(u => u.CreateTime)
          .ThenBy(u => u.Id)
          .Skip(0)
          .Take(20)
);

var total = await userService.CountAsync(u => u.Age >= 18);
var hasRecentUsers = await userService.ExistsAsync(u => u.CreateTime > now.AddDays(-7));
```

### 7.4 来自 Demo 的综合查询示例

下面这个例子整理自 `LiteOrm.Demo\Demos\PracticalQueryDemo.cs`，适合列表页筛选：

```csharp
var minAge = 18;
var searchName = "王";

var results = await userService.SearchAsync(
    q => q.Where(u => u.Age >= minAge && u.UserName.Contains(searchName))
          .OrderByDescending(u => u.Id)
          .Skip(0)
          .Take(10)
);
```

如果你需要把查询条件跨服务传递，也可以使用 `Expr` 模型：

```csharp
var expr = Expr.From<UserView>()
    .Where(Expr.Prop("Age") >= 20)
    .Where(Expr.Prop("UserName").Like("%李%"))
    .OrderBy(("Id", false))
    .Section(0, 5);

var results = await userService.SearchAsync(expr);
```

### 7.5 来自测试的条件能力示例

以下写法提炼自 `LiteOrm.Tests\ServiceTests.cs`，适合作为快速参考：

```csharp
var inList = await viewService.SearchAsync(Expr.Prop("Name").In("Alice", "Bob"));
var betweenList = await viewService.SearchAsync(Expr.Prop("Age").Between(30, 35));
var likeList = await viewService.SearchAsync(Expr.Prop("Name").Like("Cha%"));

var combinedList = await viewService.SearchAsync(
    Expr.Prop("Age").Between(20, 40) & Expr.Prop("Name").Contains("i")
);
```

## 相关链接

- [返回目录](../README.md)
- [关联查询](./05-associations.md)
- [增删改查](./04-crud-guide.md)
- [事务处理](../03-advanced-topics/01-transactions.md)
- [表达式扩展](../04-extensibility/01-expression-extension.md)
