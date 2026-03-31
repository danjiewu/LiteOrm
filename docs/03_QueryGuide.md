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

Service 层使用强类型 Lambda 表达式：

```csharp
var users = await userService.SearchAsync(u => u.Age >= 18);
```

### 5.2 DAO 查询

DAO 层支持 Expr 和 ExprString：

```csharp
// Expr 查询
var users = await userViewDAO.Search(Expr.Prop("Age") >= 18).ToListAsync();

// ExprString 查询
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

LiteOrm 不支持在查询时动态指定 JOIN，而是通过**预定义 View 类型**的方式支持多表连接。

**Step 1：定义包含外键列的 View**：

```csharp
public class UserView : User
{
    [ForeignColumn(typeof(Department), Property = "DeptName")]
    public string? DeptName { get; set; }
}
```

**Step 2：查询时自动生成 JOIN**：

```csharp
var users = await userService.SearchAsync<UserView>();
// 自动生成：SELECT u.*, d.DeptName FROM Users u LEFT JOIN Department d ON u.DeptId = d.Id
```

**多级关联**：如果 Order 通过 User 再关联到 Department：

```csharp
public class OrderView : Order
{
    [ForeignColumn(typeof(User), Property = "UserName")]
    public string? UserName { get; set; }

    [ForeignColumn(typeof(User), Property = "DeptId")]
    public int? DeptId { get; set; }

    [ForeignColumn(typeof(Department), Property = "DeptName")]
    public string? DeptName { get; set; }
}
```

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

- 学习完整操作：[增删改查](./04_CrudGuide.md)
- 事务处理：[EXP_Transaction](./EXP/EXP_Transaction.md)
- 表达式扩展：[EXP_ExpressionExtension](./EXP/EXP_ExpressionExtension.md)
