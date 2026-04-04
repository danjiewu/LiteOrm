# Generated SQL Examples

This page shows the typical SQL shapes produced by common LiteOrm patterns for debugging, performance analysis, and database adaptation.

Notes:

- These SQL shapes are for illustration only. Aliases, parameter names, and exact syntax may vary by database dialect.
- Actual SQL may differ based on `SqlBuilder`, database dialect, pagination syntax, and registered extensions.

## 1. Basic Filter

### Lambda Query

```csharp
var users = await userService.SearchAsync(u => u.Age >= 18 && u.Name!.StartsWith("A"));
```

Typical SQL shape:

```sql
SELECT [T0].[Id], [T0].[UserName], [T0].[Age], [T0].[DeptId], [T0].[CreateTime], [T0].[Status]
FROM [Users] [T0]
WHERE [T0].[Age] >= @0 AND [T0].[Name] LIKE @1
```

### Expr Query

```csharp
var expr = (Expr.Prop("Age") >= 18) & Expr.Prop("Name").StartsWith("A");
var users = await userService.SearchAsync(expr);
```

Typical SQL shape:

```sql
SELECT [T0].[Id], [T0].[UserName], [T0].[Age], [T0].[DeptId], [T0].[CreateTime], [T0].[Status]
FROM [Users] [T0]
WHERE [T0].[Age] >= @0 AND [T0].[Name] LIKE @1
```

## 2. Sorting and Pagination

```csharp
var page = await userService.SearchAsync(
    q => q.Where(u => u.Status == 1)
          .OrderByDescending(u => u.CreateTime)
          .Skip(20).Take(10)
);
```

Typical SQL shape:

```sql
SELECT [T0].[Id], [T0].[UserName], [T0].[Age], [T0].[DeptId], [T0].[CreateTime], [T0].[Status]
FROM [Users] [T0]
WHERE [T0].[Status] = @0
ORDER BY [T0].[CreateTime] DESC
OFFSET 20 ROWS FETCH NEXT 10 ROWS ONLY
```

Older databases or custom dialects may use `ROW_NUMBER()` or database-specific pagination syntax.

## 3. EXISTS Subquery

```csharp
var users = await userService.SearchAsync(
    u => Expr.Exists<Order>(o => o.UserId == u.Id && o.Status == 1)
);
```

Typical SQL shape:

```sql
SELECT [T0].[Id], [T0].[UserName], [T0].[Age], [T0].[DeptId], [T0].[CreateTime], [T0].[Status]
FROM [Users] [T0]
WHERE EXISTS (
  SELECT 1 FROM [Orders] [T1] WHERE [T1].[UserId] = [T0].[Id] AND [T1].[Status] = @0
)
```

## 4. ExistsRelated

```csharp
var expr = Expr.ExistsRelated<DepartmentView>(Expr.Prop("Name") == "R&D");
var users = await userService.SearchAsync(expr);
```

Typical SQL shape:

```sql
SELECT [T0].[Id], [T0].[UserName], [T0].[Age], [T0].[DeptId], [T0].[CreateTime], [T0].[Status]
FROM [Users] [T0]
WHERE EXISTS (
  SELECT 1 FROM [Departments] [T1] WHERE [T1].[Id] = [T0].[DeptId] AND [T1].[Name] = @0
)
```

If written as:

```csharp
var expr = Expr.ExistsRelated<DepartmentView>(Expr.Prop("Name").StartsWith("R&D")).Not();
```

The SQL becomes `NOT EXISTS (...)`.

## 5. ForeignColumn Join

```csharp
var users = await viewService.SearchAsync(u => u.DeptName == "IT Department");
```

Typical SQL shape:

```sql
SELECT [T0].[Id], [T0].[UserName], [T0].[Age], [T0].[DeptId], [T0].[CreateTime], [T0].[Status],
  [T1].[Name] AS [DeptName]
FROM [Users] [T0]
LEFT JOIN [Departments] [T1] ON [T1].[Id] = [T0].[DeptId]
WHERE [T1].[Name] = @0
```

## 6. Sharded Table Query

```csharp
var sales = await salesService.SearchAsync(
    s => s.Amount > 100,
    tableArgs: new[] { "202411" }
);
```

Typical SQL shape:

```sql
SELECT [T0].[Id], [T0].[ProductId], [T0].[Amount], [T0].[CreateTime]
FROM [Sales_202411] [T0]
WHERE [T0].[Amount] > @0
```

## 7. Batch Insert

```csharp
await userService.BatchInsertAsync(users);
```

Typical SQL shape:

### Multi-value INSERT

```sql
INSERT INTO [Users] ([UserName], [Age], [CreateTime]) VALUES (@0, @1, @2), (@3, @4, @5), (@6, @7, @8)
```

### BulkProvider

When `IBulkProvider` is registered, batch insert may use native bulk interfaces instead of regular SQL:

- SQL Server `SqlBulkCopy`
- MySQL `MySqlBulkCopy`

## 8. UpdateExpr

```csharp
await userService.UpdateAsync(
    Expr.Update<User>()
        .Set("Age", Expr.Prop("Age") + 1)
        .Where(Expr.Prop("Status") == 1)
);
```

Typical SQL shape:

```sql
UPDATE [Users] SET [Age] = [Age] + 1 WHERE [Status] = @0
```

## 9. Window Functions

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

Typical SQL shape:

```sql
SELECT [T0].[Id], [T0].[ProductId], [T0].[ProductName], [T0].[Amount], [T0].[SaleTime],
  SUM([T0].[Amount]) OVER (PARTITION BY [T0].[ProductId] ORDER BY [T0].[SaleTime] ASC) AS [ProductTotal]
FROM [Sales_202411] [T0]
```

Actual window function SQL depends on your registered function handlers and database dialect.

## 10. How to View Real SQL

- `SessionManager.Current?.SqlStack` provides the SQL executed in the current session.
- `SqlStack` keeps up to 10 recent SQL statements and clears after each `Service` method call.

## Related Links

- [Back to English docs hub](../README.md)
- [API Index](./02-api-index.en.md)
- [Query Guide](../02-core-usage/03-query-guide.en.md)
- [Associations](../02-core-usage/05-associations.en.md)
- [Custom SqlBuilder](../04-extensibility/03-custom-sqlbuilder.en.md)
