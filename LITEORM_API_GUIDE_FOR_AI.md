# LiteOrm API 使用指南（面向 AI）

## 一、配置与注册

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

| 字段                                | 类型         | 默认值        | 说明                                     |
| --------------------------------- | ---------- | ---------- | -------------------------------------- |
| `Default`                         | `string`   | —          | 默认数据源名称                                |
| `DataSources[].Name`              | `string`   | —          | 数据源名称（`[Table]` 的 `DataSource` 参数引用此值） |
| `DataSources[].ConnectionString`  | `string`   | —          | 数据库连接字符串                               |
| `DataSources[].Provider`          | `string`   | —          | 连接类型全名，格式：`TypeName, AssemblyName`     |
| `DataSources[].SqlBuilder`        | `string`   | `null`     | SQL 构建器类型全名（可选，不填则按 Provider 自动匹配）     |
| `DataSources[].KeepAliveDuration` | `TimeSpan` | `00:10:00` | 连接保活时长（`00:00:00` = 无限制）               |
| `DataSources[].PoolSize`          | `int`      | `16`       | 连接池缓存的最大连接数                            |
| `DataSources[].MaxPoolSize`       | `int`      | `100`      | 最大并发连接数限制                              |
| `DataSources[].ParamCountLimit`   | `int`      | `2000`     | SQL 参数数量上限（`0` = 无限制）                  |
| `DataSources[].SyncTable`         | `bool`     | `false`    | 是否自动同步建表                               |
| `DataSources[].ReadOnlyConfigs[]` | `array`    | `[]`       | 只读库配置列表（读写分离），各字段不填时继承主库配置             |

### 服务注册

```csharp
// 基本注册
builder.Host.RegisterLiteOrm();

// 带选项注册
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterScope = true;                          // 默认 true，自动管理 DI Scope 生命周期
    options.Assemblies = new[] { typeof(MyService).Assembly }; // 限定扫描程序集（默认扫描全部）
    options.RegisterSqlBuilder("DefaultConnection", new MySqlBuilder()); // 按数据源名称注册
    options.RegisterSqlBuilder(typeof(SqlConnection), new MySqlBuilder()); // 按连接类型注册
});
```

## 二、实体与视图定义

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

// 视图（用于关联查询）
public class UserView : User
{
    [ForeignColumn(typeof(Department), Property = "DeptName")]
    public string? DeptName { get; set; }
}
```

## 三、服务定义

```csharp
// 实体类型 == 视图类型（EntityService<T>，等价于 EntityService<T, T>）
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<User>, IEntityViewServiceAsync<User>
{ }
public class UserService : EntityService<User>, IUserService { }

