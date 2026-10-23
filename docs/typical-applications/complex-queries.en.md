# Complex Queries in Practice: Multi-Role Joins, Dynamic Sorting, Paging, Dialect Tree SQL and Saved Conditions

The basics of `QuerySource`, `Join` / `JoinRelated`, Lambda, `Expr` and `ExprString` are covered elsewhere. This article is about **composition**: a work-order list page plus an organization-tree filter that ties together two roles of one employee table, header-driven sorting, one condition set shared by count and page, custom Lambda plus dialect tree SQL, and saved/reloaded conditions — then inspects the SQL offline with `SqlGen`.

All SQL below is actual rendered output (SQLite unless noted); parameters appear as `@0`, `@1`.

## Where to find the basics

| Capability | Document |
| --- | --- |
| `QuerySource<T>`, `Join` / `JoinRelated` chaining | [Query overview](../core-usage/query-overview.en.md) |
| Lambda syntax, multi-parameter Lambdas, captured-variable parameterization | [Lambda guide](../core-usage/lambda-guide.en.md) |
| Minimal `[TableJoin]`, `[ForeignType]`, `[ForeignColumn]` setup | [Associations](../core-usage/associations.en.md) |
| `RegisterMethodHandler` / `RegisterMemberHandler` | [Expression extension](../extensibility/expression-extension.en.md) |
| `Expr.Lambda`, `To<bool>()`, mixing Lambda and Expr | [Lambda and Expr](../core-usage/lambda-expr-mixing.en.md) |
| `ExprString` interpolation, embedded `Expr`, `[column]` placeholders | [ExprString guide](../core-usage/exprstring-guide.en.md) |
| Expr JSON shapes and operator markers | [Expr JSON serialization](../extensibility/expr-serialization.en.md) |
| Constructing Expr JSON on the client | [Native Expr queries](../extensibility/frontend-native-expr.en.md) |
| Dialect paging and `Skip` / `Take` basics | [Custom paging](../advanced-topics/custom-paging.en.md) |
| Grouping, aggregation and projection comparison | [Projection on demand](./query-projection.en.md) |
| CTE `With` / `UnionAll` | [CTE guide](../core-usage/cte-guide.en.md) |
| Dynamic table names and `TableArgs` | [Sharding and TableArgs](../advanced-topics/sharding-and-tableargs.en.md) |

## 1. Requirements and model

Requirements (also the acceptance points for later sections):

- Filter the work-order list by status, assignee level, reviewer level, created-time range and a title/code keyword.
- Sort by clicking a header: created time, priority, status, code, assignee name, reviewer name, either direction.
- Page `page` with `pageSize` rows; return **both** the total and the current page from the same condition set.
- A status bar on top summarizes counts by status (a second SQL statement).
- Organization tree: query a node and all its descendants; the work-order list can filter by that tree condition.
- Users can save the current filter as a view and reload it later, then keep layering conditions.

The model is work orders plus organizations. Assignee and reviewer both come from `Employees`, distinguished by **derived types that inherit the same entity**:

```csharp
public enum WorkOrderStatus { Open = 1, Processing = 2, Closed = 3 }

[Table("Employees")]
public class Employee
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)] public int Id { get; set; }
    [Column("Name")] public string Name { get; set; } = "";
    [Column("Level")] public int Level { get; set; }
}

/// <summary>Assignee: one business role of the Employees table.</summary>
public class Assignee : Employee { }

/// <summary>Reviewer: another business role of the Employees table.</summary>
public class Reviewer : Employee { }

[Table("OrgUnits")]
public class OrgUnit
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)] public int Id { get; set; }
    [Column("ParentId")] public int? ParentId { get; set; }
    [Column("Name")] public string Name { get; set; } = "";
    [Column("Level")] public int Level { get; set; }
}

[Table("WorkOrders")]
public class WorkOrder
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)] public int Id { get; set; }
    [Column("Code")] public string Code { get; set; } = "";
    [Column("Title")] public string Title { get; set; } = "";
    [Column("Status")] public int Status { get; set; }
    [Column("Priority")] public int Priority { get; set; }
    [Column("OrgUnitId")] public int OrgUnitId { get; set; }

    [Column("AssigneeId")]
    [ForeignType(typeof(Assignee), JoinType = TableJoinType.Inner)]
    public int AssigneeId { get; set; }

    [Column("ReviewerId")]
    [ForeignType(typeof(Reviewer), JoinType = TableJoinType.Left)]
    public int? ReviewerId { get; set; }

    [Column("CreatedTime")] public DateTime CreatedTime { get; set; }
    [Column("DueTime")] public DateTime DueTime { get; set; }
}

/// <summary>List view: display columns only; reads and writes still use WorkOrder.</summary>
public class WorkOrderView : WorkOrder
{
    [ForeignColumn(typeof(Assignee), Property = nameof(Employee.Name))]
    public string AssigneeName { get; set; } = "";

    [ForeignColumn(typeof(Reviewer), Property = nameof(Employee.Name))]
    public string? ReviewerName { get; set; }

    [ForeignColumn(typeof(Assignee), Property = nameof(Employee.Level))]
    public int AssigneeLevel { get; set; }

    [ForeignColumn(typeof(Reviewer), Property = nameof(Employee.Level))]
    public int? ReviewerLevel { get; set; }
}

public class WorkOrderListItem
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string AssigneeName { get; set; } = "";
    public string? ReviewerName { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedTime { get; set; }
}

public sealed class WorkOrderQuery
{
    public int Status { get; init; }
    public int MinAssigneeLevel { get; init; }
    public int MinReviewerLevel { get; init; }
    public int? OrgUnitId { get; init; }          // organization-tree filter, see section 4
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public string? Keyword { get; init; }
    public string? SortBy { get; init; }          // createdTime / priority / status / code / assigneeName / reviewerName
    public bool Desc { get; init; } = true;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
```

