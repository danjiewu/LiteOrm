# Generated SQL Examples

This page shows the typical SQL shapes produced by common LiteOrm patterns.

## Basic filter

```csharp
var users = await userService.SearchAsync(u => u.Age >= 18 && u.Name!.StartsWith("A"));
```

Typical SQL shape:

```sql
SELECT T0.*
FROM Users T0
WHERE T0.Age >= @p0
  AND T0.Name LIKE CONCAT(@p1, '%')
```

## Paging

```csharp
var page = await userService.SearchAsync(
    q => q.Where(u => u.Status == 1)
          .OrderByDescending(u => u.CreateTime)
          .Skip(20).Take(10)
);
```

## `EXISTS`

```csharp
u => Expr.Exists<Order>(o => o.UserId == u.Id && o.Status == 1)
```

Typical SQL shape:

```sql
WHERE EXISTS (
    SELECT 1
    FROM Orders T1
    WHERE T1.UserId = T0.Id
      AND T1.Status = @p0
)
```

## `Expr.ExistsRelated(...)`

```csharp
var expr = Expr.ExistsRelated<DepartmentView>(Expr.Prop("Name") == "R&D");
```

Typical SQL shape:

```sql
WHERE EXISTS (
    SELECT 1
    FROM Departments T1
    WHERE T1.Id = T0.DeptId
      AND T1.Name = @p0
)
```

## Related Links

- [Back to English docs hub](../SUMMARY.en.md)
- [API Index](./02-api-index.en.md)
