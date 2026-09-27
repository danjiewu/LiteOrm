# 计算列的实际应用

计算列（`ColumnMode.Computed`）不生成物理列、不参与插入/更新，查询时 `SELECT` 与条件引用都按表达式返回结果。价值在于把派生值收敛到一处定义，不用多落冗余列，也不用在 C# 侧重复同一条计算。

下面用三个实际需求走一遍：按用户等级算折扣、商品上架状态位、跨表展示名。每段都附上真实生成的 SQL（SQLite 方言），并说明这套写法的边界。

## 需求一：按当前登录用户等级算折扣

运营规则是「小计 → 按会员等级打折 → 应付」。折扣率随登录用户变化，但它不能作为参数传下去：会员等级属于运行时上下文，而落库的订单又必须记住当时算出的应付金额。

费率表放在代码里，按等级取值。等级不是 SQL 参数，而是拼进表达式的一段字面量，整个构件用 `GenericSqlExpr` 封装：

```csharp
using LiteOrm.Common;
using System.Globalization;

GenericSqlExpr.Register("UserLevelDiscount", (context, _) =>
{
    decimal rate = CurrentUserContext.CurrentLevel switch
    {
        UserLevel.Silver => 0.02m,
        UserLevel.Gold => 0.05m,
        UserLevel.Diamond => 0.08m,
        _ => 0m
    };

    string amount = Expr.Prop(context.DefaultTableAliasName, nameof(Order.Amount)).ToSql(context);
    return $"{amount} * {rate.ToString(CultureInfo.InvariantCulture)}";
}, true);
```

实体上只声明列位，表达式在启动时挂上去：

```csharp
[Table("Orders")]
public class Order
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }

    [Column("DiscountAmount", ColumnMode = ColumnMode.Computed)]
    public decimal DiscountAmount { get; set; }

    [Column("Payable", Expression = "{Amount} - {DiscountAmount}", ColumnMode = ColumnMode.Computed)]
    public decimal Payable { get; set; }
}
```

```csharp
var table = TableInfoProvider.Instance.GetTableDefinition(typeof(Order))!;
table.Columns.First(c => c.Name == "DiscountAmount").ExpressionExpr =
    Expr.Sql("UserLevelDiscount").AsValue();
```

`GenericSqlExpr` 继承自 `LogicExpr` 而不是 `ValueTypeExpr`，赋值给 `ExpressionExpr` 要经过 `AsValue()`，它会把该表达式包成一个值表达式。

`DiscountAmount` 与 `Payable` 都落成计算列之后，建表只留下物理列：

```sql
CREATE TABLE "Orders" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "Amount" DECIMAL(18,2) NOT NULL
)
```

同一个查询在不同等级下渲染出不同的表达式，`Payable` 里的 `{DiscountAmount}` 会把它整段展开：

```sql
-- Silver (2%)
("T0"."Amount" * 0.02)
("T0"."Amount" - ("T0"."Amount" * 0.02))

-- Gold (5%)
("T0"."Amount" * 0.05)
("T0"."Amount" - ("T0"."Amount" * 0.05))
```

执行 `Amount = 1000` 的订单，Gold 下读出 `DiscountAmount=50`、`Payable=950`，与数据库直接求值一致。

### 折扣率为什么不能参数化

计算列有一条硬约束：**表达式必须完全不产生参数**，渲染时若 `OutputParams` 有新增，直接抛 `NotSupportedException`：

```
ColumnDefinition.ExpressionExpr for column 'DiscountAmount' produced 1 parameter(s);
only fixed SQL expressions (property references, constants, functions, arithmetic) are allowed for computed columns.
```

原因是计算列会出现在 `SELECT`、`WHERE`、`ORDER BY`、`JOIN ON` 任一位置，参数列表由构建方在外部统一管理，表达式自己往里追加参数会打乱占位符编号。

所以折扣率只能以内联字面量写进 SQL。等级一变生成的 SQL 文本就变，引用这个计算列的语句不取用命令缓存，每次重新拼接，这正是这套写法的适用边界。

还带来两个必须自己处理的点：