**Why derived types distinguish the relations** (said once, applies throughout):

- `[Table]` has `Inherited = true`, so `Assignee` / `Reviewer` inherit the table name `Employees` and all columns.
- Their `DefinitionType`s are `Assignee` and `Reviewer`. `JoinRelated` matches with `DefinitionType.IsAssignableFrom(foreignType)`, so sibling derived types never match each other.
- Joining from the base `Employee` finds nothing (`Assignee.IsAssignableFrom(Employee) == false`) and throws "No relation ... found in metadata".
- The same type with a different `Alias` does not work either: `BuildRelatedLogic` matches by type only, never by alias.
- Derived types express relations only and carry no `[ForeignColumn]`; display columns live on `WorkOrderView`, avoiding duplicate JOINs from automatic and explicit joins.

## 2. Two roles of one table with `JoinRelated`

```csharp
var query = Expr.From<WorkOrder>()
    .JoinRelated<Assignee>(TableJoinType.Inner)   // assignee: required, inner join
    .JoinRelated<Reviewer>(TableJoinType.Left)    // reviewer: nullable, keep orders without one
    .Where((w, assignee, reviewer) => w.Status != (int)WorkOrderStatus.Closed
                                   && assignee.Level >= 3)
    .Select((w, assignee, reviewer) => new WorkOrderListItem
    {
        Id = w.Id,
        Code = w.Code,
        Title = w.Title,
        AssigneeName = assignee.Name,
        ReviewerName = reviewer.Name,
        Priority = w.Priority,
        CreatedTime = w.CreatedTime
    });

List<WorkOrderListItem> items =
    await workOrderDAO.SearchAs<WorkOrderListItem>(query).ToListAsync(ct);
```

Rendered SQL (SQLite):

```sql
SELECT "T0"."Id", "T0"."Code", "T0"."Title",
       "T1"."Name" AS "AssigneeName", "T2"."Name" AS "ReviewerName",
       "T0"."Priority", "T0"."CreatedTime"
FROM "WorkOrders" "T0"
INNER JOIN "Employees" "T1" ON "T0"."AssigneeId" = "T1"."Id"
LEFT JOIN "Employees" "T2" ON "T0"."ReviewerId" = "T2"."Id"
WHERE "T0"."Status" <> 3 AND "T1"."Level" >= 3
```

Notes:

- No ON is written; `[ForeignType]` metadata supplies it. The second join falls back to the root `T0` to find its relation.
- The same physical table appears twice, separated by aliases `T1` / `T2`; no manual aliasing.
- After joining, the element is a flat tuple, and `Where` / `Select` take one parameter per table (`TupleQueryableExtensions`).
- To add conditions use `JoinRelated<T>(on, joinType)`; explicit and automatic conditions are combined with AND.
- Literal `3` inside the Lambda is inlined; an outer variable (e.g. `minLevel`) becomes parameter `@n`.
- Query methods live on `ObjectViewDAO<T>` (`ObjectDAO<T>` only writes); pass the chain directly to `SearchAs<TResult>(IQueryable<TResult>)` without converting it back to `Expr`.

## 3. List page: filtering, dynamic sorting, paging and one shared count

### 3.1 One shared filter

```csharp
using static LiteOrm.Common.Expr;

LogicExpr BuildFilter(WorkOrderQuery q)
{
    LogicExpr filter = Prop(nameof(WorkOrder.Status)) == q.Status;
    filter &= Prop(nameof(WorkOrderView.AssigneeLevel)) >= q.MinAssigneeLevel;
    filter &= Prop(nameof(WorkOrder.ReviewerId)).IsNull()
            | Prop(nameof(WorkOrderView.ReviewerLevel)) >= q.MinReviewerLevel;
    filter &= Prop(nameof(WorkOrder.CreatedTime)) >= q.From;
    filter &= Prop(nameof(WorkOrder.CreatedTime)) < q.To;

    if (q.OrgUnitId is int orgUnitId)                 // tree fragment from section 4
        filter &= Expr.Sql("OrgSubTree", orgUnitId);

    if (!string.IsNullOrWhiteSpace(q.Keyword))
        filter &= Expr.Sql("KeywordSearch", q.Keyword.Trim());

    return filter;
}
```

`filter` is built once and shared by count and page. `Prop(nameof(WorkOrderView.AssigneeLevel))` resolves to the view's joined column `"Assignee"."Level"`.

### 3.2 Header sorting: Expr instead of Lambda branching

```csharp
// Only whitelisted keys; never feed user input directly as a property name
private static readonly Dictionary<string, string> SortableFields =
    new(StringComparer.OrdinalIgnoreCase)
    {
        ["createdTime"]  = nameof(WorkOrder.CreatedTime),
        ["priority"]     = nameof(WorkOrder.Priority),
        ["status"]       = nameof(WorkOrder.Status),
        ["code"]         = nameof(WorkOrder.Code),
        ["assigneeName"] = nameof(WorkOrderView.AssigneeName),
        ["reviewerName"] = nameof(WorkOrderView.ReviewerName),
    };

OrderByItemExpr[] BuildOrderBys(WorkOrderQuery q)
{
    var items = new List<OrderByItemExpr>(2);
    if (q.SortBy is not null && SortableFields.TryGetValue(q.SortBy, out string? prop))
        items.Add(q.Desc ? Prop(prop).Desc() : Prop(prop).Asc());

    items.Add(Prop(nameof(WorkOrder.Id)).Desc());   // unique-key tiebreaker for stable page boundaries
    return items.ToArray();
}
```

With Lambda, every field × direction needs its own branch:

```csharp
IOrderedQueryable<WorkOrderView> ordered = q.SortBy switch
{
    "createdTime"  => q.Desc ? src.OrderByDescending(v => v.CreatedTime) : src.OrderBy(v => v.CreatedTime),
    "priority"     => q.Desc ? src.OrderByDescending(v => v.Priority) : src.OrderBy(v => v.Priority),
    // ... six fields × two directions = 12 branches
    _              => src.OrderByDescending(v => v.Id)
};
ordered = ordered.ThenByDescending(v => v.Id);
```

The Expr version maps "field name → expression" once and flips direction with `Desc()` / `Asc()`; a new sortable column is one whitelist line.

### 3.3 One `filter` for total and page

```csharp
SelectExpr BuildPageExpr(WorkOrderQuery q, LogicExpr filter)
{
    int skip = (q.Page - 1) * q.PageSize;
    return Expr.From(typeof(WorkOrderView))
        .Where(filter)
        .OrderBy(BuildOrderBys(q))
        .Section(skip, q.PageSize)
        .Select(
            new SelectItemExpr(Prop(nameof(WorkOrder.Id)), nameof(WorkOrderListItem.Id)),
            new SelectItemExpr(Prop(nameof(WorkOrder.Code)), nameof(WorkOrderListItem.Code)),
            new SelectItemExpr(Prop(nameof(WorkOrder.Title)), nameof(WorkOrderListItem.Title)),
            new SelectItemExpr(Prop(nameof(WorkOrderView.AssigneeName)), nameof(WorkOrderListItem.AssigneeName)),
            new SelectItemExpr(Prop(nameof(WorkOrderView.ReviewerName)), nameof(WorkOrderListItem.ReviewerName)),
            new SelectItemExpr(Prop(nameof(WorkOrder.Priority)), nameof(WorkOrderListItem.Priority)),
            new SelectItemExpr(Prop(nameof(WorkOrder.CreatedTime)), nameof(WorkOrderListItem.CreatedTime)));
}

LogicExpr filter = BuildFilter(q);

// Total: no ORDER BY / paging
int total = await workOrderService.CountAsync(filter, cancellationToken: ct);

// Page: Where → OrderBy → Section → Select
List<WorkOrderListItem> page = await workOrderService
    .SearchAsAsync<WorkOrderListItem>(BuildPageExpr(q, filter), cancellationToken: ct);
```

Count SQL (SQLite; tree and keyword fragments elided with `...`):

```sql
SELECT Count(1)
FROM "WorkOrders" "T0"
INNER JOIN "Employees" "Assignee" ON "T0"."AssigneeId" = "Assignee"."Id"
LEFT JOIN "Employees" "Reviewer" ON "T0"."ReviewerId" = "Reviewer"."Id"
WHERE "T0"."Status" = @0
  AND "Assignee"."Level" >= @1
  AND ("T0"."ReviewerId" IS NULL OR "Reviewer"."Level" >= @2)
  AND "T0"."CreatedTime" >= @3 AND "T0"."CreatedTime" < @4
  AND ...tree subquery...
  AND ...keyword fragment...
```

Page SQL (`SortBy = "assigneeName"`, `Desc = true`, `Page = 2`, `PageSize = 20`):

```sql
SELECT "T0"."Id", "T0"."Code", "T0"."Title",
       "Assignee"."Name" AS "AssigneeName", "Reviewer"."Name" AS "ReviewerName",
       "T0"."Priority", "T0"."CreatedTime"
FROM "WorkOrders" "T0"
INNER JOIN "Employees" "Assignee" ON "T0"."AssigneeId" = "Assignee"."Id"
LEFT JOIN "Employees" "Reviewer" ON "T0"."ReviewerId" = "Reviewer"."Id"
WHERE ...
ORDER BY "Assignee"."Name" DESC, "T0"."Id" DESC
LIMIT 20 OFFSET 20
```

Changing `SortBy` only changes `ORDER BY`; with `SortBy = "createdTime"`, `Desc = false`, page 1 and 10 rows, it becomes `ORDER BY "T0"."CreatedTime", "T0"."Id" DESC` + `LIMIT 10`.

Notes:

- Build `filter` once and share it; the count branch must not carry `OrderBy` / `Skip` / `Take`.
- Whitelist the sort field first; a raw `Prop(userInput)` hands a column name to the user and must be forbidden.
- Add `Id` as the last sort key: duplicates can otherwise cause repeated or missing rows across pages.
- Both joins are many-to-one, so `Count(1)` is not inflated; with a one-to-many join use `Count(DISTINCT "T0"."Id")`.
- View JOIN types come from `[ForeignType]` metadata (assignee Inner, reviewer Left), matching section 2's explicit joins.
- `Section` renders as `LIMIT n OFFSET m` on SQLite and is not parameterized; MySQL uses `LIMIT m,n`, Oracle uses `OFFSET ... ROWS FETCH NEXT ... ROWS ONLY` (see [Custom paging](../advanced-topics/custom-paging.en.md)).
- Count and page are two executions; wrap them in one transaction or snapshot read when strict consistency matters.

## 4. Tree queries: custom Lambda parsing and dialect SQL from `GenericSqlExpr`

Goal: write `u.InSubTreeOf(rootId)` and get `START WITH ... CONNECT BY` on Oracle, recursive CTE on MySQL / SQLite / PostgreSQL.

### 4.1 Business method (compiles; never runs in memory)

```csharp
public static class OrgUnitExtensions
{
    /// <summary>Whether the unit lies in rootId's subtree (including itself).</summary>
    public static bool InSubTreeOf(this OrgUnit unit, int rootId)
        => unit.Id == rootId;   // placeholder; the parser takes over during queries
}
```

### 4.2 Register custom Lambda parsing: expand the method into `GenericSqlExpr`

