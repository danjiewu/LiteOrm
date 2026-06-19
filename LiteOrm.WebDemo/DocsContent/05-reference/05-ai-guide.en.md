# LiteOrm API Usage Guide (for AI)

## 1. Configuration and registration

### appsettings.json

```json
{
  "LiteOrm": {
    "Default": "DefaultConnection",
    "DataSources": [
      {
        "Name": "DefaultConnection",
        "ConnectionString": "Server=(local);Database=TestDB;User Id=sa;Password=123456;",
        "Provider": "System.Data.SqlClient.SqlConnection, System.Data.SqlClient",
        "SqlBuilder": null,
        "KeepAliveDuration": "00:10:00",
        "PoolSize": 16,
        "MaxPoolSize": 100,
        "ParamCountLimit": 2000,
        "SyncTable": false,
        "ReadOnlyConfigs": [
          {
            "ConnectionString": "Server=replica;Database=TestDB;User Id=sa;Password=123456;",
            "KeepAliveDuration": null,
            "PoolSize": null,
            "MaxPoolSize": null,
            "ParamCountLimit": null
          }
        ]
      }
    ]
  }
}
```

| Field | Type | Default | Description |
| --- | --- | --- | --- |
| `Default` | `string` | — | Default data source name |
| `DataSources[].Name` | `string` | — | Data source name (referenced by the `DataSource` parameter on `[Table]`) |
| `DataSources[].ConnectionString` | `string` | — | Database connection string |
| `DataSources[].Provider` | `string` | — | Fully qualified connection type name in the format `TypeName, AssemblyName` |
| `DataSources[].SqlBuilder` | `string` | `null` | Fully qualified SQL builder type name (optional; auto-matched from `Provider` when omitted) |
| `DataSources[].KeepAliveDuration` | `TimeSpan` | `00:10:00` | Connection keep-alive duration (`00:00:00` = unlimited) |
| `DataSources[].PoolSize` | `int` | `16` | Maximum number of cached connections in the pool |
| `DataSources[].MaxPoolSize` | `int` | `100` | Maximum concurrent connection limit |
| `DataSources[].ParamCountLimit` | `int` | `2000` | Maximum SQL parameter count (`0` = unlimited) |
| `DataSources[].SyncTable` | `bool` | `false` | Whether to automatically synchronize table creation |
| `DataSources[].ReadOnlyConfigs[]` | `array` | `[]` | Read-only replica configuration list (read/write splitting); omitted fields inherit from the primary data source |

### Service registration

```csharp
// Basic registration
builder.Host.RegisterLiteOrm();

// Registration with options
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterScope = true;                          // Default is true; automatically manages the DI scope lifecycle
    options.Assemblies = new[] { typeof(MyService).Assembly }; // Restrict scanned assemblies (defaults to scanning all)
    options.RegisterSqlBuilder("DefaultConnection", new MySqlBuilder()); // Register by data source name
    options.RegisterSqlBuilder(typeof(SqlConnection), new MySqlBuilder()); // Register by connection type
});
```

## 2. Entity and view definitions

```csharp
[Table("Users")]
public class User : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    [Column("UserName")]
    public string? UserName { get; set; }
    [Column("Age")]
    public int Age { get; set; }
    [Column("DeptId")]
    [ForeignType(typeof(Department), Alias = "Dept")]
    public int? DeptId { get; set; }
}

// View model (used for association queries)
public class UserView : User
{
    [ForeignColumn(typeof(Department), Property = "DeptName")]
    public string? DeptName { get; set; }
}
```

## 3. Service definitions

```csharp
// Entity type == view type (EntityService<T>, equivalent to EntityService<T, T>)
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<User>, IEntityViewServiceAsync<User>
{ }
public class UserService : EntityService<User>, IUserService { }

// Entity type != view type (EntityService<T, TView>; TView must inherit from T)
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<UserView>, IEntityViewServiceAsync<UserView>
{ }
public class UserService : EntityService<User, UserView>, IUserService { }
```

## 4. API reference

### IEntityService<T> (create, update, delete)

