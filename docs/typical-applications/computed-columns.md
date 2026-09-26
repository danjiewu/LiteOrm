# 计算列的实际应用

计算列（`ColumnMode.Computed`）不生成物理列、不参与插入/更新，查询时 `SELECT` 与条件引用都按表达式返回结果。价值在于把派生值收敛到一处定义，不用多落冗余列，也不用在 C# 侧重复同一条计算。

下面以订单表为例。

```csharp
[Table("SalesOrders")]
public class SaleOrder
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("Quantity")]
    public int Quantity { get; set; }

    [Column("UnitPrice")]
    public decimal UnitPrice { get; set; }

    [Column("DeptId", AllowNull = true)]
    public int? DeptId { get; set; }                 // null 表示全局共享数据

    // 场景 1：派生展示列：小计不落库，读出来的就是表达式结果
    [Column("LineTotal", Expression = "{Quantity} * {UnitPrice}", ColumnMode = ColumnMode.Computed)]
    public decimal LineTotal { get; set; }

    // 场景 3：可见性归一化：把 null（全局共享）归一为 0
    [Column("VisibleDept", Expression = "COALESCE({DeptId}, 0)", ColumnMode = ColumnMode.Computed)]
    public int VisibleDept { get; set; }
}
```

## 场景 1：派生展示列

小计落成物理列，插入/更新都得手动同步，改算法要改两处。声明为计算列后 `SELECT` 直接返回表达式结果：

```csharp
var row = await viewService.GetObjectAsync(id, ...);   // row.LineTotal 已经是表达式算出的值
```

## 场景 2：同一表达式用于过滤与排序

派生值也能直接进 `WHERE`，`LineTotal` 在条件里展开为 `({Quantity} * {UnitPrice})`，不用把表达式再写一遍：

```csharp
var bigOrders = await viewService.SearchAsync(x => x.LineTotal >= 10000, ...);  // 大单筛选
var top       = await viewService.SearchAsync(x => x.UnitPrice > 0, orderBy: o => o.LineTotal, ...);  // 按金额排序
```

条件里展开的是表达式，这类过滤走不了普通列索引。量大的筛选应落成物理列并建索引。

## 场景 3：可见性归一化，让范围过滤少一层特判

`DeptId = null` 表示全局共享数据。范围过滤要同时匹配"本部门 + 全局"，每个分支都得写 `DeptId == 部门 || DeptId == null`；用 `COALESCE({DeptId}, 0)` 归一成 `0` 后，一个条件就能覆盖全局：

```csharp
var visible = Prop(nameof(SaleOrder.VisibleDept)) == user.DeptId
           | Prop(nameof(SaleOrder.VisibleDept)) == 0;
```

这条 `Expr` 可以直接传给查询，也能塞进 `ConstFilter` 全局生效，见[数据权限](./data-permission.md)的方式二。哨兵值要避开真实的部门编号。

## 场景 4：只读计算属性（不属于计算列）

实体上的只读计算属性也能进 `Lambda` 条件，但它走的是 `LambdaExprConverter.RegisterMemberHandler` 注册成员处理器，不进列结构、不出现在 `SELECT` 里，与 `[Column(Computed)]` 是两条路。注册步骤见[表达式扩展 · 计算属性](../extensibility/expression-extension.md)。

## 场景 5：计算列层层引用

派生值经常是链式的：小计、减免、应付。占位符可以指向同一实体上的其他计算列：

```csharp
[Column("DiscountRate")]
public decimal DiscountRate { get; set; }

[Column("DiscountAmount", Expression = "{LineTotal} * {DiscountRate}", ColumnMode = ColumnMode.Computed)]
public decimal DiscountAmount { get; set; }

[Column("Payable", Expression = "{LineTotal} - {DiscountAmount}", ColumnMode = ColumnMode.Computed)]
public decimal Payable { get; set; }
```

引用计算列时会展开它自己的表达式，每层各带一对括号：

```sql
LineTotal      => ("T0"."Quantity" * "T0"."UnitPrice")
DiscountAmount => (("T0"."Quantity" * "T0"."UnitPrice") * "T0"."DiscountRate")
Payable        => (("T0"."Quantity" * "T0"."UnitPrice") - (("T0"."Quantity" * "T0"."UnitPrice") * "T0"."DiscountRate"))
```

中间列名不进 SQL，`Payable` 在 `SELECT`、`WHERE`、`ORDER BY` 里都是上面这串完整表达式。两点留意：

