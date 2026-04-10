# Associations

This page explains how LiteOrm models relationships with `ForeignType`, `TableJoin`, `ForeignColumn`, and `AutoExpand`.

## 1. Core concepts

- `ForeignType`: property-level relationship metadata, best for a single foreign key column
- `TableJoin`: type-level relationship metadata, best for reusable joins and composite-key relationships
- `ForeignColumn`: fields projected from related tables into a view model
- `AutoExpand`: extends the available relationship path so deeper related fields can be resolved later
- `Expr.ExistsRelated(...)`: uses an existing relationship to build an `EXISTS` filter

## 2. ForeignType

Use `ForeignType` for a normal single-column foreign key.

```csharp
[Table("Orders")]
public class Order
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserId")]
    [ForeignType(typeof(User), Alias = "U", JoinType = TableJoinType.Left, AutoExpand = false)]
    public int UserId { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }
}
```

Explanation: `ForeignType` is used to annotate the foreign key column corresponding to the external entity. When querying a view, `ForeignColumn` in the view class can automatically generate JOINs and read external table columns.

### 2.1 Multiple `ForeignType` declarations on one column

The same column can now declare multiple `ForeignType` entries. This is useful when one key needs to expose multiple readable relationship paths.

```csharp
[Table("Documents")]
public class Document
{
    [Column("OwnerId")]
    [ForeignType(typeof(User), Alias = "Owner")]
    [ForeignType(typeof(Department), Alias = "OwnerDept")]
    public int OwnerId { get; set; }
}

public class DocumentView : Document
{
    [ForeignColumn("Owner", Property = nameof(User.UserName))]
    public string? OwnerName { get; set; }

    [ForeignColumn("OwnerDept", Property = nameof(Department.Name))]
    public string? OwnerDeptName { get; set; }
}
```

Notes:

- Each `ForeignType` still represents a **single-column** relationship, and LiteOrm exposes them uniformly through `SqlColumn.ForeignTables`.
- If the same target type appears more than once, give each path an explicit `Alias` to avoid ambiguity.
- `ForeignColumn` should reference the alias when you need a specific path; type-based lookup is only suitable when there is a single unambiguous target.

## 3. TableJoin

`TableJoin` is suitable for expressing complex association relationships.

If the target table uses a **composite primary key**, you can use `ForeignKeys = "Key1,Key2"` to provide multiple foreign key columns in order; `ForeignType` does not support this multi-column association scenario.

If you have a compatibility-driven mapping that must join by non-primary target fields, you can explicitly override the target join keys with `PrimeKeys = "Code"` or `PrimeKeys = "Key1,Key2"`. This overrides the default "join by target primary key" behavior, but it is **not the recommended style** for normal models.

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

In this model, `Shipment.OrderId + Shipment.LineNo` will associate with the composite primary key of `OrderItem` in order.

## 4. Multi-level relationships and AutoExpand

```csharp
// SalesRecord example: SalesUserId relates to User, and automatically expands User's relationships (such as Department)
[Column("SalesUserId")]
[ForeignType(typeof(User), AutoExpand = true)]
public int SalesUserId { get; set; }

public class SalesRecordView : SalesRecord
{
    [ForeignColumn(typeof(User))]
    public string? UserName { get; set; }

    [ForeignColumn(typeof(Department), Property = nameof(Department.Name))]
    public string? DepartmentName { get; set; }
}

// When querying SalesRecordView, LiteOrm can continue to resolve DepartmentName along the relationship path already defined in User.
```

Note: The core purpose of AutoExpand is to "make the next level of relationship path resolvable". Whether more JOINs are actually generated still depends on whether the query really references fields or conditions on those paths.

### 4.1 AutoExpand switch comparison

| Scenario | `AutoExpand = false` | `AutoExpand = true` |
|----------|----------------------|---------------------|
| Only need first-level foreign table fields | Recommended | Works but usually unnecessary |
| Need to read across second-level relationships | Need to manually declare more joins | Recommended |
| Large tables, complex views, performance-sensitive | Safer | Evaluate carefully |
| Want to reduce view declaration complexity | Normal | More convenient |

### 4.2 Cascade example

`LiteOrm.Demo\Models\User.cs` provides a practical secondary relationship expansion model:

```csharp
[Table("Sales_{0}")]
public class SalesRecord : ObjectBase, IArged
{
    [Column("SalesUserId")]
    [ForeignType(typeof(User), AutoExpand = true)]
    public int SalesUserId { get; set; }
}

public class SalesRecordView : SalesRecord
{
    [ForeignColumn(typeof(User))]
    public string? UserName { get; set; }

    [ForeignColumn(typeof(Department), Property = nameof(Department.Name))]
    public string? DepartmentName { get; set; }
}
```

Key points:

- `SalesRecord` only directly relates to `User`
- But because `User` itself relates to `Department` through `ForeignType/TableJoin`
- `AutoExpand = true` allows `SalesRecordView` to directly read `Department.Name`

Without `AutoExpand` enabled, fields like `DepartmentName` at the secondary level typically require additional join path declarations. This is also the most common and worthwhile use case for `AutoExpand`: **filling in resolvable paths for multi-level relationships**.

### 4.3 Multi-level relationship example

Demonstrates "Department + Parent Department" two-level relationship:

```csharp
[Table("Users")]
[TableJoin("Dept", typeof(Department), nameof(Department.ParentId), AliasName = "Parent")]
public class User
{
    [Column("DeptId")]
    [ForeignType(typeof(Department), Alias = "Dept")]
    public int? DeptId { get; set; }
}

public class UserView : User
{
    [ForeignColumn("Dept", Property = "Name")]
    public string? DeptName { get; set; }

    [ForeignColumn("Parent", Property = "Name")]
    public string? ParentDeptName { get; set; }
}
```