| Method | Return type |
| --- | --- |
| `Insert(T entity)` | `bool` |
| `Update(T entity)` | `bool` |
| `UpdateOrInsert(T entity)` | `bool` |
| `Delete(T entity)` | `bool` |
| `DeleteID(object id, params string[] tableArgs)` | `bool` |
| `Delete(LogicExpr expr, params string[] tableArgs)` | `int` |
| `Update(UpdateExpr expr, params string[] tableArgs)` | `int` |
| `BatchInsert(IEnumerable<T> entities)` | `void` |
| `BatchUpdate(IEnumerable<T> entities)` | `void` |
| `BatchUpdateOrInsert(IEnumerable<T> entities)` | `void` |
| `BatchDelete(IEnumerable<T> entities)` | `void` |
| `Batch(IEnumerable<EntityOperation<T>> entities)` | `void` |

### IEntityServiceAsync<T> (async create, update, delete)

| Method | Return type |
| --- | --- |
| `InsertAsync(T entity, CancellationToken ct = default)` | `Task<bool>` |
| `UpdateAsync(T entity, CancellationToken ct = default)` | `Task<bool>` |
| `UpdateOrInsertAsync(T entity, CancellationToken ct = default)` | `Task<bool>` |
| `DeleteAsync(T entity, CancellationToken ct = default)` | `Task<bool>` |
| `DeleteIDAsync(object id, string[] tableArgs = null, CancellationToken ct = default)` | `Task<bool>` |
| `DeleteAsync(LogicExpr expr, string[] tableArgs = null, CancellationToken ct = default)` | `Task<int>` |
| `UpdateAsync(UpdateExpr expr, string[] tableArgs = null, CancellationToken ct = default)` | `Task<int>` |
| `BatchInsertAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `BatchUpdateAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `BatchDeleteAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `BatchAsync(IEnumerable<EntityOperation<T>> entities, CancellationToken ct = default)` | `Task` |

### IEntityViewService<TView> (query, Expr style)

| Method | Return type |
| --- | --- |
| `GetObject(object id, params string[] tableArgs)` | `TView` |
| `SearchOne(Expr expr, params string[] tableArgs)` | `TView` |
| `Search(Expr expr = null, params string[] tableArgs)` | `List<TView>` |
| `SearchAs<TResult>(SelectExpr selectExpr, params string[] tableArgs)` | `List<TResult>` |
| `SearchOneAs<TResult>(SelectExpr selectExpr, params string[] tableArgs)` | `TResult` |
| `ForEach(Expr expr, Action<TView> func, params string[] tableArgs)` | `void` |
| `ExistsID(object id, params string[] tableArgs)` | `bool` |
| `Exists(Expr expr, params string[] tableArgs)` | `bool` |
| `Count(Expr expr = null, params string[] tableArgs)` | `int` |

### IEntityViewServiceAsync<TView> (async query)

| Method | Return type |
| --- | --- |
| `GetObjectAsync(object id, string[] tableArgs = null, CancellationToken ct = default)` | `Task<TView>` |
| `SearchOneAsync(Expr expr, string[] tableArgs = null, CancellationToken ct = default)` | `Task<TView>` |
| `SearchAsync(Expr expr = null, string[] tableArgs = null, CancellationToken ct = default)` | `Task<List<TView>>` |
| `SearchAsAsync<TResult>(SelectExpr selectExpr, params string[] tableArgs)` | `Task<List<TResult>>` |
| `SearchOneAsAsync<TResult>(SelectExpr selectExpr, params string[] tableArgs)` | `Task<TResult>` |
| `ForEachAsync(Expr expr, Func<TView, Task> func, string[] tableArgs = null, CancellationToken ct = default)` | `Task` |
| `ExistsIDAsync(object id, string[] tableArgs = null, CancellationToken ct = default)` | `Task<bool>` |
| `ExistsAsync(Expr expr, string[] tableArgs = null, CancellationToken ct = default)` | `Task<bool>` |
| `CountAsync(Expr expr = null, string[] tableArgs = null, CancellationToken ct = default)` | `Task<int>` |

### Lambda expression extension methods

> These methods come from `LambdaExprExtensions` and can be used without modifying the service class.

| Method | Return type |
| --- | --- |
| `Delete(Expression<Func<T, bool>> expression, params string[] tableArgs)` | `int` |
| `DeleteAsync(Expression<Func<T, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)` | `Task<int>` |
| `Search(Expression<Func<TView, bool>> expression, string[] tableArgs = null)` | `List<TView>` |
| `Search(Expression<Func<IQueryable<TView>, IQueryable<TView>>> expression, string[] tableArgs = null)` | `List<TView>` |
| `SearchOne(Expression<Func<TView, bool>> expression, string[] tableArgs = null)` | `TView` |
| `SearchOne(Expression<Func<IQueryable<TView>, IQueryable<TView>>> expression, string[] tableArgs = null)` | `TView` |
| `Exists(Expression<Func<TView, bool>> expression, params string[] tableArgs)` | `bool` |
| `Count(Expression<Func<TView, bool>> expression, params string[] tableArgs)` | `int` |
| `SearchAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)` | `Task<List<TView>>` |
| `SearchAsync(Expression<Func<IQueryable<TView>, IQueryable<TView>>> expression, string[] tableArgs = null, CancellationToken ct = default)` | `Task<List<TView>>` |
| `SearchOneAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)` | `Task<TView>` |
| `SearchOneAsync(Expression<Func<IQueryable<TView>, IQueryable<TView>>> expression, string[] tableArgs = null, CancellationToken ct = default)` | `Task<TView>` |
| `ExistsAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)` | `Task<bool>` |
| `CountAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)` | `Task<int>` |

> Additional note: Service query APIs include the `Expr` overloads, these Lambda extensions, and `SearchAs(...)` / `SearchAsAsync(...)` based on `SelectExpr`. If you need `ExprString`, full SQL, IQueryable-based `SearchAs(...)`, or DataTable-oriented queries, switch to DAO.

### ObjectDAO<T> (create, update, delete only)

| Method | Return type |
| --- | --- |
| `Insert(T entity)` | `bool` |
| `Update(T entity, object timestamp = null)` | `bool` |
| `Delete(T entity)` | `bool` |
| `Delete(LogicExpr expr)` | `int` |
| `Update(UpdateExpr expr)` | `int` |
| `BatchInsert(IEnumerable<T> entities)` | `void` |
| `BatchUpdate(IEnumerable<T> entities)` | `void` |
| `BatchDelete(IEnumerable<T> entities)` | `void` |
| `UpdateOrInsert(T entity)` | `UpdateOrInsertResult` |
| `BatchUpdateOrInsert(IEnumerable<T> entities)` | `void` |
| `InsertAsync(T entity, CancellationToken ct = default)` | `Task<bool>` |
| `UpdateAsync(T entity, object timestamp = null, CancellationToken ct = default)` | `Task<bool>` |
| `DeleteAsync(T entity, CancellationToken ct = default)` | `Task<bool>` |
| `DeleteAsync(LogicExpr expr, CancellationToken ct = default)` | `Task<int>` |
| `UpdateAsync(UpdateExpr expr, CancellationToken ct = default)` | `Task<int>` |
| `BatchInsertAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `BatchUpdateAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `BatchDeleteAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `UpdateOrInsertAsync(T entity, CancellationToken ct = default)` | `Task<UpdateOrInsertResult>` |
| `BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `DeleteByKeys(params object[] keys)` | `bool` |
| `DeleteByKeysAsync(object[] keys, CancellationToken ct = default)` | `Task<bool>` |
| `BatchDeleteByKeys(IEnumerable keys)` | `void` |
| `BatchDeleteByKeysAsync(IEnumerable keys, CancellationToken ct = default)` | `Task` |

### ObjectViewDAO<T> (query only)

`EnumerableResult<T>` supports: `.ToList()` / `.ToListAsync()` / `.FirstOrDefault()` / `.FirstOrDefaultAsync()` / `.GetResult()` / `.GetResultAsync()` / `await foreach`

| Method | Return type |
| --- | --- |
| `Search(Expr expr = null)` | `EnumerableResult<T>` |
| `Search(Expression<Func<IQueryable<T>, IQueryable<T>>> expr)` | `EnumerableResult<T>` |
| `Search(ref ExprString sqlBody, bool isFull = false)` | `EnumerableResult<T>` |
| `SearchAs<TResult>(SelectExpr selectExpr, Func<DbDataReader, TResult> readerFunc = null)` | `EnumerableResult<TResult>` |
| `SearchAs<TResult>(Expression<Func<IQueryable<T>, IQueryable<TResult>>> expr, Func<DbDataReader, TResult> readerFunc = null)` | `EnumerableResult<TResult>` |
| `SearchAs<TResult>(ref ExprString sqlBody)` | `EnumerableResult<TResult>` |
| `GetObject(params object[] keys)` | `EnumerableResult<T>` |
| `Count(Expr expr)` | `ValueResult<int>` |
| `Exists(object o)` / `Exists(T o)` | `ValueResult<bool>` |
| `ExistsKey(params object[] keys)` | `ValueResult<bool>` |
| `Exists(Expr expr)` | `ValueResult<bool>` |

### DataViewDAO<T> (query returning DataTable)

`DataTableResult` supports: `.GetResult()` / `.GetResultAsync()`

| Method | Return type |
| --- | --- |
| `Search(Expr expr)` | `DataTableResult` |
| `Search(string[] propertyNames, Expr expr)` | `DataTableResult` |
| `Search(ref ExprString sqlBody, bool isFull = false)` | `DataTableResult` |

## 5. Transactions

```csharp
// Declarative
[Transaction]
public void Transfer() { ... }

