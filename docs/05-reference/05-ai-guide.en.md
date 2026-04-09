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

### ObjectDAO<T> (create, update, delete only)

| Method | Return type |
| --- | --- |
| `Insert(T entity)` | `bool` |
| `Update(T entity)` | `bool` |
| `Delete(T entity)` | `bool` |
| `DeleteID(object id, params string[] tableArgs)` | `bool` |
| `Delete(LogicExpr expr)` | `int` |
| `Update(UpdateExpr expr)` | `int` |
| `BatchInsert(IEnumerable<T> entities)` | `void` |
| `BatchUpdate(IEnumerable<T> entities)` | `void` |
| `BatchDelete(IEnumerable<T> entities)` | `void` |
| `BatchDeleteID(IEnumerable ids, params string[] tableArgs)` | `void` |
| `UpdateOrInsert(T entity)` | `UpdateOrInsertResult` |
| `BatchUpdateOrInsert(IEnumerable<T> entities)` | `void` |
| `InsertAsync(T entity, CancellationToken ct = default)` | `Task<bool>` |
| `UpdateAsync(T entity, CancellationToken ct = default)` | `Task<bool>` |
| `DeleteAsync(T entity, CancellationToken ct = default)` | `Task<bool>` |
| `DeleteIDAsync(object id, CancellationToken ct = default)` | `Task<bool>` |
| `DeleteAsync(LogicExpr expr, CancellationToken ct = default)` | `Task<int>` |
| `UpdateAsync(UpdateExpr expr, CancellationToken ct = default)` | `Task<int>` |
| `BatchInsertAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `BatchUpdateAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `BatchDeleteAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `BatchDeleteIDAsync(IEnumerable ids, CancellationToken ct = default, params string[] tableArgs)` | `Task` |
| `UpdateOrInsertAsync(T entity, CancellationToken ct = default)` | `Task<UpdateOrInsertResult>` |
| `BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |

### ObjectViewDAO<T> (query only)

`EnumerableResult<T>` supports: `.ToList()` / `.ToListAsync()` / `.FirstOrDefault()` / `.FirstOrDefaultAsync()` / `.GetResult()` / `.GetResultAsync()` / `await foreach`

| Method | Return type |
| --- | --- |
| `GetObjectAsync(object id, string[] tableArgs = null, CancellationToken ct = default)` | `Task<TView>` |
| `SearchOneAsync(Expr expr, string[] tableArgs = null, CancellationToken ct = default)` | `Task<TView>` |
| `SearchAsync(Expr expr = null, string[] tableArgs = null, CancellationToken ct = default)` | `Task<List<TView>>` |
| `ForEachAsync(Expr expr, Func<TView, Task> func, string[] tableArgs = null, CancellationToken ct = default)` | `Task` |
| `ExistsAsync(Expr expr, string[] tableArgs = null, CancellationToken ct = default)` | `Task<bool>` |
| `ExistsIDAsync(object id, string[] tableArgs = null, CancellationToken ct = default)` | `Task<bool>` |
| `CountAsync(Expr expr = null, string[] tableArgs = null, CancellationToken ct = default)` | `Task<int>` |
| `SearchAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)` *(extension)* | `Task<List<TView>>` |
| `SearchAsync(Expression<Func<IQueryable<TView>, IQueryable<TView>>> expression, string[] tableArgs = null, CancellationToken ct = default)` *(extension)* | `Task<List<TView>>` |
| `SearchOneAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)` *(extension)* | `Task<TView>` |
| `SearchOneAsync(Expression<Func<IQueryable<TView>, IQueryable<TView>>> expression, string[] tableArgs = null, CancellationToken ct = default)` *(extension)* | `Task<TView>` |
| `ExistsAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)` *(extension)* | `Task<bool>` |
| `CountAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)` *(extension)* | `Task<int>` |

### ObjectDAO<T> (create, update, delete only)

