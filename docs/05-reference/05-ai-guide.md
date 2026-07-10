# LiteOrm API 使用指南（AI 适用）

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
| `DataSources[].SyncTable`         | `bool`     | `false`    | 是否自动同步建表；连接池级默认值，可被 `[Table(SyncTable = ...)]` 实体级配置或 `DatabaseSync.OnTableSyncing` 事件覆盖 |
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

### `[Table]` 特性

| 参数 | 说明 |
| --- | --- |
| `Name` | 数据库表名，支持占位符分表。 |
| `DataSource` | 指定当前实体所属数据源。 |
| `SyncTable` | 实体级表结构同步模式，枚举 `SyncTableMode`（`Default` / `Never` / `Always`），默认 `Default`。`Never`/`Always` 覆盖数据源级 `SyncTable` 配置。 |

```csharp
[Table("Logs", SyncTable = SyncTableMode.Always)] // 始终自动建表，即使数据源 SyncTable=false
public class Log { ... }

[Table("Legacy", SyncTable = SyncTableMode.Never)] // 永不自动建表，即使数据源开启了 SyncTable
public class Legacy { ... }
```

> `SyncTable` 判定优先级：`OnTableSyncing` 事件 > `[Table(SyncTable = ...)]`（`Never`/`Always`）> 连接池级 `SyncTable`。

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

### IEntityService<T>（增删改）

| 方法                                                   | 返回类型   |
| ---------------------------------------------------- | ------ |
| `Insert(T entity)`                                   | `bool` |
| `Update(T entity)`                                   | `bool` |
| `UpdateOrInsert(T entity)`                           | `bool` |
| `Delete(T entity)`                                   | `bool` |
| `DeleteID(object id, params string[] tableArgs)`     | `bool` |
| `Delete(LogicExpr expr, params string[] tableArgs)`  | `int`  |
| `Update(UpdateExpr expr, params string[] tableArgs)` | `int`  |
| `BatchInsert(IEnumerable<T> entities)`               | `void` |
| `BatchUpdate(IEnumerable<T> entities)`               | `void` |
| `BatchUpdateOrInsert(IEnumerable<T> entities)`       | `void` |
| `BatchDelete(IEnumerable<T> entities)`               | `void` |
| `Batch(IEnumerable<EntityOperation<T>> entities)`    | `void` |

### IEntityServiceAsync<T>（异步增删改）

| 方法                                                                                        | 返回类型         |
| ----------------------------------------------------------------------------------------- | ------------ |
| `InsertAsync(T entity, CancellationToken ct = default)`                                   | `Task<bool>` |
| `UpdateAsync(T entity, CancellationToken ct = default)`                                   | `Task<bool>` |
| `UpdateOrInsertAsync(T entity, CancellationToken ct = default)`                           | `Task<bool>` |
| `DeleteAsync(T entity, CancellationToken ct = default)`                                   | `Task<bool>` |
| `DeleteIDAsync(object id, string[] tableArgs = null, CancellationToken ct = default)`     | `Task<bool>` |
| `DeleteAsync(LogicExpr expr, string[] tableArgs = null, CancellationToken ct = default)`  | `Task<int>`  |
| `UpdateAsync(UpdateExpr expr, string[] tableArgs = null, CancellationToken ct = default)` | `Task<int>`  |
| `BatchInsertAsync(IEnumerable<T> entities, CancellationToken ct = default)`               | `Task`       |
| `BatchUpdateAsync(IEnumerable<T> entities, CancellationToken ct = default)`               | `Task`       |
| `BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken ct = default)`       | `Task`       |
| `BatchDeleteAsync(IEnumerable<T> entities, CancellationToken ct = default)`               | `Task`       |
| `BatchAsync(IEnumerable<EntityOperation<T>> entities, CancellationToken ct = default)`     | `Task`       |

### IEntityViewService<TView>（查询，Expr 风格）