// Manual
using var transaction = SessionManager.Current.BeginTransaction();
try { transaction.Commit(); }
catch { transaction.Rollback(); throw; }
```

## 6. Advanced features

### Sharding

```csharp
[Table("Orders_{0}")]
public class Order : ObjectBase, IArged
{
    public string[] GetArgs() => new string[] { (UserId % 10).ToString() };
}
```

### Multiple data sources

```csharp
[Table("Users", DataSource = "Secondary")]
public class User : ObjectBase { ... }
```

### Custom DAO / service

```csharp
public interface IUserCustomDAO : IObjectViewDAO<UserView> { ... }
public class UserCustomDAO : ObjectViewDAO<UserView>, IUserCustomDAO { ... }

public interface IUserService : IEntityService<User>, IEntityServiceAsync<User> { ... }
public class UserService : EntityService<User>, IUserService { ... }
```

### ServiceFactory

```csharp
public interface ServiceFactory
{
    IUserService UserService { get; }
    IUserCustomDAO UserCustomDAO { get; }
}
services.AddServiceGenerator<ServiceFactory>();
var factory = scope.ServiceProvider.GetRequiredService<ServiceFactory>();
```

### Service exception hooks

```csharp
[ExceptionHook(typeof(OrderExceptionHook), Mode = ServiceExceptionHookMode.Notify)]
public interface IOrderService
{
    Task SubmitAsync(long id);
}

