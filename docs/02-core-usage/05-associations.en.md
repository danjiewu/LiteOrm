# Associations

This page explains how LiteOrm models relationships with `ForeignType`, `TableJoin`, `ForeignColumn`, and `AutoExpand`.

## 1. Core concepts

- `ForeignType`: property-level relationship metadata, best for a single foreign key column
- `TableJoin`: type-level relationship metadata, best for reusable joins and composite-key relationships
- `ForeignColumn`: fields projected from related tables into a view model
- `AutoExpand`: extends the available relationship path so deeper related fields can be resolved later
- `Expr.ExistsRelated(...)`: uses an existing relationship to build an `EXISTS` filter

## 2. `ForeignType` vs `TableJoin`

Use `ForeignType` for a normal single-column foreign key.

Use `TableJoin` when:

- the target uses a composite primary key
- the join needs to be reused in multiple view models
- you want an explicit alias and stable join path

```csharp
[TableJoin(typeof(OrderItem), "OrderId,LineNo", AliasName = "Item")]
public class Shipment
{
    [Column("OrderId")]
    public long OrderId { get; set; }

    [Column("LineNo")]
    public int LineNo { get; set; }
}
```

## 3. What `AutoExpand` is for

`AutoExpand` is used to extend the relationship graph. It is not a filter by itself.

```csharp
[ForeignType(typeof(User), AutoExpand = true)]
public int SalesUserId { get; set; }
```

This allows the next-level relationship metadata defined on `User` to be reused when resolving deeper fields such as `DepartmentName`.

## 4. What `Expr.ExistsRelated(...)` is for

`Expr.ExistsRelated(...)` uses the relationship graph that already exists and turns it into a query condition.

```csharp
var expr = Expr.ExistsRelated<TestDepartment>(Expr.Prop("Name") == "ER_IT");
var users = await objectViewDAO.Search(expr).ToListAsync();
```

## 5. They are not alternatives

| Feature | Main purpose |
|--------|---------------|
| `AutoExpand` | Make deeper relationship paths available |
| `Expr.ExistsRelated(...)` | Build `EXISTS` / `NOT EXISTS` filters from existing relationships |

## 6. Important note about `AutoExpand`

In complex models, especially when the same table is reachable through multiple paths, `AutoExpand` should be used carefully because it may make later path resolution less obvious.

However, it does **not** force extra joins by itself. Actual joins are still decided by the fields and conditions that your query really uses.

## 7. Unused relationship paths do not become joins

```csharp
[TableJoin(typeof(Department), "DeptId", AliasName = "Dept")]
[TableJoin("Dept", typeof(Region), "RegionId", AliasName = "Region", AutoExpand = true)]
public class User
{
    [Column("DeptId")]
    public int DeptId { get; set; }
}

public class UserListView : User
{
    [ForeignColumn("Dept", Property = "Name")]
    public string? DeptName { get; set; }
}
```

If the query only uses `DeptName`, LiteOrm stops at the `Dept` join and does not automatically add the `Region` join.

## Related Links

- [Back to English docs hub](../SUMMARY.en.md)
- [Query Guide](./03-query-guide.en.md)
- [API Index](../05-reference/02-api-index.en.md)
