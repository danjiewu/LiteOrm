# Complex Queries in Practice: Department Trees, Last 7 Days, Dynamic Sorting/Paging and Saved Conditions

Basic Lambda syntax, `Expr` composition, `ExprString` interpolation, and associations are each covered by their own documents, so this document does not repeat them and instead focuses on how they combine into one realistic query path. It follows a single requirement as it evolves: the frontend submits one query JSON from which the backend extracts the filter for counting and paging; the equality condition on the department is then replaced by a department subtree; sorting and paging are then moved into the same JSON; and finally the filter is saved separately and reloaded when needed. All SQL shown below is actual rendered output, using the SQLite dialect unless noted, with parameter values written as `@0` and `@1`.

## 1. Requirement, Model and Query Input

### 1.1 Requirements

The following requirements also serve as the acceptance points referenced later in this document:

- Only orders belonging to the specified department and all of its descendants are returned, without first loading the department Ids into memory and building an `IN` list from them.
- Only orders created within the last 7 days are returned, where "last" may mean either a rule relative to the current time or a fixed time window.
- The three optional filters on status, minimum amount, and order-number keyword are supported, and a filter that is absent must not produce an extra SQL fragment.
- The list can be sorted by clicking a column header, covering created time, amount, status, and order number in either direction.
- The list is paged, and the total count and the current page must be produced from the same set of conditions.
- Query conditions can be saved so that the user can reload and reuse them later.

### 1.2 Model

Only two tables are needed: the department table provides the tree structure, and the order table provides the data to filter and sort.

```csharp
[Table("Departments")]
public class Department
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)] public int Id { get; set; }
    [Column("ParentId")] public int? ParentId { get; set; }
    [Column("Name")] public string Name { get; set; } = "";
}

[Table("Orders")]
public class SaleOrder
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)] public int Id { get; set; }
    [Column("OrderNo")] public string OrderNo { get; set; } = "";
    [Column("DeptId")] public int DeptId { get; set; }
    [Column("Status")] public int Status { get; set; }
    [Column("Amount")] public decimal Amount { get; set; }
    [Column("CreatedTime")] public DateTime CreatedTime { get; set; }
}
```

### 1.3 Query Input: One Query JSON

The list endpoint receives a single JSON string that describes the filters and may also carry sorting and paging. The JSON does not specify a table and does not need an outer `$select`, because `Search` and `SearchAsync` supply the table from the Service entity type and fill in the columns to query, so the whole path involves exactly one deserialization. The smallest query, which contains only the filter, looks like this:

```json
{
  "$and": [
    { "$": "==", "Left": { "#": "DeptId" }, "Right": { "@": 1001 } },
    { "$": ">=", "Left": { "#": "CreatedTime" },
      "Right": { "$": "func", "AddDays": [ { "$": "func", "Now": [] }, { "@": -7 } ] } },
    { "$": "==", "Left": { "#": "Status" }, "Right": { "@": 1 } },
    { "$": ">=", "Left": { "#": "Amount" }, "Right": { "@": 100 } },
    { "$": "contains", "Left": { "#": "OrderNo" }, "Right": { "@": "SO2026" } }
  ]
}
```

Keys beginning with `$` denote operators or segment types, `#` denotes a column, and `@` denotes a parameter; the complete shape is documented in [Expr JSON Serialization Format](../extensibility/expr-serialization.en.md). Sorting and paging are two additional segment layers that section 4 adds to this same JSON. Deserialization takes a single line of code, and the result may be either a logical condition or a segment chain that carries sorting and paging:

```csharp
Expr query = JsonSerializer.Deserialize<Expr>(queryJson)!;
```

In production the JSON should be validated with `ExprValidator`, and the set of fields the frontend may reference should be restricted, as described in [Security](../advanced-topics/security.en.md). When an optional filter is absent, its node is simply missing from the `$and` array, so no extra condition appears in the generated SQL.

### 1.4 Extracting the Filter from the Query

A paged query needs the whole query, whereas counting needs only the filter inside it. When the query is just a condition, the condition itself is the `filter`; when the query carries sorting or paging, its segment layers form a `SectionExpr`, `OrderByExpr`, `WhereExpr` chain through `Source`, so walking inward along `Source` and stopping at the first `WhereExpr` yields the filter:

```csharp
static LogicExpr? GetFilter(Expr? query)
{
    if (query is LogicExpr logic) return logic;

    for (Expr? node = query; node is not null; node = (node as SqlSegment)?.Source)
    {
        if (node is WhereExpr where) return where.Where;
    }
    return null;
}
```

This code copies no nodes: a condition query returns the expression itself, and a sorted or paged query returns the very object inside the `Where` node. The count and the page query therefore share one filter, and the condition has exactly one source of truth.

### 1.5 Scope and Prerequisites

