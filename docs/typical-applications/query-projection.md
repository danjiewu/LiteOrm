# 按需投影

查询需求要的形状往往和表不一样：列表页五列，详情页另一组列，统计页只要几个聚合值。LiteOrm 满足这类需求有三种写法：查实体、先查实体再在内存里投影、查询时直接投影。三种写法语法上都通，SQL 拿回多少数据、问几次库，决定了各自的开销。

下面用四个来自不同业务域的需求做对比：订单列表页、订单导出、文档详情页、订单统计页。每个需求给出两种实现，文中的 SQL 都是 SQLite 方言下的实际渲染结果。

## 三种做法

| 做法 | 入口 | SQL 拿回什么 | 结果 |
| --- | --- | --- | --- |
| 查实体 | `Search` / `SearchAsync` | VO 或 DO 的全部列，VO 还带 JOIN 列 | 实体本体，可直接回写 |
| 先查实体再投影 | `Search` + 内存 `Select` | 同上，裁剪发生在数据落地之后 | DTO，但字节一点没少传 |
| 查询时投影 | `SearchAs` / `SearchOneAs` | 只含投影列 | DTO、匿名类型、标量 |

第二种做法的裁剪发生在内存里，该过网线的数据一点没少；第三种把裁剪推给 SQL，列表页、导出、聚合场景都适用。先查实体同样有它的位置，文末单独展开。

## 模型：两个业务域

示例跨两个域：电商的订单域和文档中心的文档域。DO 与表一一对应，只放物理列。

```csharp
[Table("Orders")]
public class Order
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("OrderNo")]
    public string OrderNo { get; set; } = null!;

    [Column("CustomerId")]
    public int CustomerId { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }

    [Column("Status")]
    public int Status { get; set; }

    [Column("Remark")]
    public string? Remark { get; set; }

    [Column("CreatedTime")]
    public DateTime CreatedTime { get; set; }
}

[Table("Customers")]
public class Customer
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Name")]
    public string Name { get; set; } = null!;
}
```

VO 继承 DO，用 `[TableJoin]` 建关联、`[ForeignColumn]` 补展示列：

```csharp
[TableJoin(typeof(Customer), "CustomerId", Alias = "Customer")]
public class OrderVO : Order
{
    [ForeignColumn("Customer", Property = nameof(Customer.Name))]
    public string? CustomerName { get; set; }
}
```

文档域的 DO 多一个正文大字段，VO 挂两个关联：

```csharp
[Table("Documents")]
public class Document
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("Title")]
    public string Title { get; set; } = null!;

    [Column("CategoryId")]
    public int CategoryId { get; set; }

    [Column("OwnerId")]
    public int OwnerId { get; set; }

    [Column("Content")]
    public string? Content { get; set; }

    [Column("Status")]
    public int Status { get; set; }

    [Column("CreatedTime")]
    public DateTime CreatedTime { get; set; }
}

[Table("DocCategories")]
public class DocCategory
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Name")]
    public string Name { get; set; } = null!;
}

[Table("DocUsers")]
public class DocUser
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Name")]
    public string Name { get; set; } = null!;
}

[Table("DocAttachments")]
public class DocAttachment
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("DocumentId")]
    public long DocumentId { get; set; }

    [Column("FileName")]
    public string FileName { get; set; } = null!;
}

[TableJoin(typeof(DocCategory), "CategoryId", Alias = "Category")]
[TableJoin(typeof(DocUser), "OwnerId", Alias = "Owner")]
public class DocumentVO : Document
{
    [ForeignColumn("Category", Property = nameof(DocCategory.Name))]
    public string? CategoryName { get; set; }

    [ForeignColumn("Owner", Property = nameof(DocUser.Name))]
    public string? OwnerName { get; set; }
}
```

三点说明：

- VO 不用重复 `[Table]`，特性由基类继承，表名、主键、列定义都跟着 DO 走。
- `[TableJoin]` 负责建 JOIN，`[ForeignColumn]` 负责把外部列挂到本类属性上，两个都写全 `CategoryName` 才存在。
- 一个 VO 可以叠加多个 `[TableJoin]`，`DocumentVO` 的 `CategoryName` 和 `OwnerName` 就各来自一个关联。

