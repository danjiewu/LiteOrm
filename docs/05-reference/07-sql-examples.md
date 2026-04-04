# 生成 SQL 示例

本文整理 LiteOrm 常见能力最终会生成的 SQL 形态，帮助你在调试、性能分析和数据库适配时快速建立直觉。

注意：

- 以下 SQL 以"结构示意"为主，不保证别名、参数名和不同数据库方言下完全逐字一致。
- 实际 SQL 可能因 `SqlBuilder`、数据库方言、分页写法和注册扩展而有所不同。

## 1. 基础条件查询

### Lambda 查询

```csharp
var users = await userService.SearchAsync(u => u.Age >= 18 && u.Name!.StartsWith("A"));
```

典型 SQL 形态：

```sql
SELECT [T0].[Id], [T0].[UserName], [T0].[Age], [T0].[DeptId], [T0].[CreateTime], [T0].[Status]
FROM [Users] [T0]
WHERE [T0].[Age] >= @0 AND [T0].[Name] LIKE @1
```

### Expr 查询

```csharp
var expr = (Expr.Prop("Age") >= 18) & Expr.Prop("Name").StartsWith("A");
var users = await userService.SearchAsync(expr);
```

典型 SQL 形态：

```sql
SELECT [T0].[Id], [T0].[UserName], [T0].[Age], [T0].[DeptId], [T0].[CreateTime], [T0].[Status]
FROM [Users] [T0]
WHERE [T0].[Age] >= @0 AND [T0].[Name] LIKE @1
```

## 2. 排序与分页

```csharp
var page = await userService.SearchAsync(
    q => q.Where(u => u.Status == 1)
          .OrderByDescending(u => u.CreateTime)
          .Skip(20).Take(10)
);
```

典型 SQL 形态：

```sql
SELECT [T0].[Id], [T0].[UserName], [T0].[Age], [T0].[DeptId], [T0].[CreateTime], [T0].[Status]
FROM [Users] [T0]
WHERE [T0].[Status] = @0
ORDER BY [T0].[CreateTime] DESC
OFFSET 20 ROWS FETCH NEXT 10 ROWS ONLY
```

旧数据库或自定义方言下，分页部分可能改写为 `ROW_NUMBER()` 包裹查询或其他数据库专属写法。

## 3. EXISTS 子查询

```csharp
var users = await userService.SearchAsync(
    u => Expr.Exists<Order>(o => o.UserId == u.Id && o.Status == 1)
);
```

典型 SQL 形态：

```sql
SELECT [T0].[Id], [T0].[UserName], [T0].[Age], [T0].[DeptId], [T0].[CreateTime], [T0].[Status]
FROM [Users] [T0]
WHERE EXISTS (
  SELECT 1 FROM [Orders] [T1] WHERE [T1].[UserId] = [T0].[Id] AND [T1].[Status] = @0
)
```

## 4. ExistsRelated 自动关联过滤

```csharp
var expr = Expr.ExistsRelated<DepartmentView>(Expr.Prop("Name") == "研发中心");
var users = await userService.SearchAsync(expr);
```

典型 SQL 形态：

```sql
SELECT [T0].[Id], [T0].[UserName], [T0].[Age], [T0].[DeptId], [T0].[CreateTime], [T0].[Status]
FROM [Users] [T0]
WHERE EXISTS (
  SELECT 1 FROM [Departments] [T1] WHERE [T1].[Id] = [T0].[DeptId] AND [T1].[Name] = @0
)
```

如果写成：

```csharp
var expr = Expr.ExistsRelated<DepartmentView>(Expr.Prop("Name").StartsWith("研")).Not();
```

则典型 SQL 会变成 `NOT EXISTS (...)`。

## 5. ForeignColumn 关联查询

```csharp
var users = await viewService.SearchAsync(u => u.DeptName == "IT Department");
```

典型 SQL 形态：

```sql
SELECT [T0].[Id], [T0].[UserName], [T0].[Age], [T0].[DeptId], [T0].[CreateTime], [T0].[Status],
  [T1].[Name] AS [DeptName]
FROM [Users] [T0]
LEFT JOIN [Departments] [T1] ON [T1].[Id] = [T0].[DeptId]
WHERE [T1].[Name] = @0
```

如果视图里继续引用 `ParentDeptName`，通常会继续追加一级 `JOIN Departments T2 ...`。

## 6. 分表查询

```csharp
var sales = await salesService.SearchAsync(
    s => s.Amount > 100,
    tableArgs: new[] { "202411" }
);
```

典型 SQL 形态：

```sql
SELECT [T0].[Id], [T0].[ProductId], [T0].[Amount], [T0].[CreateTime]
FROM [Sales_202411] [T0]
WHERE [T0].[Amount] > @0
```

如果实体实现了 `IArged`，插入时表名后缀也会按对象上的 `TableArgs` 自动路由。

## 7. 批量写入

```csharp
await userService.BatchInsertAsync(users);
```

典型 SQL 形态通常有两类：

### 多值 INSERT

```sql
INSERT INTO [Users] ([UserName], [Age], [CreateTime]) VALUES (@0, @1, @2), (@3, @4, @5), (@6, @7, @8)
```

### BulkProvider 原生批量写入

当项目注册了 `IBulkProvider` 时，批量写入可能不会表现为上面的普通 SQL，而是通过数据库驱动原生批量接口完成，例如：

- SQL Server 的 `SqlBulkCopy`
- MySQL 的 `MySqlBulkCopy`

这类场景更接近"驱动级批量导入"，而不是 ORM 逐条拼接 SQL。

## 8. UpdateExpr 条件更新

```csharp
await userService.UpdateAsync(
    Expr.Update<User>()
        .Set("Age", Expr.Prop("Age") + 1)
        .Where(Expr.Prop("Status") == 1)
);
```

典型 SQL 形态：

```sql
UPDATE [Users] SET [Age] = [Age] + 1 WHERE [Status] = @0
```

## 9. 窗口函数

```csharp
var amountSum = Func("SUM", Expr.Prop("Amount"))
    .Over([Expr.Prop("ProductId")], [Expr.Prop("SaleTime").Asc()]);

var selectExpr = Expr.From<SalesRecord>("202411")
    .Select("Id", "ProductId", "ProductName", "Amount", "SaleTime")
    .SelectMore(new SelectItemExpr(amountSum, "ProductTotal"));

var results = await salesDAO
    .SearchAs<SalesWindowView>(selectExpr)
    .ToListAsync();
```

典型 SQL 形态：

```sql
SELECT [T0].[Id], [T0].[ProductId], [T0].[ProductName], [T0].[Amount], [T0].[SaleTime],
  SUM([T0].[Amount]) OVER (PARTITION BY [T0].[ProductId] ORDER BY [T0].[SaleTime] ASC) AS [ProductTotal]
FROM [Sales_202411] [T0]
```

窗口函数的最终 SQL 取决于你注册的函数处理器和当前数据库方言。

## 10. 如何查看真实 SQL

- 从 `SessionManager.Current?.SqlStack` 可获取当前会话执行的 SQL。
- `SqlStack`最多保留最后10条SQL，每次调用 `Service` 方法后`SqlStack` 会清空。

## 相关链接

- [返回目录](../README.md)
- [示例索引](./06-example-index.md)
- [查询指南](../02-core-usage/03-query-guide.md)
- [关联查询](../02-core-usage/05-associations.md)
- [自定义 SqlBuilder / 方言扩展](../04-extensibility/03-custom-sqlbuilder.md)
