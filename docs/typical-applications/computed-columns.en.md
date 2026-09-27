# Computed Columns in Practice

A computed column (`ColumnMode.Computed`) creates no physical column and takes no part in inserts/updates; query-time `SELECT` and conditions both render through its expression. Its value is centralizing a derived value in one place, with no redundant column to keep in sync.

Three real requirements follow: a discount computed from the signed-in user's level, a product on-sale flag, and a cross-table display name. Each comes with the SQL it actually generates (SQLite dialect), plus the limits of the approach.

## Requirement 1: a discount computed from the signed-in user's level

The business rule is "line total → discount by membership level → payable". The rate varies with the signed-in user, yet it cannot be passed down as a parameter: the membership level belongs to runtime context, while a persisted order has to remember the payable amount calculated at the time.

The rate table lives in code and is looked up by level. The level is not a SQL parameter but a literal spliced into the expression, and the whole fragment is wrapped in `GenericSqlExpr`:

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

The entity only declares the slots; the expression is attached at startup:

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

`GenericSqlExpr` derives from `LogicExpr` rather than `ValueTypeExpr`, so assigning it to `ExpressionExpr` goes through `AsValue()`, which wraps it as a value expression.

With both `DiscountAmount` and `Payable` as computed columns, the DDL keeps only physical columns:

```sql
CREATE TABLE "Orders" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "Amount" DECIMAL(18,2) NOT NULL
)
```

The same query renders a different expression per level, and `{DiscountAmount}` inside `Payable` expands that whole fragment:

```sql
-- Silver (2%)
("T0"."Amount" * 0.02)
("T0"."Amount" - ("T0"."Amount" * 0.02))

-- Gold (5%)
("T0"."Amount" * 0.05)
("T0"."Amount" - ("T0"."Amount" * 0.05))
```

Executing an order with `Amount = 1000` under Gold reads back `DiscountAmount=50`, `Payable=950`, matching what the database computes directly.

### Why the rate cannot be parameterized

A computed column has one hard constraint: **the expression must produce no parameters at all**. If `OutputParams` grows while rendering, it throws `NotSupportedException` outright:

```
ColumnDefinition.ExpressionExpr for column 'DiscountAmount' produced 1 parameter(s);
only fixed SQL expressions (property references, constants, functions, arithmetic) are allowed for computed columns.
```

The reason is that a computed column can appear in `SELECT`, `WHERE`, `ORDER BY` or `JOIN ON`, while the parameter list is managed by the caller outside the expression; appending a parameter from inside would scramble placeholder numbering.

So the rate can only go into the SQL as an inline literal. That means a level change changes the SQL text, which also invalidates the command cache — precisely the boundary of this approach.

It brings two things you have to handle yourself:

- **The fragment must not be empty.** When the `GenericSqlExpr` callback returns `null`, the render is `()` and the SQL is a syntax error. A level with no discount should return the literal `0` (rendered `(0)`), not `null`.
- **String constants need their own quoting.** `context.SqlBuilder.TryAppendSqlLiteral` handles escaping, but it returns `false` on a backslash or control character, in which case you must fall back to numeric or a fixed safe form.

### Level changes and the command cache

`DiscountAmount`'s SQL varies by level, and LiteOrm caches commands by SQL text, so different levels naturally land in different cache entries and never cross-use. What to note is that **a level stays fixed for a long time**: one cache entry per level is an acceptable cost. If the rule became "one rate per order", that low-cardinality benefit disappears, and the rate should instead be persisted as a physical column with the computed column reduced to `{Amount} * {DiscountRate}`.

### Where it is used

`DiscountAmount` and `Payable` are ordinary computed columns, referenceable from `SELECT`, `WHERE` and `ORDER BY` alike:

```csharp
var bigOrders = await viewService.SearchAsync(
    o => o.Payable >= 1000, cancellationToken: ct);          // filter by payable

var top = await viewDao.Search(
        Expr.Prop(nameof(Order.Payable)).Desc())
    .Section(1, 20).ToListAsync(ct);                          // top 20 by payable
```

```sql
WHERE ("T0"."Amount" - ("T0"."Amount" * 0.05)) >= @0
```

Two things to watch:

- Expressions are inlined in place. Once `{DiscountAmount}` expands into `Payable`, `Amount` appears twice in one SQL, and deeper levels keep doubling. Around three levels is a reasonable limit.
- There is no cycle detection. `A` referencing `B` while `B` references `A` recurses until the stack overflows, so that has to be prevented by hand.

## Requirement 2: a product on-sale flag