| Method | Return type |
| --- | --- |
| `Insert(T entity)` | `bool` |
| `InsertAsync(T entity, CancellationToken ct = default)` | `Task<bool>` |
| `Update(T entity)` | `bool` |
| `UpdateAsync(T entity, CancellationToken ct = default)` | `Task<bool>` |
| `Delete(T entity)` | `bool` |
| `DeleteAsync(T entity, CancellationToken ct = default)` | `Task<bool>` |
| `BatchInsert(IEnumerable<T> entities)` | `void` |
| `BatchInsertAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `BatchUpdate(IEnumerable<T> entities)` | `void` |
| `BatchUpdateAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `BatchDelete(IEnumerable<T> entities)` | `void` |
| `BatchDeleteAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `UpdateOrInsert(T entity)` | `UpdateOrInsertResult` |
| `UpdateOrInsertAsync(T entity, CancellationToken ct = default)` | `Task<UpdateOrInsertResult>` |
| `BatchUpdateOrInsert(IEnumerable<T> entities)` | `void` |
| `BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task` |
| `DeleteByKeys(params object[] keys)` | `bool` |
| `DeleteByKeysAsync(object[] keys, CancellationToken ct = default)` | `Task<bool>` |
| `BatchDeleteByKeys(IEnumerable keys)` | `void` |
| `BatchDeleteByKeysAsync(IEnumerable keys, CancellationToken ct = default)` | `Task` |
| `Delete(LogicExpr expr)` | `int` |
| `DeleteAsync(LogicExpr expr, CancellationToken ct = default)` | `Task<int>` |

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

## 7. Attribute quick reference

| Attribute | Purpose |
| --- | --- |
| `[Table("TableName")]` | Specifies the table name; optional `DataSource` parameter |
| `[Column("ColName", IsPrimaryKey, IsIdentity)]` | Specifies the column name and property behavior |
| `[ForeignType(typeof(T), Alias, AutoExpand)]` | Specifies the foreign-key related type; `AutoExpand` extends relation paths |
| `[TableJoin(typeof(T), ForeignKeys, AliasName, AutoExpand)]` | Type-level relation definition supporting composite keys and path reuse |
| `[ForeignColumn(typeof(T), Property)]` | Column projected from a related table (for view models) |
| `[Transaction]` | Declarative transaction |
| `[AutoRegister]` | Automatically registers the type into the DI container |

## 8. Expr expression system

### Three query styles

| Style | Suitable for |
| --- | --- |
| Lambda expression `u => u.Age > 18` | Simple conditions with compile-time type safety |
| Expr object (operators / fluent methods) | Complex conditions, dynamically accumulated conditions, and chained queries |
| ExprString interpolated string | Writing SQL fragments directly inside custom DAOs |

### Expr static factory methods

| Method | Return type | Description |
| --- | --- | --- |
| `Expr.Prop("Name")` | `PropertyExpr` | Property expression |
| `Expr.Prop("alias", "Name")` | `PropertyExpr` | Property expression with a table alias |
| `Expr.Value(obj)` | `ValueExpr` | Parameterized value |
| `Expr.Const(obj)` | `ValueExpr` | Constant value (inlined into SQL, not parameterized) |
| `Expr.PropEqual("Name", value)` | `LogicBinaryExpr` | Property equals value |
| `Expr.Func("ABS", expr)` | `FunctionExpr` | Function call |
| `Expr.Aggregate("SUM", expr, isDistinct)` | `FunctionExpr` | Aggregate function (`IsAggregate=true`) |
| `Expr.Concat(e1, e2)` | `ValueSet` | CONCAT string composition (extension method) |
| `Expr.Lambda<T>(u => u.Age > 18)` | `LogicExpr` | Converts Lambda to Expr |
| `Expr.From<T>(tableArgs)` | `FromExpr` | Starting point for chained queries |
| `Expr.Sql("key", arg)` | `GenericSqlExpr` | Dynamic SQL fragment |
| `Expr.Delete<T>(tableArgs)` | `DeleteExpr` | Creates a DELETE expression |
| `Expr.If(condition, thenExpr, elseExpr)` | `FunctionExpr` | IF expression |
| `Expr.Case(cases, elseExpr)` | `FunctionExpr` | CASE expression |
| `Expr.Query<T>(expression)` | `Expr` | Converts an IQueryable Lambda to Expr |
| `Expr.Query<T, TResult>(expression)` | `Expr` | Converts an IQueryable Lambda with a return value to Expr |
| `Expr.Exists<T>(innerExpr, tableArgs)` | `ForeignExpr` | EXISTS query on a related table |
| `Expr.Exists(type, innerExpr, tableArgs)` | `ForeignExpr` | EXISTS query on a related table (explicit type) |
| `Expr.ExistsRelated<T>(innerExpr, tableArgs)` | `ForeignExpr` | EXISTS query using automatic relation discovery |
| `Expr.ExistsRelated(type, innerExpr, tableArgs)` | `ForeignExpr` | EXISTS query using automatic relation discovery (explicit type) |
| `Expr.Exists<T>(lambda)` | `bool` | Used only inside Lambda expressions to build EXISTS queries (direct calls throw an exception) |
| `Expr.ExistsRelated<T>(lambda)` | `bool` | Used only inside Lambda expressions to build automatic-related EXISTS queries (direct calls throw an exception) |

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

### PropertyExpr extension methods

| Category | Methods |
| --- | --- |
| Comparison | `.Equal(v)` `.NotEqual(v)` `.GreaterThan(v)` `.LessThan(v)` `.GreaterThanOrEqual(v)` `.LessThanOrEqual(v)` |
| Set | `.In(IEnumerable)` `.In(params items)` `.In(Expr)` |
| Range | `.Between(low, high)` |
| String | `.Like(pattern)` `.Contains(text)` `.StartsWith(text)` `.EndsWith(text)` |
| Null | `.IsNull()` `.IsNotNull()` |
| Alias | `.As("alias")` → `SelectItemExpr` |
| Aggregate | `.Count(isDistinct)` `.Sum()` `.Avg()` `.Max()` `.Min()` |
| Sort | `.Asc()` `.Desc()` → `OrderByItemExpr` |

### LogicExpr extension methods

`.And(right)` `.Or(right)` `.Not()`

### Chained query construction

`Expr.From<T>()` is the entry point and supports the following chained calls in SQL clause order:

```csharp
var query = Expr.From<User>()
    .Where(Expr.Prop("Age") > 18)                         // WhereExpr
    .GroupBy(Expr.Prop("DeptId"))                         // GroupByExpr
    .Having(Expr.Prop("Id").Count() > 5)                  // HavingExpr
    .Select(Expr.Prop("DeptId"),                          // SelectExpr
            Expr.Prop("Id").Count().As("Cnt"))
    .OrderBy(Expr.Prop("DeptId").Asc())                   // OrderByExpr
    .Section(0, 20);                                      // SectionExpr (skip, take)