| 方法                                                                  | 返回类型          |
| ------------------------------------------------------------------- | ------------- |
| `GetObject(object id, params string[] tableArgs)`                   | `TView`       |
| `SearchOne(Expr expr, params string[] tableArgs)`                   | `TView`       |
| `Search(Expr expr = null, params string[] tableArgs)`               | `List<TView>` |
| `SearchAs<TResult>(SelectExpr selectExpr, params string[] tableArgs)` | `List<TResult>` |
| `SearchOneAs<TResult>(SelectExpr selectExpr, params string[] tableArgs)` | `TResult` |
| `ForEach(Expr expr, Action<TView> func, params string[] tableArgs)` | `void`        |
| `ExistsID(object id, params string[] tableArgs)`                    | `bool`        |
| `Exists(Expr expr, params string[] tableArgs)`                      | `bool`        |
| `Count(Expr expr = null, params string[] tableArgs)`                | `int`         |

### IEntityViewServiceAsync<TView>（异步查询）

| 方法                                                                                                           | 返回类型                |
| ------------------------------------------------------------------------------------------------------------ | ------------------- |
| `GetObjectAsync(object id, string[] tableArgs = null, CancellationToken ct = default)`                       | `Task<TView>`       |
| `SearchOneAsync(Expr expr, string[] tableArgs = null, CancellationToken ct = default)`                       | `Task<TView>`       |
| `SearchAsync(Expr expr = null, string[] tableArgs = null, CancellationToken ct = default)`                   | `Task<List<TView>>` |
| `SearchAsAsync<TResult>(SelectExpr selectExpr, params string[] tableArgs)`                                   | `Task<List<TResult>>` |
| `SearchOneAsAsync<TResult>(SelectExpr selectExpr, params string[] tableArgs)`                                | `Task<TResult>` |
| `ForEachAsync(Expr expr, Func<TView, Task> func, string[] tableArgs = null, CancellationToken ct = default)` | `Task`              |
| `ExistsIDAsync(object id, string[] tableArgs = null, CancellationToken ct = default)`                        | `Task<bool>`        |
| `ExistsAsync(Expr expr, string[] tableArgs = null, CancellationToken ct = default)`                          | `Task<bool>`        |
| `CountAsync(Expr expr = null, string[] tableArgs = null, CancellationToken ct = default)`                    | `Task<int>`         |

### Lambda 表达式扩展方法

> 以下方法来自 `LambdaExprExtensions`，无需修改服务类即可使用。

| 方法                                                                                                                                             | 返回类型                |
| ---------------------------------------------------------------------------------------------------------------------------------------------- | ------------------- |
| `Delete(Expression<Func<T, bool>> expression, params string[] tableArgs)`                                                                      | `int`               |
| `DeleteAsync(Expression<Func<T, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)`                                 | `Task<int>`         |
| `Search(Expression<Func<TView, bool>> expression, string[] tableArgs = null)`                                                                  | `List<TView>`       |
| `Search(Expression<Func<IQueryable<TView>, IQueryable<TView>>> expression, string[] tableArgs = null)`                                         | `List<TView>`       |
| `SearchOne(Expression<Func<TView, bool>> expression, string[] tableArgs = null)`                                                               | `TView`             |
| `SearchOne(Expression<Func<IQueryable<TView>, IQueryable<TView>>> expression, string[] tableArgs = null)`                                      | `TView`             |
| `Exists(Expression<Func<TView, bool>> expression, params string[] tableArgs)`                                                                  | `bool`              |
| `Count(Expression<Func<TView, bool>> expression, params string[] tableArgs)`                                                                   | `int`               |
| `SearchAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)`                             | `Task<List<TView>>` |
| `SearchAsync(Expression<Func<IQueryable<TView>, IQueryable<TView>>> expression, string[] tableArgs = null, CancellationToken ct = default)`    | `Task<List<TView>>` |
| `SearchOneAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)`                          | `Task<TView>`       |
| `SearchOneAsync(Expression<Func<IQueryable<TView>, IQueryable<TView>>> expression, string[] tableArgs = null, CancellationToken ct = default)` | `Task<TView>`       |
| `ExistsAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)`                             | `Task<bool>`        |
| `CountAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken ct = default)`                              | `Task<int>`         |

