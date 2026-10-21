# Projection on Demand: Entity-First or Query-Time

The shape a query needs rarely matches the table: a list page wants five columns, a detail page another set, a stats page only aggregates. LiteOrm covers this with three approaches: query the entity, query the entity and project in memory, or project inside the query. All three are valid syntax; how much data the SQL brings back and how many times it asks the database decide the cost of each.

Below are four requirements from different business domains: an order list page, an order export, a document detail page, and an order stats page. Each requirement shows two implementations, and every SQL statement below is what really renders under the SQLite dialect.

## The three approaches

| Approach | Entry point | What the SQL brings back | Result |
| --- | --- | --- | --- |
| Query the entity | `Search` / `SearchAsync` | All columns of the VO or DO; the VO also carries JOIN columns | The entity itself, writable as-is |
| Entity first, then project | `Search` + in-memory `Select` | Same as above; trimming happens after the data lands | A DTO, but every byte was still transferred |
| Project in the query | `SearchAs` / `SearchOneAs` | Only the projected columns | DTO, anonymous type, scalar |

The second approach leaves trimming to memory, and the data crossing the wire is no less for it; the third pushes trimming into SQL, which suits list pages, exports, and aggregates. Querying the entity still has its place, covered in the last section.

## Models: two business domains

The examples span two domains: an e-commerce order domain and a document-center domain. The DO maps one-to-one to the table and holds only physical columns.

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

The VO inherits the DO, adds the association with `[TableJoin]` and the display column with `[ForeignColumn]`:

```csharp
[TableJoin(typeof(Customer), "CustomerId", Alias = "Customer")]
public class OrderVO : Order
{
    [ForeignColumn("Customer", Property = nameof(Customer.Name))]
    public string? CustomerName { get; set; }
}
```

The document DO carries one more large field for the body, and its VO hangs two associations:

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

Three points:

- The VO does not repeat `[Table]`; attributes are inherited from the base class, so the table name, primary key and column definitions all come from the DO.
- `[TableJoin]` builds the JOIN and `[ForeignColumn]` attaches the external column to a property on this class; both are needed for `CategoryName` to exist.
- Multiple `[TableJoin]` attributes stack on one VO; `CategoryName` and `OwnerName` on `DocumentVO` each come from their own association.

## Precise JOIN control

Projection decides which columns the `SELECT` brings back, while `[TableJoin]` and `[ForeignColumn]` on the VO decide which tables get joined, on which keys, and with which join type. The rule holds for all three approaches: entity queries, projections, and grouped aggregation keep the same JOIN structure.

To control the JOIN precisely, declare the metadata on the VO and query with the generic `EntityViewService<TView>`; the framework generates the `FROM` and `JOIN` from it.

`[TableJoin]` declares one joined table:

| Member | Purpose |
| --- | --- |
| First constructor argument | The source table, a string or an entity type; omitted means the main table, the table behind the DO the VO inherits from |
| `TargetType` | Type of the joined target table |
| `ForeignKeys` | Foreign keys; composite keys are comma-separated, ordered by the primary key |
| `Alias` | Table alias in SQL; `ForeignColumn` recognizes the table by it |
| `JoinType` | `Inner`, `Left`, `Right`, `Full`, `Cross`; defaults to `Left` |
| `AutoExpand` | Brings in the foreign table's own joins automatically when it is referenced; defaults to `false` |
| `PrimeKeys` | Overrides the target table's primary-key association; a compatibility form, not for regular use |

`[ForeignColumn]` attaches a column of the foreign table to a VO property. `Foreign` accepts an entity type or the `Alias` of a `[TableJoin]`:

```csharp
[ForeignColumn("Customer", Property = nameof(Customer.Name))]
public string? CustomerName { get; set; }
```

Change the metadata and the SQL changes. A report counts only orders that certainly have a customer, so switch the join type to `Inner` and the alias to `C`:

```csharp
[TableJoin(typeof(Customer), "CustomerId", Alias = "C", JoinType = TableJoinType.Inner)]
public class OrderReportVO : Order
{
    [ForeignColumn("C", Property = nameof(Customer.Name))]
    public string? CustomerName { get; set; }
}
```

Query with the generic `EntityViewService<TView>`. The framework registers `EntityViewService<>` as an open generic, so inject it directly when no custom service is needed:

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