- **片段不能为空**。`GenericSqlExpr` 回调返回 `null` 时渲染出 `()`，SQL 直接语法错误。无折扣的等级应当返回字面量 `0`（渲染为 `(0)`）而不是 `null`。
- **字符串常量要自己加引号**。`context.SqlBuilder.TryAppendSqlLiteral` 可以帮忙转义，但它遇到反斜杠或控制字符会返回 `false`，此时只能退回数字化或固定的安全写法。

### 等级变化与命令缓存

`DiscountAmount` 的 SQL 随等级变化，而 `SELECT` 字段列表里引用计算列的语句（`GetObject`）不取用命令缓存，每次按当前等级重新拼接，等级一变立即生效，也不会把上一等级的 SQL 串用过来。代价是这类语句每次查询多一次 SQL 拼接：等级这种低频维度下可以接受，如果折扣规则改成「每个订单一个费率」，就该把费率落成物理列，让计算列只做 `{Amount} * {DiscountRate}`。

### 用在哪里

`DiscountAmount` 与 `Payable` 都是普通计算列，`SELECT`、`WHERE`、`ORDER BY` 里都能直接引用：

```csharp
var bigOrders = await viewService.SearchAsync(
    o => o.Payable >= 1000, cancellationToken: ct);          // 按应付金额筛选

var top = await viewDao.Search(
        Expr.Prop(nameof(Order.Payable)).Desc())
    .Section(1, 20).ToListAsync(ct);                          // 按应付金额取前 20
```

```sql
WHERE ("T0"."Amount" - ("T0"."Amount" * 0.05)) >= @0
```

两点留意：

- 表达式是原地内联的。`{DiscountAmount}` 展开进 `Payable` 后，`Amount` 在一条 SQL 里出现两次；层数再深会继续翻倍，三层左右够用。
- 不做环形引用检测。`A` 引用 `B`、`B` 又引用 `A` 会一直递归到栈溢出，得靠人保证。

## 需求二：商品上架状态位

「能不能卖」是三个条件的组合：已上架、有库存、没被下架。这个判断在列表页、搜索页、导出接口里都要用，散在各处迟早会漂移。

```csharp
[Table("Products")]
public class Product
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Stock")]
    public int Stock { get; set; }

    [Column("IsOnline")]
    public bool IsOnline { get; set; }

    [Column("OnSale", Expression = "CASE WHEN {IsOnline} = 1 AND {Stock} > 0 THEN 1 ELSE 0 END",
        ColumnMode = ColumnMode.Computed)]
    public bool OnSale { get; set; }
}
```

把它归一成一个 `bool` 状态位之后，调用方需要的判断只剩一句：

```csharp
var onSale = await viewService.SearchAsync(p => p.OnSale, cancellationToken: ct);
```

```sql
WHERE (CASE WHEN "T0"."IsOnline" = 1 AND "T0"."Stock" > 0 THEN 1 ELSE 0 END) = 1
```

`OnSale` 属性类型是 `int` 还是 `bool` 只影响 C# 侧的回填，表达式怎么写、SQL 长什么样都由表达式本身决定。写成 `bool` 更贴合语义，`p => p.OnSale` 可以直接进条件。

表达式里的常量必须内联，所以 `{IsOnline} = 1` 的 `1` 要直接写字面量，不能参数化。同一个判断用 Expr 树写条件时可以直接用 `bool` 常量，渲染结果一致：

```csharp
table.Columns.First(c => c.Name == "OnSale").ExpressionExpr =
    Expr.If(Expr.Prop("IsOnline") == Expr.Const(true) & Expr.Prop("Stock") > Expr.Const(0),
            Expr.Const(true), Expr.Const(false));
```

需要注意：表达式不能生成参数，拼接一个运行时的开关进去（比如「当前是否强制下架」）会抛 `NotSupportedException`。这类逻辑只能放在 `WHERE` 里用 `Expr.Value(...)` 参数化，不能放进计算列。

## 需求三：跨表拼展示名

列表页要显示「华东-Acme-C001 / SO-20260927-01」这种一眼能认的组合名。客户简称和订单号分属两张表，为了列表把冗余字段落到订单表上又得跟着上游改。