## 精确控制 JOIN

投影只决定 `SELECT` 拿哪些列，JOIN 哪些表、按什么键关联、用什么连接类型，则由 VO 上的 `[TableJoin]` 和 `[ForeignColumn]` 决定。查实体、投影、分组聚合三种查法下，这条规则都一样，JOIN 结构不变。

需要精确控制 JOIN 时，把元数据完整写在 VO 上，查询用泛型 `EntityViewService<TView>`，SQL 里的 `FROM` 和 `JOIN` 由框架生成，不用手写。

`[TableJoin]` 声明一张关联表：

| 成员 | 作用 |
| --- | --- |
| 构造函数首参 | 源表，字符串或实体类型，省略时是主表，即 VO 继承的 DO 对应的表 |
| `TargetType` | 关联的目标表类型 |
| `ForeignKeys` | 外键，联合主键以英文逗号分隔，按主键顺序排列 |
| `Alias` | SQL 里的表别名，`ForeignColumn` 靠它认表 |
| `JoinType` | `Inner`、`Left`、`Right`、`Full`、`Cross`，默认 `Left` |
| `AutoExpand` | 外表被引用时自动带上它自己的关联，默认 `false` |
| `PrimeKeys` | 覆盖目标表的主键关联，兼容写法，常规场景不用 |

`[ForeignColumn]` 把外部表的列挂到 VO 属性上，`Foreign` 可以是实体类型，也可以是某个 `[TableJoin]` 的 `Alias`：

```csharp
[ForeignColumn("Customer", Property = nameof(Customer.Name))]
public string? CustomerName { get; set; }
```

改元数据就改 SQL。报表只统计一定有关联客户的订单，连接类型换成 `Inner`，别名换成 `C`：

```csharp
[TableJoin(typeof(Customer), "CustomerId", Alias = "C", JoinType = TableJoinType.Inner)]
public class OrderReportVO : Order
{
    [ForeignColumn("C", Property = nameof(Customer.Name))]
    public string? CustomerName { get; set; }
}
```

查询用泛型 `EntityViewService<TView>`。框架把 `EntityViewService<>` 注册成了开放泛型，不写自定义服务时直接注入：

```csharp
public class OrderDashboard
{
    private readonly IEntityViewServiceAsync<OrderReportVO> _orderReportService;

    public OrderDashboard(IEntityViewServiceAsync<OrderReportVO> orderReportService)
    {
        _orderReportService = orderReportService;
    }

    public Task<List<OrderReportVO>> LoadAsync(int status, CancellationToken ct)
    {
        return _orderReportService.SearchAsync(
            q => q.Where(o => o.Status == status)
                 .OrderByDescending(o => o.CreatedTime),
            cancellationToken: ct);
    }
}
```

渲染出的 JOIN 跟着元数据走：

```sql
SELECT "C"."Name" AS "CustomerName", "T0"."Id", "T0"."OrderNo", "T0"."CustomerId", "T0"."Amount", "T0"."Status", "T0"."Remark", "T0"."CreatedTime"
FROM "Orders" "T0"
INNER JOIN "Customers" "C" ON "T0"."CustomerId" = "C"."Id"
WHERE "T0"."Status" = 2
ORDER BY "T0"."CreatedTime" DESC
```

要加自定义方法再继承 `EntityViewService<TView>`，同步和异步两个接口一起继承：

```csharp
public interface IOrderReportService
    : IEntityViewService<OrderReportVO>, IEntityViewServiceAsync<OrderReportVO>
{ }

[AutoRegister(Lifetime = Lifetime.Scoped)]
public class OrderReportService : EntityViewService<OrderReportVO>, IOrderReportService
{
    public OrderReportService(IServiceProvider serviceProvider) : base(serviceProvider) { }
}
```

## 服务：写 DO，读 VO

写入和读取的形状不同时，用 `EntityService<T, TView>` 组合：写操作走 DO，读操作走 VO。

```csharp
public interface IOrderService
    : IEntityService<Order>, IEntityServiceAsync<Order>,
      IEntityViewService<OrderVO>, IEntityViewServiceAsync<OrderVO>
{ }

[AutoRegister(Lifetime = Lifetime.Scoped)]
public class OrderService : EntityService<Order, OrderVO>, IOrderService
{
    public OrderService(IServiceProvider serviceProvider) : base(serviceProvider) { }
}
```

