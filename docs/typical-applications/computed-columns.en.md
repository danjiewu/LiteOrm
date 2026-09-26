# Computed Columns in Practice

A computed column (`ColumnMode.Computed`) creates no physical column and takes no part in inserts/updates; query-time `SELECT` and conditions both render through its expression. Its value is centralizing a derived value in one place, with no redundant column to keep in sync.

Below is a sales-order table.

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
    public int? DeptId { get; set; }                 // null = globally shared data

    // Scenario 1: derived display column: the line total is not persisted, it reads back as its expression result
    [Column("LineTotal", Expression = "{Quantity} * {UnitPrice}", ColumnMode = ColumnMode.Computed)]
    public decimal LineTotal { get; set; }

    // Scenario 3: visibility normalization: collapse null (globally shared) to 0
    [Column("VisibleDept", Expression = "COALESCE({DeptId}, 0)", ColumnMode = ColumnMode.Computed)]
    public int VisibleDept { get; set; }
}
```

## Scenario 1: derived display columns

As a physical column the line total would need syncing on every insert/update, and a formula change would touch two places. As a computed column, `SELECT` returns the expression result directly:

```csharp
var row = await viewService.GetObjectAsync(id, ...);   // row.LineTotal is already the expression's computed value
```

## Scenario 2: the same expression drives filtering and ordering

A derived value also goes straight into `WHERE`: `LineTotal` expands to `({Quantity} * {UnitPrice})` in a condition, with no re-typed expression:

```csharp
var bigOrders = await viewService.SearchAsync(x => x.LineTotal >= 10000, ...);   // flag large orders
var top       = await viewService.SearchAsync(x => x.UnitPrice > 0, orderBy: o => o.LineTotal, ...);   // sort by amount
```

Because the condition expands into an expression, these filters cannot reuse a plain column index. For high-volume filtering, persist a physical column and index it.

## Scenario 3: normalize visibility so scoping needs one less branch

`DeptId = null` means globally shared data. To match "own dept + global" a scope would otherwise need `DeptId == dept || DeptId == null` in every branch; normalizing with `COALESCE({DeptId}, 0)` turns that into a single `VisibleDept == 0`:

```csharp
var visible = Prop(nameof(SaleOrder.VisibleDept)) == user.DeptId
           | Prop(nameof(SaleOrder.VisibleDept)) == 0;
```

This `Expr` can be passed to a single query or attached as a `ConstFilter` to apply everywhere, see Way 2 of [Data Permissions](./data-permission.en.md). The sentinel must not collide with any real `DeptId`.

## Scenario 4: read-only computed properties (not computed columns)

A read-only computed property on an entity can also appear in `Lambda` conditions, but it is registered through `LambdaExprConverter.RegisterMemberHandler`: it stays out of the column structure and out of `SELECT`, a separate route from `[Column(Computed)]`. Registration steps live in [Expression Extension · computed properties](../extensibility/expression-extension.en.md).

## Scenario 5: computed column referencing another computed column

Derived values are often chained: line total, discount, amount payable. A placeholder can point at another computed column on the same entity:

```csharp
[Column("DiscountRate")]
public decimal DiscountRate { get; set; }

[Column("DiscountAmount", Expression = "{LineTotal} * {DiscountRate}", ColumnMode = ColumnMode.Computed)]
public decimal DiscountAmount { get; set; }

