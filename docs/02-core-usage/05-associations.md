# 关联查询（TableJoin / ForeignType / AutoExpand）

本文档介绍 LiteOrm 的关联查询能力。

- 涵盖 TableJoin（类级）与 ForeignType（属性级）的使用。
- 说明 ForeignColumn 的取值方式、AutoExpand 的作用与常见实践。

## 概念总览

- ForeignType（属性级）：在某个列上声明它引用的外表实体类型（例如外键列）。支持 Alias（别名）、JoinType（连接类型）、AutoExpand（自动扩展）。
  适用于**单列外键**场景。

- TableJoin（类级）：在实体或表上预定义与其它表的连接关系，适合多列联合外键或复用同一连接逻辑。
  支持指定 Source、TargetType、ForeignKeys、AliasName、JoinType、AutoExpand 等。

- ForeignColumn（视图字段）：在视图模型中声明要从外表选择的列。Foreign 参数可以是外部类型或 TableJoin 中定义的 AliasName。

- AutoExpand（自动扩展）：当被标记为 true 时，如果该表作为外表被引用，LiteOrm 会把该表已定义的关联路径继续暴露给后续关联解析使用。
  它的作用是**扩展可用的关联路径**，而不是单独作为过滤手段。

---

## 使用示例

### 0) 最小可用闭环

下面这个例子适合第一次接触 LiteOrm 关联查询时先跑通：

```csharp
[Table("Users")]
public class User
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserName")]
    public string? UserName { get; set; }
}

[Table("Orders")]
public class Order
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserId")]
    [ForeignType(typeof(User))]
    public int UserId { get; set; }
}

public class OrderView : Order
{
    [ForeignColumn(typeof(User), Property = nameof(User.UserName))]
    public string? UserName { get; set; }
}
```

```csharp
var orders = await orderService.SearchAsync<OrderView>();
```

如果 `OrderView.UserName` 能正确取到值，说明最基础的 `ForeignType + ForeignColumn` 关联链已经打通。

### 1) ForeignType（属性级）

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

- 说明：ForeignType 用于标注外键列对应的外部实体。查询视图时，通过视图类中的 ForeignColumn 可以自动生成 JOIN 并读取外表列。

### 2) TableJoin（类级）

```csharp
[TableJoin(typeof(Department), "ParentId", AliasName = "Parent", JoinType = TableJoinType.Left)]
[TableJoin(typeof(Department), "DeptId", AliasName = "Dept", JoinType = TableJoinType.Left)]
public class User { /* ... */ }

public class OrderView : Order
{
    [ForeignColumn(typeof(User), Property = "UserName")]
    public string? UserName { get; set; }

    [ForeignColumn("Dept", Property = "Name")]
    public string? DeptName { get; set; } // 使用 TableJoin 的 Alias 引用
}
```

- 说明：`TableJoin` 适合表达复合关联关系。  
  如果目标表使用**联合主键**，可以通过 `ForeignKeys = "Key1,Key2"` 这种写法，按目标主键顺序提供多个外键列；`ForeignType` 不支持这种多列关联场景。

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

这类模型里，`Shipment.OrderId + Shipment.LineNo` 会按顺序关联到 `OrderItem` 的联合主键。

### 3) 多级关联与 AutoExpand

```csharp
// SalesRecord 示例：SalesUserId 关联 User，并自动展开 User 的关联（如 Department）
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

// 查询 SalesRecordView 时，LiteOrm 可以继续沿着 User 已定义好的关联路径解析 DepartmentName。
```

- 注意：AutoExpand 的核心作用是“让下一层关联路径可被继续解析”。  
  实际是否生成更多 JOIN，仍然取决于查询里是否真的引用了这些路径上的字段或条件。

### 4) AutoExpand 开关对比

| 场景 | `AutoExpand = false` | `AutoExpand = true` |
|------|----------------------|---------------------|
| 只需要一级外表字段 | 推荐 | 也可用，但通常没有必要 |
| 需要跨二级关联读取字段 | 需要手动声明更多连接 | 推荐 |
| 大表、复杂视图、性能敏感 | 更稳妥 | 需谨慎评估 |
| 想减少视图声明复杂度 | 一般 | 更方便 |

### 4.1 来自 Demo 的 AutoExpand 级联示例

`LiteOrm.Demo\Models\SalesRecord.cs` 给出了一个很实用的二级关联展开模型：

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

这里的关键点是：

- `SalesRecord` 只直接关联 `User`
- 但因为 `User` 本身又通过 `ForeignType/TableJoin` 关联了 `Department`
- `AutoExpand = true` 允许 `SalesRecordView` 直接读取 `Department.Name`

如果没有开启 `AutoExpand`，`DepartmentName` 这类二级字段通常需要额外声明连接路径。  
这也是 `AutoExpand` 最常见、也最值得使用的场景：**补足多级关联的可解析路径**。

### 5) 来自 Demo 的多级关联示例

下面这个模型直接整理自 `LiteOrm.Demo\Models\User.cs`，演示“部门 + 上级部门”两级关联：

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

这类写法适合“用户 → 部门 → 上级部门”这种稳定的多级读取场景。