| 服务基类 | 写入 | 读取 | 适合 |
| --- | --- | --- | --- |
| `EntityService<T>` | `T` | `T` | 读写同形 |
| `EntityService<T, TView>` | `T` | `TView` | 写 DO 读 VO |
| `EntityViewService<TView>` | 无 | `TView` | 纯查询服务 |

`EntityService<T, TView>` 约束 `TView : T`，VO 必须继承 DO。自定义服务要标 `[AutoRegister(Lifetime = Lifetime.Scoped)]`，框架服务都是 Scoped 生命周期，用默认的 Singleton 会在根容器多解析出一个 `SessionManager`。

## 需求一：订单列表页

管理后台的订单列表，20 条一页，页面五列：订单号、客户名、金额、状态、下单时间。订单表带一个 `Remark` 长文本，列表用不到。

### 方案 A：先查实体再投影

服务读端返回的就是 VO，先拿实体，再在内存里裁：

```csharp
var orders = await orderService.SearchAsync(
    q => q.Where(o => o.Status == 2)
         .OrderByDescending(o => o.CreatedTime)
         .Skip(20).Take(20),
    cancellationToken: ct);

var page = orders.Select(o => new OrderListItem
{
    Id = o.Id,
    OrderNo = o.OrderNo,
    CustomerName = o.CustomerName,
    Amount = o.Amount,
    Status = o.Status,
    CreatedTime = o.CreatedTime
}).ToList();
```

SQL 把 8 列全部拉回来，`Remark` 长文本也在内：

```sql
SELECT "Customer"."Name" AS "CustomerName", "T0"."Id", "T0"."OrderNo", "T0"."CustomerId", "T0"."Amount", "T0"."Status", "T0"."Remark", "T0"."CreatedTime"
FROM "Orders" "T0"
LEFT JOIN "Customers" "Customer" ON "T0"."CustomerId" = "Customer"."Id"
WHERE "T0"."Status" = 2
ORDER BY "T0"."CreatedTime" DESC
LIMIT 20 OFFSET 20
```

换成读 DO（`ObjectViewDAO<Order>`，或读写同形的 `EntityService<Order>`），SQL 不带 JOIN：

```sql
SELECT "T0"."Id", "T0"."OrderNo", "T0"."CustomerId", "T0"."Amount", "T0"."Status", "T0"."Remark", "T0"."CreatedTime"
FROM "Orders" "T0"
WHERE "T0"."Status" = 2
ORDER BY "T0"."CreatedTime" DESC
LIMIT 20 OFFSET 20
```

但 `CustomerName` 不在 DO 里，还要再查一次客户表，按 Id 在内存里合并，一个页面凑出两次查询。

### 方案 B：查询时投影

投影类型是普通类，不用注册 `[Table]`：

```csharp
public class OrderListItem
{
    public long Id { get; set; }
    public string? OrderNo { get; set; }
    public string? CustomerName { get; set; }
    public decimal Amount { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
}
```

```csharp
var page = await orderService.SearchAsAsync(
    q => q.Where(o => o.Status == 2)
         .OrderByDescending(o => o.CreatedTime)
         .Skip(20).Take(20)
         .Select(o => new OrderListItem
         {
             Id = o.Id,
             OrderNo = o.OrderNo,
             CustomerName = o.CustomerName,
             Amount = o.Amount,
             Status = o.Status,
             CreatedTime = o.CreatedTime
         }),
    cancellationToken: ct);
```

SQL 只出投影选中的 6 列：

```sql
SELECT "T0"."Id", "T0"."OrderNo", "Customer"."Name" AS "CustomerName", "T0"."Amount", "T0"."Status", "T0"."CreatedTime"
FROM "Orders" "T0"
LEFT JOIN "Customers" "Customer" ON "T0"."CustomerId" = "Customer"."Id"
WHERE "T0"."Status" = 2
ORDER BY "T0"."CreatedTime" DESC
LIMIT 20 OFFSET 20
```

### 对比

