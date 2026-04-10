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

### 1) 最小可用闭环

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

### 2) ForeignType（属性级）

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
```

- 说明：ForeignType 用于标注外键列对应的外部实体。查询视图时，通过视图类中的 ForeignColumn 可以自动生成 JOIN 并读取外表列。

#### 2.1 一个列上声明多个 `ForeignType`

现在同一个列可以重复声明多个 `ForeignType`。适合“底层还是单列外键，但需要暴露多条可读关联路径”的场景。

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

- 每个 `ForeignType` 仍然只描述一条**单列关联**，运行时统一收敛到 `SqlColumn.ForeignTables`。
- 如果同一个目标类型要出现多次，建议都显式指定 `Alias`，避免路径歧义。
- 视图里的 `ForeignColumn` 需要精确命中某条路径时，优先引用 `Alias`；只有目标唯一时，才适合直接按类型引用。

### 3) TableJoin（类级）

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

- 如确有历史兼容需求，也可以通过 `PrimeKeys = "Code"` 或 `PrimeKeys = "Key1,Key2"` 显式覆盖目标表参与关联的键属性。
  这会覆盖默认“按目标表主键关联”的行为，但**不作为推荐写法**，常规场景仍应优先保持目标表主键定义与关联关系一致。

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

### 4) 多级关联与 AutoExpand

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

### 4.1 AutoExpand 开关对比

| 场景 | `AutoExpand = false` | `AutoExpand = true` |
|------|----------------------|---------------------|
| 只需要一级外表字段 | 推荐 | 也可用，但通常没有必要 |
| 需要跨二级关联读取字段 | 需要手动声明更多连接 | 推荐 |
| 大表、复杂视图、性能敏感 | 更稳妥 | 需谨慎评估 |
| 想减少视图声明复杂度 | 一般 | 更方便 |

### 4.2 级联示例

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

### 4.3 多级关联示例

演示“部门 + 上级部门”两级关联：

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

### 4.4 查询示例

验证多级关联字段是否可用于筛选：

```csharp
var usersByDept = await viewService.SearchAsync(u => u.DeptName == "Sub Dept");
var usersByParentDept = await viewService.SearchAsync(u => u.ParentDeptName == "Root Dept");

var combinedUsers = await viewService.SearchAsync(
    u => u.DeptName == "Sub Dept" && u.ParentDeptName == "Root Dept"
);
```

### 4.5 关联字段排序与分页

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

---

## 5. ExistsRelated

当你不想在视图模型中显式暴露关联字段，而只是想“按关联表条件过滤主表”时，可以使用 `ExistsRelated`

### 5.1 匹配规则

`ExistsRelated` 在构造关联路径时遵循以下优先级规则：

**关联匹配顺序：**
1. **正向关联优先**：首先尝试从主表出发的外键关联（如 `Order.UserId -> User.Id`）
2. **反向关联备选**：若主表上没有到目标类型的正向关联，则尝试从目标表反向推断（如 `User.DeptId -> Department.Id`）

**多路径时的合并逻辑：**
- 如果从主表到目标表存在多条关联路径，它们会以 `OR` 连接作为关联条件，也就是满足任意一个关联条件即匹配成功，请在使用时注意。

```csharp
// 查询"拥有名为 ERRev_User1 的用户"的部门
var expr = Expr.ExistsRelated<TestUser>(Expr.Prop("Name") == "ERRev_User1");
var results = await objectViewDAO.Search(expr).ToListAsync();
```

即使 `TestDepartment` 自身没有直接声明到 `TestUser` 的 `ForeignType`，框架仍可通过 `TestUser.DeptId -> TestDepartment.Id` 的已知关联关系完成反向推断。

### 5.2 组合过滤

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

- [返回目录](../README.md)
- [基础概念](./01-entity-mapping.md)
- [查询指南](./03-query-guide.md)
- [增删改查](./04-crud-guide.md)
- [性能优化](../03-advanced-topics/03-performance.md)
- [API 索引](../05-reference/02-api-index.md)

更多示例请参考代码中的 Demo（LiteOrm.Demo.Models）以及单元测试中的 TableJoin/AutoExpand 相关测试用例。