This pattern is suitable for stable multi-level read scenarios like "User → Department → Parent Department".

### 4.4 Query example

Verifying that multi-level relationship fields can be used for filtering:

```csharp
var usersByDept = await viewService.SearchAsync(u => u.DeptName == "Sub Dept");
var usersByParentDept = await viewService.SearchAsync(u => u.ParentDeptName == "Root Dept");

var combinedUsers = await viewService.SearchAsync(
    u => u.DeptName == "Sub Dept" && u.ParentDeptName == "Root Dept"
);
```

### 4.5 Association field sorting and pagination

Association fields can directly participate in sorting and pagination:

```csharp
var expr1 = Expr.From<TestUserView>()
    .Where<TestUserView>(u => u.DeptName != null)
    .OrderBy((nameof(TestUserView.DeptName), true))
    .OrderBy((nameof(TestUser.Age), false))
    .Section(0, 3);

var users1 = await viewService.SearchAsync(expr1);
```

And for deeper parent department fields:

```csharp
var expr2 = Expr.From<TestUserView>()
    .Where<TestUserView>(u => u.ParentDeptName == "Parent Dept")
    .OrderBy(nameof(TestUserView.ParentDeptName))
    .OrderBy(nameof(TestUserView.DeptName))
    .OrderBy(nameof(TestUser.Age))
    .Section(0, 5);

var users2 = await viewService.SearchAsync(expr2);
```

This shows that `ForeignColumn` can not only display data, but also directly participate in:

- Filtering
- Sorting
- Pagination window calculations

---

## 5. ExistsRelated

When you don't want to explicitly expose association fields in the view model but just want to "filter the main table by association table conditions", you can use `ExistsRelated`.

### 5.1 Matching rules

`ExistsRelated` follows these priority rules when constructing relationship paths:

**Association matching order:**
1. **Forward association first**: First try foreign key associations from the main table (e.g., `Order.UserId -> User.Id`)
2. **Reverse association fallback**: If the main table has no forward association to the target type, try reverse inference from the target table (e.g., `User.DeptId -> Department.Id`)

**Multi-path merge logic:**
- If there are multiple association paths from the main table to the target table, they are connected with `OR` conditions
- Any one path matching returns success

```csharp
// Query departments that "have users named ERRev_User1"
var expr = Expr.ExistsRelated<TestUser>(Expr.Prop("Name") == "ERRev_User1");
var results = await objectViewDAO.Search(expr).ToListAsync();
```

Even if `TestDepartment` itself does not have a directly declared `ForeignType` to `TestUser`, the framework can still complete reverse inference through the known association `TestUser.DeptId -> TestDepartment.Id`.

### 5.2 Combination filtering

```csharp
// 1. Forward: filter users by associated department
var expr = Expr.ExistsRelated<TestDepartment>(Expr.Prop("Name") == "ER_IT");
var users = await objectViewDAO.Search(expr).ToListAsync();

// 2. Negation: exclude users belonging to the target department
var notInIT = await objectViewDAO.Search(
    !Expr.ExistsRelated<TestDepartment>(Expr.Prop("Name") == "ERNot_IT")
).ToListAsync();

// 3. Combine with regular field conditions
var matureItUsers = await objectViewDAO.Search(
    Expr.ExistsRelated<TestDepartment>(Expr.Prop("Name") == "ERCombo_IT")
    & (Expr.Prop("Age") >= 30)
).ToListAsync();
```

Usage recommendations:

- Only filtering, no need to project association fields to results: prefer `ExistsRelated`
- Both filtering and displaying `DeptName / ParentDeptName`: prefer `ForeignColumn` view

---

## API Key Points

- ForeignTypeAttribute: ObjectType, Alias, JoinType, AutoExpand
- TableJoinAttribute: Source, TargetType, ForeignKeys, AliasName, JoinType, AutoExpand
- ForeignColumnAttribute: Foreign (Type or AliasName), Property (column to retrieve)

In implementation, LiteOrm merges ForeignType and TableJoin information during the metadata phase to generate JoinedTable / ForeignTable structures. When building SQL, TableJoinExpr is inserted into FromExpr.Joins.

---

## Best Practices

- Regular single-column foreign keys: prefer ForeignType + ForeignColumn, clear semantics, low maintenance cost.
- Composite keys, joint primary keys, or joins that need to be reused: use TableJoin to pre-define at the type level, avoiding repeated declarations across multiple views.
- AutoExpand: only enable for stable, clear, and predictable cascade paths. If there are multiple relationships to the same target table, prefer explicit modeling and use cautiously.
- Alias: use Alias/AliasName to avoid column name conflicts, only declare necessary foreign columns in views to reduce network transmission.
- Performance verification: review generated SQL for complex views before production and use database execution plans (EXPLAIN) to check indexes and join order.

---

## FAQ

- Q: Can ForeignColumn's Foreign be a TableJoin's Alias?
  A: Yes. ForeignColumn's Foreign parameter can be either an external type (Type) or an AliasName defined in TableJoin.

- Q: Does AutoExpand expand infinitely?
  A: AutoExpand expands level by level according to defined associations, but the actual expansion depth depends on registered TableJoin/ForeignType configurations. Control carefully to avoid circular or explosive expansion.

- Q: How to choose between ForeignType and TableJoin?
  A: Prefer ForeignType for single-column foreign keys; prefer TableJoin for joint primary keys, multi-column associations, relationships that need reuse, or when you need explicit named aliases.

---

## Next Steps

- [Back to English docs hub](../README.md)
- [Query Guide](./03-query-guide.en.md)
- [API Index](../05-reference/02-api-index.en.md)