// 实体类型 != 视图类型（EntityService<T, TView>，TView 必须继承自 T）
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<UserView>, IEntityViewServiceAsync<UserView>
{ }
public class UserService : EntityService<User, UserView>, IUserService { }
```

## 四、API 参考

### EntityService<T>

> 以下标注 *(扩展)* 的方法来自 `LambdaExprExtensions`，无需修改服务类即可使用。

| 方法                                                                                                                    | 返回类型                  |
| --------------------------------------------------------------------------------------------------------------------- | --------------------- |
| `Insert(T entity)`                                                                                                    | `bool`                |
| `InsertAsync(T entity, CancellationToken ct = default)`                                                               | `Task<bool>`          |
| `Update(T entity)`                                                                                                    | `bool`                |
| `UpdateAsync(T entity, CancellationToken ct = default)`                                                               | `Task<bool>`          |
| `Delete(T entity)`                                                                                                    | `bool`                |
| `DeleteAsync(T entity, CancellationToken ct = default)`                                                               | `Task<bool>`          |
| `Search(Expression<Func<T, bool>> predicate)`                                                                         | `IQueryable<T>`       |
| `SearchAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)`                                    | `IAsyncEnumerable<T>` |
| `Search(Expr<T> expr)`                                                                                                | `IQueryable<T>`       |
| `SearchAsync(Expr<T> expr, CancellationToken ct = default)`                                                           | `IAsyncEnumerable<T>` |
| `Search(string exprString)`                                                                                           | `IQueryable<T>`       |
| `SearchAsync(string exprString, CancellationToken ct = default)`                                                      | `IAsyncEnumerable<T>` |
| `SearchOne(Expression<Func<T, bool>> predicate)`                                                                      | `T`                   |
| `SearchOneAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)`                                 | `Task<T>`             |
| `GetObject(object id)`                                                                                                | `T`                   |
| `GetObjectAsync(object id, CancellationToken ct = default)`                                                           | `Task<T>`             |
| `Count(Expression<Func<T, bool>> predicate)`                                                                          | `int`                 |
| `CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)`                                     | `Task<int>`           |
| `Exists(Expression<Func<T, bool>> predicate)`                                                                         | `bool`                |
| `ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)`                                    | `Task<bool>`          |
| `Delete(Expression<Func<T, bool>> expression, params string[] tableArgs)` *(扩展)*                                      | `int`                 |
| `DeleteAsync(Expression<Func<T, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)` *(扩展)* | `Task<int>`           |

### IEntityViewService<TView>（查询，Expr 风格）

> *(扩展)* 方法来自 `LambdaExprExtensions`。

| 方法                                                                                                               | 返回类型          |
| ---------------------------------------------------------------------------------------------------------------- | ------------- |
| `GetObject(object id, params string[] tableArgs)`                                                                | `TView`       |
| `SearchOne(Expr expr, params string[] tableArgs)`                                                                | `TView`       |
| `Search(Expr expr = null, params string[] tableArgs)`                                                            | `List<TView>` |
| `ForEach(Expr expr, Action<TView> func, params string[] tableArgs)`                                              | `void`        |
| `Exists(Expr expr, params string[] tableArgs)`                                                                   | `bool`        |
| `ExistsID(object id, params string[] tableArgs)`                                                                 | `bool`        |
| `Count(Expr expr = null, params string[] tableArgs)`                                                             | `int`         |
| `Search(Expression<Func<TView, bool>> expression, string[] tableArgs = null)` *(扩展)*                             | `List<TView>` |
| `Search(Expression<Func<IQueryable<TView>, IQueryable<TView>>> expression, string[] tableArgs = null)` *(扩展)*    | `List<TView>` |
| `SearchOne(Expression<Func<TView, bool>> expression, string[] tableArgs = null)` *(扩展)*                          | `TView`       |
| `SearchOne(Expression<Func<IQueryable<TView>, IQueryable<TView>>> expression, string[] tableArgs = null)` *(扩展)* | `TView`       |
| `Exists(Expression<Func<TView, bool>> expression, params string[] tableArgs)` *(扩展)*                             | `bool`        |
| `Count(Expression<Func<TView, bool>> expression, params string[] tableArgs)` *(扩展)*                              | `int`         |

### IEntityViewServiceAsync<TView>

| 方法                                                                                                                                                    | 返回类型                |
| ----------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------- |
| `GetObjectAsync(object id, string[] tableArgs = null, CancellationToken ct = default)`                                                                | `Task<TView>`       |
| `SearchOneAsync(Expr expr, string[] tableArgs = null, CancellationToken ct = default)`                                                                | `Task<TView>`       |
| `SearchAsync(Expr expr = null, string[] tableArgs = null, CancellationToken ct = default)`                                                            | `Task<List<TView>>` |
| `ForEachAsync(Expr expr, Func<TView, Task> func, string[] tableArgs = null, CancellationToken ct = default)`                                          | `Task`              |
| `ExistsAsync(Expr expr, string[] tableArgs = null, CancellationToken ct = default)`                                                                   | `Task<bool>`        |
| `ExistsIDAsync(object id, string[] tableArgs = null, CancellationToken ct = default)`                                                                 | `Task<bool>`        |
| `CountAsync(Expr expr = null, string[] tableArgs = null, CancellationToken ct = default)`                                                             | `Task<int>`         |
| `SearchAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)` *(扩展)*                             | `Task<List<TView>>` |
| `SearchAsync(Expression<Func<IQueryable<TView>, IQueryable<TView>>> expression, string[] tableArgs = null, CancellationToken ct = default)` *(扩展)*    | `Task<List<TView>>` |
| `SearchOneAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)` *(扩展)*                          | `Task<TView>`       |
| `SearchOneAsync(Expression<Func<IQueryable<TView>, IQueryable<TView>>> expression, string[] tableArgs = null, CancellationToken ct = default)` *(扩展)* | `Task<TView>`       |
| `ExistsAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)` *(扩展)*                             | `Task<bool>`        |
| `CountAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)` *(扩展)*                              | `Task<int>`         |

### ObjectDAO<T>（仅增删改）

| 方法                                                                                  | 返回类型                         |
| ----------------------------------------------------------------------------------- | ---------------------------- |
| `Insert(T entity)`                                                                  | `bool`                       |
| `InsertAsync(T entity, CancellationToken ct = default)`                             | `Task<bool>`                 |
| `Update(T entity)`                                                                  | `bool`                       |
| `UpdateAsync(T entity, CancellationToken ct = default)`                             | `Task<bool>`                 |
| `Delete(T entity)`                                                                  | `bool`                       |
| `DeleteAsync(T entity, CancellationToken ct = default)`                             | `Task<bool>`                 |
| `BatchInsert(IEnumerable<T> entities)`                                              | `void`                       |
| `BatchInsertAsync(IEnumerable<T> entities, CancellationToken ct = default)`         | `Task`                       |
| `BatchUpdate(IEnumerable<T> entities)`                                              | `void`                       |
| `BatchUpdateAsync(IEnumerable<T> entities, CancellationToken ct = default)`         | `Task`                       |
| `BatchDelete(IEnumerable<T> entities)`                                              | `void`                       |
| `BatchDeleteAsync(IEnumerable<T> entities, CancellationToken ct = default)`         | `Task`                       |
| `UpdateOrInsert(T entity)`                                                          | `UpdateOrInsertResult`       |
| `UpdateOrInsertAsync(T entity, CancellationToken ct = default)`                     | `Task<UpdateOrInsertResult>` |
| `BatchUpdateOrInsert(IEnumerable<T> entities)`                                      | `void`                       |
| `BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task`                       |
| `DeleteByKeys(params object[] keys)`                                                | `bool`                       |
| `DeleteByKeysAsync(object[] keys, CancellationToken ct = default)`                  | `Task<bool>`                 |
| `BatchDeleteByKeys(IEnumerable keys)`                                               | `void`                       |
| `BatchDeleteByKeysAsync(IEnumerable keys, CancellationToken ct = default)`          | `Task`                       |
| `Delete(LogicExpr expr)`                                                            | `int`                        |
| `DeleteAsync(LogicExpr expr, CancellationToken ct = default)`                       | `Task<int>`                  |

### ObjectViewDAO<T>（仅查询）

`EnumerableResult<T>` 支持：`.ToList()` / `.ToListAsync()` / `.FirstOrDefault()` / `.FirstOrDefaultAsync()` / `.GetResult()` / `.GetResultAsync()` / `await foreach`

| 方法                                                                                                                            | 返回类型                        |
| ----------------------------------------------------------------------------------------------------------------------------- | --------------------------- |
| `Search(Expression<Func<T, bool>> predicate)`                                                                                 | `EnumerableResult<T>`       |
| `Search(Expr<T> expr)`                                                                                                        | `EnumerableResult<T>`       |
| `Search(string exprString)`                                                                                                   | `EnumerableResult<T>`       |
| `SearchAs<TResult>(SelectExpr selectExpr, Func<DbDataReader, TResult> readerFunc = null)`                                     | `EnumerableResult<TResult>` |
| `SearchAs<TResult>(Expression<Func<IQueryable<T>, IQueryable<TResult>>> expr, Func<DbDataReader, TResult> readerFunc = null)` | `EnumerableResult<TResult>` |
| `GetObject(params object[] keys)`                                                                                             | `EnumerableResult<T>`       |
| `Count(Expr expr)`                                                                                                            | `ValueResult<int>`          |
| `Exists(object o)` / `Exists(T o)`                                                                                            | `ValueResult<bool>`         |
| `ExistsKey(params object[] keys)`                                                                                             | `ValueResult<bool>`         |
| `Exists(Expr expr)`                                                                                                           | `ValueResult<bool>`         |

### DataViewDAO<T>（查询，返回 DataTable）

`DataTableResult` 支持：`.GetResult()` / `.GetResultAsync()`

| 方法                                                             | 返回类型              |
| -------------------------------------------------------------- | ----------------- |
| `Search(Expr<T> expr)`                                         | `DataTableResult` |
| `Search(string[] fields, Expression<Func<T, bool>> predicate)` | `DataTableResult` |
| `Search(string[] fields, Expr<T> whereExpr)`                   | `DataTableResult` |

## 五、事务

```csharp
// 声明式
[Transaction]
public void Transfer() { ... }