- 分页、排序、JOIN 在两种方案里写法一样，`Skip` / `Take` / `OrderByDescending` 都下推成 SQL，投影不改变查询结构。
- 差别全在 `SELECT` 列表：方案 A 拿 8 列，含 `Remark` 长文本和页面不显示的 `CustomerId`；方案 B 拿 6 列。
- 方案 A 若读 DO，还要多一次客户表查询才能补上 `CustomerName`。

列表页默认用查询时投影，页面上不出现的列，没有理由过网线。

## 需求二：订单导出

运营导出订单报表写 CSV，字段：订单号、客户名、金额、状态、下单时间。百万行，分批拉取。

### 方案 A：分页查实体

```csharp
const int pageSize = 1000;
for (var page = 0; ; page++)
{
    var orders = await orderService.SearchAsync(
        q => q.Where(o => o.CreatedTime >= from && o.CreatedTime < to)
             .OrderBy(o => o.Id)
             .Skip(page * pageSize).Take(pageSize),
        cancellationToken: ct);
    if (orders.Count == 0) break;
    foreach (var o in orders)
    {
        csv.WriteLine($"{o.OrderNo},{o.CustomerName},{o.Amount},{o.Status},{o.CreatedTime}");
    }
}
```

每页 8 列，`Remark` 长文本照拉：

```sql
SELECT "Customer"."Name" AS "CustomerName", "T0"."Id", "T0"."OrderNo", "T0"."CustomerId", "T0"."Amount", "T0"."Status", "T0"."Remark", "T0"."CreatedTime"
FROM "Orders" "T0"
LEFT JOIN "Customers" "Customer" ON "T0"."CustomerId" = "Customer"."Id"
WHERE "T0"."CreatedTime" >= @0 AND "T0"."CreatedTime" < @1
ORDER BY "T0"."Id"
LIMIT 1000
```

### 方案 B：分页投影

导出的行结构是一次性的，用匿名类型，连 DTO 都不用定义：

```csharp
const int pageSize = 1000;
for (var page = 0; ; page++)
{
    var rows = await orderService.SearchAsAsync(
        q => q.Where(o => o.CreatedTime >= from && o.CreatedTime < to)
             .OrderBy(o => o.Id)
             .Skip(page * pageSize).Take(pageSize)
             .Select(o => new { o.OrderNo, o.CustomerName, o.Amount, o.Status, o.CreatedTime }),
        cancellationToken: ct);
    if (rows.Count == 0) break;
    foreach (var r in rows)
    {
        csv.WriteLine($"{r.OrderNo},{r.CustomerName},{r.Amount},{r.Status},{r.CreatedTime}");
    }
}
```

每页只传 5 列，`Remark` 不在其中：

```sql
SELECT "T0"."OrderNo", "Customer"."Name" AS "CustomerName", "T0"."Amount", "T0"."Status", "T0"."CreatedTime"
FROM "Orders" "T0"
LEFT JOIN "Customers" "Customer" ON "T0"."CustomerId" = "Customer"."Id"
WHERE "T0"."CreatedTime" >= @0 AND "T0"."CreatedTime" < @1
ORDER BY "T0"."Id"
LIMIT 1000
```

### 对比

- 每页从 8 列降到 5 列，导出动辄百万行，每页少传的列会跟着行数一起放大，`Remark` 占的比例越高，省得越多。
- 分批用 `Skip` / `Take` 翻页，配合 `OrderBy(o => o.Id)` 保证顺序稳定，两种方案同样适用。

## 需求三：文档详情页

文档详情页要：标题、正文、分类名、上传人、附件数。`DocumentVO` 已经挂了分类和上传人两个关联。

### 方案 A：先查实体再投影

读 VO 时实体里的关联列是齐的，内存里裁一次即可，附件数单独查一条：

```csharp
var doc = await documentService.SearchOneAsync(d => d.Id == id, cancellationToken: ct);
var attachmentCount = await attachmentService.CountAsync(a => a.DocumentId == id, cancellationToken: ct);

var detail = new DocumentDetail
{
    Id = doc.Id,
    Title = doc.Title,
    Content = doc.Content,
    CategoryName = doc.CategoryName,
    OwnerName = doc.OwnerName,
    AttachmentCount = attachmentCount
};
```