The rendered JOIN follows the metadata:

```sql
SELECT "C"."Name" AS "CustomerName", "T0"."Id", "T0"."OrderNo", "T0"."CustomerId", "T0"."Amount", "T0"."Status", "T0"."Remark", "T0"."CreatedTime"
FROM "Orders" "T0"
INNER JOIN "Customers" "C" ON "T0"."CustomerId" = "C"."Id"
WHERE "T0"."Status" = 2
ORDER BY "T0"."CreatedTime" DESC
```

Subclass `EntityViewService<TView>` when custom methods are needed, and inherit both the sync and the async interface:

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

## Services: write the DO, read the VO

When the write and read shapes differ, use the `EntityService<T, TView>` combination: writes go through the DO, reads through the VO.

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

| Service base class | Writes | Reads | Fits |
| --- | --- | --- | --- |
| `EntityService<T>` | `T` | `T` | Same shape on both sides |
| `EntityService<T, TView>` | `T` | `TView` | Write the DO, read the VO |
| `EntityViewService<TView>` | none | `TView` | Query-only services |

`EntityService<T, TView>` constrains `TView : T`, so the VO must inherit the DO. Custom services should be marked `[AutoRegister(Lifetime = Lifetime.Scoped)]`; framework services are all Scoped, and the default Singleton resolves an extra `SessionManager` in the root container.

## Requirement 1: the order list page

An admin order list, 20 rows per page, five columns on screen: order number, customer name, amount, status, order time. The orders table carries a `Remark` long text the list never shows.

### Approach A: entity first, then project

The read side of the service returns the VO, so take the entity and trim it in memory:

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

The SQL pulls all 8 columns back, the `Remark` long text included:

```sql
SELECT "Customer"."Name" AS "CustomerName", "T0"."Id", "T0"."OrderNo", "T0"."CustomerId", "T0"."Amount", "T0"."Status", "T0"."Remark", "T0"."CreatedTime"
FROM "Orders" "T0"
LEFT JOIN "Customers" "Customer" ON "T0"."CustomerId" = "Customer"."Id"
WHERE "T0"."Status" = 2
ORDER BY "T0"."CreatedTime" DESC
LIMIT 20 OFFSET 20
```

Reading the DO instead (`ObjectViewDAO<Order>`, or the same-shape `EntityService<Order>`) drops the JOIN:

```sql
SELECT "T0"."Id", "T0"."OrderNo", "T0"."CustomerId", "T0"."Amount", "T0"."Status", "T0"."Remark", "T0"."CreatedTime"
FROM "Orders" "T0"
WHERE "T0"."Status" = 2
ORDER BY "T0"."CreatedTime" DESC
LIMIT 20 OFFSET 20
```

But `CustomerName` is not on the DO, so the customers table needs another query and an in-memory merge by Id, making two round trips for a single page.

### Approach B: project in the query

Projection types are plain classes, no `[Table]` registration required:

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

The SQL selects only the 6 columns the projection asks for:

```sql
SELECT "T0"."Id", "T0"."OrderNo", "Customer"."Name" AS "CustomerName", "T0"."Amount", "T0"."Status", "T0"."CreatedTime"
FROM "Orders" "T0"
LEFT JOIN "Customers" "Customer" ON "T0"."CustomerId" = "Customer"."Id"
WHERE "T0"."Status" = 2
ORDER BY "T0"."CreatedTime" DESC
LIMIT 20 OFFSET 20
```

### Comparison

- Paging, sorting and the JOIN read the same in both approaches; `Skip` / `Take` / `OrderByDescending` all push down into SQL, and a projection does not change the query structure.
- The difference is entirely in the `SELECT` list: approach A takes 8 columns, including the `Remark` long text and the `CustomerId` the page never shows; approach B takes 6.
- Under approach A, reading the DO needs one more query against the customers table to fill in `CustomerName`.

List pages default to query-time projection, since a column that never reaches the screen has no reason to cross the wire.

## Requirement 2: the order export

Operations exports an order report as CSV with columns: order number, customer name, amount, status, order time. Millions of rows, pulled in batches.

### Approach A: paged entity queries

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

Each page carries 8 columns, the `Remark` long text included:

```sql
SELECT "Customer"."Name" AS "CustomerName", "T0"."Id", "T0"."OrderNo", "T0"."CustomerId", "T0"."Amount", "T0"."Status", "T0"."Remark", "T0"."CreatedTime"
FROM "Orders" "T0"
LEFT JOIN "Customers" "Customer" ON "T0"."CustomerId" = "Customer"."Id"
WHERE "T0"."CreatedTime" >= @0 AND "T0"."CreatedTime" < @1
ORDER BY "T0"."Id"
LIMIT 1000
```

### Approach B: paged projections

The exported row shape is one-off, so use an anonymous type and skip the DTO entirely:

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

Each page carries only 5 columns, `Remark` not among them:

```sql
SELECT "T0"."OrderNo", "Customer"."Name" AS "CustomerName", "T0"."Amount", "T0"."Status", "T0"."CreatedTime"
FROM "Orders" "T0"
LEFT JOIN "Customers" "Customer" ON "T0"."CustomerId" = "Customer"."Id"
WHERE "T0"."CreatedTime" >= @0 AND "T0"."CreatedTime" < @1
ORDER BY "T0"."Id"
LIMIT 1000
```

### Comparison

- Pages drop from 8 columns to 5, and exports run to millions of rows, so the columns saved per page scale with the row count: the larger the `Remark` share, the more this saves.
- Batching with `Skip` / `Take` plus `OrderBy(o => o.Id)` for a stable order works the same in both approaches.

## Requirement 3: the document detail page

A document detail page shows: title, body, category name, uploader, attachment count. `DocumentVO` already hangs the category and uploader associations.

### Approach A: entity first, then project

Reading the VO leaves the associated columns already in the entity, so trim it in memory and count the attachments separately:

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

The main query takes 9 columns plus two JOINs:

```sql
SELECT "Category"."Name" AS "CategoryName", "Owner"."Name" AS "OwnerName", "T0"."Id", "T0"."Title", "T0"."CategoryId", "T0"."OwnerId", "T0"."Content", "T0"."Status", "T0"."CreatedTime"
FROM "Documents" "T0"
LEFT JOIN "DocUsers" "Owner" ON "T0"."OwnerId" = "Owner"."Id"
LEFT JOIN "DocCategories" "Category" ON "T0"."CategoryId" = "Category"."Id"
WHERE "T0"."Id" = 100
```

The attachment count is a separate aggregate query:

```sql
SELECT Count(1)
FROM "DocAttachments" "T0"
WHERE "T0"."DocumentId" = 100
```

Reading the DO instead (`ObjectViewDAO<Document>`, or the same-shape `EntityService<Document>`) leaves out the category name and uploader name, so each needs its own query: 4 round trips in total, all assembled in memory.

### Approach B: project in the query

The projection type needs no `[Table]` registration either:

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

The main query takes 5 columns, with both JOINs intact:

```sql
SELECT "T0"."Id", "T0"."Title", "T0"."Content", "Category"."Name" AS "CategoryName", "Owner"."Name" AS "OwnerName"
FROM "Documents" "T0"
LEFT JOIN "DocUsers" "Owner" ON "T0"."OwnerId" = "Owner"."Id"
LEFT JOIN "DocCategories" "Category" ON "T0"."CategoryId" = "Category"."Id"
WHERE "T0"."Id" = 100
```

### Comparison

- Both JOINs come from the view metadata of `DocumentVO`; the projection only picks columns from them and drops neither JOIN.
- The attachment count is an aggregate and does not fit into a projection: `ForeignColumn` maps columns of the foreign table and does not do `COUNT`, so numbers like this still take their own query.
- A detail page needs the `Content` large field anyway, so the projection only saves incidental columns like `Status` and `CreatedTime`, a smaller gain than on a list page. The gain shows up mostly in the DO scenario: 4 round trips become 2.

## Requirement 4: the order stats page

An operations dashboard groups orders by status and counts them, summing the amount per status.

### Approach A: entity first, then aggregate

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

The SQL is all columns, all rows, with no `LIMIT`:

```sql
SELECT "Customer"."Name" AS "CustomerName", "T0"."Id", "T0"."OrderNo", "T0"."CustomerId", "T0"."Amount", "T0"."Status", "T0"."Remark", "T0"."CreatedTime"
FROM "Orders" "T0"
LEFT JOIN "Customers" "Customer" ON "T0"."CustomerId" = "Customer"."Id"
WHERE "T0"."CreatedTime" >= @0 AND "T0"."CreatedTime" < @1
```