[AutoRegister(Lifetime.Scoped, typeof(IServiceExceptionHook))]
public class OrderExceptionHook : IServiceExceptionHook
{
    public void OnException(ServiceExceptionContext context)
    {
        // Access exception, method name, arguments, SQL stack, and more
    }
}
```

- `[ExceptionHook]` can be applied to methods, classes, and interfaces
- `Notify` is observe-only and must not swallow exceptions
- `Handle` can convert the exception into a defined result through `context.Handle(result)`
- Method-level `ExceptionHook` runs before the global `ServiceInvokeInterceptor.ExceptionHandling` event

## 7. Attribute quick reference

| Attribute | Purpose |
| --- | --- |
| `[Table("TableName")]` | Specifies the table name; optional `DataSource` parameter |
| `[Column("ColName", IsPrimaryKey, IsIdentity)]` | Specifies the column name and property behavior |
| `[ForeignType(typeof(T), Alias, AutoExpand)]` | Specifies the foreign-key related type; `AutoExpand` extends relation paths |
| `[TableJoin(typeof(T), ForeignKeys, AliasName, AutoExpand)]` | Type-level relation definition supporting composite keys and path reuse |
| `[ForeignColumn(typeof(T), Property)]` | Column projected from a related table (for view models) |
| `[Transaction]` | Declarative transaction |
| `[ExceptionHook(typeof(THook), Mode = ...)]` | Declares a service exception hook for alerting or exception-to-result handling |
| `[AutoRegister]` | Automatically registers the type into the DI container |

## 8. Expr expression system

### Three query styles

| Style | Suitable for |
| --- | --- |
| Lambda expression `u => u.Age > 18` | Simple conditions with compile-time type safety |
| Expr object (operators / fluent methods) | Complex conditions, dynamically accumulated conditions, and chained queries |
| ExprString interpolated string | Condition fragments or full SQL inside custom DAOs |

### Expr static factory methods

| Method | Return type | Description |
| --- | --- | --- |
| `Expr.Prop("Name")` | `PropertyExpr` | Property expression |
| `Expr.Prop("alias", "Name")` | `PropertyExpr` | Property expression with a table alias |
| `Expr.Value(obj)` | `ValueExpr` | Parameterized value |
| `Expr.Const(obj)` | `ValueExpr` | Constant value (inlined into SQL, not parameterized) |
| `Expr.Func("ABS", expr)` | `FunctionExpr` | Function call |
| `Expr.Aggregate("SUM", expr, isDistinct)` | `FunctionExpr` | Aggregate function (`IsAggregate=true`) |
| `Expr.Concat(e1, e2)` | `ValueSet` | CONCAT string composition (extension method) |
| `Expr.Lambda<T>(u => u.Age > 18)` | `LogicExpr` | Converts Lambda to Expr |
| `Expr.From<T>(tableArgs)` | `FromExpr` | Starting point for chained queries |
| `Expr.Sql("key", arg)` | `GenericSqlExpr` | Dynamic SQL fragment |
| `Expr.Delete<T>(tableArgs)` | `DeleteExpr` | Creates a DELETE expression |
| `Expr.If(condition, thenExpr, elseExpr)` | `FunctionExpr` | IF expression |
| `Expr.Case(cases, elseExpr)` | `FunctionExpr` | CASE expression (tuple array form) |
| `Expr.Case(params (LogicExpr, ValueTypeExpr)[])` | `FunctionExpr` | CASE expression (tuple array form, no ELSE) |
| `Expr.Case(params Expr[])` | `FunctionExpr` | CASE expression (alternating argument form) |
| `Expr.Query<T>(expression)` | `Expr` | Converts an IQueryable Lambda to Expr |
| `Expr.Query<T, TResult>(expression)` | `Expr` | Converts an IQueryable Lambda with a return value to Expr |
| `Expr.Exists<T>(innerExpr, tableArgs)` | `ForeignExpr` | EXISTS query on a related table |
| `Expr.Exists(type, innerExpr, tableArgs)` | `ForeignExpr` | EXISTS query on a related table (explicit type) |
| `Expr.ExistsRelated<T>(innerExpr, tableArgs)` | `ForeignExpr` | EXISTS query using automatic relation discovery |
| `Expr.ExistsRelated(type, innerExpr, tableArgs)` | `ForeignExpr` | EXISTS query using automatic relation discovery (explicit type) |
| `Expr.Exists<T>(lambda)` | `bool` | Used only inside Lambda expressions to build EXISTS queries (direct calls throw an exception) |
| `Expr.ExistsRelated<T>(lambda)` | `bool` | Used only inside Lambda expressions to build automatic-related EXISTS queries (direct calls throw an exception) |

Additional rule:

- Expression objects such as `PropertyExpr`, `TableExpr`, `ForeignExpr`, `FunctionExpr`, `SelectExpr`, `SelectItemExpr`, `CommonTableExpr`, and `GenericSqlExpr` treat names and aliases as **case-insensitive** when comparing objects and calculating hash codes.
- As a result, `Prop("User", "Name")` and `Prop("user", "name")` are treated as the same expression, and aliases `T0` and `t0` are treated as the same alias.

### ExprVisitor traversal methods

`ExprVisitor` provides four traversal modes. All methods accept an optional `CancellationToken` parameter for external interruption:

| Method | Traversal mode | Short-circuit | Description |
|--------|---------------|--------------|-------------|
| `ExprVisitor.Visit(Func<Expr,bool>, root, order?, ct?)` | `Func<Expr,bool>` delegate | ✅ | Returns `false` or cancel to stop |
| `ExprVisitor.Visit(Action<Expr>, root, order?, ct?)` | `Action<Expr>` delegate | ❌ | Always completes (unless cancelled) |
| `ExprVisitor.Visit(IExprNodeVisitor, root, ct?)` | `IExprNodeVisitor` interface | ❌ | BeginVisit (pre) + EndVisit (post) |
| `ExprVisitor.Validate(ExprValidator, root, order?, ct?)` | `ExprValidator` base class | ✅ | Validate returns `false` or cancel to stop |

> `IExprNodeVisitor.BeginVisit(Expr, CancellationToken)` and `EndVisit(Expr, CancellationToken)` receive the cancellation token. If cancelled via `CancellationTokenSource.Cancel()` inside a callback, traversal will stop.

### CycleDetector circular reference detection

`CycleDetector` uses `ExprVisitor` to detect circular references in `Expr` trees (e.g., `Source` chain loops):

| Method | Description |
|--------|-------------|
| `CycleDetector.HasCycle(Expr root)` | Whether a circular reference exists |
| `CycleDetector.FindCycle(Expr root)` | Returns the node causing the cycle, or `null` |
| `CycleDetector.Detect(Expr root)` | Returns `CycleResult` (with `CycleNode` and `Path`) |

Detection is based on reference equality (`ReferenceEquals`) and uses `CancellationTokenSource` to interrupt traversal without exception-based control flow.

### Operator overloads

Operators on `PropertyExpr` / `ValueTypeExpr`:

| Operator | Description | Return type |
| --- | --- | --- |
| `==` `!=` `>` `<` `>=` `<=` | Comparison | `LogicExpr` |
| `+` `-` `*` `/` `%` | Arithmetic | `ValueTypeExpr` |
| `-expr` `~expr` | Unary negation / bitwise NOT | `ValueTypeExpr` |

Operators on `LogicExpr`:

| Operator | Description | Return type |
| --- | --- | --- |
| `&` | AND (returns the other side when left or right is null, useful for dynamic accumulation) | `AndExpr` |
| `\|` | OR (returns the other side when left or right is null, useful for dynamic accumulation) | `OrExpr` |
| `!` | NOT | `NotExpr` |

Additional notes:

- The Lambda conditional operator `a ? b : c` is automatically converted into `Expr.If(...)`, then rendered as SQL `CASE`

### PropertyExpr extension methods

| Category | Methods |
| --- | --- |
| Comparison | `.Equal(v)` `.NotEqual(v)` `.GreaterThan(v)` `.LessThan(v)` `.GreaterThanOrEqual(v)` `.LessThanOrEqual(v)` |
| Set | `.In(IEnumerable)` `.In(params items)` `.In(Expr)` |
| Range | `.Between(low, high)` |
| String | `.Like(pattern)` `.Contains(text)` `.StartsWith(text)` `.EndsWith(text)` |
| Null | `.IsNull()` `.IsNotNull()` |
| Type conversion | `.Cast(DbType)` |
| Alias | `.As("alias")` → `SelectItemExpr` |
| Aggregate | `.Count(isDistinct)` `.Sum()` `.Avg()` `.Max()` `.Min()` |
| Sort | `.Asc()` `.Desc()` → `OrderByItemExpr` |

### LogicExpr extension methods

`.And(right)` `.Or(right)` `.Not()`

### Chained query construction

`Expr.From<T>()` is the entry point and supports the following chained calls in SQL clause order:

```csharp
using static LiteOrm.Common.Expr;
var query = From<User>()
    .Where(Prop("Age") > 18)                         // WhereExpr
    .SelectAll()                                     // SelectExpr, equivalent to SELECT *
    .Where(Prop("Status") == 1)
    .OrderBy(Prop("DeptId").Asc())                   // OrderByExpr
    .Section(0, 20);                                 // SectionExpr (skip, take)