```csharp
static WorkOrderQueries()
{
    // Register the fragment before the Lambda handler: Expr.Sql requires the key at parse time
    RegisterOrgSubTree();

    LambdaExprConverter.RegisterMethodHandler(
        typeof(OrgUnitExtensions), nameof(OrgUnitExtensions.InSubTreeOf),
        (node, converter) =>
        {
            // Extension methods are static calls in expression trees: node.Object == null,
            // the receiver is Arguments[0] and the root Id is Arguments[1].
            // Hand the argument expression straight to GenericSqlExpr: parameterization, dialect
            // and serialization are all handled downstream.
            return Expr.Sql("OrgSubTree", converter.Convert(node.Arguments[1]));
        });
}
```

### 4.3 `GenericSqlExpr`: emit the parameter from Expr, build the recursive CTE with Expr then `ToSql`

```csharp
static void RegisterOrgSubTree()
{
    GenericSqlExpr.Register("OrgSubTree", (context, arg) =>
    {
        // Lambda parsing passes an Expr; a reloaded condition may pass a raw value
        ValueTypeExpr root = arg switch
        {
            ValueTypeExpr value => value,
            null => throw new NotSupportedException("OrgSubTree needs a root node argument."),
            _ => Expr.Value(arg)          // rendered as a parameter, never inlined
        };

        ISqlBuilder builder = context.SqlBuilder;

        // The same fragment serves OrgUnit (Id) and WorkOrderView (OrgUnitId)
        string column = context.Table?.DefinitionType == typeof(OrgUnit)
            ? nameof(OrgUnit.Id)
            : nameof(WorkOrder.OrgUnitId);
        string outerColumn = context.SingleTable || context.DefaultTableAliasName is null
            ? builder.ToSqlName(column)
            : $"{builder.ToSqlName(context.DefaultTableAliasName)}.{builder.ToSqlName(column)}";

        if (builder is OracleBuilder)
        {
            // Oracle hierarchical queries cannot be expressed with Expr, so write it by hand;
            // the root is still emitted by Expr as :0 rather than constructing Param manually
            string table = builder.ToSqlName(
                TableInfoProvider.Instance.GetTableDefinition(typeof(OrgUnit)).Name!);
            string id = builder.ToSqlName(nameof(OrgUnit.Id));
            string parentId = builder.ToSqlName(nameof(OrgUnit.ParentId));
            string rootSql = root.ToSql(context);

            return $"{outerColumn} IN (" +
                   $"SELECT {id} FROM {table} " +
                   $"START WITH {id} = {rootSql} " +
                   $"CONNECT BY PRIOR {id} = {parentId})";
        }

        // MySQL / SQLite / PostgreSQL: build the recursive CTE with Expr, emit via ToSql
        using (context.BeginScope())   // CTE rendering rewrites the default alias; isolate it
        {
            return $"{outerColumn} IN ({BuildOrgSubTreeQuery(root).ToSql(context)})";
        }
    });
}

/// <summary>Org subtree query: anchor plus self-referencing recursion, wrapped as a CTE.</summary>
static SelectExpr BuildOrgSubTreeQuery(ValueTypeExpr rootId)
{
    SelectExpr anchor = Expr.From(typeof(OrgUnit))
        .Where(Expr.Prop(nameof(OrgUnit.Id)) == rootId)
        .Select(new SelectItemExpr(Expr.Prop(nameof(OrgUnit.Id)), nameof(OrgUnit.Id)));

    var selfRef = new CommonTableExpr { Alias = "OrgTree" };

    SelectExpr recursive = Expr.From(typeof(OrgUnit))
        .Join(selfRef, Expr.Prop(nameof(OrgUnit.ParentId)) == Expr.Prop("OrgTree", nameof(OrgUnit.Id)))
        .Select(new SelectItemExpr(Expr.Prop(nameof(OrgUnit.Id)), nameof(OrgUnit.Id)));

    return anchor.UnionAll(recursive)
        .With("OrgTree")
        .Select(new SelectItemExpr(Expr.Prop(nameof(OrgUnit.Id)), nameof(OrgUnit.Id)));
}
```

### 4.4 Two call styles, one SQL shape

```csharp
int rootId = 1001;

// Style 1: business method inside a Lambda (the parser expands it to the OrgSubTree fragment)
List<OrgUnit> nodes = await orgUnitDAO
    .Search(q => q.Where(u => u.InSubTreeOf(rootId))
                  .OrderBy(u => u.Level))
    .ToListAsync(ct);

// Style 2: build Expr directly (good for dynamic conditions and saved conditions)
List<OrgUnit> sameNodes = await orgUnitDAO
    .Search(Expr.Sql("OrgSubTree", rootId))
    .ToListAsync(ct);
```

Oracle (`OracleBuilder`; Oracle uppercases identifiers):

```sql
SELECT "T0"."ID", "T0"."PARENTID", "T0"."NAME", "T0"."LEVEL"
FROM "ORGUNITS" "T0"
WHERE "T0"."ID" IN (
    SELECT "ID" FROM "ORGUNITS"
    START WITH "ID" = :0
    CONNECT BY PRIOR "ID" = "PARENTID"
)
ORDER BY "T0"."LEVEL"
```

SQLite / MySQL / PostgreSQL (recursive CTE built with Expr; aliases are measured: anchor `T1`, recursive `T2`, the CTE itself gets no extra alias):