> 补充说明：Service 查询公开入口包括 `Expr`、其 Lambda 扩展，以及基于 `SelectExpr` 的 `SearchAs(...)` / `SearchAsAsync(...)`；如果需要 `ExprString`、完整 SQL、IQueryable 投影版 `SearchAs(...)` 或 DataTable 查询，请切换到 DAO。

### ObjectDAO<T>（仅增删改）

| 方法                                                                                  | 返回类型                         |
| ----------------------------------------------------------------------------------- | ---------------------------- |
| `Insert(T entity)`                                                                  | `bool`                       |
| `Update(T entity, object timestamp = null)`                                         | `bool`                       |
| `Delete(T entity)`                                                                  | `bool`                       |
| `Delete(LogicExpr expr)`                                                            | `int`                        |
| `Update(UpdateExpr expr)`                                                           | `int`                        |
| `BatchInsert(IEnumerable<T> entities)`                                               | `void`                       |
| `BatchUpdate(IEnumerable<T> entities)`                                               | `void`                       |
| `BatchDelete(IEnumerable<T> entities)`                                               | `void`                       |
| `UpdateOrInsert(T entity)`                                                          | `UpdateOrInsertResult`       |
| `BatchUpdateOrInsert(IEnumerable<T> entities)`                                      | `void`                       |
| `InsertAsync(T entity, CancellationToken ct = default)`                              | `Task<bool>`                 |
| `UpdateAsync(T entity, object timestamp = null, CancellationToken ct = default)`    | `Task<bool>`                 |
| `DeleteAsync(T entity, CancellationToken ct = default)`                              | `Task<bool>`                 |
| `DeleteAsync(LogicExpr expr, CancellationToken ct = default)`                       | `Task<int>`                  |
| `UpdateAsync(UpdateExpr expr, CancellationToken ct = default)`                      | `Task<int>`                  |
| `BatchInsertAsync(IEnumerable<T> entities, CancellationToken ct = default)`       | `Task`                       |
| `BatchUpdateAsync(IEnumerable<T> entities, CancellationToken ct = default)`         | `Task`                       |
| `BatchDeleteAsync(IEnumerable<T> entities, CancellationToken ct = default)`         | `Task`                       |
| `UpdateOrInsertAsync(T entity, CancellationToken ct = default)`                      | `Task<UpdateOrInsertResult>` |
| `BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken ct = default)` | `Task`                       |
| `DeleteByKeys(params object[] keys)`                                                | `bool`                       |
| `DeleteByKeysAsync(object[] keys, CancellationToken ct = default)`                  | `Task<bool>`                 |
| `BatchDeleteByKeys(IEnumerable keys)`                                               | `void`                       |
| `BatchDeleteByKeysAsync(IEnumerable keys, CancellationToken ct = default)`          | `Task`                       |

### ObjectViewDAO<T>（仅查询）

`EnumerableResult<T>` 支持：`.ToList()` / `.ToListAsync()` / `.FirstOrDefault()` / `.FirstOrDefaultAsync()` / `.GetResult()` / `.GetResultAsync()` / `await foreach`