```

`SelectExpr` can be used for `IN` subqueries:

```csharp
// IN subquery
var subQuery = Expr.From<Department>()
    .Where(Expr.Prop("Name") == "IT")
    .Select("Id");
var expr = Expr.Prop("DeptId").In(subQuery);
```

UpdateExpr / DeleteExpr (used by `ObjectDAO.Delete(LogicExpr)` and similar APIs):

```csharp
var update = new UpdateExpr(Expr.From<User>(), Expr.Prop("Id") == 1);
update.Set(("UserName", Expr.Value("NewName")), ("Age", Expr.Value(30)));

var delete = new DeleteExpr(Expr.From<User>(), Expr.Prop("Age") < 18);
```

### ExprString

The interpolated string handler lets you embed Expr objects directly into the string argument passed to the DAO `Search(ExprString exprString)` method:

```csharp
// Embedded Expr objects are automatically converted into parameterized SQL fragments
var result = dao.Search($"WHERE {Expr.Prop("DeptName") == deptName} AND {Expr.Prop("Age") > 18}");
```

### Common patterns

```csharp
// Dynamically accumulate conditions (& is null-safe)
LogicExpr condition = null;
if (minAge.HasValue)  condition &= Expr.Prop("Age") >= minAge.Value;
if (deptId.HasValue)  condition &= Expr.Prop("DeptId") == deptId.Value;
if (!string.IsNullOrEmpty(name)) condition &= Expr.Prop("UserName").Contains(name);
var users = await dao.Search(condition).ToListAsync();

// EXISTS query on a related table
var expr = Expr.Exists<Department>(Expr.Prop("Name") == "IT");
// Equivalent Lambda form (inside a Lambda query):
var expr = Expr.Lambda<User>(u => Expr.Exists<Department>(d => d.Name == "IT"));

// SearchAs selects a subset of fields
var result = dao.SearchAs(
    Expr.From<User>()
        .Where(Expr.Prop("Age") > 18)
        .Select("Id", "UserName")
).ToList();
```

### ExistsRelated notes

`ExistsRelated` filters the primary table by conditions on a related table without requiring the related fields to be explicitly exposed on the view model.

**Relation matching order: forward relations first; if no forward relation is found, reverse relations are tried. Multiple relation paths are combined with** **`OR`** **.**

```csharp
// Filter users by related department
var expr = Expr.ExistsRelated<Department>(Expr.Prop("Name") == "IT");
var users = await objectViewDAO.Search(expr).ToListAsync();

// Lambda form
var lambdaExpr = Expr.Lambda<User>(u => Expr.ExistsRelated<Department>(d => d.Name == "IT"));
```

## Related links

- [Back to docs hub](../README.md)
- [API Index](./02-api-index.en.md)
- [Glossary](./03-glossary.en.md)