```sql
SELECT "T0"."Id", "T0"."ParentId", "T0"."Name", "T0"."Level"
FROM "OrgUnits" "T0"
WHERE "T0"."Id" IN (
    WITH RECURSIVE "OrgTree" AS (
        SELECT "T1"."Id"
        FROM "OrgUnits" "T1"
        WHERE "T1"."Id" = @0
        UNION ALL
        SELECT "T2"."Id"
        FROM "OrgUnits" "T2"
        LEFT JOIN "OrgTree" ON "T2"."ParentId" = "OrgTree"."Id"
    )
    SELECT "Id" FROM "OrgTree"
)
ORDER BY "T0"."Level"
```

Reusing the fragment in the work-order list switches the outer column to `OrgUnitId`:

```csharp
// Equivalent to filter &= Expr.Sql("OrgSubTree", orgUnitId) from section 3
// Produces: "T0"."OrgUnitId" IN (...tree subquery...)
List<WorkOrderListItem> page = await workOrderDAO
    .SearchAs<WorkOrderListItem>(BuildPageExpr(q, BuildFilter(q)))
    .ToListAsync(ct);
```

### 4.5 Notes

- The outer column comes from `context.Table.DefinitionType`: `Id` for `OrgUnit`, `OrgUnitId` otherwise, with the current default alias; single-table mode (`SingleTable`) omits the prefix.
- The Oracle branch obtains the table name through `TableInfoProvider` + `FormatTableName`, so sharded `TableArgs` apply automatically.
- Every parameter is emitted by Expr: Oracle uses `root.ToSql(context)` to get `:0`; the recursive CTE's root participates in `Where` as a value expression and `ToSql` appends it. Do not concatenate values or construct `Param` by hand.
- Build the CTE with Expr (`From → Where → Select`, `UnionAll`, `With("OrgTree")`) then call `ToSql(context)`; aliases, parameters and the `RECURSIVE` keyword follow framework rules.
- CTE rendering rewrites `DefaultTableAliasName`, so it must run inside `context.BeginScope()` to keep outer conditions untouched.
- `Arg` may be an Expr (from Lambda parsing) or a raw value (after reload); the leading `switch` normalizes it to `ValueTypeExpr`.
- `Expr.Sql(key, arg)` requires the key to be registered at parse time; register in a static constructor or during startup.
- Whether `Arg` can be saved as Expr JSON depends on its type: raw values can, `ValueExpr` cannot (see 5.5).
- For cyclic data add `NOCYCLE` on Oracle, or carry `Depth` in the recursive part and cap it on MySQL / SQLite.

### 4.6 Also useful: a reusable multi-column keyword fragment

Outside tree SQL, a generic fragment only needs parameters, not dialect awareness:

```csharp
GenericSqlExpr.Register("KeywordSearch", (context, arg) =>
{
    string keyword = arg as string ?? string.Empty;
    // Emit the parameter through Expr, same rules as the tree fragment
    string param = Expr.Value($"%{keyword}%").ToSql(context);
    string title = context.SqlBuilder.ToSqlName(nameof(WorkOrder.Title));
    string code = context.SqlBuilder.ToSqlName(nameof(WorkOrder.Code));
    return $"({title} LIKE {param} OR {code} LIKE {param})";
});
```

```sql
WHERE ("Title" LIKE @0 OR "Code" LIKE @0)
```

Both columns share one parameter; only the tree fragment needs dialect and table names.

## 5. Saving and reloading historical query conditions: choosing functions vs parameters

### 5.1 Save (conditions only, no paging)

```csharp
public sealed class SavedView
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string FilterJson { get; set; } = "";   // stores the LogicExpr JSON
    public int OwnerId { get; set; }
}

static readonly JsonSerializerOptions SaveOptions = new()
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};

// The user clicks "Save view"
string json = JsonSerializer.Serialize<Expr>(BuildFilter(q), SaveOptions);
```

The resulting condition JSON:

```json
{
  "$and": [
    { "$": "==", "Left": { "#": "Status" }, "Right": { "@": 1 } },
    { "$": "contains", "Left": { "#": "Title" }, "Right": { "@": "invoice" } },
    { "$": "sql", "Key": "OrgSubTree", "Arg": 1001 }
  ]
}
```

(Actual output is one compact line; wrapped here for readability. `{"@": {"$datetime": "..."}}` is a `DateTime` constant, `{"$": "func", "Now": []}` is a database function.)

### 5.2 Reload and keep layering conditions

```csharp
// Open a saved view
LogicExpr? saved = JsonSerializer.Deserialize<Expr>(savedView.FilterJson)?.AsLogic()
    ?? throw new InvalidDataException("The saved condition cannot be parsed.");

// Combine the historical condition with the current request
LogicExpr filter = saved & BuildFilter(q);

int total = await workOrderService.CountAsync(filter, cancellationToken: ct);
List<WorkOrderListItem> page = await workOrderService
    .SearchAsAsync<WorkOrderListItem>(BuildPageExpr(q, filter), cancellationToken: ct);
```

Sorting and paging are not persisted; the current request decides them. The saved condition only decides which rows qualify.

### 5.3 Two outcomes for `DateTime.Now` after reload

The same time condition behaves completely differently after save/reload depending on how it is written:

```csharp
// A. Lambda: parses to a database function, evaluated by the database on every execution
List<WorkOrder> byFunction = await workOrderDAO
    .Search(q => q.Where(w => w.DueTime < DateTime.Now))
    .ToListAsync(ct);

// B. Expr + Const: parses to a parameter whose value is fixed at build time
LogicExpr frozen = Prop(nameof(WorkOrder.DueTime)) < Expr.Const(DateTime.Now);

// C. Expr + Now(): parses to a database function and survives JSON round-trips
LogicExpr dynamicExpr = Prop(nameof(WorkOrder.DueTime)) < Expr.Now();
```

Rendered SQL (SQLite):

```sql
-- A / C
"T0"."DueTime" < datetime('now', 'localtime')

-- B
"T0"."DueTime" < @0        -- parameter value: 2026-10-23T10:30:45.123
```

