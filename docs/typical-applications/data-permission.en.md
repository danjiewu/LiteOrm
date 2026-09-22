# Data Permissions

Data permissions are, at bottom, translating "who is the current user and what role they hold" into query conditions. LiteOrm offers two ways, differing only in where the condition lives:

| Way | Where the condition lives | Best for |
| --- | --- | --- |
| Manually build an `Expr` | Written into the query, passed by the caller | Few entry points, a rule that is often debugged and unit-tested |
| `GenericSqlExpr` written into `ConstFilter` | Attached to the table definition, applied by every entry point | A rule reused across many entry points and fairly stable |

Both ways express the same role rule. We first set the scene with a concrete requirement, then implement it completely both ways.

## The requirement

An internal procurement system's "order query" page. The `Orders` table already exists:

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `long` | primary key, identity |
| `Title` | `string` | order title |
| `Amount` | `decimal` | amount |
| `OwnerId` | `int` | ordering user (owner) |
| `DeptId` | `int` | owning department |
| `Status` | `int` | order status |
| `CreateTime` | `DateTime` | placed-at time |

Roles and their visibility:

- **Admins** see every order.
- **Department managers** see all orders of their own department plus their own orders placed in other departments.
- **Regular employees** see only their own orders.

The current user comes from the authentication stack, gathered behind one `CurrentUserContext`:

```csharp
public sealed class CurrentUser
{
    public int Id { get; init; }         // user id
    public int DeptId { get; init; }     // department
    public bool IsAdmin { get; init; }
    public bool IsManager { get; init; }
}

// request-scoped: written by the login middleware, read at SQL-generation time
public static class CurrentUserContext
{
    private static readonly AsyncLocal<CurrentUser?> _current = new();
    public static CurrentUser? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
```

The order entity and the service declaration shared by both ways:

```csharp
[Table("Orders")]
public class Order
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("Title")] public string? Title { get; set; }
    [Column("Amount")] public decimal Amount { get; set; }
    [Column("OwnerId")] public int OwnerId { get; set; }
    [Column("DeptId")] public int DeptId { get; set; }
    [Column("Status")] public int Status { get; set; }
    [Column("CreateTime")] public DateTime CreateTime { get; set; }
}

// DI registration (either way)
builder.Services.AddScoped<IEntityViewServiceAsync<Order>, EntityService<Order>>();
```

The list page fetches "current page rows" and "total count" in two queries, both carrying a business condition (only in-progress orders) plus ordering and paging. Below is the implementation both ways.

## Way 1: build the `Expr` manually

Collapse the role rule into one condition-assembling function, shared by lists, counts and exports:

```csharp
using static LiteOrm.Common.Expr;

public static class OrderScopes
{
    public static LogicExpr? For(CurrentUser user)
    {
        if (user.IsAdmin) return null;                 // admins are unrestricted

        var own = Prop(nameof(Order.OwnerId)) == user.Id;

        return user.IsManager
            ? own | (Prop(nameof(Order.DeptId)) == user.DeptId)
            : own;
    }
}
```

The list page carries it explicitly:

```csharp
var user = CurrentUserContext.Current!;

var page = await orderService.SearchAsync(
    From<Order>()
        .Where(OrderScopes.For(user) & (Prop(nameof(Order.Status)) == 0))
        .OrderBy(Prop(nameof(Order.CreateTime)).Desc())
        .Section(0, 20));

var total = await orderService.CountAsync(OrderScopes.For(user) & (Prop(nameof(Order.Status)) == 0));
```

For employee `Id=1001` (not an admin), `SearchAsync` produces (SQL Server dialect, illustrative):

```sql
SELECT [T0].[Id], [T0].[Title], [T0].[Amount], [T0].[OwnerId],
       [T0].[DeptId], [T0].[Status], [T0].[CreateTime]
FROM [Orders] [T0]
WHERE ([T0].[OwnerId] = @0) AND ([T0].[Status] = @1)
ORDER BY [T0].[CreateTime] DESC
OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY
-- @0 = 1001, @1 = 0
```