// 手动
using var transaction = SessionManager.Current.BeginTransaction();
try { transaction.Commit(); }
catch { transaction.Rollback(); throw; }
```

## 六、高级特性

### 分表

```csharp
[Table("Orders_{0}")]
public class Order : ObjectBase, IArged
{
    public object[] GetArgs() => new object[] { UserId % 10 };
}
```

### 多数据源

```csharp
[Table("Users", DataSource = "Secondary")]
public class User : ObjectBase { ... }
```

### 自定义 DAO / Service

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

## 七、特性速查

| 特性                                              | 用途                      |
| ----------------------------------------------- | ----------------------- |
| `[Table("TableName")]`                          | 指定表名，可选 `DataSource` 参数 |
| `[Column("ColName", IsPrimaryKey, IsIdentity)]` | 指定列名和属性                 |
| `[ForeignType(typeof(T), Alias)]`               | 指定外键关联类型                |
| `[ForeignColumn(typeof(T), Property)]`          | 从关联表获取的列（用于视图）          |
| `[Transaction]`                                 | 声明式事务                   |
| `[AutoRegister]`                                | 自动注册到 DI 容器             |

## 八、Expr 表达式系统

### 三种查询方式

| 方式                           | 适用场景                  |
| ---------------------------- | --------------------- |
| Lambda 表达式 `u => u.Age > 18` | 简单条件，编译时类型安全          |
| Expr 对象（运算符 / Fluent 方法）     | 复杂条件、动态条件累加、链式查询      |
| ExprString 插值字符串             | 自定义 DAO 中需要直接写 SQL 片段 |

### Expr 静态工厂方法

| 方法                                        | 返回类型              | 说明                             |
| ----------------------------------------- | ----------------- | ------------------------------ |
| `Expr.Prop("Name")`                       | `PropertyExpr`    | 属性表达式                          |
| `Expr.Prop("alias", "Name")`              | `PropertyExpr`    | 带表别名的属性表达式                     |
| `Expr.Value(obj)`                         | `ValueExpr`       | 参数化值                           |
| `Expr.Const(obj)`                         | `ValueExpr`       | 常量值（内联到 SQL，不参数化）              |
| `Expr.PropEqual("Name", value)`           | `LogicBinaryExpr` | 属性等于值                          |
| `Expr.Func("ABS", expr)`                  | `FunctionExpr`    | 函数调用                           |
| `Expr.Aggregate("SUM", expr, isDistinct)` | `FunctionExpr` | 聚合函数（IsAggregate=true） |
| `Expr.Concat(e1, e2)` | `ValueSet` | CONCAT 字符串拼接（扩展方法） |
| `Expr.Lambda<T>(u => u.Age > 18)` | `LogicExpr` | Lambda 转 Expr |
| `Expr.From<T>(tableArgs)`                 | `FromExpr`        | 链式查询起点                         |
| `Expr.Sql("key", arg)`                    | `GenericSqlExpr`  | 动态 SQL 片段                      |
| `Expr.Delete<T>(tableArgs)`               | `DeleteExpr`      | 创建 DELETE 表达式                  |
| `Expr.If(condition, thenExpr, elseExpr)`  | `FunctionExpr`    | IF 表达式                         |
| `Expr.Case(cases, elseExpr)`              | `FunctionExpr`    | CASE 表达式                       |
| `Expr.Query<T>(expression)`               | `Expr`            | IQueryable Lambda 转 Expr       |
| `Expr.Query<T, TResult>(expression)` | `Expr` | 带返回值的 IQueryable Lambda 转 Expr |
| `Expr.Exists<T>(innerExpr, tableArgs)` | `ForeignExpr` | 关联表 EXISTS 查询 |
| `Expr.Exists(type, innerExpr, tableArgs)` | `ForeignExpr` | 关联表 EXISTS 查询（指定类型） |
| `Expr.ExistsRelated<T>(innerExpr, tableArgs)` | `ForeignExpr` | 自动关联的 EXISTS 查询 |
| `Expr.ExistsRelated(type, innerExpr, tableArgs)` | `ForeignExpr` | 自动关联的 EXISTS 查询（指定类型） |
| `Expr.Exists<T>(lambda)` | `bool` | 仅用于 Lambda 表达式中构造 EXISTS 查询（直接调用会抛出异常） |
| `Expr.ExistsRelated<T>(lambda)` | `bool` | 仅用于 Lambda 表达式中构造自动关联的 EXISTS 查询（直接调用会抛出异常） |

### 运算符重载

`PropertyExpr` / `ValueTypeExpr` 上的运算符：

| 运算符                         | 说明          | 返回类型            |
| --------------------------- | ----------- | --------------- |
| `==` `!=` `>` `<` `>=` `<=` | 比较          | `LogicExpr`     |
| `+` `-` `*` `/` `%`         | 算术          | `ValueTypeExpr` |
| `-expr` `~expr`             | 一元负号 / 按位取反 | `ValueTypeExpr` |

`LogicExpr` 上的运算符：

| 运算符  | 说明                           |
| ---- | ---------------------------- |
| `&`  | AND（左或右为 null 时返回另一侧，适合动态累加） |
| `|` | OR                           |
| `!`  | NOT                          |

### PropertyExpr 扩展方法

| 分类   | 方法                                                                                                         |
| ---- | ---------------------------------------------------------------------------------------------------------- |
| 比较   | `.Equal(v)` `.NotEqual(v)` `.GreaterThan(v)` `.LessThan(v)` `.GreaterThanOrEqual(v)` `.LessThanOrEqual(v)` |
| 集合   | `.In(IEnumerable)` `.In(params items)` `.In(ValueTypeExpr)`                                                |
| 范围   | `.Between(low, high)`                                                                                      |
| 字符串  | `.Like(pattern)` `.Contains(text)` `.StartsWith(text)` `.EndsWith(text)`                                   |
| Null | `.IsNull()` `.IsNotNull()`                                                                                 |
| 别名   | `.As("alias")` → `SelectItemExpr`                                                                          |
| 聚合   | `.Count(isDistinct)` `.Sum()` `.Avg()` `.Max()` `.Min()`                                                   |
| 排序   | `.Asc()` `.Desc()` → `OrderByItemExpr`                                                                     |

### LogicExpr 扩展方法

`.And(right)` `.Or(right)` `.Not()`

### 链式查询构建

`Expr.From<T>()` 起点，支持如下链式调用（顺序按 SQL 子句顺序）：

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

`SelectExpr` 也是 `ValueTypeExpr`，可用于 IN 子查询：

```csharp
// IN 子查询
var subQuery = Expr.From<Department>()
    .Where(Expr.Prop("Name") == "IT")
    .Select("Id");