Oracle / MySQL:

```sql
-- A / C
"T0"."DUETIME" < sysdate          -- Oracle
`T0`.`DueTime` < NOW()            -- MySQL
```

After saving to JSON:

```json
// B: the time is frozen at save time
{ "$": "<", "Left": { "#": "DueTime" }, "Right": { "$datetime": "2026-10-23T10:30:45.123+08:00" } }

// C: the function is preserved and stays dynamic after reload
{ "$": "<", "Left": { "#": "DueTime" }, "Right": { "$": "func", "Now": [] } }
```

Comparison:

| Style | Emitted form | Evaluation time | After save and reload |
| --- | --- | --- | --- |
| `w => w.DueTime < DateTime.Now` | dialect function (`datetime('now','localtime')` / `NOW()` / `sysdate`) | every execution, database clock | convert with `Expr.Lambda<T>(...)` first, then saveable; still a function |
| `Prop(...) < Expr.Const(DateTime.Now)` | parameter `@0` (DateTime is never inlined) | build time, application clock | JSON holds a fixed timestamp; never changes after reload |
| `Prop(...) < Expr.Now()` | dialect function | every execution, database clock | JSON keeps `{"$":"func","Now":[]}`; stays dynamic |
| `var now = DateTime.Now;` outside the Lambda | parameter | assignment time | fixed value (basics in the [Lambda guide](../core-usage/lambda-guide.en.md)) |

### 5.4 Which to use after reloading a saved condition

| Scenario | Function or parameter | Style | Behaviour after reload |
| --- | --- | --- | --- |
| Last 7 days, overdue, pending reminders | function | `Expr.Now()` | reads the database time on every execution |
| Report snapshots, audit replay, reconciliation | parameter | `Expr.Const(DateTime.Now)` | frozen at save time |
| Absolute date range picked by the user | parameter | `Prop(...) >= q.From` (outer variable) | stays at the value the user chose |
| Must follow the database time zone | function | `Expr.Now()` | follows the database time zone |
| Must follow the application time zone | parameter | `Expr.Const(DateTime.Now)` | follows the application time zone |

In one sentence: use a function for "relative to execution time", use a parameter for "a snapshot at save time".

### 5.5 Notes

- What is saved is an `Expr`, not an `Expression<Func<T,bool>>`; when starting from a Lambda, call `Expr.Lambda<T>(...)` first to get a serializable `LogicExpr`.
- Save only the condition (`LogicExpr`), not a whole `SelectExpr`: paging and sorting belong to the current request. Save a `SelectExpr` only when the entire view must be replayed.
- `GenericSqlExpr` restores only `Key` + `Arg`; the loading side must register the same key before rendering.
- **`Arg` must be a JSON-serializable raw value.** The `Arg` produced by Lambda parsing is a `ValueExpr` (an `Expr` object); const or not, `JsonSerializer.Serialize` throws `InvalidOperationException: Type 'LiteOrm.Common.ValueExpr' is not registered in ExprJsonSerializerContext.` To save a condition that contains the tree fragment, build it from a raw value with `Expr.Sql("OrgSubTree", rootId)` (as section 3 does); the JSON is `{"$":"sql","Key":"OrgSubTree","Arg":1001}` and `Arg` is still an `int` after reload.
- The difference between "function" and "parameter" is not SQL length but **where evaluation happens**: functions in the database, parameters in the application.
- Align time zones and precision: `sysdate` / `NOW()` / `datetime('now','localtime')` use the database time zone, `DateTime.Now` / `Expr.Const` use the application time zone; unify across deployments.
- `DateTime.Today` behaves the same: `CURRENT_DATE` / `curdate()` / `trunc(sysdate)` in a Lambda, a fixed date as a parameter.
- `ExprString` / `RawSql` are not serializable; keep them out of saved conditions.
- Prefix saved JSON with a version (e.g. wrap it in `"v": 1`) so the shape can evolve.
- Format details and markers: [Expr JSON serialization](../extensibility/expr-serialization.en.md); client-side construction: [Native Expr queries](../extensibility/frontend-native-expr.en.md).

## 6. Mixing Lambda, Expr and ExprString: three styles in one endpoint

```csharp
public async Task<WorkOrderPageResult> GetPageAsync(WorkOrderQuery q, CancellationToken ct)
{
    // 1) Fixed business baseline as a Lambda, converted into a composable Expr
    LogicExpr filter = Expr.Lambda<WorkOrderView>(
        v => v.Status != (int)WorkOrderStatus.Closed)!;

    // 2) Dynamic filters and sorting layered on with Expr
    filter &= BuildFilter(q);
    OrderByItemExpr[] orderBys = BuildOrderBys(q);

    // 3) One condition set: total + current page
    int total = await workOrderService.CountAsync(filter, cancellationToken: ct);
    List<WorkOrderListItem> items = await workOrderService
        .SearchAsAsync<WorkOrderListItem>(BuildPageExpr(q, filter), cancellationToken: ct);

    // 4) Status bar: a custom DAO writes GROUP BY with ExprString, embedding the same filter
    List<StatusSummary> summary = await workOrderDAO.QueryStatusSummaryAsync(filter, ct);

    return new WorkOrderPageResult(total, items, summary);
}
```

Custom DAO (`ObjectViewDAO<WorkOrderView>`):

```csharp
public class WorkOrderDAO : ObjectViewDAO<WorkOrderView>
{
    public WorkOrderDAO(SessionManager sessionManager) : base(sessionManager) { }

    public Task<List<StatusSummary>> QueryStatusSummaryAsync(LogicExpr filter, CancellationToken ct = default)
        => SearchAs<StatusSummary>(
                $"SELECT [Status], COUNT(1) AS [Total] FROM {From} WHERE {filter} GROUP BY [Status]")
            .ToListAsync(ct);
}

public sealed class StatusSummary
{
    public int Status { get; set; }
    public int Total { get; set; }
}
```

