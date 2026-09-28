# 计算列的实际应用

计算列不生成物理列、不参与插入/更新，查询时 `SELECT` 与条件都按表达式返回结果。

用 `Expression`（字符串）或运行时的 `ExpressionExpr`（Expr 树）声明。不写 `ColumnMode` 时按属性可访问性推导：可写属性得到 `Read | Computed`，查询时按表达式取值并回填，只读属性得到 `Computed`，只用于查询条件。

下面走三个场景：按用户等级算折扣、商品上架状态位、跨表拼展示名。SQL 是 SQLite 方言下真实渲染出来的结果。

## 需求一：按当前登录用户等级算折扣

运营规则是「小计 → 按会员等级打折 → 应付」。折扣率跟着登录用户变，又不能当 SQL 参数传下去：会员等级是运行时才知道的东西，而订单得记住当时算出的应付金额。等级按取值拼成字面量，整段包进 `GenericSqlExpr`：

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

    // 表达式在启动时动态挂上，声明时就得标出计算列；要读出结果，Read 位不能省
    [Column("DiscountAmount", ColumnMode = ColumnMode.Read | ColumnMode.Computed)]
    public decimal DiscountAmount { get; set; }

    // 只声明 Expression 即为计算列，可写属性默认推导为 Read | Computed
    [Column("Payable", Expression = "{Amount} - {DiscountAmount}")]
    public decimal Payable { get; set; }
}
```

```csharp
var table = TableInfoProvider.Instance.GetTableDefinition(typeof(Order))!;
table.Columns.First(c => c.Name == "DiscountAmount").ExpressionExpr =
    Expr.Sql("UserLevelDiscount");
```

`Expr.Sql(...)` 赋给 `ExpressionExpr` 时，隐式转换会把它包成值表达式，不用手写 `AsValue()`；只有参与运算符或扩展方法链时才要显式调用。

建表时只剩物理列：

```sql
CREATE TABLE "Orders" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "Amount" DECIMAL(18,2) NOT NULL
)
```

同一个查询在不同等级下渲染出的表达式不同，`Payable` 里的 `{DiscountAmount}` 会被整段展开：

```sql
-- Silver (2%)
("T0"."Amount" * 0.02)
("T0"."Amount" - ("T0"."Amount" * 0.02))

-- Gold (5%)
("T0"."Amount" * 0.05)
("T0"."Amount" - ("T0"."Amount" * 0.05))
```

执行一条 `Amount = 1000` 的订单，Gold 等级下读出 `DiscountAmount=50`、`Payable=950`。

### 三条硬约束

- **表达式不能产生参数**：渲染时 `OutputParams` 只要多了参数就抛 `NotSupportedException`，所以折扣率只能内联成字面量，等级一变 SQL 文本就变，引用它的语句不走命令缓存，每次重新拼。要是规则改成每个订单一个费率，就该把费率落成物理列，计算列只剩 `{Amount} * {DiscountRate}`。
- **片段不能为空**：`GenericSqlExpr` 回调返回 `null` 会渲染出 `()`，SQL 直接报语法错。没有折扣的等级要返回字面量 `0`，渲染成 `(0)`。
- **字符串常量自己加引号**：`context.SqlBuilder.TryAppendSqlLiteral` 能帮忙转义，但它遇到反斜杠或控制字符会返回 `false`，这种只能改用数字或固定的安全写法。

报错原文：

```
ColumnDefinition.ExpressionExpr for column 'DiscountAmount' produced 1 parameter(s);
only fixed SQL expressions (property references, constants, functions, arithmetic) are allowed for computed columns.
```

### 用在哪里

`DiscountAmount` 与 `Payable` 是普通计算列，`SELECT`、`WHERE`、`ORDER BY` 里都能直接用：

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

- 表达式是原地内联的：`{DiscountAmount}` 展开进 `Payable` 之后，`Amount` 在一条 SQL 里出现两次，层数再深还会继续翻倍，三层左右够用。
- 不做环形引用检测：`A` 引用 `B`、`B` 又引用 `A` 会一直递归到栈溢出，只能靠人保证。

## 需求二：商品上架状态位

「能不能卖」是已上架、有库存、没被下架三个条件的组合，列表页、搜索页、导出接口都要用同一份判断。

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

    // 只用于查询条件：只读属性推导为 Computed（无 Read 位），不进 SELECT
    [Column("OnSale", Expression = "CASE WHEN {IsOnline} = 1 AND {Stock} > 0 THEN 1 ELSE 0 END")]
    public bool OnSale => IsOnline && Stock > 0;
}
```

调用方写起来只剩一句：

