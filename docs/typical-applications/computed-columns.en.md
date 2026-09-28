# Computed Columns in Practice

A computed column creates no physical column and takes no part in inserts/updates; query-time `SELECT` and conditions both render through its expression.

Declaration: `Expression` (string form) or `ExpressionExpr` (Expr tree form) at runtime. Without an explicit `ColumnMode` it is inferred from the property's accessibility: a writable property gets `Read | Computed` (selected and read back), a read-only property gets `Computed` (query conditions only).

Three scenarios follow: a discount by user level, a product on-sale flag, a cross-table display name. The SQL shown is what actually renders (SQLite dialect).

## Requirement 1: a discount computed from the signed-in user's level

The rule is "line total → discount by membership level → payable". The rate varies with the signed-in user and cannot be a SQL parameter: the level is runtime context, while a persisted order has to remember the payable amount calculated at the time. The rate becomes a literal spliced into the expression, wrapped in `GenericSqlExpr`:

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

    // The expression is attached at startup, so the column is marked as computed at declaration; the Read bit is required to read it back
    [Column("DiscountAmount", ColumnMode = ColumnMode.Read | ColumnMode.Computed)]
    public decimal DiscountAmount { get; set; }

    // Declaring Expression alone is enough; a writable property is inferred as Read | Computed
    [Column("Payable", Expression = "{Amount} - {DiscountAmount}")]
    public decimal Payable { get; set; }
}
```

```csharp
var table = TableInfoProvider.Instance.GetTableDefinition(typeof(Order))!;
table.Columns.First(c => c.Name == "DiscountAmount").ExpressionExpr =
    Expr.Sql("UserLevelDiscount");
```

Assigning `Expr.Sql(...)` to `ExpressionExpr` wraps it as a value expression through the implicit conversion, so `AsValue()` is not needed; operator or extension-method chains still require it explicitly.

The DDL keeps only physical columns:

```sql
CREATE TABLE "Orders" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "Amount" DECIMAL(18,2) NOT NULL
)
```

The same query renders a different expression per level, and `{DiscountAmount}` expands in full:

```sql
-- Silver (2%)
("T0"."Amount" * 0.02)
("T0"."Amount" - ("T0"."Amount" * 0.02))

-- Gold (5%)
("T0"."Amount" * 0.05)
("T0"."Amount" - ("T0"."Amount" * 0.05))
```

Executing an order with `Amount = 1000` under Gold reads back `DiscountAmount=50`, `Payable=950`.

### Three hard constraints

- **The expression must not produce parameters**: if `OutputParams` grows while rendering, it throws `NotSupportedException`, so the rate can only be an inline literal; a level change changes the SQL text, and statements referencing it do not use the command cache, recomposing on every call. If the rule became one rate per order, persist the rate as a physical column and leave the computed column as `{Amount} * {DiscountRate}`.
- **The fragment must not be empty**: a `GenericSqlExpr` callback returning `null` renders `()`, a syntax error; a level with no discount should return the literal `0` (rendered `(0)`).
- **String constants need their own quoting**: `context.SqlBuilder.TryAppendSqlLiteral` escapes for you but returns `false` on a backslash or control character, in which case fall back to numeric or a fixed safe form.

The thrown message:

```
ColumnDefinition.ExpressionExpr for column 'DiscountAmount' produced 1 parameter(s);
only fixed SQL expressions (property references, constants, functions, arithmetic) are allowed for computed columns.
```

### Where it is used

`DiscountAmount` and `Payable` are ordinary computed columns, directly referenceable from `SELECT`, `WHERE` and `ORDER BY`:

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

- Expressions are inlined in place: once `{DiscountAmount}` expands into `Payable`, `Amount` appears twice in one SQL, and deeper levels keep doubling. Around three levels is a reasonable limit.
- There is no cycle detection: `A` referencing `B` while `B` references `A` recurses until the stack overflows, so that has to be prevented by hand.

## Requirement 2: a product on-sale flag

"Can this be sold" combines three conditions: online, in stock, not taken down, and the list page, search page and export endpoints all need the same judgement.

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

    // Query conditions only: a read-only property infers Computed (no Read bit), so it stays out of SELECT
    [Column("OnSale", Expression = "CASE WHEN {IsOnline} = 1 AND {Stock} > 0 THEN 1 ELSE 0 END")]
    public bool OnSale => IsOnline && Stock > 0;
}
```

A caller's check is one condition:

```csharp
var onSale = await viewService.SearchAsync(p => p.OnSale, cancellationToken: ct);
```

```sql
WHERE (CASE WHEN "T0"."IsOnline" = 1 AND "T0"."Stock" > 0 THEN 1 ELSE 0 END) = 1
```

`OnSale` serves conditions only: the property is read-only, so the inferred mode is `Computed` without a `Read` bit and it stays out of `SELECT`, taking part only in `WHERE` and `ORDER BY` through its expression. Make it writable and it would infer `Read | Computed`; only then does keeping it query-only require `ColumnMode = ColumnMode.Computed` explicitly. The property body and `Expression` must state the same rule. Whether `OnSale` is typed `int` or `bool` does not affect the SQL, and `bool` fits the meaning better.

Constants must be inlined, so the `1` in `{IsOnline} = 1` cannot be parameterized. The same judgement in the Expr tree takes a `bool` constant directly and renders identically:

```csharp
table.Columns.First(c => c.Name == "OnSale").ExpressionExpr =
    Expr.If(Expr.Prop("IsOnline") == Expr.Const(true) & Expr.Prop("Stock") > Expr.Const(0),
            Expr.Const(true), Expr.Const(false));
```

Splicing in a runtime switch (say "force off-sale right now") throws `NotSupportedException`; that logic belongs in `WHERE` with `Expr.Value(...)`.

## Requirement 3: a cross-table display name

A list page wants a combination name such as "East-Acme-C001 / SO-20260927-01", but the parts live in two tables and persisting a redundant column means tracking every upstream change.

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

    [Column("Label", Expression = "{Region} || '-' || {Name} || '-' || {Code}")]
    public string? Label { get; set; }
}
```

The order view needs three additions: the foreign key column, a `[ForeignColumn]` exposing the customer display name on this table, and the computed column that references it.

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

`{CustomerName}` targets a `Label` that is itself a computed column on the customer table, so both levels expand together, with the associated table's columns qualified by its own alias:

```sql
(("Customer"."Region" || '-' || "Customer"."Name" || '-' || "Customer"."Code") || '/' || "T0"."OrderNo")
```

`CustomerLabel` is that full expression in `SELECT`, `WHERE` and `ORDER BY`, and the `LEFT JOIN` comes along automatically:

```sql
SELECT (("Customer"."Region" || '-' || "Customer"."Name" || '-' || "Customer"."Code") || '/' || "T0"."OrderNo") AS "CustomerLabel"
FROM "SalesOrders" "T0"
LEFT JOIN "Customers" "Customer" ON "T0"."CustomerId" = "Customer"."Id"
WHERE (("Customer"."Region" || '-' || "Customer"."Name" || '-' || "Customer"."Code") || '/' || "T0"."OrderNo") = @0
```

Points that tend to trip people up:

- The entity needs `[Table("...")]`: `[TableJoin]` alone does not make a type a table, and `GetTableDefinition` returns null.
- The association declaration must be complete: `[TableJoin]` (or `[ForeignType]`) builds the JOIN and `[ForeignColumn]` attaches the external column to a property; miss one and the name does not exist.
- A mistyped placeholder raises nothing: it is emitted as a qualified column name as written (`"T0"."CustomerLable"`), and the database only complains at execution time.
- A `[ForeignColumn]` property must be writable to enter `SELECT`: a read-only property has no setter, so a read-back cannot fill it and the value comes from the property body instead.
- A left join without a match yields nothing for the whole chain: wrap the expression in `COALESCE` for a fallback, for example `Expression = "COALESCE({CustomerName}, 'unknown') || '/' || {OrderNo}"`.

## When not to reach for it

- **Hot filtering/joining that relies on an index**: the computed column expands to an expression in `WHERE` and cannot reuse a plain column index; on high-volume filtering or joining by that field, persist a physical column and index it.
- **Dialect-specific string/function logic**: an expression can embed raw dialect SQL (the `||` above is SQLite / PostgreSQL; MySQL uses `CONCAT(...)`); prefer the Expr tree's `Concat` for string concatenation, since it renders per dialect.
- **Dynamic fragments**: an expression accepts no runtime parameters, so a value-carrying concatenation throws `NotSupportedException`.

## Related links

- [Back to index](../README.md)
- [Entity Mapping · computed column definition](../core-usage/entity-mapping.en.md)
- [Data Permissions](./data-permission.en.md)
- [Tenant Isolation](./tenant-isolation.en.md)