The second SQL statement (SQLite):

```sql
SELECT "Status", COUNT(1) AS "Total"
FROM "WorkOrders" "T0"
INNER JOIN "Employees" "Assignee" ON "T0"."AssigneeId" = "Assignee"."Id"
LEFT JOIN "Employees" "Reviewer" ON "T0"."ReviewerId" = "Reviewer"."Id"
WHERE "T0"."Status" = @0
  AND "Assignee"."Level" >= @1
  AND ("T0"."ReviewerId" IS NULL OR "Reviewer"."Level" >= @2)
  ...
GROUP BY "Status"
```

`{From}` is a DAO marker (the whole FROM clause, including view JOINs) while `filter` is an `Expr` interpolated in; both use the DAO's view metadata, so joined-column aliases such as `"Assignee"."Level"` line up.

Choosing a style:

| Scenario | Style | Why |
| --- | --- | --- |
| Fixed business baseline | Lambda or `Expr.Lambda<T>` | readable, refactorable, compile-time checked |
| Filters driven by request parameters | Expr | number and combination decided at runtime |
| Header sorting | Expr (whitelist + `Asc` / `Desc`) | one mapping instead of a switch explosion |
| Coexisting with Lambda queries | `expr.To<bool>()` | embeds a dynamic fragment into a typed query |
| Repeated, parameterized SQL fragments | `GenericSqlExpr` | register once, parameterize uniformly, render per dialect |
| Aggregates / UNION / dialect-specific SQL | `ExprString` | statements Expr cannot express, still reusing `{From}` and embedding `Expr` |
| Saving historical conditions | Expr JSON | Lambda / ExprString cannot be persisted directly |
| Inspecting the final SQL | `SqlGen` | offline, dialect-switchable |

Notes:

- `Expr.Lambda<T>` turns a Lambda into a `LogicExpr`, which can then be combined with request conditions using `&` / `|`.
- An `Expr` embedded in `ExprString` keeps Expr parameterization; plain values become named parameters; `[Status]` is replaced with the dialect's quoting.
- `{From}` and embedded `Expr`s share the DAO's build context; if aliases disagree, check whether DAOs for different views are mixed.
- `ExprString` is only available at the DAO layer; services do not expose it. Subclass a DAO when needed (inject `SessionManager` through the constructor).

## 7. How this compares with other ORMs

Wording is deliberately cautious; capabilities vary by version:

| Requirement | LiteOrm | Typical EF Core | Typical Dapper |
| --- | --- | --- | --- |
| Two business relations on one table | two derived types + `[ForeignType]` on the columns; `JoinRelated<Assignee>()` / `JoinRelated<Reviewer>()` fill ON | two navigation properties or two `HasOne`s; write two `Join`s with ON by hand | hand-written joins with two aliases and manual mapping |
| View auto-joins from the same metadata | `[ForeignColumn]` on `WorkOrderView` reuses the derived types | projection + `Include` or manual `Join` | manual JOIN + column aliases |
| Header-click dynamic sorting | whitelist + `OrderByItemExpr[]`, one line per field | switch + separate `OrderBy` branches, or Dynamic LINQ | string concatenation, no compile-time checks |
| Total and page from one condition | one `filter` used twice (`CountAsync` / `Where`) | keep two `IQueryable` branches or duplicate the condition | two SQL statements with duplicated conditions |
| Dialect-aware tree queries | `GenericSqlExpr` + `context.SqlBuilder` branches (`CONNECT BY` / `WITH RECURSIVE`) | raw SQL or `FromSql`, handling dialects yourself | dialect SQL by hand |
| Business methods in SQL | one `RegisterMethodHandler` | custom methods must be translatable or fall back to client evaluation | SQL functions by hand |
| Saving and reloading conditions | `Expr` JSON round-trip, including `GenericSqlExpr` key / arg | expression trees have no built-in serialization; usually store a DTO and rebuild | no equivalent; store SQL yourself |
| Offline SQL for any dialect | `SqlGen` with another `SqlBuilder` | `ToQueryString()` depends on the current provider | the SQL is already hand-written |

Only the differences: LiteOrm's distinguishing points are **metadata-supplied ON clauses, one parameter per table after joining, one condition reused directly, serializable query conditions, and Expr rendered offline for any dialect**.

## 8. Debugging queries with SqlGen

### 8.1 Minimal usage

```csharp
using LiteOrm;
using LiteOrm.CodeGen;
using LiteOrm.Common;

var sqlGen = new SqlGen(typeof(WorkOrderView))
{
    SqlBuilder = SQLiteBuilder.Instance          // defaults to the generic SqlBuilder; set the dialect explicitly
};

PreparedSql prepared = sqlGen.ToSql(BuildPageExpr(q, filter));

Console.WriteLine(prepared.Sql);
foreach (Param p in prepared.Params)
    Console.WriteLine($"{p.Name} = {p.Value} ({p.DbType})");
```

Functions and Lambda handlers initialize automatically on first use of `SqlBuilder` / `LambdaExprConverter`; a standalone console app needs no explicit `Initialize` call.

### 8.2 Debugging the page SQL

The chain must end in a `SelectExpr` (only `Select` / `Update` / `Delete` produce complete statements). Vary `SortBy` to compare `ORDER BY`:

```csharp
foreach (string sortBy in new[] { "createdTime", "priority", "assigneeName" })
{
    var probe = new WorkOrderQuery { SortBy = sortBy, Page = 2, PageSize = 20 /* ... */ };
    Console.WriteLine(sqlGen.ToSql(BuildPageExpr(probe, BuildFilter(probe))).Sql);
}
```