### 6) 来自测试的查询示例

下面的查询提炼自 `LiteOrm.Tests\ServiceTests.cs`，能直接验证多级关联字段是否可用于筛选：

```csharp
var usersByDept = await viewService.SearchAsync(u => u.DeptName == "Sub Dept");
var usersByParentDept = await viewService.SearchAsync(u => u.ParentDeptName == "Root Dept");

var combinedUsers = await viewService.SearchAsync(
    u => u.DeptName == "Sub Dept" && u.ParentDeptName == "Root Dept"
);
```

### 6.1 关联字段排序与分页

`LiteOrm.Tests\ServiceTests.cs` 还验证了关联字段可以直接参与排序与分页：

```csharp
var expr1 = Expr.From<TestUserView>()
    .Where<TestUserView>(u => u.DeptName != null)
    .OrderBy((nameof(TestUserView.DeptName), true))
    .OrderBy((nameof(TestUser.Age), false))
    .Section(0, 3);

var users1 = await viewService.SearchAsync(expr1);
```

以及更深一层的父部门字段：

```csharp
var expr2 = Expr.From<TestUserView>()
    .Where<TestUserView>(u => u.ParentDeptName == "Parent Dept")
    .OrderBy(nameof(TestUserView.ParentDeptName))
    .OrderBy(nameof(TestUserView.DeptName))
    .OrderBy(nameof(TestUser.Age))
    .Section(0, 5);

var users2 = await viewService.SearchAsync(expr2);
```

这说明 `ForeignColumn` 不仅能显示，还能直接参与：

- 过滤
- 排序
- 分页窗口计算

### 6.2 ExistsRelated 组合过滤示例

当你不想在视图模型中显式暴露关联字段，而只是想“按关联表条件过滤主表”时，可以使用 `ExistsRelated`。

下面的用法整理自 `LiteOrm.Demo\Demos\ExistsRelatedDemo.cs` 和 `LiteOrm.Tests\ExprEnhancedTests.cs`：

```csharp
// 1. 正向：按关联部门过滤用户
var expr = Expr.ExistsRelated<TestDepartment>(Expr.Prop("Name") == "ER_IT");
var users = await objectViewDAO.Search(expr).ToListAsync();

// 2. 取反：排除属于目标部门的用户
var notInIT = await objectViewDAO.Search(
    !Expr.ExistsRelated<TestDepartment>(Expr.Prop("Name") == "ERNot_IT")
).ToListAsync();

// 3. 组合普通字段条件
var matureItUsers = await objectViewDAO.Search(
    Expr.ExistsRelated<TestDepartment>(Expr.Prop("Name") == "ERCombo_IT")
    & (Expr.Prop("Age") >= 30)
).ToListAsync();
```

适用建议：

- 只做过滤，不需要把关联字段投影到结果里：优先考虑 `ExistsRelated`
- 既要过滤又要展示 `DeptName / ParentDeptName`：优先考虑 `ForeignColumn` 视图

### 6.3 ExistsRelated 的反向路径

`ExistsRelated` 不只支持“主表上显式声明了外键”的正向路径，也支持从对端元数据反推：

```csharp
// 查询“拥有名为 ERRev_User1 的用户”的部门
var expr = Expr.ExistsRelated<TestUser>(Expr.Prop("Name") == "ERRev_User1");
var results = await objectViewDAO.Search(expr).ToListAsync();
```

这个例子来自 `LiteOrm.Tests\ExprEnhancedTests.cs`。  
即使 `TestDepartment` 自身没有直接声明到 `TestUser` 的 `ForeignType`，框架仍可通过 `TestUser.DeptId -> TestDepartment.Id` 的已知关联关系完成反向推断。

### 6.4 AutoExpand 和 ExistsRelated 不是同类能力

这两个概念很容易被放在一起比较，但它们解决的问题并不一样：

| 能力 | 主要用途 | 常见搭配 |
|------|----------|----------|
| `AutoExpand` | 扩展可用的关联路径，让更深层的 `ForeignColumn` / 关联字段可以继续被解析 | `ForeignColumn` |
| `ExistsRelated` | 利用已经存在的关联关系，构造 `EXISTS` / `NOT EXISTS` 过滤条件 | `Expr` / `SearchAsync` |

更直接地说：

- `AutoExpand` 用于**建立和延展可用的关联关系**
- `ExistsRelated` 用于**使用这些关联关系构造查询条件**

它们并不是二选一关系，很多场景下甚至会同时出现。

---

#### 错误示例（说明性对比）

##### 示例 A：未开启 AutoExpand 时，不能直接引用二级关联表的属性

```csharp
// 假设：User 类型通过 TableJoin 关联到 Department（User -> Department）
public class SalesRecord
{
    [Column("SalesUserId")]
    [ForeignType(typeof(User))]  // AutoExpand 未设置（默认 false）
    public int SalesUserId { get; set; }
}

public class SalesRecordView : SalesRecord
{
    [ForeignColumn(typeof(Department), Property = "Name")]
    public string? DepartmentName { get; set; } // 期望通过 SalesRecord -> User -> Department 引入 Department.Name
}
```