The following table points to the documents that explain each building block, since this document only discusses how they compose:

| Capability | Document |
| --- | --- |
| Lambda syntax, closure parameterization, multi-parameter Lambda | [Lambda Guide](../core-usage/lambda-guide.en.md) |
| `RegisterMethodHandler` / `RegisterMemberHandler` mechanics | [Expression Extension](../extensibility/expression-extension.en.md) |
| `Expr.Lambda`, `AsValue()`, Lambda/Expr conversion | [Lambda and Expr Mixing](../core-usage/lambda-expr-mixing.en.md) |
| `ExprString` interpolation, embedded `Expr`, `[Column]` placeholders | [ExprString Guide](../core-usage/exprstring-guide.en.md) |
| Expr JSON shape and operator markers | [Expr JSON Serialization Format](../extensibility/expr-serialization.en.md) |
| Building Expr JSON on the frontend | [Frontend Native Expr Queries](../extensibility/frontend-native-expr.en.md) |
| Dialect paging with `Section` / `Skip` / `Take` | [Custom Paging](../advanced-topics/custom-paging.en.md) |
| CTE `With` / `UnionAll` | [CTE Guide](../core-usage/cte-guide.en.md) |
| Grouping/aggregation and projection comparison | [On-Demand Projection](./query-projection.en.md) |
| Custom function mapping and `SqlBuilder` extension | [Custom SqlBuilder / Dialect Extension](../extensibility/custom-sqlbuilder.en.md) |

## 2. Step 1: This Department + Last 7 Days + Optional Filters

The deserialized `query` can be handed directly to the DAO, and the first step is to query only the current department so that the path from JSON to SQL works end to end.

### 2.1 Execute the Deserialized Result Directly

```csharp
Expr query = JsonSerializer.Deserialize<Expr>(queryJson)!;
LogicExpr? filter = GetFilter(query);

int total = await orderService.CountAsync(filter, cancellationToken: ct);
List<SaleOrder> items = await orderService.SearchAsync(query, cancellationToken: ct);
```

`CountAsync` accepts only a condition, so it wraps that condition in a `WHERE` clause and renders `SELECT Count(1)`, which means the count SQL never carries sorting or paging; `SearchAsync` accepts any `Expr`, supplies the table and query columns from the Service entity type, and renders sorting and paging exactly as they appear in the JSON. The `$and` array may hold any number of conditions or just one, and all parameter values travel in `@` nodes into `context.OutputParams` rather than being spliced into the SQL text. A `contains` node corresponds to Lambda `Contains`, and the framework adds wildcards on both sides of the parameter as well as handling escaping and parameterization.

### 2.2 "Last 7 Days": Function or Parameter

The same "last 7 days" condition has two JSON forms, and they differ both in the SQL they render and in what they mean once saved:

```json
// Function: recompute with the database's current time on every execution
{ "$": ">=", "Left": { "#": "CreatedTime" },
  "Right": { "$": "func", "AddDays": [ { "$": "func", "Now": [] }, { "@": -7 } ] } }

// Parameter: the point in time is fixed when the JSON is produced
{ "$": ">=", "Left": { "#": "CreatedTime" },
  "Right": { "$datetime": "2026-10-18T13:53:02.2826566+08:00" } }
```

SQLite renders the two forms as follows:

```sql
-- Function: -7 participates in date arithmetic as a parameter
"T0"."CreatedTime" >= DATE(datetime('now', 'localtime'), CAST(@0 AS TEXT)||' days')
-- @0 = -7

-- Parameter: time is fixed; SQL has only an ordinary parameter
"T0"."CreatedTime" >= @0
-- @0 = 2026/10/18 13:53:02
```

MySQL and Oracle render the function form as ``DATE_ADD(NOW(), INTERVAL -7 DAY)`` and `(sysdate + NUMTODSINTERVAL(-7, 'DAY'))` respectively, because each dialect's SqlBuilder is responsible for adapting function names and date arithmetic.

When the server builds the same condition with Lambda or `Expr`, `DateTime.Now` is resolved into a `Now()` function, and only an explicit constant becomes a parameter:

```csharp
// Lambda: DateTime.Now → Now() function
Expr.Lambda<SaleOrder>(o => o.CreatedTime >= DateTime.Now.AddDays(-7))!;

// Parameter: compute the time first, then treat it as a constant
Expr.Const(DateTime.Now.AddDays(-7));
```

The choice depends on whether the condition expresses a rule or a fact. Live dashboards should use the function form, because every execution takes the database's current time as the reference and a saved condition always means "last 7 days"; reconciliation, exports, and audit snapshots should use the parameter form, because the time window is frozen into the condition and reruns always return the same rows. Both forms can be saved and reloaded, and the difference is explained in 5.2.

### 2.3 SQL for This Step