For manager `Id=2001, DeptId=20`, `CountAsync` produces:

```sql
SELECT COUNT(*) FROM [Orders] [T0]
WHERE (([T0].[OwnerId] = @0) OR ([T0].[DeptId] = @1)) AND ([T0].[Status] = @2)
-- @0 = 2001, @1 = 20, @2 = 0
```

With `IsAdmin = true`, `For` returns `null`, the `&` combination ignores it, and only the business condition remains:

```sql
SELECT [T0].[Id], [T0].[Title], [T0].[Amount], [T0].[OwnerId],
       [T0].[DeptId], [T0].[Status], [T0].[CreateTime]
FROM [Orders] [T0]
WHERE ([T0].[Status] = @0)
ORDER BY [T0].[CreateTime] DESC
OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY
-- @0 = 0
```

Notes:

- One function returns the conditions per role, so callers do not assemble their own branches. An admin gets `null`, which an `&` combination ignores.
- The manager branch is an `OR`; each sub-condition carries its own department or ownership. Do not split it into separate "managers query departments, employees query themselves" entry points.
- This only limits the table actually written into the query. Joined tables, the `WHERE` of `UpdateAll` / `DeleteAll`, and primary-key read paths are not covered automatically and need their own setup; bulk writes must embed `For(user)` in the same expression.
- The condition sits right in the query — visible to readers, and breakpoints and unit tests all land on the single `OrderScopes.For` function.

## Way 2: write a `GenericSqlExpr` into `ConstFilter`

Once the rule has to reach dozens of entry points (lists, counts, exports, bulk writes, primary-key reads), passing the condition at each site means one missed site silently over-reads. Attach the condition to the table definition and every entry point applies it automatically.

First register a fragment that returns one piece of SQL per role:

```csharp
using LiteOrm.Common;
using static LiteOrm.Common.Expr;

GenericSqlExpr.Register("OwnerScope", (context, _) =>
{
    var user = CurrentUserContext.Current
        ?? throw new InvalidOperationException("User not resolved.");
    if (user.IsAdmin) return null;                     // admins produce no fragment

    var own = Prop(nameof(Order.OwnerId)) == user.Id;

    var condition = user.IsManager
        ? own | (Prop(nameof(Order.DeptId)) == user.DeptId)
        : own;

    return condition.ToSql(context);
});
```

Then set `ConstFilter` to that fragment. `TableDefinition.ConstFilter` is a publicly writable `get; set;` property, so assign it directly on the resolved table definition; no custom metadata provider is needed:

```csharp
public static class OrderScopeSetup
{
    public static void Enable()
    {
        var tableDefinition = TableInfoProvider.Instance.GetTableDefinition(typeof(Order))
            ?? throw new InvalidOperationException("Table definition not found.");
        tableDefinition.ConstFilter &= Expr.Sql("OwnerScope");   // &= keeps existing conditions, adds the scoping rule
    }
}
```

Call `OrderScopeSetup.Enable()` once at startup. From then on the list page writes only the business condition; the scope is injected automatically:

```csharp
var page = await orderService.SearchAsync(
    From<Order>()
        .Where(Prop(nameof(Order.Status)) == 0)
        .OrderBy(Prop(nameof(Order.CreateTime)).Desc())
        .Section(0, 20));

var order = await orderService.GetObjectAsync(12345);   // primary-key reads are scoped too
```

Employee `Id=1001`, `SearchAsync`:

```sql
SELECT [T0].[Id], [T0].[Title], [T0].[Amount], [T0].[OwnerId],
       [T0].[DeptId], [T0].[Status], [T0].[CreateTime]
FROM [Orders] [T0]
WHERE (([T0].[Status] = @0) AND ([T0].[OwnerId] = @1))
ORDER BY [T0].[CreateTime] DESC
OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY
-- @0 = 0, @1 = 1001
```

Manager `Id=2001, DeptId=20` — the fragment expands into the `OR` branch:

```sql
SELECT [T0].[Id], [T0].[Title], [T0].[Amount], [T0].[OwnerId],
       [T0].[DeptId], [T0].[Status], [T0].[CreateTime]
FROM [Orders] [T0]
WHERE (([T0].[Status] = @0) AND (([T0].[OwnerId] = @1) OR ([T0].[DeptId] = @2)))
ORDER BY [T0].[CreateTime] DESC
OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY
-- @0 = 0, @1 = 2001, @2 = 20
```

`GetObjectAsync(12345)` takes the primary-key read path and carries the condition too — rows outside the scope are simply not found:

```sql
SELECT [T0].[Id], [T0].[Title], [T0].[Amount], [T0].[OwnerId],
       [T0].[DeptId], [T0].[Status], [T0].[CreateTime]
FROM [Orders] [T0]
WHERE ([T0].[Id] = @0) AND ([T0].[OwnerId] = @1)
-- @0 = 12345, @1 = 1001
```

Admins produce no fragment, so the SQL is identical to the no-rule statement; but once a table definition declares `ConstFilter`, that table no longer reuses the predefined-command cache — regardless of who the current user is.

Notes:

- The role branch is recomputed at SQL-generation time: every query re-reads the current user, so switching roles takes effect immediately rather than from a snapshot taken at login.
- Column references are built as `PropertyExpr` with `Prop(...)` and rendered by `ExprSqlConverter.ToSql`; columns pick up the current table alias automatically (e.g. `[T0].[OwnerId]`). Comparing against a concrete value (e.g. `user.Id`) uses the built-in operator overload, which parameterizes the value, so no explicit `Value(...)` or `context.OutputParams` index bookkeeping is needed.
- The generated SQL is parameterized; values never land in the text. An admin returns `null`, so the fragment is ignored.
- `ConstFilter` does not only constrain the driving table: when this table is the joined (right) table of a `JOIN` or the target table of an `EXISTS` subquery, its `ConstFilter` is still applied, so the scope rides along every association path.
- A table that declares `ConstFilter` no longer reuses the predefined-command cache; every operation re-assembles its SQL. For the performance cost and the NativeAOT difference between the two ways, see example 3 of [Tenant Isolation](./tenant-isolation.en.md).

## Choosing between the two

| Scenario | Build the `Expr` by hand | `GenericSqlExpr` + `ConstFilter` |
| --- | --- | --- |
| One scope rule shared by a list, detail view and count, with few entry points | Good fit | Overkill: hanging a global table definition for one entry point |
| The same rule must reach dozens of queries, bulk writes and primary-key reads | Wire it in or set up another check at each site; miss one and it silently over-reads | Good fit: attaches to the definition and covers everything automatically |
| The rule changes often and you want to breakpoint and unit-test it | Good fit: you only change the assembling function | Awkward: the rule lives in the SQL-generation path and is hard to unit-test |
| You want the scope visible and readable in the query | Good fit: the condition sits right in the query | Hidden: the rule is buried in the table definition |
| Primary-key reads (`GetObjectAsync`) must be restricted too | Not covered; add object-level checks | Applied automatically |

In one sentence: use a hand-built `Expr` when there are few entry points, the rule changes often and you want it explicit in the query; use `ConstFilter` when the same rule must be stably reused across many entry points, including primary-key reads, joins and bulk writes.

## Primary-key reads fall inside the scope too

A condition attached as `ConstFilter` (Way 2) is recognized and applied by primary-key read paths such as `GetObject` / `GetObjectAsync` / `ExistsKey`. Way 1 (a hand-built `Expr`) does not cover primary-key reads, so `GetObjectAsync(id)` still needs its own ownership check (return `404` and `403` separately). Any object-level check must read from the master — a read replica returns lagging data and misjudges ownership. See [Concurrency and Read/Write Splitting](./concurrency-and-read-write-splitting.en.md).

## Related links

- [Back to index](../README.md)
- [Service Authorization](../di/service-authorization.en.md)
- [Tenant Isolation](./tenant-isolation.en.md)
- [Soft Deletes and Historical Data](./soft-delete-and-archive.en.md)
- [Audit and Change Tracking](./audit-and-change-tracking.en.md)
- [Security](../advanced-topics/security.en.md)