| 方法                                                                                                                            | 返回类型                        |
| ----------------------------------------------------------------------------------------------------------------------------- | --------------------------- |
| `Search(Expr expr = null)`                                                                                                    | `EnumerableResult<T>`       |
| `Search(Expression<Func<IQueryable<T>, IQueryable<T>>> expr)`                                                                 | `EnumerableResult<T>`       |
| `Search(ref ExprString sqlBody, bool isFull = false)`                                                                         | `EnumerableResult<T>`       |
| `SearchAs<TResult>(SelectExpr selectExpr, Func<DbDataReader, TResult> readerFunc = null)`                                     | `EnumerableResult<TResult>` |
| `SearchAs<TResult>(Expression<Func<IQueryable<T>, IQueryable<TResult>>> expr, Func<DbDataReader, TResult> readerFunc = null)` | `EnumerableResult<TResult>` |
| `SearchAs<TResult>(ref ExprString sqlBody)`                                                                                  | `EnumerableResult<TResult>` |
| `GetObject(params object[] keys)`                                                                                             | `EnumerableResult<T>`       |
| `Count(Expr expr)`                                                                                                            | `ValueResult<int>`          |
| `Exists(object o)` / `Exists(T o)`                                                                                            | `ValueResult<bool>`         |
| `ExistsKey(params object[] keys)`                                                                                             | `ValueResult<bool>`         |
| `Exists(Expr expr)`                                                                                                           | `ValueResult<bool>`         |

### DataViewDAO<T>（查询，返回 DataTable）

`DataTableResult` 支持：`.GetResult()` / `.GetResultAsync()`

| 方法                                                    | 返回类型              |
| ----------------------------------------------------- | ----------------- |
| `Search(Expr expr)`                                   | `DataTableResult` |
| `Search(string[] propertyNames, Expr expr)`           | `DataTableResult` |
| `Search(ref ExprString sqlBody, bool isFull = false)` | `DataTableResult` |

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
    public string[] GetArgs() => new string[] { (UserId % 10).ToString() };
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

### Service 异常处理事件

```csharp
// 全局静态事件，服务方法抛出异常时触发
ServiceInvokeInterceptor.ExceptionHandling += (sender, context) =>
{
    // 读取异常、方法名、参数、SQL 栈等上下文
    if (context.Exception is TimeoutException)
        context.Handle(123); // 把异常转成约定结果
};
```

- `ServiceInvokeInterceptor.ExceptionHandling` 为全局静态事件
- `RemoteServiceInvokeInterceptor.ExceptionHandling` 行为一致
- 不调用 `context.Handle(...)` 时异常继续抛出，适合只做告警/埋点
- 调用 `context.Handle(result)` 后把异常转成正常返回结果

## 七、特性速查

| 特性                                                           | 用途                           |
| ------------------------------------------------------------ | ---------------------------- |
| `[Table("TableName")]`                                       | 指定表名，可选 `DataSource` 参数      |
| `[Column("ColName", IsPrimaryKey, IsIdentity, IdentityStart, IdentityIncreasement)]` | 指定列名和属性；`IdentityStart`/`IdentityIncreasement` 自定义自增起始值与增量（SQL Server/达梦/Oracle 列级支持，MySQL 表级 `AUTO_INCREMENT=n`，SQLite/PG 不支持） |
| `[ForeignType(typeof(T), Alias, AutoExpand)]`                | 指定外键关联类型，`AutoExpand` 扩展关联路径 |
| `[TableJoin(typeof(T), ForeignKeys, AliasName, AutoExpand)]` | 类级关联定义，支持复合键和路径复用            |
| `[ForeignColumn(typeof(T), Property)]`                       | 从关联表获取的列（用于视图）               |
| `[Transaction]`                                              | 声明式事务                        |
| `[AutoRegister]`                                             | 自动注册到 DI 容器                  |

## 八、Expr 表达式系统

### 三种查询方式

| 方式                           | 适用场景                  |
| ---------------------------- | --------------------- |
| Lambda 表达式 `u => u.Age > 18` | 简单条件，编译时类型安全          |
| Expr 对象（运算符 / Fluent 方法）     | 复杂条件、动态条件累加、链式查询      |
| ExprString 插值字符串             | 自定义 DAO 中的条件片段或完整 SQL |

### Expr 静态工厂方法