[Column("Payable", Expression = "{LineTotal} - {DiscountAmount}", ColumnMode = ColumnMode.Computed)]
public decimal Payable { get; set; }
```

Referencing a computed column expands its expression too, each level in its own parentheses:

```sql
LineTotal      => ("T0"."Quantity" * "T0"."UnitPrice")
DiscountAmount => (("T0"."Quantity" * "T0"."UnitPrice") * "T0"."DiscountRate")
Payable        => (("T0"."Quantity" * "T0"."UnitPrice") - (("T0"."Quantity" * "T0"."UnitPrice") * "T0"."DiscountRate"))
```

Intermediate names stay out of the SQL; `SELECT`, `WHERE` and `ORDER BY` all use that full expression for `Payable`. Two things to keep in mind:

- Expressions are inlined in place: `Payable` expands `LineTotal` twice, so `Quantity * UnitPrice` appears 4 times, and each extra level doubles the repetition. Around three levels is a reasonable limit; beyond that, persist a redundant column.
- There is no cycle detection: a self-reference or a mutual A/B reference recurses until the stack overflows.

## Scenario 6: computed column referencing an associated column

An expression can also reference a column on an associated table; when that column is computed, both levels expand together.

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

    // computed column on the associated table
    [Column("Label", Expression = "{Name} || '-' || {Code}", ColumnMode = ColumnMode.Computed)]
    public string? Label { get; set; }
}
```

`SaleOrder` needs three additions: the foreign key column, a `[ForeignColumn]` exposing the associated column as a property, and the computed column that references it.

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

`FullLabel` renders as:

```sql
(("Customer"."Name" || '-' || "Customer"."Code") || '-' || "T0"."OrderNo")
```

Associated columns are qualified with their own alias; local columns use the main-table alias of the current query (`T0` for a single-table query). Points that tend to trip people up:

- Placeholders resolve by property name, case-insensitively, so `{CustomerLabel}` hits the association property on this table.
- The association declaration must be complete: `[ForeignType]` (or `[TableJoin]`) builds the JOIN and `[ForeignColumn]` attaches the external column to a property. Miss one and the name does not exist.
- A mistyped placeholder raises nothing; it is emitted as a qualified column name as written (`"T0"."CustomerLable"`), and the database only complains at execution time.
- On a left join without a match the whole chain yields nothing, so wrap the expression in `COALESCE` if you need a fallback.

## Scenario 7: functions and conditionals in the Expr tree

The string form takes dialect SQL directly; tiered mapping is easier in the Expr tree:

```csharp
// on the entity: [Column("Grade", ColumnMode = ColumnMode.Computed)] public int Grade { get; set; }
var table = TableInfoProvider.Instance.GetTableDefinition(typeof(SaleOrder))!;
table.Columns.First(c => c.Name == "Grade").ExpressionExpr = Expr.Case(
    Expr.Prop("LineTotal") >= Expr.Const(10000), Expr.Const(1),
    Expr.Prop("LineTotal") >= Expr.Const(1000), Expr.Const(2),
    Expr.Const(3));
```

Conditions and results alternate in pairs, a trailing odd argument becomes the `ELSE`, and the result is a flat multi-branch CASE:

```sql
(CASE WHEN ("T0"."Quantity" * "T0"."UnitPrice") >= 10000 THEN 1 WHEN ("T0"."Quantity" * "T0"."UnitPrice") >= 1000 THEN 2 ELSE 3 END)
```

With only two tiers, `Expr.If(condition, then, else)` is shorter. `Expr.Case((c1, r1), (c2, r2))` carries no `ELSE`, and a tuple array with an `ELSE` needs its element type spelled out as `new (LogicExpr, ValueTypeExpr)[] { ... }`. `Expr.Func`, `Concat` and the like work here too.

The whole expression must produce no parameters, so constants are limited to `bool`, integers and floating-point numbers, and ordinary strings; `decimal`, `DateTime` and strings with special characters get parameterized and throw `NotSupportedException`. A decimal constant is easier in the string form (`"{LineTotal} * 0.9"`).

## When not to reach for it

- **Hot filtering/join that relies on an index**: a computed column expands to an expression in `WHERE` and cannot reuse a plain column index; on large result sets, persist a physical column and index it instead.
- **Dialect-specific string/function logic**: an expression can embed raw dialect SQL, and migrating databases means reworking that fragment.
- **Dynamic fragments**: the expression must not produce parameters, so a runtime value throws `NotSupportedException`.

## Related links

- [Back to index](../README.md)
- [Entity Mapping · computed column definition](../core-usage/entity-mapping.en.md)
- [Data Permissions](./data-permission.en.md)
- [Tenant Isolation](./tenant-isolation.en.md)