For the request `DeptId=1001, Days=7, Status=1, MinAmount=100, Keyword="SO2026"`, this smallest query renders as follows. The JSON contains no SELECT segment, so `SearchAsync` has already supplied every `SaleOrder` column, which is why the SQL still lists columns explicitly instead of using `*`:

```sql
SELECT "T0"."Id", "T0"."OrderNo", "T0"."DeptId", "T0"."Status", "T0"."Amount", "T0"."CreatedTime"
FROM "Orders" "T0"
WHERE "T0"."DeptId" = @0
 AND "T0"."CreatedTime" >= DATE(datetime('now', 'localtime'), CAST(@1 AS TEXT)||' days')
 AND "T0"."Status" = @2 AND "T0"."Amount" >= @3 AND "T0"."OrderNo" LIKE @4
-- @0 = 1001, @1 = -7, @2 = 1, @3 = 100, @4 = %SO2026%
```

The JSON does not yet contain the sorting and paging layers, so the SQL has no `ORDER BY` or `LIMIT`. Section 4 adds those layers to the query JSON; for now the remaining issue is that `DeptId = @0` covers only one department.

## 3. Step 2: Expand the Department into a Tree

The requirement is to query a department and all of its descendants, so the JSON condition `"$": "=="` becomes `"$": "in"` with its right-hand side replaced by a subtree fragment, while every other condition stays unchanged. The difficulty lies in how the subtree becomes SQL: Oracle uses `CONNECT BY`, while the other dialects use a recursive CTE.

The two approaches below implement the same `DeptSubTree` fragment, and either one may be chosen; section 3.4 gives the JSON for each option.

### 3.1 Option A: `GenericSqlExpr` + `AsValue()`

#### 3.1.1 Register a Value Fragment

`GenericSqlExpr` itself is a `LogicExpr` and can only sit in a WHERE position, so using it as the right-hand side of an order's `DeptId` inside `IN (...)` requires wrapping it with `AsValue()` to produce a value expression.

`Arg` is of type `Expr`, so passing a raw value wraps it into a `ValueExpr` automatically, while a property or function expression may also be passed directly; the handler normalizes whatever it receives with `AsValue()`.

The registered handler returns a parenthesized subquery, which allows it to appear inside `IN`, a SELECT column, a comparison, or a function argument:

```csharp
public static class DeptSubTree
{
    public static void Register()
    {
        GenericSqlExpr.Register("DeptSubTree", (context, arg) =>
        {
            if (arg is null) throw new NotSupportedException("DeptSubTree requires a department Id.");
            ValueTypeExpr rootId = arg.AsValue();

            ISqlBuilder builder = context.SqlBuilder;
            if (builder is OracleBuilder)
            {
                string table = builder.ToSqlName(
                    TableInfoProvider.Instance.GetTableDefinition(typeof(Department)).Name!);
                string id = builder.ToSqlName(nameof(Department.Id));
                string parentId = builder.ToSqlName(nameof(Department.ParentId));
                return $"(SELECT {id} FROM {table} START WITH {id} = {rootId.ToSql(context)} " +
                       $"CONNECT BY PRIOR {id} = {parentId})";
            }

            using (context.BeginScope())
                return $"({BuildRecursiveQuery(rootId).ToSql(context)})";
        });
    }

    // The recursive CTE is built with Expr directly: anchor + UNION ALL self-reference, then With("DeptTree")
    // internal so the FunctionExpr version in 3.2 can reuse the same construction logic
    internal static SelectExpr BuildRecursiveQuery(ValueTypeExpr rootId)
    {
        SelectExpr anchor = Expr.From(typeof(Department))
            .Where(Expr.Prop(nameof(Department.Id)) == rootId)
            .Select(new SelectItemExpr(Expr.Prop(nameof(Department.Id)), nameof(Department.Id)));

        var selfRef = new CommonTableExpr { Alias = "DeptTree" };

        SelectExpr recursive = Expr.From(typeof(Department))
            .Join(selfRef, Expr.Prop(nameof(Department.ParentId)) == Expr.Prop("DeptTree", nameof(Department.Id)))
            .Select(new SelectItemExpr(Expr.Prop(nameof(Department.Id)), nameof(Department.Id)));

        return anchor.UnionAll(recursive)
            .With("DeptTree")
            .Select(new SelectItemExpr(Expr.Prop(nameof(Department.Id)), nameof(Department.Id)));
    }
}
```

#### 3.1.2 Use It in a Condition

```csharp
LogicExpr InDeptTree(int deptId)
    => Expr.Prop(nameof(SaleOrder.DeptId)).In(Expr.Sql("DeptSubTree", deptId).AsValue());
```

SQLite renders this condition as a recursive CTE subquery:

```sql
"T0"."DeptId" IN (WITH RECURSIVE "DeptTree" AS (SELECT "T1"."Id"
    FROM "Departments" "T1"
    WHERE "T1"."Id" = @0
    UNION ALL SELECT "T2"."Id"
      FROM "Departments" "T2"
      LEFT JOIN "DeptTree" ON "T2"."ParentId" = "DeptTree"."Id")
SELECT "Id"
  FROM "DeptTree")
-- @0 = 1001
```

Oracle renders the same condition as a hierarchical query instead:

```sql
"T0"."DEPTID" IN (SELECT "ID" FROM "DEPARTMENTS" START WITH "ID" = :0 CONNECT BY PRIOR "ID" = "PARENTID")
-- :0 = 1001
```

#### 3.1.3 Make It Writable in Lambda

Business code reads more naturally as `o.InDeptSubTree(deptId)`, and the method below exists only so that the code compiles, since it never executes in memory:

```csharp
public static class OrderExtensions
{
    public static bool InDeptSubTree(this SaleOrder order, int deptId) => false;
}
```

When registering the resolver, remember that an extension method is a static call in an expression tree, so `node.Object == null`, the receiver is `Arguments[0]`, and the department Id is `Arguments[1]`:

```csharp
LambdaExprConverter.RegisterMethodHandler(
    typeof(OrderExtensions), nameof(OrderExtensions.InDeptSubTree),
    (node, converter) =>
    {
        Expr deptId = converter.Convert(node.Arguments[1]);
        return Expr.Prop(nameof(SaleOrder.DeptId)).In(Expr.Sql("DeptSubTree", deptId).AsValue());
    });

// Usage
int deptId = 1001;
LogicExpr filter = Expr.Lambda<SaleOrder>(o => o.InDeptSubTree(deptId))!;
```

This Lambda produces exactly the same SQL as the `Expr` in 3.1.2, because closure variables are resolved into a `ValueExpr` that `Arg` accepts directly and that is saved as `{"@":1001}`.

#### 3.1.4 Key Points

The handler only produces a value fragment, while the positions such as `IN`, `=`, and SELECT columns are decided by the caller, which is why one key can be reused in different contexts. The recursive CTE is assembled from `UnionAll` and `With`, and `CommonTableExpr { Alias = "DeptTree" }` carries only the alias so that no table alias is appended on output. `context.BeginScope()` isolates the inner subquery's alias and parameter scope, while `@0` still joins the same parameter collection. The Oracle branch obtains the table name from `TableInfoProvider` and converts identifiers with `builder.ToSqlName`, rather than hardcoding uppercase table names. Finally, `AsValue()` is essential, because without it a `GenericSqlExpr` works only as a condition and cannot be the right-hand side of `IN`.

### 3.2 Option B: Custom `FunctionExpr`

`FunctionExpr` is a value expression by nature and can be the right-hand side of `IN` directly, so no `AsValue()` is needed.

#### 3.2.1 Default Implementation + Dialect Overrides

Register a generic implementation on `SqlBuilder.Instance` so that every dialect can use the recursive CTE, then override it on concrete types such as `OracleBuilder`. `BuildFunctionSql` looks up handlers in the order "concrete type → base class", so an override affects only that dialect:

```csharp
public static class DeptSubTreeFunction
{
    public static void Register()
    {
        // Default implementation: recursive CTE shared by all dialects
        SqlBuilder.Instance.RegisterFunctionSqlHandler("DeptSubTree", RenderRecursive);

        // Oracle overrides with CONNECT BY
        OracleBuilder.Instance.RegisterFunctionSqlHandler("DeptSubTree", RenderOracle);
    }

    static void RenderRecursive(ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context)
    {
        ValueTypeExpr rootId = expr.Args[0];
        using (context.BeginScope())
            outSql.Append($"({DeptSubTree.BuildRecursiveQuery(rootId).ToSql(context)})");
    }

    static void RenderOracle(ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context)
    {
        ValueTypeExpr rootId = expr.Args[0];
        ISqlBuilder builder = context.SqlBuilder;
        string table = builder.ToSqlName(
            TableInfoProvider.Instance.GetTableDefinition(typeof(Department)).Name!);
        string id = builder.ToSqlName(nameof(Department.Id));
        string parentId = builder.ToSqlName(nameof(Department.ParentId));
        outSql.Append($"(SELECT {id} FROM {table} START WITH {id} = {rootId.ToSql(context)} " +
                       $"CONNECT BY PRIOR {id} = {parentId})");
    }
}
```

Registering the default on `SqlBuilder.Instance` and overriding only where needed avoids repeating the same recursive CTE for every dialect, leaving dialect differences only where they genuinely exist.

#### 3.2.2 Use It in a Condition