| 方法                                               | 返回类型              | 说明                                          |
| ------------------------------------------------ | ----------------- | ------------------------------------------- |
| `Expr.Prop("Name")`                              | `PropertyExpr`    | 属性表达式                                       |
| `Expr.Prop("alias", "Name")`                     | `PropertyExpr`    | 带表别名的属性表达式                                  |
| `Expr.Value(obj)`                                | `ValueExpr`       | 参数化值                                        |
| `Expr.Const(obj)`                                | `ValueExpr`       | 常量值（内联到 SQL，不参数化）                           |
| `Expr.Func("ABS", expr)`                         | `FunctionExpr`    | 函数调用                                        |
| `Expr.Aggregate("SUM", expr, isDistinct)`        | `FunctionExpr`    | 聚合函数（IsAggregate=true）                      |
| `Expr.Concat(e1, e2)`                            | `ValueSet`        | CONCAT 字符串拼接（扩展方法）                          |
| `Expr.Lambda<T>(u => u.Age > 18)`                | `LogicExpr`       | Lambda 转 Expr                               |
| `Expr.From<T>(tableArgs)`                        | `FromExpr`        | 链式查询起点                                      |
| `Expr.Sql("key", arg)`                           | `GenericSqlExpr`  | 动态 SQL 片段                                   |
| `Expr.Delete<T>(tableArgs)`                      | `DeleteExpr`      | 创建 DELETE 表达式                               |
| `Expr.If(condition, thenExpr, elseExpr)`         | `FunctionExpr`    | IF 表达式                                      |
| `Expr.Case(cases, elseExpr)`                     | `FunctionExpr`    | CASE 表达式（元组数组形式）                          |
| `Expr.Case(params (LogicExpr, ValueTypeExpr)[])` | `FunctionExpr`    | CASE 表达式（元组数组形式，无 ELSE）                  |
| `Expr.Case(params Expr[])`                       | `FunctionExpr`    | CASE 表达式（交替参数形式）                          |
| `Expr.Query<T>(expression)`                      | `Expr`            | IQueryable Lambda 转 Expr                    |
| `Expr.Query<T, TResult>(expression)`             | `Expr`            | 带返回值的 IQueryable Lambda 转 Expr              |
| `Expr.Exists<T>(innerExpr, tableArgs)`           | `ForeignExpr`     | 关联表 EXISTS 查询                               |
| `Expr.Exists(type, innerExpr, tableArgs)`        | `ForeignExpr`     | 关联表 EXISTS 查询（指定类型）                         |
| `Expr.ExistsRelated<T>(innerExpr, tableArgs)`    | `ForeignExpr`     | 自动关联的 EXISTS 查询                             |
| `Expr.ExistsRelated(type, innerExpr, tableArgs)` | `ForeignExpr`     | 自动关联的 EXISTS 查询（指定类型）                       |
| `Expr.Exists<T>(lambda)`                         | `bool`            | 仅用于 Lambda 表达式中构造 EXISTS 查询（直接调用会抛出异常）      |
| `Expr.ExistsRelated<T>(lambda)`                  | `bool`            | 仅用于 Lambda 表达式中构造自动关联的 EXISTS 查询（直接调用会抛出异常） |

补充规则：

- `PropertyExpr`、`TableExpr`、`ForeignExpr`、`FunctionExpr`、`SelectExpr`、`SelectItemExpr`、`CommonTableExpr`、`GenericSqlExpr` 等表达式，在对象比较和哈希计算时，名称与别名按**忽略大小写**处理。
- 因此 `Prop("User", "Name")` 与 `Prop("user", "name")` 可以视为同一个表达式；别名 `T0` 与 `t0` 也会被视为相同别名。

### ExprVisitor 遍历方法

`ExprVisitor` 提供四种遍历模式，所有方法均支持可选的 `CancellationToken` 参数用于外部中断：