```

You can also explicitly project selected fields when needed:

```csharp
using static LiteOrm.Common.Expr;
var query = From<User>()
    .Where(Prop("Age") > 18)                         // WhereExpr
    .GroupBy(Prop("DeptId"))                         // GroupByExpr
    .Having(Prop("Id").Count() > 5)                  // HavingExpr
    .Select(Prop("DeptId"),                          // SelectExpr
            Prop("Id").Count().As("Cnt"))
    .OrderBy(Prop("DeptId").Asc())                   // OrderByExpr
    .Section(0, 20);                                 // SectionExpr (skip, take)
```

`SelectExpr` can be used for `IN` subqueries:

```csharp
using static LiteOrm.Common.Expr;
// IN subquery
var subQuery = From<Department>()
    .Where(Prop("Name") == "IT")
    .Select("Id");
var expr = Prop("DeptId").In(subQuery);
```

UpdateExpr / DeleteExpr (used by `ObjectDAO.Delete(LogicExpr)` and similar APIs):

```csharp
using static LiteOrm.Common.Expr;
var update = new UpdateExpr(From<User>(), Prop("Id") == 1);
update.Set(("UserName", Value("NewName")), ("Age", Value(30)));

var delete = new DeleteExpr(From<User>(), Prop("Age") < 18);
```

### ExprString

The interpolated string handler lets you embed Expr objects directly into DAO methods that accept `ExprString`, such as `Search(...)` and `SearchAs(...)`. Service query APIs do not expose `ExprString`:

```csharp
using static LiteOrm.Common.Expr;
// Embedded Expr objects are automatically converted into parameterized SQL fragments
var result = dao.Search($"WHERE {Prop("DeptName") == deptName} AND {Prop("Age") > 18}");