主查询拿 9 列加两个 JOIN：

```sql
SELECT "Category"."Name" AS "CategoryName", "Owner"."Name" AS "OwnerName", "T0"."Id", "T0"."Title", "T0"."CategoryId", "T0"."OwnerId", "T0"."Content", "T0"."Status", "T0"."CreatedTime"
FROM "Documents" "T0"
LEFT JOIN "DocUsers" "Owner" ON "T0"."OwnerId" = "Owner"."Id"
LEFT JOIN "DocCategories" "Category" ON "T0"."CategoryId" = "Category"."Id"
WHERE "T0"."Id" = 100
```

附件数是一条独立的聚合查询：

```sql
SELECT Count(1)
FROM "DocAttachments" "T0"
WHERE "T0"."DocumentId" = 100
```

如果读的是 DO（`ObjectViewDAO<Document>`，或读写同形的 `EntityService<Document>`），分类名和上传人名都不在实体里，要各查一次，加上附件数共 4 次查询，全部在内存组装。

### 方案 B：查询时投影

投影类型同样不用注册 `[Table]`：

```csharp
public class DocumentDetail
{
    public long Id { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? CategoryName { get; set; }
    public string? OwnerName { get; set; }
    public int AttachmentCount { get; set; }
}
```

```csharp
var detail = await documentService.SearchOneAsAsync(
    q => q.Where(d => d.Id == id)
         .Select(d => new DocumentDetail
         {
             Id = d.Id,
             Title = d.Title,
             Content = d.Content,
             CategoryName = d.CategoryName,
             OwnerName = d.OwnerName
         }),
    cancellationToken: ct);

var attachmentCount = await attachmentService.CountAsync(a => a.DocumentId == id, cancellationToken: ct);
detail.AttachmentCount = attachmentCount;
```

主查询拿 5 列，两个 JOIN 一个不少：

```sql
SELECT "T0"."Id", "T0"."Title", "T0"."Content", "Category"."Name" AS "CategoryName", "Owner"."Name" AS "OwnerName"
FROM "Documents" "T0"
LEFT JOIN "DocUsers" "Owner" ON "T0"."OwnerId" = "Owner"."Id"
LEFT JOIN "DocCategories" "Category" ON "T0"."CategoryId" = "Category"."Id"
WHERE "T0"."Id" = 100
```

### 对比

- 两个 JOIN 都来自 `DocumentVO` 的视图元数据，投影只是从中挑列，JOIN 一条没少。
- 附件数是聚合，塞不进投影：`ForeignColumn` 只映射外部表的列，不做 `COUNT`，这类数字只能单独查一条。
- 详情页本来就要 `Content` 大字段，投影省下的只是 `Status`、`CreatedTime` 这些顺带列，收益不如列表页明显，差距主要体现在读 DO 时：4 次查询压到 2 次。

## 需求四：订单统计页

运营看板按状态分组，统计每个状态的订单数和总金额。

### 方案 A：先查实体再聚合

```csharp
var orders = await orderService.SearchAsync(
    o => o.CreatedTime >= from && o.CreatedTime < to,
    cancellationToken: ct);

var stats = orders.GroupBy(o => o.Status)
    .Select(g => new OrderStatusStat
    {
        Status = g.Key,
        Count = g.Count(),
        Amount = g.Sum(o => o.Amount)
    })
    .ToList();
```

SQL 是全列全行，没有 `LIMIT`：

```sql
SELECT "Customer"."Name" AS "CustomerName", "T0"."Id", "T0"."OrderNo", "T0"."CustomerId", "T0"."Amount", "T0"."Status", "T0"."Remark", "T0"."CreatedTime"
FROM "Orders" "T0"
LEFT JOIN "Customers" "Customer" ON "T0"."CustomerId" = "Customer"."Id"
WHERE "T0"."CreatedTime" >= @0 AND "T0"."CreatedTime" < @1
```

区间内有多少订单就拉回多少行，分组在内存里做；十万行的区间，就是十万行全列数据过网线，最后只要几行分组结果。

### 方案 B：聚合下推

投影类型按聚合结果定义：