先给客户表定义展示名（`Region`、`Name`、`Code` 三段拼接，本身也是计算列）：

```csharp
[Table("Customers")]
public class Customer
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Region", AllowNull = true)]
    public string? Region { get; set; }

    [Column("Name", AllowNull = true)]
    public string? Name { get; set; }

    [Column("Code", AllowNull = true)]
    public string? Code { get; set; }

    [Column("Label", Expression = "{Region} || '-' || {Name} || '-' || {Code}", ColumnMode = ColumnMode.Computed)]
    public string? Label { get; set; }
}
```

订单视图上要补三样：外键列、把客户展示名挂到本表的 `[ForeignColumn]`、引用它的计算列。

```csharp
[Table("SalesOrders")]
[TableJoin(typeof(Customer), "CustomerId", Alias = "Customer", JoinType = TableJoinType.Left)]
public class SaleOrderView
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("CustomerId")]
    public int CustomerId { get; set; }

    [Column("OrderNo", AllowNull = true)]
    public string? OrderNo { get; set; }

    [ForeignColumn("Customer", Property = nameof(Customer.Label))]
    public string? CustomerName { get; set; }

    [Column("OrderCustomerLabel", Expression = "{CustomerName} || '/' || {OrderNo}", ColumnMode = ColumnMode.Computed)]
    public string? CustomerLabel { get; set; }
}
```

`{CustomerName}` 是 `[ForeignColumn]`，它指向的 `Label` 又是客户表上的计算列，两层一起展开，关联表的列按它自己的别名限定：

```sql
(("Customer"."Region" || '-' || "Customer"."Name" || '-' || "Customer"."Code") || '/' || "T0"."OrderNo")
```

`CustomerLabel` 出现在 `SELECT`、`WHERE`、`ORDER BY` 里都是完整表达式，查询时 `LEFT JOIN` 会自动带上：

```sql
SELECT (("Customer"."Region" || '-' || "Customer"."Name" || '-' || "Customer"."Code") || '/' || "T0"."OrderNo") AS "CustomerLabel"
FROM "SalesOrders" "T0"
LEFT JOIN "Customers" "Customer" ON "T0"."CustomerId" = "Customer"."Id"
WHERE (("Customer"."Region" || '-' || "Customer"."Name" || '-' || "Customer"."Code") || '/' || "T0"."OrderNo") = @0
```

几个容易踩的点：

- 实体上必须有 `[Table("...")]`。只标 `[TableJoin]` 不会让类型被识别成表，`GetTableDefinition` 会直接返回 null。
- 关联声明要齐全：`[TableJoin]`（或 `[ForeignType]`）建 JOIN，`[ForeignColumn]` 把外部列挂到本表属性，缺一个名字就不存在。
- 占位符按属性名查找，忽略大小写，`{CustomerName}` 命中的是本表的那个关联列属性。
- 占位符写错不报错，会原样输出限定列名（如 `"T0"."CustomerLable"`），数据库执行时才提示列不存在。
- 左联接未命中时整条链取不到值。要兜底就在表达式里套一层 `COALESCE`，比如 `Expression = "COALESCE({CustomerName}, '未知客户') || '/' || {OrderNo}"`。

## 何时不适合

- **要高频过滤/关联且靠索引**：计算列在 `WHERE` 里展开为表达式，复用不了普通列索引。命中量大、要按这个字段频繁过滤或关联时，应落成物理列并建索引。
- **跨方言的字符串/函数逻辑**：表达式里可以写方言原始 SQL（上面的 `||` 是 SQLite / PostgreSQL 写法，MySQL 用 `CONCAT(...)`），迁移数据库时这段要跟着改。字符串拼接优先用 Expr 树的 `Concat`，它会按方言生成。
- **动态拼接**：表达式不接受运行时参数，带值的拼接会抛 `NotSupportedException`。

## 相关链接

- [返回目录](../README.md)
- [实体映射 · 计算列定义](../core-usage/entity-mapping.md)
- [数据权限](./data-permission.md)
- [多租户隔离](./tenant-isolation.md)
