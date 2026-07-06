# Query Overview

LiteOrm supports three query styles: **Lambda**, **`Expr`**, and **`ExprString`**. They all ultimately produce parameterized SQL, but differ in positioning and applicable scenarios.

This article is the overview and entry point for query capabilities. Full usage of each style is covered in:

- [Lambda Guide](./05-lambda-guide.en.md)
- [Expr Guide](./06-expr-guide.en.md)
- [ExprString Guide](./07-exprstring-guide.en.md)

## 1. Comparison of the three query styles

| Style | Syntax | Best for | Type safety | Entry layer |
|-------|--------|----------|-------------|-------------|
| Lambda | `u => u.Age > 18` | Fixed conditions, clear business semantics | ✅ Strong | Service + DAO |
| `Expr` | `Expr.Prop("Age") > 18` | Dynamic assembly, query builders, multi-condition backend filtering | ✅ Compile-time | Service + DAO |
| `ExprString` | `$"WHERE {expr}"` | SQL fragments or full SQL inside the DAO | ❌ Runtime | DAO only |

### 1.1 Relationship

```
Lambda  ──(convert)──▶  Expr  ──(ToSql)──▶  parameterized SQL
                          ▲
                          │ (embed)
ExprString  ──────────────┘ ──(ToSql)──▶  parameterized SQL
```

- **Lambda** is first converted into `Expr`, then SQL is generated uniformly, so Lambda and `Expr` are not two isolated capability systems.
- **`ExprString`** is a separate SQL-assembly channel, but its interpolation holes can embed `Expr` objects to reuse `Expr`'s parameterization and identifier-quoting rules.

### 1.2 Heuristics

- **Prefer Lambda**: most business queries are the most intuitive — strongly typed, best readability.
- **Use `Expr` when conditions must be accumulated dynamically**: e.g. admin filters, frontend query builders, cross-layer condition passing.
- **Use `ExprString` only when the DAO needs handwritten SQL**: it can supplement a `Search` condition fragment or carry full SQL; the Service layer does not expose this entry point.

### 1.3 Decision tree

```
Do you need to handwrite a SQL fragment or full SQL?
├─ Yes ─▶ use ExprString (DAO only)
└─ No  ─▶ must conditions be accumulated dynamically / passed across layers?
        ├─ Yes ─▶ use Expr
        └─ No  ─▶ use Lambda
```

## 2. Service vs DAO query entry points

LiteOrm's query entry points are split into two layers: the **Service layer** for business, and the **DAO layer** for lower-level capabilities.

### 2.1 Service

```csharp
using static LiteOrm.Common.Expr;

var users1 = await userService.SearchAsync(u => u.Age >= 18);
var users2 = await userService.SearchAsync(Prop("Age") >= 18);
var users3 = await userService.SearchAsAsync<UserSummary>(
    From<UserView>()
        .Where(Prop("Age") >= 18)
        .Select(
            Prop("Id"),
            Prop("UserName"),
            Expr.If(Prop("IsVip") == true, "VIP", "Normal").As("Level")
        )
);
```

- Service query entry points are primarily Lambda / `Expr`, and also support `SelectExpr`-based `SearchAs(...)` / `SearchAsAsync(...)` projection queries, suitable for scenarios that need clear business semantics, transactions, AOP, or service encapsulation.
- The Service **does not** provide `ExprString` query overloads; if the requirement has become "handwrite SQL", switch to the DAO.

### 2.2 DAO

```csharp
using static LiteOrm.Common.Expr;

var users1 = await userViewDAO.Search(u => u.Age >= 18).ToListAsync();
var users2 = await userViewDAO.Search(Prop("Age") >= 18).ToListAsync();
var users3 = await userViewDAO.Search($"WHERE {Prop("Age")} > {minAge}").ToListAsync();
```

- The DAO supports Lambda / `Expr`, plus `ExprString`, so it is more suitable for custom SQL fragments, full SQL, complex projection queries, and DataTable queries.
- When you need IQueryable projection `SearchAs(...)`, `ExprString` `SearchAs(...)`, `Query(...)`, `Execute(...)`, `GetValue(...)`, or similar lower-level capabilities, use the DAO directly.

## 3. `Search` vs `SearchAs`

Both `Search` and `SearchAs` are query entry points, but with different responsibilities:

| Aspect | `Search` / `SearchAsync` | `SearchAs<TResult>` / `SearchAsAsync<TResult>` |
|--------|--------------------------|------------------------------------------------|
| Return type | Entity type `T` (or view `TView`) | **Any** `TResult`: entity, anonymous type, scalar, custom projection class |
| Construction | `Expr` / Lambda / `ExprString` (DAO only) | `SelectExpr` (Service+DAO) / Lambda projection / `ExprString` (DAO only) |
| Field mapping | Positional mapping by `SelectColumns` registered in `TableInfoProvider` | See notes below |
| Typical scenario | "query rows of this table" | "cross-table projection, aggregation, computed columns, type switching" |