```csharp
LogicExpr InDeptTree(int deptId)
    => Expr.Prop(nameof(SaleOrder.DeptId)).In(Expr.Func("DeptSubTree", Expr.Value(deptId)));
```

SQLite and Oracle output is byte-for-byte identical to 3.1, and the Lambda version only changes the `Expr` that the handler returns:

```csharp
// With option B, the handler from 3.1.3 returns a FunctionExpr instead
LambdaExprConverter.RegisterMethodHandler(
    typeof(OrderExtensions), nameof(OrderExtensions.InDeptSubTree),
    (node, converter) =>
    {
        Expr deptId = converter.Convert(node.Arguments[1]);
        return Expr.Prop(nameof(SaleOrder.DeptId)).In(Expr.Func("DeptSubTree", deptId.AsValue()));
    });
```

### 3.3 How to Choose

| Concern | `GenericSqlExpr` + `AsValue()` | Custom `FunctionExpr` |
| --- | --- | --- |
| Registration | One global key registry | Default on `SqlBuilder.Instance`, overridden by dialect as needed |
| Dialect branching | Handler checks `context.SqlBuilder` | Default implementation + concrete-type overrides |
| Expression type | `LogicExpr`; needs `AsValue()` in value positions | Native `ValueTypeExpr` |
| Reuse | Any Expr can reference the key | Fits the function system and is visible to function validators |
| Best for | Fragments with small cross-dialect differences, centralized management | Fragments with large dialect differences, or already-customized SqlBuilders |

### 3.4 JSON for the Department Tree

Option A replaces the right-hand side with a `$sql` node carrying an `Arg` that is written as a parameter according to the `Expr` JSON rules:

```json
{ "$": "in", "Left": { "#": "DeptId" },
  "Right": { "$": "sql", "Key": "DeptSubTree", "Arg": { "@": 1001 } } }
```

Option B replaces the right-hand side with a function-call node whose argument array also contains a single department Id:

```json
{ "$": "in", "Left": { "#": "DeptId" },
  "Right": { "$": "func", "DeptSubTree": [ { "@": 1001 } ] } }
```

Replacing only this one condition node while leaving every other node untouched produces the new query JSON for "department tree + last 7 days + optional filters".

## 4. Step 3: Write Sorting and Paging into the JSON

### 4.1 The JSON Shape for Sorting and Paging

Sorting and paging are not additional request parameters but two segment layers of the query JSON. The query needs neither `$select` nor `$table`, so the outermost segment is the paging segment `$section`, whose source is the sorting segment `$orderby`, whose source is the condition segment `$where`; because no table is specified, `$where` is written as `null` so that `Search` can supply it from the entity type:

```json
{
  "$section": {
    "$orderby": {
      "$where": null,
      "Where": { "$and": [ ... ] }
    },
    "OrderBys": [
      { "Field": { "#": "Amount" }, "Asc": false },
      { "Field": { "#": "Id" }, "Asc": false }
    ]
  },
  "Skip": 20,
  "Take": 20
}
```

Each segment places its inner segment under the key named by its `$` marker, and its own properties sit at the same level as that key: `Skip` and `Take` are siblings of `$section`, `OrderBys` is a sibling of `$orderby`, and `Where` is a sibling of `$where`. An omitted `Asc` means ascending order, `false` means descending order, `Skip` may be omitted when it is zero, and `Take` equal to zero means no limit.

When `OrderBys` sorts `Amount` descending and the query takes page 2 with 20 rows per page, the sorting and paging portion renders as follows:

```sql
ORDER BY "T0"."Amount" DESC, "T0"."Id" DESC
LIMIT 20 OFFSET 20
```

When `OrderBys` contains only `CreatedTime` ascending, it renders as follows:

```sql
ORDER BY "T0"."CreatedTime", "T0"."Id" DESC
```

MySQL writes paging as ``LIMIT 20,20`` and Oracle writes it as `OFFSET 20 ROWS FETCH NEXT 20 ROWS ONLY`, and these differences are handled by each dialect's SqlBuilder, so the JSON itself stays dialect-neutral.

Sorting fields are written directly in the JSON, so the query method does not need a second whitelist mapping. If the endpoint is exposed publicly, `ExprValidator` can restrict the field scope once (see [Security](../advanced-topics/security.en.md)), which validates sorting fields together with filter fields and avoids maintaining a separate list of sortable columns.

### 4.2 Total and Page from One Filter

`query` comes from one deserialization, and `filter` is extracted from it, so the two serve counting and paging respectively:

```csharp
LogicExpr? filter = GetFilter(query);

int total = await orderService.CountAsync(filter, cancellationToken: ct);
List<SaleOrder> items = await orderService.SearchAsync(query, cancellationToken: ct);
```

The count SQL has neither `ORDER BY` nor `LIMIT`, because it receives only `filter`:

```sql
SELECT Count(1)
FROM "Orders" "T0"
WHERE "T0"."DeptId" IN (WITH RECURSIVE "DeptTree" AS (SELECT "T1"."Id"
    FROM "Departments" "T1"
    WHERE "T1"."Id" = @0
    UNION ALL SELECT "T2"."Id"
      FROM "Departments" "T2"
      LEFT JOIN "DeptTree" ON "T2"."ParentId" = "DeptTree"."Id")
SELECT "Id"
  FROM "DeptTree")
 AND "T0"."CreatedTime" >= DATE(datetime('now', 'localtime'), CAST(@1 AS TEXT)||' days')
-- @0 = 1001, @1 = -7
```

The page SQL receives the whole `query` and therefore adds the three filter parameters together with sorting and paging:

```sql
SELECT "T0"."Id", "T0"."OrderNo", "T0"."DeptId", "T0"."Status", "T0"."Amount", "T0"."CreatedTime"
FROM "Orders" "T0"
WHERE "T0"."DeptId" IN (...)
 AND "T0"."CreatedTime" >= DATE(datetime('now', 'localtime'), CAST(@1 AS TEXT)||' days')
 AND "T0"."Status" = @2 AND "T0"."Amount" >= @3 AND "T0"."OrderNo" LIKE @4
ORDER BY "T0"."Amount" DESC, "T0"."Id" DESC
LIMIT 20 OFFSET 20
-- @0 = 1001, @1 = -7, @2 = 1, @3 = 100, @4 = %SO2026%
```

### 4.3 Key Points

- `filter` and `query` come from the same deserialization, since counting uses the `Where` inside it while the page uses the whole `query`, which means the two are consistent by construction and a condition change happens in one place only.
- Sorting must include a unique column as a tiebreaker, `Id` in this example, otherwise paging may repeat rows or skip rows.
- The table is determined by the Service entity type while the JSON describes only conditions, sorting, and paging, so counting and the page always read the same table.

## 5. Step 4: Save and Reload the Filter

### 5.1 Save the Filter, Not the Whole Query

The JSON received by the list endpoint contains the filter together with sorting and paging, but only the filter needs to be persisted: sorting and paging belong to the current browsing session and should follow the user's current view rather than being frozen into history. Saving therefore extracts `Where` from the query and serializes it, and reloading puts it back into the `$where` node of the query JSON; when the query is itself a condition, that condition is used directly:

```csharp
static Expr SetFilter(Expr query, LogicExpr? filter)
{
    if (query is LogicExpr) return filter!;

    for (SqlSegment? node = query as SqlSegment; node is not null; node = node.Source)
    {
        if (node is WhereExpr where) { where.Where = filter; return query; }
    }
    throw new ArgumentException("The query JSON has no $where node.");
}

// Save: store only the filter
store.Save(userId, JsonSerializer.Serialize<Expr>(GetFilter(query)));

// Reload: put the filter back into the current query
query = SetFilter(query, (LogicExpr?)JsonSerializer.Deserialize<Expr>(store.Load(userId)));
```

`GenericSqlExpr.Arg` is of type `Expr` and therefore serializes by the `Expr` rules: a `ValueExpr` becomes `{"@": value}` and `Expr.Const(...)` becomes a native value. Consequently `"Arg": { "@": 1001 }` from 3.4 round-trips directly, without restoring a raw value during save and reload, and a filter that is saved and reloaded renders SQL byte-for-byte identical to the original.

### 5.2 Two Semantics for "Last 7 Days" After Reload

The two forms from 2.2 mean completely different things once reloaded. The function form, with `Now` and `AddDays` inside `$func`, saves the rule "last 7 days", so opening it today means 10-18 through 10-25 while opening it next week means 10-25 through 11-01, which makes it suitable for live dashboards and always-on filters. The parameter form, written as `$datetime`, saves the fact "since 10-18", so rerunning it at any later time returns the same rows, which makes it suitable for reconciliation, exports, and audit trails. The decision rule is straightforward: if the user explicitly selects a date range in the UI, use a parameter, and if the user selects a relative option such as "last N days", use a function.

### 5.3 Add More Conditions After Reload

The reloaded filter can be combined with new conditions, and the combined result can be written back into the query while sorting and paging remain in the JSON:

```csharp
LogicExpr saved = (LogicExpr?)JsonSerializer.Deserialize<Expr>(store.Load(userId))!;
query = SetFilter(query, saved & (Expr.Prop(nameof(SaleOrder.Amount)) >= 500));
```

### 5.4 The Handler Must Be Registered in the Current Process

The `DeptSubTree` handler must be registered in the current process, for example by calling `DeptSubTree.Register()` or `DeptSubTreeFunction.Register()` at startup. Otherwise a reloaded condition cannot resolve the key at SQL generation time, and the subquery that should appear is rendered as an empty fragment.