As many rows come back as the range holds, with the grouping left to memory; a hundred-thousand-row range means a hundred thousand full-column rows cross the wire just to land a handful of grouped rows.

### Approach B: push the aggregate down

The projection type mirrors the aggregate result:

```csharp
public class OrderStatusStat
{
    public int Status { get; set; }
    public int Count { get; set; }
    public decimal Amount { get; set; }
}
```

`SearchAs` supports grouped projection natively; `Select` follows `GroupBy` directly:

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

`g.Key` takes the group key and `g.Count()` / `g.Sum(...)` produce the aggregates, all written inside the lambda; queries assembled at runtime can fall back to the hand-built `SelectExpr` form (`Expr.From(typeof(OrderVO)).GroupBy(...).Select(...)`).

The SQL has just 3 columns, and as many rows as there are statuses:

```sql
SELECT "T0"."Status", Count(1) AS "Count", Sum("T0"."Amount") AS "Amount"
FROM "Orders" "T0"
LEFT JOIN "Customers" "Customer" ON "T0"."CustomerId" = "Customer"."Id"
WHERE "T0"."CreatedTime" >= @0 AND "T0"."CreatedTime" < @1
GROUP BY "T0"."Status"
```

### Comparison

- This requirement rules out querying the entity first: aggregating in memory has to pull back every row in the range, while aggregating in SQL returns as many rows as there are groups.
- The projection selects no `Customer` column at all, yet the `LEFT JOIN` remains, because the JOIN is declared in the VO's view metadata and has nothing to do with the projection.

## When querying the entity first is better

Querying the entity first fits these cases better:

1. **The result gets written back**. `Update` and `Delete` want DO instances; querying one and saving it after edits skips a layer compared to projecting a DTO and hunting for the entity again.
2. **One dataset, multiple shapes**. The same query result feeds both a list and a dropdown, so query the entity once and project two shapes in memory, cheaper than two round trips.
3. **Merging across sources**. Orders, inventory and logistics come from three services or three tables; assembling them in memory is more direct than forcing a three-table JOIN, and easier to maintain.
4. **The row count is small anyway**. For a few dozen rows, column trimming saves nothing; pick the most readable form.
5. **No DTO wanted**. Query the VO itself; the `ForeignColumn` columns are already filled in, a middle ground between entity queries and projections.

## Points that tend to trip people up

- **Do not register projection types with `[Table]`**: a registered `TResult` maps positionally by `SelectColumns`, so the `Select` order must match the registered order; an unregistered DTO maps by column name, with the `As` alias matching the property name. Keep projection types unregistered; the full rules are in [Query Overview](../core-usage/query-overview.en.md).
- **A mistyped alias raises nothing**: when a column name does not match a property name, that property keeps its default value. Read real data once before going live.
- **The JOIN is decided by the view, not the projection**: even when the projection selects no associated column, the `LEFT JOIN` still appears; the stats statement in requirement 4 is the example. To control the JOIN precisely, edit the VO metadata — see "Precise JOIN control".
- **SearchOneAs appends no LIMIT**: the single projection takes the first row of the result set; the statement itself carries no `LIMIT 1`.
- **Service has no ExprString overload of SearchAs**: switch to `ObjectViewDAO<OrderVO>` when you need hand-written SQL projections.
- **Aggregates do not fit into a ForeignColumn projection**: `ForeignColumn` maps columns of the foreign table only, so aggregates like `COUNT` and `SUM` take their own query, like the attachment count in requirement 3.
- **Entity-first projection pushes paging down, never column trimming**: `Skip` / `Take` still reach the SQL, but the `SELECT` is always all columns, large fields included.
- **The VO stays out of the write path**: pass the DO to inserts, updates and deletes; foreign key columns and computed columns take no part in writes.

## Related links

- [Back to index](../README.md)
- [Query Overview: Search vs SearchAs](../core-usage/query-overview.en.md)
- [View Models and Services](../core-usage/view-models-and-services.en.md)
- [Associations](../core-usage/associations.en.md)
- [Computed Columns in Practice](./computed-columns.en.md)