| 方法 | 遍历模式 | 短路终止 | 说明 |
|------|---------|---------|------|
| `ExprVisitor.Visit(Func<Expr,bool>, root, order?, ct?)` | `Func<Expr,bool>` 委托 | ✅ | 返回 `false` 或取消令牌终止 |
| `ExprVisitor.Visit(Action<Expr>, root, order?, ct?)` | `Action<Expr>` 委托 | ❌ | 总是完整遍历（除非取消） |
| `ExprVisitor.Visit(IExprNodeVisitor, root, ct?)` | `IExprNodeVisitor` 接口 | ❌ | BeginVisit(前序) + EndVisit(后序) |
| `ExprVisitor.Validate(ExprValidator, root, order?, ct?)` | `ExprValidator` 基类 | ✅ | Validate 返回 `false` 或取消终止 |

> `IExprNodeVisitor` 的 `BeginVisit(Expr, CancellationToken)` 和 `EndVisit(Expr, CancellationToken)` 接收取消令牌。若在回调内通过 `CancellationTokenSource.Cancel()` 取消，遍历将中断。

### CycleDetector 循环引用检测

`CycleDetector` 使用 `ExprVisitor` 检测 `Expr` 树中的循环引用（如 `Source` 链回环）：

| 方法 | 说明 |
|------|------|
| `CycleDetector.HasCycle(Expr root)` | 是否存在循环引用 |
| `CycleDetector.FindCycle(Expr root)` | 返回造成循环的节点，无循环返回 `null` |
| `CycleDetector.Detect(Expr root)` | 返回 `CycleResult`（含 `CycleNode` 和 `Path`） |

检测基于引用相等性（`ReferenceEquals`），通过 `CancellationTokenSource` 中断遍历，不使用异常控制流。

### 运算符重载

`PropertyExpr` / `ValueTypeExpr` 上的运算符：

| 运算符                         | 说明          | 返回类型            |
| --------------------------- | ----------- | --------------- |
| `==` `!=` `>` `<` `>=` `<=` | 比较          | `LogicExpr`     |
| `+` `-` `*` `/` `%`         | 算术          | `ValueTypeExpr` |
| `-expr` `~expr`             | 一元负号 / 按位取反 | `ValueTypeExpr` |

`LogicExpr` 上的运算符：

| 运算符  | 说明                           | 返回类型      |
| ---- | ---------------------------- | --------- |
| `&`  | AND（左或右为 null 时返回另一侧，适合动态累加） | `AndExpr` |
| `|`  | OR （左或右为 null 时返回另一侧，适合动态累加） | `OrExpr`  |
| `!`  | NOT                          | `NotExpr` |

补充说明：

- Lambda 中的三目运算符 `a ? b : c` 会自动转换为 `Expr.If(...)`，最终生成 SQL `CASE`

### PropertyExpr 扩展方法

| 分类   | 方法                                                                                                         |
| ---- | ---------------------------------------------------------------------------------------------------------- |
| 比较   | `.Equal(v)` `.NotEqual(v)` `.GreaterThan(v)` `.LessThan(v)` `.GreaterThanOrEqual(v)` `.LessThanOrEqual(v)` |
| 集合   | `.In(IEnumerable)` `.In(params items)` `.In(Expr)`                                                         |
| 范围   | `.Between(low, high)`                                                                                      |
| 字符串  | `.Like(pattern)` `.Contains(text)` `.StartsWith(text)` `.EndsWith(text)`                                   |
| Null | `.IsNull()` `.IsNotNull()`                                                                                 |
| 类型转换 | `.Cast(DbType)`                                                                                             |
| 别名   | `.As("alias")` → `SelectItemExpr`                                                                          |
| 聚合   | `.Count(isDistinct)` `.Sum()` `.Avg()` `.Max()` `.Min()`                                                   |
| 排序   | `.Asc()` `.Desc()` → `OrderByItemExpr`                                                                     |

### LogicExpr 扩展方法

`.And(right)` `.Or(right)` `.Not()`

### 链式查询构建