### 3.1 Basic usage comparison

```csharp
using static LiteOrm.Common.Expr;

// Search: returns a list of User entities
List<User> users = await viewService.SearchAsync(Prop("Age") >= 18);

// SearchAs: project to an anonymous type
var summaries = await viewService.SearchAsAsync<dynamic>(
    From<UserView>()
        .Where(Prop("Age") >= 18)
        .Select(Prop("UserName"), Prop("Age"))
);

// SearchAs: project to a custom type
var summaries = await viewService.SearchAsAsync<UserSummary>(
    From<UserView>()
        .Where(Prop("Age") >= 18)
        .Select(
            Prop("Id"),
            Prop("UserName"),
            Expr.If(Prop("IsVip") == true, "VIP", "Normal").As("Level")
        )
);
```

### 3.2 Caveats when using `SearchAs`

`SearchAs<TResult>` result mapping is handled by `DataReaderConverter` and takes different paths depending on whether `TResult` is registered in `TableInfoProvider`. Beginners most often trip over the following:

#### 3.2.1 Three mapping paths for `TResult`

| `TResult` type | Mapping | Needs registration? |
|----------------|---------|---------------------|
| **Scalar** (`int` / `string` / `DateTime`, ...) | Reads column 0 directly | No |
| **Anonymous type** (`new { UserName = ..., Age = ... }`) | Matches column names against constructor parameter names (case-insensitive) | No |
| **Type registered in `TableInfoProvider`** | Positional mapping by `SelectColumns` | Yes |
| **Unregistered plain type** | Takes **the first public constructor** and matches column names against its parameter names (case-insensitive) | No |

> Key difference: registered types use "positional mapping" — the `Select` column order must match the order registered in `TableInfoProvider`; unregistered plain types / anonymous types use "name matching" — the alias of each `Select` column must match a constructor parameter name.

#### 3.2.2 Column aliases must match target member names

For types mapped by name (anonymous types, unregistered plain classes), the alias of a `Select` column (`.As("xxx")`) must match a member name (constructor parameter name) of the target type, otherwise the field falls back to its default value:

```csharp
public class UserSummary
{
    public string UserName { get; set; }
    public string Level { get; set; }

    // Note: DataReaderConverter takes GetConstructors()[0]
    // parameter names must match column aliases
    public UserSummary(string userName, string level)
    {
        UserName = userName;
        Level = level;
    }
}

// ✅ Column alias "Level" matches constructor parameter name "level"
await viewService.SearchAsAsync<UserSummary>(
    From<UserView>().Select(
        Prop("UserName"),
        Expr.If(Prop("IsVip") == true, "VIP", "Normal").As("Level")
    )
);

// ❌ Without .As("Level"), the column name is something like "Expr_If_...",
//    so Level becomes null
```

#### 3.2.3 Use `SearchAs<T>` for scalar results, not `Search`

Aggregate queries like `COUNT` / `SUM` / `MAX` return a single scalar value; use `SearchAs<int>` / `SearchAs<long>` to read column 0 directly:

```csharp
// ✅ Scalar projection
var count = await viewService.SearchAsAsync<int>(
    From<UserView>().Select(Expr.Func("COUNT", Prop("Id")))
);
```

#### 3.2.4 Registered `TResult` uses positional mapping; column order matters

If `TResult` is an entity type registered in `TableInfoProvider` (e.g. using `SearchAs<User>` directly), mapping is positional by `SelectColumns` — the **order** of `Select` columns must match the registered column order; column names do not participate in matching. Prefer **unregistered projection types** (DTOs / anonymous types) to avoid order coupling.

#### 3.2.5 `TResult` must be instantiable

- It needs a public constructor
- Anonymous types naturally satisfy this (compiler-generated)
- DTOs / records need a public constructor
- Interfaces / abstract classes / types without a public constructor will fail

#### 3.2.6 DAO has two more overloads than Service

| Overload | Service | DAO |
|----------|---------|-----|
| `SearchAs<TResult>(SelectExpr)` | ✅ returns `List<TResult>` | ✅ returns `EnumerableResult<TResult>` |
| `SearchAs<TResult>(Expression<Func<IQueryable<T>, IQueryable<TResult>>>)` | ❌ | ✅ Lambda projection |
| `SearchAs<TResult>(ref ExprString sqlBody)` | ❌ | ✅ raw SQL projection |

When you need Lambda projection or raw SQL projection, switch to the DAO (see [2.2 DAO](#22-dao)).

## 4. Related links

- [CRUD Guide](./03-crud-guide.en.md)
- [Lambda Guide](./05-lambda-guide.en.md)
- [Expr Guide](./06-expr-guide.en.md)
- [ExprString Guide](./07-exprstring-guide.en.md)
- [Associations](./08-associations.en.md)
- [Mixing Lambda and Expr](./09-lambda-expr-mixing.en.md)
- [CTE Guide](./10-cte-guide.en.md)
- [Expression Extension](../04-extensibility/01-expression-extension.en.md)