## 6. Assembly: The Complete Query Method

Each step above added exactly one capability, and the following code puts them together in call order.

### 6.1 Service: Total and Page

```csharp
public sealed record OrderPage(int Total, List<SaleOrder> Items);

public sealed class OrderListService
{
    private readonly EntityService<SaleOrder> _orders;

    public OrderListService(EntityService<SaleOrder> orders) => _orders = orders;

    public async Task<OrderPage> QueryAsync(string queryJson, CancellationToken ct = default)
    {
        // One deserialization yields the whole query
        Expr query = JsonSerializer.Deserialize<Expr>(queryJson)!;

        // Counting uses only Where; the page uses the whole query
        LogicExpr? filter = GetFilter(query);
        int total = await _orders.CountAsync(filter, cancellationToken: ct);
        List<SaleOrder> items = await _orders.SearchAsync(query, cancellationToken: ct);

        return new OrderPage(total, items);
    }
}
```

`GetFilter` (1.4) and `SetFilter` (5.1) live in the same class, and `DeptSubTree.Register()` or `DeptSubTreeFunction.Register()` is called once at startup. Saving and reloading conditions follow section 5, and no other part of `QueryAsync` needs to change.

### 6.2 Status Summary on the Same Page: Mixing `ExprString`

The list passes conditions as JSON, while the status summary on the same page reads more clearly with `ExprString`, because it is hand-written SQL with an embedded `Expr` that can reuse the extracted `filter` directly:

```csharp
public class OrderDAO : ObjectViewDAO<SaleOrder>
{
    public OrderDAO(SessionManager sessionManager) : base(sessionManager) { }

    public Task<List<StatusSummary>> QueryStatusSummaryAsync(LogicExpr filter, CancellationToken ct = default)
        => SearchAs<StatusSummary>(
                $"SELECT [Status], COUNT(1) AS [Total] FROM {From} WHERE {filter} GROUP BY [Status]")
            .ToListAsync(ct);
}
```

The rendering is:

```sql
SELECT "Status", COUNT(1) AS "Total"
FROM "Orders" "T0"
WHERE "DeptId" IN (WITH RECURSIVE "DeptTree" AS (SELECT "T1"."Id"
    FROM "Departments" "T1"
    WHERE "T1"."Id" = @0
    UNION ALL SELECT "T2"."Id"
      FROM "Departments" "T2"
      LEFT JOIN "DeptTree" ON "T2"."ParentId" = "DeptTree"."Id")
SELECT "Id"
  FROM "DeptTree")
 AND "CreatedTime" >= DATE(datetime('now', 'localtime'), CAST(@1 AS TEXT)||' days')
GROUP BY "Status"
```

`{From}` is a DAO placeholder that is replaced by the table or view together with its joins, and `filter` is the interpolated `Expr` that expands directly into SQL and shares the same parameter collection. The columns in the rendered `filter` carry no table alias, as in `"DeptId"` above, because the `ExprString` context already registers the main table and omits the alias for a single-table query. A `[Column]` placeholder in a hand-written fragment is converted to the current dialect's real quoting, which is `"` for SQLite and `` ` `` for MySQL.

## 7. Comparison with Other ORMs

| Scenario | LiteOrm approach | Extra work in common ORMs |
| --- | --- | --- |
| Passing in conditions | Frontend Expr JSON deserialized directly | Define a DTO per query, then map it to a query |
| Passing in sorting and paging | The `$orderby` / `$section` layers of the same JSON | Accept sorting and paging separately, then splice them into the query |
| Department-tree filter | One Expr fragment renders dialect `CONNECT BY` or a recursive CTE | Recursively query Ids first, then build `IN`; or write raw SQL |
| Count + page from one condition | Extract `Where` from the same `Expr` for counting | Two hand-written SQL statements or two expression trees |
| Dynamic header sorting | `OrderBys` written directly in the query JSON | Lots of `switch` or string concatenation |
| Saving conditions | Store only the extracted filter JSON | A separate DTO conversion layer |
| Lambda / Expr / ExprString | All three compose and share parameters | Usually only one is supported |

## 8. Debugging Queries with SqlGen

`SqlGen` does not touch a database and only renders an `Expr` to SQL for a chosen dialect, which makes it ideal for documentation and dialect troubleshooting.

### 8.1 Minimal Usage

```csharp
var sqlGen = new SqlGen(typeof(SaleOrder)) { SqlBuilder = SQLiteBuilder.Instance };
PreparedSql sql = sqlGen.ToSql(query);
Console.WriteLine(sql.Sql);
foreach (Param p in sql.Params) Console.WriteLine($"{p.Name} = {p.Value}");
```