// Use full SQL through DAO together with isFull: true
var table = dataViewDao.Search(
    $"SELECT [Id], [UserName] FROM [Users] WHERE {Prop("Age")} > {minAge}",
    isFull: true
);
```

> When hand-writing table or column names in `ExprString`, you can use `[` and `]` as provider-agnostic quote placeholders. LiteOrm rewrites them to the current database's real identifier delimiters right before command execution.
>
> `ExprString` does not auto-expand `SelectExpr.With(name)` / `CommonTableExpr` into `WITH` SQL. When you need CTE, prefer the structured `Expr` / `SelectExpr` path; if you must use `ExprString`, handwrite the full `WITH ... SELECT ...` SQL.

### Common patterns

```csharp
using static LiteOrm.Common.Expr;
// Dynamically accumulate conditions (& is null-safe)
LogicExpr condition = null;
if (minAge.HasValue)  condition &= Prop("Age") >= minAge.Value;
if (deptId.HasValue)  condition &= Prop("DeptId") == deptId.Value;
if (!string.IsNullOrEmpty(name)) condition &= Prop("UserName").Contains(name);
var users = await dao.Search(condition).ToListAsync();

// EXISTS query on a related table
var expr = Exists<Department>(Prop("Name") == "IT");
// Equivalent Lambda form (inside a Lambda query):
var expr = Lambda<User>(u => Exists<Department>(d => d.Name == "IT"));

// SearchAs selects a subset of fields
var result = dao.SearchAs(
    From<User>()
        .Where(Prop("Age") > 18)
        .Select("Id", "UserName")
).ToList();
```

### ExistsRelated notes

`ExistsRelated` filters the primary table by conditions on a related table without requiring the related fields to be explicitly exposed on the view model.

**Relation matching order: forward relations first; if no forward relation is found, reverse relations are tried. Multiple relation paths are combined with** **`OR`** **.**

```csharp
using static LiteOrm.Common.Expr;
// Filter users by related department
var expr = ExistsRelated<Department>(Prop("Name") == "IT");
var users = await objectViewDAO.Search(expr).ToListAsync();

// Lambda form
var lambdaExpr = Lambda<User>(u => ExistsRelated<Department>(d => d.Name == "IT"));
```

## Related Links

- [Back to docs hub](../README.md)
- [API Index](./02-api-index.en.md)
- [Glossary](./03-glossary.en.md)
