# 查询指南

LiteOrm 支持三种查询方式：Lambda 表达式、Expr 对象、ExprString 插值字符串。本文档详细介绍每种方式的使用场景和语法。

## 1. 三种查询方式对比

| 方式 | 语法 | 适用场景 | 类型安全 |
|------|------|----------|----------|
| Lambda 表达式 | `u => u.Age > 18` | 简单条件，编译时验证 | ✅ 强类型 |
| Expr 对象 | `Expr.Prop("Age") > 18` | 复杂条件、动态拼接 | ✅ 编译时 |
| ExprString | `$"WHERE {expr}"` | 自定义 SQL 片段 | ❌ 运行时 |

## 2. Lambda 表达式查询

Lambda 查询是最简洁的方式，编译时进行类型检查。

### 2.1 基础查询

```csharp
// 等于
var users = await userService.SearchAsync(u => u.UserName == "admin");

// 不等于
var users = await userService.SearchAsync(u => u.Status != 0);

// 大于/小于
var users = await userService.SearchAsync(u => u.Age >= 18);

// 模糊查询
var users = await userService.SearchAsync(u => u.Email.Contains("@test.com"));

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
    q => q.Where(u => u.Status == 1)
          .OrderBy(u => u.DeptId)
          .ThenByDescending(u => u.CreateTime)
);
```

### 2.3 分页

```csharp
// Skip/Take 分页
var page = await userService.SearchAsync(
    q => q.Where(u => u.Status == 1)
          .OrderByDescending(u => u.CreateTime)
          .Skip(10).Take(20)
);
```

### 2.4 EXISTS 子查询

```csharp
// 检查关联数据存在性
var users = await userService.SearchAsync(
    u => Expr.Exists<Order>(o => o.UserId == u.Id)
);

// 带条件的 EXISTS
var users = await userService.SearchAsync(
    u => Expr.Exists<Order>(o => o.UserId == u.Id && o.Status == 1)
);
```

### 2.5 表达式解析机制

**常量处理**：

表达式中的基础值类型常量（如 `int`）会直接拼接进 SQL，不会被参数化。`string` 类型不论常量还是变量，都会自动被参数化。

```csharp
var users = await userService.SearchAsync(u => u.Age >= 18);
// 生成 SQL: SELECT * FROM Users WHERE Age >= 18

var users = await userService.SearchAsync(u => u.Name == "admin");
// 生成 SQL: SELECT * FROM Users WHERE Name = @p0（参数化）
```

**变量捕获与参数化**：

在 Lambda 外部定义的变量在 Lambda 中引用时，会被参数化传入 SQL：

```csharp
var age = 18;  // 在 Lambda 外定义
var users = await userService.SearchAsync(u => u.Age > age);
// 生成 SQL: SELECT * FROM Users WHERE Age > @p0（参数化）
```

`DateTime` 没有常量表达式，在 Lambda 中直接写 `DateTime.Now` 会被解析为 `NOW()` SQL 函数，而非参数化。如果希望 DateTime 值被参数化，需要先定义变量：

```csharp
var users = await userService.SearchAsync(u => u.CreateTime > DateTime.Now);
// 生成 SQL: SELECT * FROM Users WHERE CreateTime > NOW()

var now = DateTime.Now;  // 在 Lambda 外定义
var users = await userService.SearchAsync(u => u.CreateTime > now);
// 生成 SQL: SELECT * FROM Users WHERE CreateTime > @p0（参数化）
```

**Expr 与 Lambda 组合**：

Lambda 中 `Expr` 类型的值可以与 Lambda 表达式组合拼接，但需要通过 `To<T>()` 方法符合 Lambda 类型检查：

```csharp
var condition = u => u.Age >= 18 && Expr.Prop("Name").Contains("John").To<bool>();
var users = await userService.SearchAsync(condition);
// 生成 SQL: SELECT * FROM Users WHERE Age >= 18 AND Name LIKE @p0（参数化）
```