`SqlGen` uses the generic `SqlBuilder.Instance` (`OFFSET ... FETCH`) by default, so `SqlBuilder` must be set explicitly to verify SQLite, MySQL, or Oracle behavior.

### 8.2 Verify the Tree Fragment Across Dialects

```csharp
LogicExpr treeFilter = InDeptTree(1001);

var sqlite = new SqlGen(typeof(SaleOrder)) { SqlBuilder = SQLiteBuilder.Instance };
var oracle = new SqlGen(typeof(SaleOrder)) { SqlBuilder = OracleBuilder.Instance };

Console.WriteLine(sqlite.ToSql(treeFilter).Sql);   // WITH RECURSIVE
Console.WriteLine(oracle.ToSql(treeFilter).Sql);   // START WITH ... CONNECT BY
```

With the `Expr.Func("DeptSubTree", ...)` implementation the output must be identical, and any difference means the two implementations register or handle parameters differently.

### 8.3 Verify Count, Page and Reload

```csharp
// Equivalent to the count query that CountAsync builds internally
SelectExpr countExpr = new SelectExpr(
    GetFilter(query)!.ToSource<SaleOrder>(),
    new SelectItemExpr(Expr.Aggregate("Count", Expr.Const(1))));

Console.WriteLine(sqlGen.ToSql(countExpr).Sql);   // SELECT Count(1), no ORDER BY/LIMIT
Console.WriteLine(sqlGen.ToSql(query).Sql);       // ORDER BY + LIMIT/OFFSET

string saved = JsonSerializer.Serialize<Expr>(GetFilter(query));
LogicExpr reloaded = (LogicExpr?)JsonSerializer.Deserialize<Expr>(saved)!;
Console.WriteLine(sqlGen.ToSql(GetFilter(query)!).Sql == sqlGen.ToSql(reloaded).Sql);   // True
```

### 8.4 Compare with Runtime SQL and a Checklist

Runtime SQL is available from `SessionManager.Current?.SqlStack`, which keeps the last 10 statements and is cleared after each `Service` method call. `SqlGen` validates only generation logic and does not validate parameter types or whether database functions exist, so the query should still be run once against the real dialect before release.

When something goes wrong, the following checklist suggests an order of investigation:

- Empty tree SQL: the `DeptSubTree` key is not registered, or `GenericSqlExpr` is not wrapped with `AsValue()`.
- SQLite emits `CONNECT BY`: `RegisterFunctionSqlHandler` was not registered on a concrete type such as `OracleBuilder`.
- SQL changed after reload: determine whether the saved form is a function or a parameter by looking for `"Now"` or `"$datetime"` in the JSON.
- Sorting or paging has no effect: confirm that the `$orderby` / `$section` layers exist and sit at the correct level in the JSON.
- Reload fails: verify that `Arg` is written as Expr JSON, with a numeric parameter written as `{"@": 1001}`.
- Duplicate rows across pages: sorting lacks a unique tiebreaker column.

## 9. Limitations and Further Reading

- A recursive CTE nested inside an `IN (...)` subquery works on SQLite, MySQL 8, and PostgreSQL; SQL Server does not allow `WITH` inside a subquery, so a materialized path column such as `Path LIKE '1/5/%'` should be used or the CTE lifted to the statement root; MySQL 5.7 has no recursive CTE support.
- `GenericSqlExpr`'s `Arg` is a single `Expr`, so multiple parameters or more complex structures call for `FunctionExpr` or an explicitly constructed `Expr`.
- When accepting JSON from the frontend, restrict expression types and field scope with `ExprValidator`, and never feed arbitrary JSON straight into SQL generation; sorting fields and filter fields can be restricted in that same place.
- Counting uses only the extracted `Where` while the table comes from the Service entity type, so the query JSON neither needs to specify a table nor should.
- The tree handler solves only the "descendants of a department" semantic; ancestor chains or per-level aggregation need their own fragments rather than overloading one key with multiple semantics.
- Related links: [Query Overview](../core-usage/query-overview.en.md), [Lambda Guide](../core-usage/lambda-guide.en.md), [Associations](../core-usage/associations.en.md), [Expression Extension](../extensibility/expression-extension.en.md), [Lambda and Expr Mixing](../core-usage/lambda-expr-mixing.en.md), [ExprString Guide](../core-usage/exprstring-guide.en.md), [Expr JSON Serialization Format](../extensibility/expr-serialization.en.md), [Frontend Native Expr Queries](../extensibility/frontend-native-expr.en.md), [On-Demand Projection](./query-projection.en.md), [Custom Paging](../advanced-topics/custom-paging.en.md), [CTE Guide](../core-usage/cte-guide.en.md), [Sharding and TableArgs](../advanced-topics/sharding-and-tableargs.en.md), [Custom SqlBuilder / Dialect Extension](../extensibility/custom-sqlbuilder.en.md).
