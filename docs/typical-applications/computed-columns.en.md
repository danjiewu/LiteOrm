# Computed Columns in Practice

A computed column (`ColumnMode.Computed`) creates no physical column and takes no part in inserts/updates; query-time `SELECT` and conditions both render through its expression. Its practical value is centralizing a derived value in one place — no extra redundant column to keep in sync, and no copy-pasted calculation all over your C# code.

Below is how it plays out on a sales-order table.

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

    // Scenario 1: derived display column — the line total is not persisted, it reads back as its expression result
    [Column("LineTotal", Expression = "{Quantity} * {UnitPrice}", ColumnMode = ColumnMode.Computed)]
    public decimal LineTotal { get; set; }

    // Scenario 3: visibility normalization — collapse null (globally shared) to 0 so scoping needs one less special case
    [Column("VisibleDept", Expression = "COALESCE({DeptId}, 0)", ColumnMode = ColumnMode.Computed)]
    public int VisibleDept { get; set; }
}
```

## Scenario 1: derived display columns

The line total is `Quantity * UnitPrice`. Persisted as a physical column it would have to be kept in sync on every insert/update, and changing the formula means touching two places. Declared as a computed column, `SELECT` returns the expression result directly — the list view needs no client-side math and there is no redundant column to maintain.

```csharp
var row = await viewService.GetObjectAsync(id, ...);   // row.LineTotal is already the expression's computed value
```

## Scenario 2: the same expression drives filtering and ordering

A derived value is not just for display — it goes straight into `WHERE`. `LineTotal` expands to `({Quantity} * {UnitPrice})` in a condition, so you neither re-type the expression nor hand-assemble a string:

```csharp
var bigOrders = await viewService.SearchAsync(x => x.LineTotal >= 10000, ...);   // flag large orders
var top       = await viewService.SearchAsync(x => x.UnitPrice > 0, orderBy: o => o.LineTotal, ...);   // sort by amount
```

Because the condition expands into an expression, these filters cannot reuse a plain column index. For high-volume "large order" filtering, index a physical column or persist the redundant one after all.

## Scenario 3: normalize visibility so scoping needs one less branch

`DeptId = null` means globally shared data, visible to everyone. To match "own dept + global" a scope would otherwise need `DeptId == dept || DeptId == null` in every branch. Normalizing with `COALESCE({DeptId}, 0)` turns that into a single `VisibleDept == 0`:

```csharp
var visible = Prop(nameof(SaleOrder.VisibleDept)) == user.DeptId
           | Prop(nameof(SaleOrder.VisibleDept)) == 0;
```

This `Expr` can be passed to a single query or attached as a `ConstFilter` to apply everywhere — see Way 2 of [Data Permissions](./data-permission.en.md). The sentinel (here `0`) must not collide with any real `DeptId`.

## Scenario 4: read-only computed properties via Lambda resolution

Some derived values are awkward to declare statically with `[Column]` — the logic is dynamic, or you only want to reference the property inside a `Lambda` and not treat it as a column at all. Register a Lambda member handler that translates the read-only computed property into SQL, then use it directly in `Lambda` queries:

```csharp
public class SaleOrder
{
    public DateTime CreateTime { get; set; }

    // read-only computed property, not persisted: days since the order was placed
    public int DaysAgo => (int)(DateTime.Now - CreateTime).TotalDays;
}

LambdaExprConverter.RegisterMemberHandler(typeof(SaleOrder), "DaysAgo", (node, converter) =>
{
    return new FunctionExpr("DATEDIFF", new FunctionExpr("DAY"), new PropertyExpr("CreateTime"), new FunctionExpr("CURRENT_DATE"));
});

// usable in Lambda afterwards
var recent = await viewService.SearchAsync(x => x.DaysAgo <= 7, ...);
```

The difference from `[Column(Computed)]` is "property versus column": the former stays out of the column structure and out of `SELECT` — it only serves dynamic, on-demand Lambda conditions; the latter is a real computed column whose reads and conditions both render through its expression. Use `RegisterMemberHandler` to hook it up as needed; full steps live in [Expression Extension · computed properties](../extensibility/expression-extension.en.md).

## When not to reach for it

- **Hot filtering/join that relies on an index**: a computed column expands to an expression in `WHERE` and usually cannot reuse a plain column index; on large result sets, persist a physical column and index it instead.
- **Dialect-specific string/function logic**: the string `Expression` accepts `{property}` placeholders and can also embed raw SQL for your dialect, but migrating databases means reworking that fragment per dialect.
- **Dynamic logic that needs parameters**: the Expr-tree `ExpressionExpr` only allows fixed, non-parameterized expressions; a value-bearing dynamic fragment throws `NotSupportedException`.

## Related links

- [Back to index](../README.md)
- [Entity Mapping · computed column definition](../core-usage/entity-mapping.en.md)
- [Data Permissions](./data-permission.en.md)
- [Tenant Isolation](./tenant-isolation.en.md)