- 表达式原地内联，`Payable` 把 `LineTotal` 展开两遍，`Quantity * UnitPrice` 出现 4 次；层数再深，重复次数按 2 的幂涨。三层左右够用，再深应换成冗余列。
- 不做环形引用检测，自引用或互相引用会一直递归到栈溢出。

## 场景 6：计算列引用关联列

表达式也能引用关联表的列，关联列本身是计算列时一起展开。

```csharp
[Table("Customers")]
public class Customer
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Name", AllowNull = true)]
    public string? Name { get; set; }

    [Column("Code", AllowNull = true)]
    public string? Code { get; set; }

    // 关联表上的计算列
    [Column("Label", Expression = "{Name} || '-' || {Code}", ColumnMode = ColumnMode.Computed)]
    public string? Label { get; set; }
}
```

`SaleOrder` 上补三样：外键列、把关联列挂成本表属性的 `[ForeignColumn]`、引用它的计算列。

```csharp
[Column("OrderNo", AllowNull = true)]
public string? OrderNo { get; set; }

[Column("CustomerId")]
[ForeignType(typeof(Customer), Alias = "Customer")]
public int CustomerId { get; set; }

[ForeignColumn("Customer", Property = nameof(Customer.Label))]
public string? CustomerLabel { get; set; }

[Column("FullLabel", Expression = "{CustomerLabel} || '-' || {OrderNo}", ColumnMode = ColumnMode.Computed)]
public string? FullLabel { get; set; }
```

`FullLabel` 渲染为：

```sql
(("Customer"."Name" || '-' || "Customer"."Code") || '-' || "T0"."OrderNo")
```

关联列按自己的表别名限定，本表列用当前查询的主表别名（单表查询是 `T0`）。容易踩的点：

- 占位符按属性名查找，忽略大小写，`{CustomerLabel}` 命中的是本表的那个关联列属性。
- 关联声明要齐全：`[ForeignType]`（或 `[TableJoin]`）建 JOIN，`[ForeignColumn]` 把外部列挂到本表属性，缺一个名字就不存在。
- 占位符写错不报错，会原样输出限定列名（`"T0"."CustomerLable"`），数据库执行时才提示列不存在。
- 左联接未命中时整条链取不到值，需要兜底就在表达式里套一层 `COALESCE`。

## 场景 7：Expr 树里的函数与条件

字符串形式直接写方言 SQL，分档映射用 Expr 树更顺手：

```csharp
// 实体上： [Column("Grade", ColumnMode = ColumnMode.Computed)] public int Grade { get; set; }
var table = TableInfoProvider.Instance.GetTableDefinition(typeof(SaleOrder))!;
table.Columns.First(c => c.Name == "Grade").ExpressionExpr = Expr.Case(
    Expr.Prop("LineTotal") >= Expr.Const(10000), Expr.Const(1),
    Expr.Prop("LineTotal") >= Expr.Const(1000), Expr.Const(2),
    Expr.Const(3));
```

条件和结果成对交替写，末尾多出来的参数落成 `ELSE`，渲染为扁平的多分支 CASE：

```sql
(CASE WHEN ("T0"."Quantity" * "T0"."UnitPrice") >= 10000 THEN 1 WHEN ("T0"."Quantity" * "T0"."UnitPrice") >= 1000 THEN 2 ELSE 3 END)
```

只有两档时 `Expr.If(条件, 成立值, 否则值)` 更短。元组形式 `Expr.Case((c1, r1), (c2, r2))` 不带 `ELSE`，元组数组要带 `ELSE` 需显式写明元素类型 `new (LogicExpr, ValueTypeExpr)[] { ... }`。`Expr.Func`、`Concat` 等组合同样可用。

整条表达式不能生成参数，所以常量只认 `bool`、整数与浮点、常规字符串，`decimal`、`DateTime` 与含特殊字符的字符串会被参数化并抛 `NotSupportedException`。小数直接写在字符串形式里更省事（`"{LineTotal} * 0.9"`）。

## 何时不适合

- **要高频过滤/关联且靠索引**：计算列在 `WHERE` 展开为表达式，复用不了普通列索引，命中量大时应落成物理列并建索引。
- **跨方言的字符串/函数逻辑**：表达式可以写方言原始 SQL，迁移数据库时要跟着改。
- **动态拼接**：表达式不能生成参数，带运行时值的拼接会抛 `NotSupportedException`。

## 相关链接

- [返回目录](../README.md)
- [实体映射 · 计算列定义](../core-usage/entity-mapping.md)
- [数据权限](./data-permission.md)
- [多租户隔离](./tenant-isolation.md)