"Can this be sold" is a combination of three conditions: online, in stock, not taken down. That judgement is needed by the list page, the search page and every export endpoint, and scattered copies drift apart sooner or later.

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

Once normalized into a `bool` flag, a caller's check is one condition:

```csharp
var onSale = await viewService.SearchAsync(p => p.OnSale, cancellationToken: ct);
```

```sql
WHERE (CASE WHEN "T0"."IsOnline" = 1 AND "T0"."Stock" > 0 THEN 1 ELSE 0 END) = 1
```

Whether `OnSale` is typed `int` or `bool` only affects the C# read-back; how the expression is written and what the SQL looks like are decided by the expression itself. `bool` fits the meaning better, and `p => p.OnSale` can go straight into a condition.

Constants inside the expression must be inlined, so the `1` in `{IsOnline} = 1` is a literal and cannot be parameterized. Building the same judgement in the Expr tree accepts a `bool` constant directly and renders identically:

```csharp
table.Columns.First(c => c.Name == "OnSale").ExpressionExpr =
    Expr.If(Expr.Prop("IsOnline") == Expr.Const(true) & Expr.Prop("Stock") > Expr.Const(0),
            Expr.Const(true), Expr.Const(false));
```

One thing to note: the expression must not produce parameters, so interpolating a runtime switch (say "force off-sale right now") throws `NotSupportedException`. That kind of logic belongs in `WHERE` with `Expr.Value(...)`, not in a computed column.

## Requirement 3: a cross-table display name

A list page wants a combination name such as "East-Acme-C001 / SO-20260927-01" that reads at a glance. The customer's short name and the order number live in two tables, and persisting a redundant column on the order table means tracking every upstream change.

Start with a display name on the customer table (`Region`, `Name`, `Code` concatenated, itself a computed column):

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

The order view needs three additions: the foreign key column, a `[ForeignColumn]` exposing the customer display name as a property, and the computed column that references it.

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

`{CustomerName}` is a `[ForeignColumn]` whose target `Label` is itself a computed column on the customer table, so both levels expand together, with the associated table's columns qualified by its own alias:

```sql
(("Customer"."Region" || '-' || "Customer"."Name" || '-' || "Customer"."Code") || '/' || "T0"."OrderNo")
```

`CustomerLabel` is that full expression in `SELECT`, `WHERE` and `ORDER BY`, and a query carries the `LEFT JOIN` along:

```sql
SELECT (("Customer"."Region" || '-' || "Customer"."Name" || '-' || "Customer"."Code") || '/' || "T0"."OrderNo") AS "CustomerLabel"
FROM "SalesOrders" "T0"
LEFT JOIN "Customers" "Customer" ON "T0"."CustomerId" = "Customer"."Id"
WHERE (("Customer"."Region" || '-' || "Customer"."Name" || '-' || "Customer"."Code") || '/' || "T0"."OrderNo") = @0
```

Points that tend to trip people up:

- The entity needs `[Table("...")]`. `[TableJoin]` alone does not make a type a table, and `GetTableDefinition` returns null.
- The association declaration must be complete: `[TableJoin]` (or `[ForeignType]`) builds the JOIN and `[ForeignColumn]` attaches the external column to a property. Miss one and the name does not exist.
- Placeholders resolve by property name, case-insensitively, so `{CustomerName}` hits the association property on this table.
- A mistyped placeholder raises nothing; it is emitted as a qualified column name as written (`"T0"."CustomerLable"`), and the database only complains at execution time.
- On a left join without a match the whole chain yields nothing. Wrap the expression in `COALESCE` for a fallback, for example `Expression = "COALESCE({CustomerName}, 'unknown') || '/' || {OrderNo}"`.

## When not to reach for it

- **Hot filtering/join that relies on an index**: a computed column expands to an expression in `WHERE` and cannot reuse a plain column index. On high-volume filtering or joining by that field, persist a physical column and index it instead.
- **Dialect-specific string/function logic**: an expression can embed raw dialect SQL (the `||` above is SQLite / PostgreSQL; MySQL uses `CONCAT(...)`), and that fragment must be reworked when the database changes. Prefer the Expr tree's `Concat` for string concatenation, since it renders per dialect.
- **Dynamic fragments**: an expression accepts no runtime parameters, so a value-carrying concatenation throws `NotSupportedException`.

## Related links

- [Back to index](../README.md)
- [Entity Mapping · computed column definition](../core-usage/entity-mapping.en.md)
- [Data Permissions](./data-permission.en.md)
- [Tenant Isolation](./tenant-isolation.en.md)