### 8.3 Debugging count

Same rule as `CountAsync`: `ToSource` first, then wrap in `Count(1)`.

```csharp
Expr countExpr = new SelectExpr(
    filter.ToSource<WorkOrderView>(),
    new SelectItemExpr(Expr.Aggregate("Count", Expr.Const(1))));

PreparedSql countSql = sqlGen.ToSql(countExpr);
```

### 8.4 Debugging a `JoinRelated` query

Take `GetQueryExpr()` at the end of the chain and hand it to SqlGen:

```csharp
SqlSegment joinExpr = query.GetQueryExpr();   // the IQueryable chain from section 2
PreparedSql joinSql = sqlGen.ToSql(joinExpr);
```

### 8.5 Debugging tree fragments: one Expr, several dialects, no database

```csharp
// In a standalone console app trigger registration first (static constructor or an explicit call)
RegisterOrgSubTree();

var treeExpr = new SelectExpr(
    Expr.From(typeof(OrgUnit)).Where(Expr.Sql("OrgSubTree", 1001)),
    new SelectItemExpr(Expr.Prop(nameof(OrgUnit.Id)), nameof(OrgUnit.Id)));

var treeGen = new SqlGen(typeof(OrgUnit)) { SqlBuilder = OracleBuilder.Instance };
Console.WriteLine(treeGen.ToSql(treeExpr).Sql);          // START WITH ... CONNECT BY

treeGen.SqlBuilder = SQLiteBuilder.Instance;
Console.WriteLine(treeGen.ToSql(treeExpr).Sql);          // WITH RECURSIVE (built with Expr)
```

### 8.6 Debugging saved / reloaded conditions

```csharp
string json = JsonSerializer.Serialize<Expr>(filter);
Expr? loaded = JsonSerializer.Deserialize<Expr>(json);
if (loaded is null) throw new InvalidDataException("Deserialization failed");

Console.WriteLine(json);
Console.WriteLine(sqlGen.ToSql(loaded).Sql);   // should match the original filter's SQL
```

### 8.7 Switching dialects

```csharp
sqlGen.SqlBuilder = MySqlBuilder.Instance;     // or PostgreSqlBuilder / OracleBuilder / SqlServerBuilder
PreparedSql mysql = sqlGen.ToSql(BuildPageExpr(q, filter));
```

With one Expr and another builder you can compare quoting, paging syntax and parameter placeholders; for sharded entities assign `sqlGen.TableArgs` (see [Sharding and TableArgs](../advanced-topics/sharding-and-tableargs.en.md)).

### 8.8 Comparing with runtime SQL

```csharp
string? lastSql = SessionManager.Current?.SqlStack.LastOrDefault();
```

- `SqlStack` holds `CommandText` only, no parameter values; read parameter values from the offline `PreparedSql.Params` or logs.
- At most 10 entries; cleared after a service call, so read immediately.
- When offline and runtime SQL differ, check in order: `TableArgs`, `SqlBuilder` dialect, root table alias, service-level `ConstFilter`, parameter values and `DbType`.

### 8.9 Checklist

- Call `ToSql` first to inspect structure and parameters, then execute; never reconstruct SQL by string concatenation.
- The chain must end in `Select`, otherwise you only get a fragment.
- Paging numbers are inlined in SQL; a missing parameter for them is expected.
- For joins, check the number of JOIN clauses and their ONs before WHERE; for dynamic sorting, verify ORDER BY hits the whitelist.
- If a custom Lambda / `GenericSqlExpr` misbehaves, first check whether the emitted fragment is empty (registration did not take effect).
- For tree queries confirm the dialect branch: Oracle should show `CONNECT BY`, others `WITH RECURSIVE`.
- A CTE self-reference uses only its alias (e.g. `LEFT JOIN "OrgTree" ON ...`); no extra `T3` alias is appended.
- If a deserialized condition fails to render, check that the `GenericSqlExpr` key is registered in this process and that `Arg` is a serializable raw value.

## 9. Limitations and related links

- Derived types must inherit an entity marked with `[Table]`; joining from the base class finds nothing.
- `JoinRelated` relies on `[ForeignType]` / `[TableJoin]` metadata and throws when no relation can be inferred; fall back to `Join` with an explicit condition only then.
- Tuple joins support at most 7 tables.
- Dynamic sorting must go through a whitelist; never pass a request field straight to `Expr.Prop`.
- `GenericSqlExpr` suits fragments with a fixed shape; when the condition structure changes with the business, use Expr.
- A recursive CTE inside an `IN (...)` subquery works on SQLite / MySQL / PostgreSQL; SQL Server does not allow `WITH` inside a subquery, so use a materialized path column (e.g. `Path LIKE '1/5/%'`) or hoist the CTE to the statement root.
- `SqlGen.ToSql` emits a complete statement only for `Select` / `Update` / `Delete`; anything else is a fragment.
- Related links: [Query overview](../core-usage/query-overview.en.md), [Lambda guide](../core-usage/lambda-guide.en.md), [Associations](../core-usage/associations.en.md), [Expression extension](../extensibility/expression-extension.en.md), [Lambda and Expr](../core-usage/lambda-expr-mixing.en.md), [ExprString guide](../core-usage/exprstring-guide.en.md), [Expr JSON serialization](../extensibility/expr-serialization.en.md), [Native Expr queries](../extensibility/frontend-native-expr.en.md), [Projection on demand](./query-projection.en.md), [Custom paging](../advanced-topics/custom-paging.en.md), [Sharding and TableArgs](../advanced-topics/sharding-and-tableargs.en.md), [CTE guide](../core-usage/cte-guide.en.md), [Permission filtering](../advanced-topics/permission-filtering.en.md).