`Expr.From<T>()` 起点，支持如下链式调用（顺序按 SQL 子句顺序）：

```csharp
using static LiteOrm.Common.Expr;
var query = From<User>()
    .Where(Prop("Age") > 18)                         // WhereExpr
    .SelectAll()                                     // SelectExpr，等价于 SELECT *
    .Where(Prop("Status") == 1)
    .OrderBy(Prop("DeptId").Asc())                   // OrderByExpr
    .Section(0, 20);                                 // SectionExpr (skip, take)
```

也可以按需显式选择字段：

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

`SelectExpr` 可用于 IN 子查询：

```csharp
using static LiteOrm.Common.Expr;
// IN 子查询
var subQuery = From<Department>()
    .Where(Prop("Name") == "IT")
    .Select("Id");
var expr = Prop("DeptId").In(subQuery);
```

UpdateExpr / DeleteExpr（用于 `ObjectDAO.Delete(LogicExpr)` / `ObjectDAO.Update(UpdateExpr)`，以及 Service 层的 `IEntityService<T>.DeleteAll(LogicExpr)` / `UpdateAll(UpdateExpr)` 等）：

```csharp
using static LiteOrm.Common.Expr;
var update = new UpdateExpr(From<User>(), Prop("Id") == 1);
update.Set(("UserName", Value("NewName")), ("Age", Value(30)));

var delete = new DeleteExpr(From<User>(), Prop("Age") < 18);
```

### ExprString

插值字符串处理器，可在 DAO 的 `Search(...)` / `SearchAs(...)` 等支持 `ExprString` 的方法参数中直接嵌入 Expr 对象。Service 不提供 `ExprString` 查询入口：

```csharp
using static LiteOrm.Common.Expr;
// 嵌入 Expr 对象自动转为带参数 SQL 片段
var result = dao.Search($"WHERE {Prop("DeptName") == deptName} AND {Prop("Age") > 18}");

// 需要完整 SQL 时，在 DAO 中配合 isFull: true 使用
var table = dataViewDao.Search(
    $"SELECT [Id], [UserName] FROM [Users] WHERE {Prop("Age")} > {minAge}",
    isFull: true
);
```

> 在 `ExprString` 里手写表名、列名时，可以用 `[`、`]` 作为通用引用符占位；LiteOrm 会在命令真正执行前替换成当前数据库的真实标识符引用符。
>
> `ExprString` 不支持把 `SelectExpr.With(name)` / `CommonTableExpr` 这种 CTE 表达式自动展开成 `WITH` SQL。需要 CTE 时，请使用 `Expr` / `SelectExpr` 结构化构建；如果必须走 `ExprString`，请手动写完整 `WITH ... SELECT ...` SQL。

#### RawSql —— 插入原始 SQL 片段

`RawSql` 是独立的 `readonly struct`（**不**继承 `Expr`），作为 `ExprString` 的辅助入口存在。在插值字符串中插入 `RawSql` 时，其内容会**原样拼入 SQL**，不参数化、不做语法处理、不替换 `[ ]` 占位符。专用于**不适合参数化的动态值**；纯静态的 SQL 文本直接写在 `ExprString` 字面量中即可，无需使用 `RawSql`：