详细内容请参阅：[表达式扩展 - 默认注册的 Lambda 方法](./EXP/EXP_ExpressionExtension.md#默认注册的-lambda-方法)

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
var expr = Expr.Prop("Status") == 1;
var expr = Expr.Prop("UserName") != "admin";

// 字符串比较
var expr = Expr.Prop("Email").Contains("@test.com");
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

### 3.4 逻辑运算

```csharp
// AND 运算
var expr = Expr.Prop("Age") >= 18 & Expr.Prop("Status") == 1;

// OR 运算
var expr = Expr.Prop("Status") == 0 | Expr.Prop("Status") == 1;

// NOT 运算
var expr = !Expr.Prop("IsDeleted").Equal(true);

// 组合示例
var expr = (Expr.Prop("Age") >= 18) & (Expr.Prop("Status") == 1 | Expr.Prop("Status") == 2);
```

### 3.5 动态条件累加

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

### 3.6 链式查询构建

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

### 3.7 聚合函数

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

### 3.8 Expr 静态方法

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
| `Expr.Exists<T>(innerExpr)` | 创建 EXISTS 子查询 | `Expr.Exists<Order>(Expr.Prop("UserId") == Expr.Prop("Id"))` |
| `Expr.ExistsRelated<T>(innerExpr)` | 自动关联 EXISTS 查询 | `Expr.ExistsRelated<Order>(...)` |
| `Expr.Lambda<T>(expr)` | 从 Lambda 表达式创建 LogicExpr | `Expr.Lambda<User>(u => u.Age > 18)` |
| `Expr.Func(name, args)` | 创建函数调用表达式 | `Expr.Func("COUNT", Expr.Prop("Id"))` |
| `Expr.If(condition, then, else)` | 条件表达式 CASE WHEN | `Expr.If(Expr.Prop("Age") > 18, Expr.Value("成年"), Expr.Value("未成年"))` |
| `Expr.Now()` | 当前时间戳 | `Expr.Now()` |
| `Expr.Today()` | 当前日期 | `Expr.Today()` |
| `Expr.Sql(key, arg)` | 动态 SQL 片段 | `Expr.Sql("@p0", value)` |

### 3.9 ExprExtensions 扩展方法

`ExprExtensions` 为 `ValueTypeExpr` 和 `LogicExpr` 提供链式扩展方法：

**逻辑表达式组合**：

| 方法 | 说明 | 示例 |
|------|------|------|
| `.And(right)` | AND 连接 | `Expr.Prop("Age") > 18 .And(Expr.Prop("Status") == 1)` |
| `.Or(right)` | OR 连接 | `condition1.Or(condition2)` |
| `.Not()` | 取反 | `Expr.Prop("IsDeleted").Equal(true).Not()` |

**比较运算**：

| 方法 | 说明 | 示例 |
|------|------|------|
| `.Equal(right)` | 等于 | `Expr.Prop("Status").Equal(1)` |
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
| `.Contains(text)` | 包含 | `Expr.Prop("Email").Contains("@test.com")` |
| `.StartsWith(text)` | 前缀 | `Expr.Prop("UserName").StartsWith("admin")` |

**NULL 检查**：

| 方法 | 说明 | 示例 |
|------|------|------|
| `.IsNull()` | IS NULL | `Expr.Prop("DeletedAt").IsNull()` |
| `.IsNotNull()` | IS NOT NULL | `Expr.Prop("Email").IsNotNull()` |
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
| `.Select(props)` | SELECT 子句 | `fromExpr.Select("Id", "Name")` |
| `.OrderBy(props)` | ORDER BY 子句 | `fromExpr.OrderBy("CreateTime".Desc())` |
| `.Section(skip, take)` | 分页子句 | `fromExpr.Section(0, 20)` |
| `.Set(assignments)` | UPDATE SET 子句 | `updateExpr.Set(("Status", Expr.Value(1)))` |

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

有关多表关联查询的详细说明、示例与最佳实践，请参阅： [关联查询](./05_Associations.md)。

简要：通过 ForeignType/TableJoin + ForeignColumn 在类型层面定义关联，LiteOrm 会自动生成 JOIN。生成复杂视图前建议审查最终 SQL 与执行计划以确保性能。

### 7.2 SelectAs 选择部分字段

```csharp
// 只查询部分字段
var result = await userService.SearchAs<UserView>(
    Expr.From<User>()
        .Where(Expr.Prop("Age") > 18)
        .Select("Id", "UserName", "Email")
);
```

## 8. 下一步

- 关联查询：[关联查询](./05_Associations.md)
- 学习完整操作：[增删改查](./04_CrudGuide.md)
- 事务处理：[事务处理](./EXP/EXP_Transaction.md)
- 表达式扩展：[表达式扩展](./EXP/EXP_ExpressionExtension.md)
