# Data Permissions

Data permissions are, at bottom, translating "who is the current user and what role they hold" into query conditions. LiteOrm offers two ways, differing only in where the condition lives:

| Way | Where the condition lives | Best for |
| --- | --- | --- |
| Manually build an `Expr` | Written into the query, passed by the caller | Few entry points, a rule that is often debugged and unit-tested |
| `GenericSqlExpr` written into `ConstFilter` | Attached to the table definition, applied by every entry point | A rule reused across many entry points and fairly stable |

Both ways express the same role rule. This page runs one set of roles throughout: **admins see everything, managers see their own rows plus their department's, and regular employees see only their own.** The entity carries `OwnerId` (assignee) and `DeptId` (owning department); the current user provides `Id`, `DeptId`, `IsAdmin` and `IsManager`.

## Way 1: build the `Expr` manually

Collapse the rule into one condition-assembling function that returns a different condition per role, shared by lists, counts and exports:

```csharp
using static LiteOrm.Common.Expr;

public static class OrderScopes
{
    public static LogicExpr For(CurrentUser user)
    {
        if (user.IsAdmin) return null;                 // admins are unrestricted

        var own = Prop(nameof(Order.OwnerId)) == user.Id;

        if (user.IsManager)
            return own | (Prop(nameof(Order.DeptId)) == user.DeptId);

        return own;
    }
}
```

```csharp
var page = await orderService.SearchAsync(
    From<Order>().Where(OrderScopes.For(user) & Prop(nameof(Order.IsDeleted)) == false)
                 .OrderBy(Prop(nameof(Order.CreateTime)).Desc())
                 .Section(0, 20));

var total = await orderService.CountAsync(OrderScopes.For(user));
```

Notes:

- One function returns the conditions per role, so callers do not assemble their own branches. An admin gets `null`, which an `&` combination ignores, yielding the whole table.
- The manager branch is an `OR`; each sub-condition carries its own department or ownership. Do not split it into separate "managers query departments, employees query themselves" entry points.
- This only limits the table actually written into the query. Joined tables, the `WHERE` of `UpdateAll` / `DeleteAll`, and primary-key read paths are not covered automatically and need their own setup; bulk writes must embed `For(user)` in the same expression.

## Way 2: write a `GenericSqlExpr` into `ConstFilter`

Attach the condition to the table definition so every entry point applies it automatically. First register a fragment that returns one piece of SQL per role:

```csharp
using LiteOrm.Common;
using static LiteOrm.Common.Expr;

GenericSqlExpr.Register("OwnerScope", (context, sqlBuilder, outputParams, _) =>
{
    var user = UserContext.Current ?? throw new InvalidOperationException("User not resolved.");
    if (user.IsAdmin) return null;                     // admins produce no fragment

    // Build the equality conditions from PropertyExpr (Prop(...)); comparing against a concrete type uses operator overloading
    var own = Prop(nameof(Order.OwnerId)) == user.Id;

    var condition = user.IsManager
        ? own | (Prop(nameof(Order.DeptId)) == user.DeptId)
        : own;

    return condition.ToSql(context, sqlBuilder, outputParams);
});
```

Then set `ConstFilter` to that fragment. `TableDefinition.ConstFilter` is a publicly writable `get; set;` property, so assign it directly on the resolved table definition; no custom metadata provider is needed:

```csharp
foreach (var type in typeof(Order).Assembly.GetTypes())
{
    var tableDefinition = TableInfoProvider.Instance.GetTableDefinition(type);
    if (tableDefinition is null || !type.IsAssignableTo(typeof(IUserScoped))) continue;

    tableDefinition.ConstFilter &= Expr.Sql("OwnerScope");   // &= keeps existing conditions, adds the scoping rule
}
```

Notes:

- The role branch is recomputed at SQL-generation time: every query re-reads the current user, so switching roles takes effect immediately rather than from a snapshot taken at login.
- Column references are no longer hand concatenated as bare names. The delegate builds a `PropertyExpr` with `Prop(...)`, and the rendered SQL is produced by `ExprSqlConverter.ToSql`; columns pick up the current table alias automatically (e.g. `"T0"."OwnerId"`). Comparing against a concrete value (e.g. `user.Id`) uses the built-in operator overload, which parameterizes the value, so no explicit `Value(...)` or `outputParams` index bookkeeping is needed.
- The generated SQL is parameterized; values never land in the text. An admin returns `null`, so the fragment is ignored.

The underlying mechanics of `GenericSqlExpr` and `ConstFilter` — the coverage (primary-key reads, `JOIN ON`, `EXISTS`, `UPDATE` / `DELETE`), the performance cost, and the NativeAOT difference — match example 3 of [Tenant Isolation](./tenant-isolation.en.md), so they are not repeated here.

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
- [Tenant Isolation](./tenant-isolation.en.md)
- [Soft Deletes and Historical Data](./soft-delete-and-archive.en.md)
- [Audit and Change Tracking](./audit-and-change-tracking.en.md)
- [Security](../advanced-topics/security.en.md)