```csharp
using LiteOrm.Common;
using static LiteOrm.Common.Expr;

// 1) LIMIT/OFFSET 动态分页（数值类，校验范围：非负整数+上限）
int pageSize = 20;
int offset = pageSize * pageIndex;
var paged = await dataViewDAO.Search(
    $"WHERE {Prop("Age")} >= {minAge} ORDER BY Id LIMIT {new RawSql(offset.ToString())}, {new RawSql(pageSize.ToString())}"
).ToListAsync();

// 2) 动态排序方向 ASC/DESC（SQL 关键字类，白名单枚举）
string direction = ascending ? "ASC" : "DESC";
var sorted = await dataViewDAO.Search(
    $"WHERE {Prop("Age")} >= {minAge} ORDER BY Id {new RawSql(direction)}"
).ToListAsync();

// 3) 动态列名/排序字段（标识符类，白名单校验：仅允许实体真实列名 + 字符集校验）
string[] allowed = { "Id", "Name", "Age", "CreatedAt" };
string sortField = allowed.Contains(userField) ? userField : "Id";
var result = await dataViewDAO.Search(
    $"WHERE {Prop("Age")} >= {minAge} ORDER BY {new RawSql(sortField)} {new RawSql(direction)}"
).ToListAsync();
// 注：简单列名也可用 Expr.Prop(sortField)（自带名称校验和引用符包裹，更安全）；
//     仅当列为复杂表达式或确需绕过名称校验时才用 RawSql。
```

**安全约束（务必遵守）**：

| 规则 | 说明 |
|------|------|
| 数值类动态值需范围校验 | 如 `LIMIT` 行数：非负整数 + 合理上限 |
| 字符串/token 类需白名单校验 | `ASC`/`DESC` 用枚举白名单；列名用实体列名白名单 + 字符集校验 |
| 纯静态内容不要用 RawSql | 写死的 SQL 片段直接写在 `ExprString` 字面量中即可，包成 `RawSql` 反而掩盖真实意图 |
| 不被验证器扫描 | `RawSql` 不是 `Expr`，`ExprValidator.CreateQueryOnly()` 不会扫描它 |
| 不支持 JSON 往返 | `RawSql` 不能通过 `ExprJsonConverter` 序列化/反序列化，前端 Expr JSON 不能携带 |
| 优先用 Expr | 简单列名用 `Expr.Prop`（自带名称校验和引用符包裹）；凡是能用 `Expr.Func`/`Expr.Sql`（预注册 `GenericSqlExpr`）表达的，不要用 `RawSql` |

> 需要在自定义 SQL 中传递运行时字符串/复杂值时，请用 `GenericSqlExpr.Register` 注册回调，在回调内部使用 `outputParams` 参数化。详见 [ExprString 指南 - 第 8 节](../02-core-usage/07-exprstring-guide.md#8-插入原始-sql-rawsql) 与 [安全性](../03-advanced-topics/08-security.md)。

### 常用模式

```csharp
using static LiteOrm.Common.Expr;
// 动态条件累加（& 对 null 安全）
LogicExpr condition = null;
if (minAge.HasValue)  condition &= Prop("Age") >= minAge.Value;
if (deptId.HasValue)  condition &= Prop("DeptId") == deptId.Value;
if (!string.IsNullOrEmpty(name)) condition &= Prop("UserName").Contains(name);
var users = await dao.Search(condition).ToListAsync();

// 关联表 EXISTS 查询
var expr = Exists<Department>(Prop("Name") == "IT");
// 等价 Lambda 写法（在 Lambda 查询中）：
var expr = Lambda<User>(u => Exists<Department>(d => d.Name == "IT"));

// SearchAs 选择部分字段
var result = dao.SearchAs(
    From<User>()
        .Where(Prop("Age") > 18)
        .Select("Id", "UserName")
).ToList();
```

### ExistsRelated 使用说明

`ExistsRelated` 通过关联表条件过滤主表，不需要在视图模型中显式暴露关联字段。

**关联匹配顺序：正向关联优先，未找到正向关联再尝试反向关联。多条关联路径时以** **`OR`** **连接。**

```csharp
using static LiteOrm.Common.Expr;
// 按关联部门过滤用户
var expr = ExistsRelated<Department>(Prop("Name") == "IT");
var users = await objectViewDAO.Search(expr).ToListAsync();

// Lambda 写法
var lambdaExpr = Lambda<User>(u => ExistsRelated<Department>(d => d.Name == "IT"));
```

## 相关链接

- [返回目录](../README.md)
- [API 索引](./02-api-index.md)
- [术语表](./03-glossary.md)