var expr = Expr.Prop("DeptId").In(subQuery);
```

UpdateExpr / DeleteExpr（用于 `ObjectDAO.Delete(LogicExpr)` 等）：

```csharp
var update = new UpdateExpr(Expr.From<User>(), Expr.Prop("Id") == 1);
update.Set(("UserName", Expr.Value("NewName")), ("Age", Expr.Value(30)));

var delete = new DeleteExpr(Expr.From<User>(), Expr.Prop("Age") < 18);
```

### ExprString

插值字符串处理器，在DAO的 `Search(ExprString exprString)` 方法的字符串参数中中直接嵌入 Expr 对象：

```csharp
// 嵌入 Expr 对象自动转为带参数 SQL 片段
var result = dao.Search($"WHERE {Expr.Prop("DeptName") == deptName} AND {Expr.Prop("Age") > 18}");
```

### 常用模式

```csharp
// 动态条件累加（& 对 null 安全）
LogicExpr condition = null;
if (minAge.HasValue)  condition &= Expr.Prop("Age") >= minAge.Value;
if (deptId.HasValue)  condition &= Expr.Prop("DeptId") == deptId.Value;
if (!string.IsNullOrEmpty(name)) condition &= Expr.Prop("UserName").Contains(name);
var users = await dao.Search(condition).ToListAsync();

// 关联表 EXISTS 查询
var expr = Expr.Exists<Department>(Expr.Prop("Name") == "IT");
// 等价 Lambda 写法（在 Lambda 查询中）：
var expr = Expr.Lambda<User>(u => Expr.Exists<Department>(d => d.Name == "IT"));

// SearchAs 选择部分字段
var result = dao.SearchAs(
    Expr.From<User>()
        .Where(Expr.Prop("Age") > 18)
        .Select("Id", "UserName")
).ToList();
```