```csharp
var onSale = await viewService.SearchAsync(p => p.OnSale, cancellationToken: ct);
```

```sql
WHERE (CASE WHEN "T0"."IsOnline" = 1 AND "T0"."Stock" > 0 THEN 1 ELSE 0 END) = 1
```

`OnSale` 只用于条件：属性是只读的，推导出的模式就是 `Computed`，不带 `Read` 位，因此不进 `SELECT`，只在 `WHERE`、`ORDER BY` 里按表达式参与。换成可写属性会推导成 `Read | Computed`，那时想只用于条件才需要显式写 `ColumnMode = ColumnMode.Computed`。属性体跟 `Expression` 得保持同一条规则。属性类型写 `int` 还是 `bool` 不影响 SQL，`bool` 更贴合语义。

常量必须内联，`{IsOnline} = 1` 里的 `1` 不能参数化。同一个判断改用 Expr 树写，可以直接用 `bool` 常量，渲染结果一样：

```csharp
table.Columns.First(c => c.Name == "OnSale").ExpressionExpr =
    Expr.If(Expr.Prop("IsOnline") == Expr.Const(true) & Expr.Prop("Stock") > Expr.Const(0),
            Expr.Const(true), Expr.Const(false));
```

想在表达式里拼一个运行时开关（比如「当前是否强制下架」）会抛 `NotSupportedException`，这类逻辑只能放到 `WHERE` 里用 `Expr.Value(...)` 参数化。

## 需求三：跨表拼展示名

列表页要显示「华东-Acme-C001 / SO-20260927-01」这种一眼能认的组合名，可它的两段分属两张表，为了列表把冗余字段落到订单表上又得跟着上游改。

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

    [Column("Label", Expression = "{Region} || '-' || {Name} || '-' || {Code}")]
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

    [Column("OrderCustomerLabel", Expression = "{CustomerName} || '/' || {OrderNo}")]
    public string? CustomerLabel { get; set; }
}
```

`{CustomerName}` 指向的 `Label` 又是客户表上的计算列，两层一起展开，关联表的列按它自己的别名限定：

```sql
(("Customer"."Region" || '-' || "Customer"."Name" || '-' || "Customer"."Code") || '/' || "T0"."OrderNo")
```

`CustomerLabel` 在 `SELECT`、`WHERE`、`ORDER BY` 里都是这段完整表达式，查询时 `LEFT JOIN` 会自动带上：

```sql
SELECT (("Customer"."Region" || '-' || "Customer"."Name" || '-' || "Customer"."Code") || '/' || "T0"."OrderNo") AS "CustomerLabel"
FROM "SalesOrders" "T0"
LEFT JOIN "Customers" "Customer" ON "T0"."CustomerId" = "Customer"."Id"
WHERE (("Customer"."Region" || '-' || "Customer"."Name" || '-' || "Customer"."Code") || '/' || "T0"."OrderNo") = @0
```

几点注意：

- 实体上必须有 `[Table("...")]`：只标 `[TableJoin]` 不会让类型被当成表，`GetTableDefinition` 会返回 null。
- 关联声明要齐全：`[TableJoin]`（或 `[ForeignType]`）负责建 JOIN，`[ForeignColumn]` 负责把外部列挂到本表属性上，缺一个名字就不存在。
- 占位符写错不报错：它会原样输出限定列名（比如 `"T0"."CustomerLable"`），数据库执行到那一步才提示列不存在。
- `[ForeignColumn]` 的属性得可写才会进 `SELECT`：只读属性没有 setter，读回来也填不进去，值只能靠属性体自己算。
- 左联接没命中时整条链都取不到值：要兜底就在表达式里套一层 `COALESCE`，比如 `Expression = "COALESCE({CustomerName}, '未知客户') || '/' || {OrderNo}"`。

## 何时不适合

- **要高频过滤或关联、又指望索引**：计算列在 `WHERE` 里展开成表达式，用不上普通列的索引。命中量大、要频繁按这个字段过滤或关联时，就落成物理列并建索引。
- **跨方言的字符串与函数逻辑**：表达式里可以写方言的原始 SQL（上面的 `||` 是 SQLite / PostgreSQL 的写法，MySQL 用 `CONCAT(...)`），换数据库时这段要跟着改。字符串拼接优先用 Expr 树的 `Concat`，它会按方言生成。
- **动态拼接**：表达式不接受运行时参数，带值的拼接会抛 `NotSupportedException`。

## 相关链接

- [返回目录](../README.md)
- [实体映射 · 计算列定义](../core-usage/entity-mapping.md)
- [数据权限](./data-permission.md)
- [多租户隔离](./tenant-isolation.md)