```csharp
public class OrderStatusStat
{
    public int Status { get; set; }
    public int Count { get; set; }
    public decimal Amount { get; set; }
}
```

`SearchAs` 原生支持分组投影，`GroupBy` 之后直接 `Select`：

```csharp
var stats = await orderService.SearchAsAsync(
    q => q.Where(o => o.CreatedTime >= from && o.CreatedTime < to)
         .GroupBy(o => o.Status)
         .Select(g => new OrderStatusStat
         {
             Status = g.Key,
             Count = g.Count(),
             Amount = g.Sum(o => o.Amount)
         }),
    cancellationToken: ct);
```

`g.Key` 取分组键，`g.Count()`、`g.Sum(...)` 生成聚合值，都写在 Lambda 里；运行期动态拼装的场景，再用 `SelectExpr` 的手工形式（`Expr.From(typeof(OrderVO)).GroupBy(...).Select(...)`）。

SQL 只有 3 列，行数等于状态数：

```sql
SELECT "T0"."Status", Count(1) AS "Count", Sum("T0"."Amount") AS "Amount"
FROM "Orders" "T0"
LEFT JOIN "Customers" "Customer" ON "T0"."CustomerId" = "Customer"."Id"
WHERE "T0"."CreatedTime" >= @0 AND "T0"."CreatedTime" < @1
GROUP BY "T0"."Status"
```

### 对比

- 这种需求先查实体不可行：内存里做聚合，至少要拉回区间内的全部行；SQL 里做聚合，结果行数等于分组数。
- 投影里一个 `Customer` 列都没选，`LEFT JOIN` 仍在，因为 JOIN 声明在 VO 的视图元数据上，与投影无关。

## 什么时候先查实体更好

以下情况先查实体更合适：

1. **结果要回写**。`Update`、`Delete` 要的是 DO 实例，先查出来改完直接存，比投影出 DTO 再反过来找实体少一层。
2. **一份数据多种形状**。同一个查询结果既要给列表又要给下拉框，先查一次实体，在内存里投影出两个形状，比查两次库便宜。
3. **跨来源合并**。订单、库存、物流来自三个服务或三张表，在内存里组装比硬写一个三表 JOIN 直接，也更好维护。
4. **行数本来就少**。几十行的数据，列裁剪省不下什么，选可读性最好的写法。
5. **不想定义 DTO**。直接查 VO 本体，`ForeignColumn` 列已经补齐，是查实体与投影之间的中间态。

## 容易踩的点

- **投影类型别注册 `[Table]`**：注册过的 `TResult` 按 `SelectColumns` 位置映射，`Select` 列顺序必须与注册顺序一致；未注册的 DTO 按列名匹配，`As` 的别名与属性同名即可。投影类型保持未注册状态，完整规则见[查询总览](../core-usage/query-overview.md)。
- **别名写错不报错**：列名与属性名对不上时，对应属性留默认值，不抛异常。上线前用真实数据读一遍校验。
- **JOIN 由视图决定，不由投影决定**：投影里一个关联列都不选，`LEFT JOIN` 照样出现在 SQL 里，需求四的统计语句就是例子。要精确控制 JOIN 就改 VO 元数据，见「精确控制 JOIN」。
- **SearchOneAs 不追加 LIMIT**：单条投影取的是结果集第一条，语句本身不带 `LIMIT 1`。
- **Service 没有 ExprString 版 SearchAs**：要手写 SQL 投影时切到 `ObjectViewDAO<OrderVO>`。
- **聚合塞不进 ForeignColumn 投影**：`ForeignColumn` 只映射外部表的列，`COUNT`、`SUM` 这类聚合值要单独查一条，如需求三的附件数。
- **先查实体再投影，分页会下推，列裁剪不会**：`Skip` / `Take` 照样进 SQL，但 `SELECT` 永远是全列，大字段照拉。
- **VO 不进写路径**：插入、更新、删除统一传 DO，外键列和计算列都不参与写入。

## 相关链接

- [返回目录](../README.md)
- [查询总览：Search 与 SearchAs](../core-usage/query-overview.md)
- [视图模型与服务](../core-usage/view-models-and-services.md)
- [关联查询](../core-usage/associations.md)
- [计算列的实际应用](./computed-columns.md)
