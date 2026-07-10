# ExprString Guide

`ExprString` is a C# interpolated-string handler (`[InterpolatedStringHandler]`) provided by LiteOrm for handwriting SQL fragments or full SQL inside the DAO layer. A single interpolated string can mix `Expr` objects and plain values; the framework handles both SQL assembly and parameterization.

This is the standalone reference for `ExprString`. For the overall choice between Lambda / `Expr` / `ExprString`, start with the [Query Overview](./04-query-overview.en.md). For Lambda usage, see the [Lambda Guide](./05-lambda-guide.en.md); to construct `Expr` objects themselves, see the [Expr Guide](./06-expr-guide.en.md).

## 1. Scope and applicability

| Aspect | Notes |
|--------|-------|
| Type | `LiteOrm.Common.ExprString` (`ref struct`, compile-time interpolated-string handler) |
| Entry layer | Only the **DAO layer** exposes `ExprString` overloads; the Service layer does not |
| Typical scenarios | Supplement `Search` with a `WHERE/ORDER BY` fragment in a custom DAO, pass full SQL, run `DataTable` queries, project to arbitrary types |
| Type safety | Runtime only; no compile-time column/type checking |
| SQL injection | Plain values are auto-parameterized; `Expr` objects go through `Expr`'s own parameterization |

`ExprString` does not replace structured `Expr`/`SelectExpr`. It is the low-friction entry point for cases where you have already decided to write SQL by hand, while still staying inside LiteOrm's parameterization pipeline and reusing `Expr` conditions you have already built.

## 2. How it works

`ExprString` is a `ref struct`. On construction it obtains the `SqlBuilder` and `SqlBuildContext` from the current DAO (`IExprStringBuildContext`) and maintains an internal SQL text buffer plus a parameter list. The compiler decomposes the interpolated string into two kinds of calls:

- `AppendLiteral(string)`: handles literal fragments, appended verbatim to the buffer.
- `AppendFormatted<T>(T value)`: handles each interpolation hole.
  - When `value` is an `Expr`, it calls `expr.ToSql(...)` to translate the expression into a SQL fragment (and its parameters) and splices it in.
  - When `value` is a `RawSql` (see [Section 8 Inserting raw SQL](#8-inserting-raw-sql-rawsql)), its content is appended verbatim to the buffer, with no parameterization or syntax processing.
  - Otherwise it wraps the value as a named parameter (`@0`, `@1`, ... ordered by appearance, then converted to the current dialect prefix via `ISqlBuilder.ToSqlParam`) and stores the value in the parameter list.

Finally, `GetSql()` / `GetParams()` / `GetResult()` produce the SQL text and parameters, packed into a `PreparedSql` for the DAO to execute.

> A single interpolated string can therefore contain `{Expr}` holes, `{plain variable}` holes, and literal SQL text, all consumed in order of appearance.

## 3. Basic usage

### 3.1 As a Search condition fragment only

The most common usage: pass a `WHERE ... ORDER BY ...` fragment to `Search`; the DAO automatically prepends `SELECT {AllFields} FROM {From}`.

```csharp
using static LiteOrm.Common.Expr;

public async Task<List<UserView>> GetActiveAdultsAsync(CancellationToken ct = default)
{
    var condition = Prop("Age") >= 18;
    return await userViewDAO
        .Search($"WHERE {condition} ORDER BY CreateTime DESC")
        .ToListAsync(ct);
}
```

### 3.2 Mixing Expr and plain values

`Expr` is expanded inline; plain values (`int`, `string`, ...) are auto-parameterized.

```csharp
using static LiteOrm.Common.Expr;

int minAge = 18;
string keyword = "admin";

var users = await userViewDAO.Search(
    $"WHERE {Prop("UserName").Contains(keyword)} AND {Prop("Age")} >= {minAge}"
).ToListAsync();
```

Prefer building the condition as an `Expr` (e.g. `Prop("UserName").Contains(keyword)` above) rather than hardcoding column names and values in the string — column names still go through `Expr`'s identifier quoting, and values still go through parameterization.

### 3.3 Full SQL (isFull: true)

When you need the full `SELECT ... FROM ...`, pass `isFull: true`; the DAO will not auto-prepend `SELECT {AllFields} FROM {From}`.

```csharp
var result = await dataViewDAO.Search(
    $"SELECT [Id], [UserName] FROM [Users] WHERE [Age] >= {minAge}",
    isFull: true
).GetResultAsync();
```

### 3.4 Multi-line interpolated strings

For complex SQL, prefer C# raw interpolated strings (`"""..."""`) for readability:

```csharp
var result = await dataViewDAO.Search(
    $"""
    SELECT [Id], [UserName], [Age]
    FROM [Users]
    WHERE [Age] >= {minAge}
    ORDER BY [Age] DESC, [UserName] ASC
    """,
    isFull: true
).GetResultAsync();
```

## 4. Parameterization and safety

- **Plain values**: when you insert a non-`Expr` value (`int`, `string`, `DateTime`, ...), `ExprString` auto-generates a named parameter (default `@0`, `@1`, ...; dialects such as Oracle are converted by `ISqlBuilder.ToSqlParam` to `:0`, etc.) and stores the value in the parameter list, preventing SQL injection.
- **`Expr` objects**: `Expr`'s own `ToSql` decides inline vs. parameterized. For example, in `Prop("Age") >= 18`, the `18` is handled by `Expr` (parameterized via `Expr.Value` semantics), while `Prop("Age")` itself expands to a quoted column name.
- **Handwritten identifiers**: see the next section, "Identifier placeholders".

> Never splice user input into the SQL via string concatenation. Always route it through `{variable}` or `{Expr.Value(...)}` so the framework can parameterize it.

## 5. Identifier placeholder `[ ]`

Different databases use different identifier quote characters (SQL Server `[ ]`, MySQL `` ` ` ``, PostgreSQL/Oracle `" "`). When handwriting identifiers inside `ExprString`, use `[` and `]` as a universal placeholder; before execution, the DAO replaces them with the real quote characters via `ISqlBuilder.ReplaceSqlName`.

```csharp
// Becomes [Users] on SQL Server, `Users` on MySQL, "Users" on PostgreSQL
var result = await dataViewDAO.Search(
    $"SELECT [Id], [UserName] FROM [Users] WHERE [Age] >= {minAge}",
    isFull: true
).GetResultAsync();
```

If you build column references with `Expr.Prop(...)`, you do not need to worry about quoting — `Expr` handles it via `SqlBuilder.ToSqlName`.

## 6. Sorting and paging

Sorting in `ExprString` is written directly in the `ORDER BY` clause.

**Column names directly:**

```csharp
var users = await userViewDAO.Search(
    $"WHERE {Prop("Age")} >= {minAge} ORDER BY DeptId ASC, CreateTime DESC"
).ToListAsync();
```

**Embedding an `OrderByExpr`:**

```csharp
using static LiteOrm.Common.Expr;

var orderBy = new OrderByExpr(
    null,
    Prop("DeptId").Asc(),
    Prop("CreateTime").Desc()
);

var users = await userViewDAO.Search(
    $"WHERE {Prop("Age")} >= {minAge} {orderBy}"
).ToListAsync();
```

**Paging** is typically delegated to the DAO's paging helpers (e.g. `Search().ToPageListAsync(...)`), or written as dialect-specific `LIMIT/OFFSET`/`ROW_NUMBER` inside full SQL.

## 7. Placeholder tokens `{Table}` / `{From}` / `{AllFields}`

`DAOBase` predefines three SQL tokens; subclasses provide replacement values in `GetReplacements()`, and a `MultiReplacer` substitutes them before executing the command:

| Token | Meaning | Default replacement |
|-------|---------|----------------------|
| `{Table}` | The physical table name (after sharding-arg formatting) | `FactTableName` |
| `{From}` | The multi-table JOIN segment used by the query (alias, JOINs) | `Table.ToSql(...)` |
| `{AllFields}` | The full column SQL for the current view/entity query | Built from `SelectColumns` |

These tokens also work with `isFull: true`, so you can reuse the main-table definition in custom projections:

```csharp
var result = await dataViewDAO.Search(
    $"SELECT {AllFields} FROM {From} WHERE {Prop("Age")} >= {minAge}",
    isFull: true
).GetResultAsync();
```

> Custom DAOs may override `GetReplacements()` to add more tokens, but should not break the three defaults above.

## 8. Inserting raw SQL (RawSql)

### 8.1 When to use

`ExprString` is safe by default: plain values are parameterized, `Expr` goes through structured translation, handwritten identifiers can use the `[ ]` placeholder, and **purely static SQL text can be written directly in the literal**. But there is one kind of content that can neither be parameterized nor hardcoded in the literal: **dynamic values unsuitable for parameters**. Typical scenarios:

| Scenario | Why it can't be parameterized | Why it can't be a literal |
|----------|-------------------------------|---------------------------|
| `LIMIT`/`OFFSET` integer values, page sizes | Some databases reject `LIMIT @0` or change the execution plan | Value is runtime-computed |
| `ORDER BY` direction `ASC`/`DESC` | SQL keyword, not a value | Asc/desc is chosen by the user at runtime |
| Dynamic column name / sort field | A column name is an identifier, not a value | Field is chosen by the user at runtime |
| `TOP n` row count | Same as LIMIT | Value is runtime-computed |

In this case, use the `RawSql` marker type to inline the validated dynamic value verbatim into the SQL text as a string.

> **When NOT to use RawSql**: All hardcoded SQL keywords, table names, function calls, `CASE WHEN` fragments, etc. — just write them in the literal part of the interpolated string; do not wrap them in `RawSql`.

### 8.2 Type definition

`RawSql` is an independent `readonly struct` that does **not** inherit from `Expr`. It exists only as an `ExprString` helper:

```csharp
namespace LiteOrm.Common;

public readonly struct RawSql
{
    public string Sql { get; }
    public RawSql(string sql);
    public static RawSql From(string sql);
    public override string ToString();
}
```

`ExprString` provides a dedicated `AppendFormatted(RawSql value)` overload for `RawSql`: it appends `Sql` verbatim to the SQL buffer — **no** parameter is generated, **no** syntax processing is applied, and `[ ]` identifier placeholders are **not** replaced.

### 8.3 Usage example

**Example 1: LIMIT/OFFSET dynamic paging (numeric dynamic value)**

```csharp
using LiteOrm.Common;
using static LiteOrm.Common.Expr;

int pageSize = 20;
int offset = pageSize * pageIndex;

// Validate offset / pageSize as non-negative integers with upper bound before splicing (see 8.4)
var result = await dataViewDAO.Search(
    $"WHERE {Prop("Age")} >= {minAge} ORDER BY Id LIMIT {new RawSql(offset.ToString())}, {new RawSql(pageSize.ToString())}"
).ToListAsync();
```

**Example 2: Dynamic sort direction ASC/DESC (SQL keyword dynamic value)**

```csharp
// Asc/desc is chosen by the user; validate via enum whitelist (only "ASC" / "DESC" allowed), then inline
string direction = ascending ? "ASC" : "DESC";
var result = await dataViewDAO.Search(
    $"WHERE {Prop("Age")} >= {minAge} ORDER BY Id {new RawSql(direction)}"
).ToListAsync();
```

**Example 3: Dynamic column name / sort field (identifier dynamic value)**

```csharp
// sortField comes from the frontend; validate via whitelist (only real columns on the entity, alphanumeric + underscore)
string[] allowed = { "Id", "Name", "Age", "CreatedAt" };
string sortField = allowed.Contains(userField) ? userField : "Id";
var result = await dataViewDAO.Search(
    $"WHERE {Prop("Age")} >= {minAge} ORDER BY {new RawSql(sortField)} {new RawSql(direction)}"
).ToListAsync();

// Note: simple column names can also use Expr.Prop(sortField) (built-in name validation and quote wrapping, safer);
//       use RawSql only for complex expressions (e.g. "COALESCE(col1, col2)") or when you need to bypass name validation.
```

The interpolation holes above go through different paths:
- `new RawSql(...)` → dynamic values spliced verbatim into the SQL text
- `Expr.Prop("Age")` → goes through `Expr.ToSql`, with the column name wrapped in dialect quotes
- `{minAge}` → auto-parameterized as `@0`

### 8.4 Security constraints

`RawSql` bypasses LiteOrm's parameterization mechanism. **The caller must ensure** the `Sql` text is safe. Validation differs by value type:

| Dynamic value type | Validation | Example |
|-------------------|------------|---------|
| Numeric (LIMIT row count, etc.) | Range validation: non-negative integer + reasonable upper bound | `if (pageSize < 0 || pageSize > 1000) throw ...` |
| SQL keyword (ASC/DESC) | Enum whitelist: only predefined legal tokens | `direction = ascending ? "ASC" : "DESC";` |
| Identifier (column name) | Whitelist: only real columns on the entity, with charset validation (alphanumeric + underscore) | `string[] allowed = {...}; if (!allowed.Contains(f)) throw ...` |

Other general rules:

| Rule | Description |
|------|--------------|
| Not covered by ExprValidator | `RawSql` is not an `Expr`; it is not scanned by validators such as `ExprValidator.CreateQueryOnly()` — use it only in trusted server-side DAO code |
| No JSON round-trip | `RawSql` is not an `Expr` and cannot be serialized/deserialized via `ExprJsonConverter`; frontend Expr JSON cannot carry raw SQL |
| Do not use RawSql for static content | Hardcoded SQL fragments should be written directly in the `ExprString` literal; wrapping them in `RawSql` obscures the real intent |
| Prefer Expr | Use `Expr.Prop` for simple column names (built-in name validation and quote wrapping); anything expressible via `Expr.Func`/`Expr.Sql` (pre-registered `GenericSqlExpr`) should not use `RawSql` |

> If you need to safely pass runtime strings/complex values inside custom SQL, register a callback via `GenericSqlExpr.Register` and parameterize using `outputParams` inside the callback. See [Security](../03-advanced-topics/08-security.en.md).

### 8.5 Difference from GenericSqlExpr

| Aspect | `RawSql` | `GenericSqlExpr` |
|--------|----------|------------------|
| Is it an `Expr` | No (independent struct) | Yes (inherits `LogicExpr`) |
| Registration | None, construct directly | Must call `Register` first |
| Parameterization | Not supported, plain text inlining | Supported, callback can use `outputParams` |
| Use case | Dynamic values unsuitable for parameterization (LIMIT row counts, ASC/DESC, dynamic column names) | Reusable, dynamic SQL fragments needing runtime parameters |
| Validator control | Not scanned | `ExprValidator.CreateQueryOnly()` allows it by default |

In short: **use `RawSql` for dynamic values that cannot be parameterized; use `GenericSqlExpr` for reusable fragments that need parameterization; write purely static content directly in the literal**.

## 9. Available entry points

| DAO | Method | Notes |
|-----|--------|-------|
| `ObjectViewDAO<T>` / `IObjectViewDAO` | `Search(ref ExprString sqlBody, bool isFull = false)` | Auto-prepends `SELECT {AllFields} FROM {From}`; returns entity/view list |
| `ObjectViewDAO<T>` / `IObjectViewDAO` | `SearchAs<TResult>(ref ExprString sqlBody)` | Full-SQL projection to any `TResult` |
| `DataViewDAO<T>` | `Search(ref ExprString sqlBody, bool isFull = false)` | Returns `DataTable` |
| `DAOBase` | `GetValue<T>(ref ExprString sqlBody)` | Returns a scalar value |
| `DAOBase` | `Execute(ref ExprString sqlBody)` | Runs a non-query; returns affected rows |
| `DAOBase` | `Query<TResult>(ref ExprString sqlBody, ...)` | Query with a custom reader |

All of these use `[InterpolatedStringHandlerArgument("")]` to pass the current DAO as the context to `ExprString`, so you just pass the interpolated string directly — no need to manually `new ExprString(...)`.

## 10. Boundaries and caveats

- **Order-sensitive**: `ExprString` consumes `Expr` holes in order of appearance, and parameter indices increment in that order. This differs from full `SelectExpr` traversal, so in complex queries table aliases may not bind by scope automatically.
- **Main-table alias**: `ExprString` registers the main table with alias `T0` in the context at construction. Inserting the main table again as a `FromExpr` hole can cause duplicate alias assignment — either pre-set alias `T0` on that `FromExpr`, or use the `{From}` placeholder instead.
- **SelectExpr before FromExpr**: inside `ExprString`, if a `SelectExpr` appears before a `FromExpr`, columns without an explicit table alias may not bind to the default table correctly (the main query already mitigates this by pre-creating the default main-table context, but subqueries still need care). For complex multi-table queries, pre-assign aliases on `FromExpr`/`PropertyExpr`.
- **No automatic `CommonTableExpr` expansion**: `ExprString` does not translate `CommonTableExpr` into a `WITH` clause. When you need a CTE, write the full `WITH ... SELECT ...` directly, or build the CTE block via `SelectExpr.With(name)` and route through the structured pipeline.

```csharp
// CTE inside ExprString must be handwritten in full
var result = await dataViewDAO.Search(
    $"""
    WITH ActiveUsers AS (
        SELECT Id, UserName, Age
        FROM Users
        WHERE Age >= {minAge}
    )
    SELECT Id, UserName, Age
    FROM ActiveUsers
    """,
    isFull: true
).GetResultAsync();
```

## 11. Common pitfalls

The following are common mistakes when using `ExprString`. Each item shows an "❌ Wrong" vs "✅ Right" comparison.

### 11.1 Wrapping an interpolation hole inside a LIKE pattern

`{keyword}` is parameterized into `@0`, but wrapping it inside the `'%...%'` literal produces `WHERE UserName LIKE '%@0%'` — the placeholder sits inside a literal and is not recognized.

❌ `WHERE UserName LIKE '%{keyword}%'`
✅ `WHERE {Prop("UserName").Contains(keyword)}`

### 11.2 Inserting a column name as a plain value

Plain values are parameterized into `@0`; inserting a column name as a plain variable turns it into a string constant parameter, not a column reference.

❌ `WHERE {ageField} >= {minAge}` (`ageField` is a `string`)
✅ `WHERE {Prop(ageField)} >= {minAge}`

### 11.3 Hardcoding a single-dialect quote character

Writing `` ` `` or `"` directly will not be replaced; cross-database code breaks.

❌ `` SELECT `Id` FROM `Users` ``
✅ `SELECT [Id] FROM [Users]` (the framework replaces `[ ]` per dialect)

### 11.4 Forgetting `isFull: true` for full SQL

Without `isFull: true`, the DAO auto-prepends `SELECT {AllFields} FROM {From}`, producing a duplicated `SELECT`.

❌ Write a full `SELECT ... FROM ...` without `isFull: true`
✅ Declare `isFull: true` explicitly for full SQL

### 11.5 Re-inserting the main-table FromExpr

`ExprString` already registers the main table with alias `T0` at construction; inserting the main-table `FromExpr` again triggers duplicate alias allocation.

❌ `WHERE {mainFrom} {Prop("Age")} >= {minAge}` (`mainFrom` is the main table)
✅ Just write the condition `WHERE {Prop("Age")} >= {minAge}` — the main table is managed by the context

### 11.6 Expecting CommonTableExpr to auto-expand

`ExprString` does not translate `CommonTableExpr` into a `WITH` clause.

❌ `SELECT * FROM {cte}` (`cte` is a `CommonTableExpr`)
✅ Handwrite the full `WITH ... SELECT ...`, or use `SelectExpr.With(name)` via the structured pipeline

### 11.7 Calling ExprString from the Service layer

`ExprString` overloads are exposed only on the DAO layer; the Service layer has no such entry point, so the compiler will fail to find the overload.

❌ `userViewService.SearchAsync($"WHERE ...")`
✅ Use the Service's `Expr`/Lambda overloads, or drop down to a custom DAO

### 11.8 Subquery SelectExpr before FromExpr, no alias

`ExprString` consumes `Expr` holes in order; when a `SelectExpr` precedes a `FromExpr` inside a subquery, columns without an explicit table alias cannot bind to the default table.

❌ Insert `SelectExpr` (columns without alias) before `FromExpr` in a subquery
✅ Assign explicit table aliases on both `FromExpr` and `PropertyExpr`

### 11.9 Putting unvalidated input inside RawSql

`RawSql` content is spliced into SQL verbatim, with no parameterization. `RawSql` can inline dynamic values (e.g. a `LIMIT` row count), but the value **must be strictly validated first** (e.g. non-negative integer); never splice unvalidated user input, especially string-type input.

❌ `$"LIMIT {new RawSql(userInput)}"` (`userInput` is a frontend string, unvalidated)
❌ `$"WHERE {new RawSql($"Name = '{userInput}'")}"` (`userInput` comes from the frontend)
✅ Validate numeric dynamic values before inlining: `if (pageSize < 0 || pageSize > 1000) throw ...; ... LIMIT {new RawSql(pageSize.ToString())}`
✅ Parameterize string-type user values: `$"WHERE {Expr.Prop("Name")} = {userInput}"`

## 12. Recommended style summary

1. For conditions expressible via `Expr`/Lambda, build the `Expr` first and interpolate it, instead of hardcoding column names and values in the string.
2. Insert plain values via `{variable}` to let the framework parameterize them; never splice user input via string concatenation.
3. Use `[ ]` as the identifier placeholder when handwriting; the framework replaces it per dialect.
4. For complex multi-table queries, pre-assign table aliases on `FromExpr`/`PropertyExpr` instead of relying on automatic context allocation.
5. Use `isFull: true` for full SQL, and combine with `{Table}`/`{From}`/`{AllFields}` to reuse the DAO's table definition.
6. Write CTEs as full `WITH ... SELECT ...`, or use `SelectExpr.With(name)`.
7. Use `RawSql` only for dynamic values unsuitable for parameterization (e.g. `LIMIT` row counts, validate as non-negative integers first) or trusted static dialect fragments; string-type user values must go through the `Expr` or parameterization path.

## 13. Related links

- [Query Overview](./04-query-overview.en.md)
- [Lambda Guide](./05-lambda-guide.en.md)
- [Expr Guide](./06-expr-guide.en.md)
- [CRUD Guide](./03-crud-guide.en.md)
- [Associations](./08-associations.en.md)
- [Mixing Lambda and Expr](./09-lambda-expr-mixing.en.md)
- [CTE Guide](./10-cte-guide.en.md)
- [Expression Extension](../04-extensibility/01-expression-extension.en.md)