说明：由于 SalesRecord 上的 ForeignType 未启用 AutoExpand，LiteOrm 不会将 User 的 TableJoin 级联到 Department。
因此，视图中直接引用 Department 的字段不会自动生成对应的 JOIN。
要么在 SalesRecord 或查询层显式声明 TableJoin，
要么在中间的 ForeignType 上设置 AutoExpand = true。

##### 示例 B：AutoExpand 会忽略已显式引用的别名以避免重复 JOIN

```csharp
[TableJoin(typeof(Department), "DeptId", AliasName = "Dept", JoinType = TableJoinType.Left)]
public class User { /* ... */ }

public class SalesRecord
{
    [Column("SalesUserId")]
    [ForeignType(typeof(User), Alias = "U", AutoExpand = true)]
    public int SalesUserId { get; set; }
}

public class SalesRecordView : SalesRecord
{
    [ForeignColumn(typeof(User), Property = "UserName")]
    public string? UserName { get; set; }

    [ForeignColumn("Dept", Property = "Name")]
    public string? DepartmentName { get; set; }
}
```

说明：SalesRecord 引用了 User 且 User 的 TableJoin 已包含 Alias "Dept"。
当 AutoExpand = true 时，LiteOrm 会自动把 User 的 Dept 引入查询。
如果视图已经通过别名 "Dept" 显式引用 Department 字段，框架会避免重复生成第二次 Dept 的 JOIN，以免产生冗余连接。

##### 示例 C：未被任何属性直接或间接引用的关联表不会被构造为 JOIN（避免额外开销）

```csharp
[Table("Users")]
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

    // 注意：这里没有声明 RegionName
}

var users = await userViewService.SearchAsync<UserListView>();
```

说明：虽然 `Dept -> Region` 的关联路径已经存在，甚至开启了 `AutoExpand = true`，但本次查询只读取 `DeptName`，没有任何属性或条件引用 `Region`。  
因此，LiteOrm 只会生成到 `Dept` 为止的必要 JOIN，不会把 `Region` 无条件拼进 SQL。

总结：

- 当需要从二级或更深层级的关联中读取列时，若希望继续沿既有关系解析，请在中间 ForeignType 或 TableJoin 上设置 AutoExpand = true；
- AutoExpand 会智能避免重复别名的重复 JOIN；
- 框架只会为视图中声明的列构造必要的 JOIN，未引用的关联不会被加入。

---

- 注意：AutoExpand 在复杂关联关系里要慎用，尤其是“同一张表有多条关联路径”时，可能让后续解析命中并非你原本预期的那条关系。  
但它本身不会强制增加 JOIN 数量；是否生成 JOIN，仍取决于查询实际引用了哪些路径。

---

## API 要点

- ForeignTypeAttribute: ObjectType、Alias、JoinType、AutoExpand
- TableJoinAttribute: Source、TargetType、ForeignKeys、AliasName、JoinType、AutoExpand
- ForeignColumnAttribute: Foreign（Type 或 AliasName）、Property（要获取的列）

实现上，LiteOrm 会在元数据阶段合并 ForeignType 与 TableJoin 的信息，生成 JoinedTable / ForeignTable 结构。
最终在构建 SQL 时，会把 TableJoinExpr 插入 FromExpr.Joins 中。

---

## 最佳实践

- 常规单列外键：优先使用 ForeignType + ForeignColumn，语义清晰、维护成本低。
- 复合键、联合主键或需要复用的连接：使用 TableJoin 在类型上预定义，避免在多个视图重复声明。
- AutoExpand：仅对稳定、明确、可预期的级联路径开启。若同一目标表存在多条关系，请优先显式建模并谨慎使用。
- Alias：使用 Alias/AliasName 避免列名冲突，视图中只声明必要的外表列以减少网络传输。
- 性能验证：对复杂视图在生产前审查生成的 SQL，并用数据库执行计划（EXPLAIN）检查索引与连接顺序。

---

## 常见问题

- Q：ForeignColumn 的 Foreign 可以是 TableJoin 的 Alias 吗？
  A：可以。ForeignColumn 的 Foreign 参数既可为外部类型（Type），也可为 TableJoin 中的 AliasName。

- Q：AutoExpand 是否会展开无限层级？
  A：AutoExpand 按定义的关联逐级扩展，但实际扩展深度取决于已注册的 TableJoin/ForeignType 配置，需谨慎控制以避免循环或爆炸式扩展。

- Q：ForeignType 和 TableJoin 怎么选？
  A：单列外键优先选 ForeignType；只要涉及联合主键、多列关联、同一关系需要复用或显式命名别名，优先选 TableJoin。

---

## 下一步

- [返回目录](../SUMMARY.md)
- 基础概念：[基础概念](./01-entity-mapping.md)
- 查询指南：[查询指南](./03-query-guide.md)
- 增删改查：[增删改查](./04-crud-guide.md)
- 性能优化：[性能优化](../03-advanced-topics/03-performance.md)
- 接口索引：[API 索引](../05-reference/02-api-index.md)

更多示例请参考代码中的 Demo（LiteOrm.Demo.Models）以及单元测试中的 TableJoin/AutoExpand 相关测试用例。

