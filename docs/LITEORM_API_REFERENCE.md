# LiteOrm API 参考指南

## 1. 项目概述

LiteOrm 是一个轻量级、高性能的 .NET ORM 框架，支持多种数据库（SQL Server, MySQL, Oracle, PostgreSQL, SQLite），提供完整的 CRUD 操作、灵活的查询表达式系统、声明式事务、自动化关联查询和动态分表功能。

**项目结构：**
- `LiteOrm.Common/` - 核心元数据、Expr表达式系统、接口定义
- `LiteOrm/` - ORM核心实现、DAO基类、SQL构建器
- `LiteOrm.ASPNetCore/` - ASP.NET Core 集成支持
- `LiteOrm.Demo/` - 示例项目
- `LiteOrm.Tests/` - 单元测试

## 2. 快速入门

### 2.1 安装配置

在 `appsettings.json` 中配置数据库连接：

```json
{
    "LiteOrm": {
        "Default": "DefaultConnection",
        "DataSources": [
            {
                "Name": "DefaultConnection",
                "ConnectionString": "Server=localhost;Port=3306;Database=liteorm;Uid=root;Pwd=123456;",
                "Provider": "MySql.Data.MySqlClient.MySqlConnection, MySql.Data",
                "KeepAliveDuration": "00:10:00",
                "PoolSize": 20,
                "MaxPoolSize": 100,
                "ParamCountLimit": 2000,
                "SyncTable": true,
                "ReadOnlyConfigs": [
                    {
                        "ConnectionString": "Server=readonly01;Port=3306;Database=liteorm;Uid=readonly;Pwd=123456;"
                    },
                    {
                        "ConnectionString": "Server=readonly02;Port=3306;Database=liteorm;Uid=readonly;Pwd=123456;",
                        "PoolSize": 10,
                        "KeepAliveDuration": "00:30:00"
                    }
                ]
            }
        ]
    }
}
```

**配置参数详解：**

| 参数名 | 默认值 | 说明 |
| :--- | :--- | :--- |
| **Default** | - | 默认数据源名称，如果实体未指定数据源则使用此项。 |
| **Name** | - | 必填，数据源名称。 |
| **ConnectionString** | - | 必填，物理连接字符串。 |
| **Provider** | - | 必填，DbConnection 实现类的类型全名（Assembly Qualified Name）。 |
| **PoolSize** | 16 | 基础连接池容量，超过此数量的数据库空闲连接会被释放。 |
| **MaxPoolSize** | 100 | 最大并发连接限制，防止耗尽数据库资源。 |
| **KeepAliveDuration** | 10min | 连接空闲存活时间，超过此时间后空闲连接将被物理关闭。 |
| **ParamCountLimit** | 2000 | 单条 SQL 支持的最大参数个数，批量操作时参数超过此限制会自动分批执行，避免触发 DB 限制。 |
| **SyncTable** | false | 是否在启动时自动检测实体类并尝试同步数据库表结构。 |
| **ReadOnlyConfigs** | - | 只读库配置 |

### 2.2 ReadOnlyConfigs（只读从库配置）

LiteOrm 支持为每个主数据源配置若干只读从库，用于读写分离、负载均衡或故障切换。只读配置放在对应数据源对象的 `ReadOnlyConfigs` 数组中。

说明：
- `ReadOnlyConfigs`：可选数组，每项为只读数据源配置对象（可为空）。
- 每个只读项至少包含 `ConnectionString`，当只读库与主库使用不同驱动时也可指定 `Provider`。
- LiteOrm 在执行只读操作（例如 SELECT 查询）时会优先选择只读配置，从而减轻主库写入压力并实现读扩展。
- 如果所有只读配置不可用或未配置，LiteOrm 会回退到主数据源的连接。
- 可结合连接池与自定义路由策略实现更复杂的读写分离、负载均衡或高可用策略。

### 2.3 注册 LiteOrm

在 `Program.cs` 中注册 LiteOrm：

```csharp
// Generic Host / Console 应用
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()  // 自动扫描 AutoRegister 特性并初始化连接池
    .Build();

// ASP.NET Core 应用
var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();
var app = builder.Build();
```

**注意：** LiteOrm 使用 `RegisterLiteOrm()` 进行注册，其内部注册 `Autofac` 作为根容器，而非使用 `AddLiteOrm()` 注册服务。

### 2.4 定义实体

示例代码：

```csharp
using LiteOrm.Common;

[Table("Users")]
public class User : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserName")]
    public string? UserName { get; set; }

    [Column("Age")]
    public int Age { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    [Column("DeptId")]
    [ForeignType(typeof(Department), Alias = "Dept")]
    public int? DeptId { get; set; }
}
```

### 2.5 定义服务

示例代码：

```csharp
// 定义服务接口（推荐显式声明 Async 接口）
public interface IUserService : IEntityService<User>, IEntityServiceAsync<User>,
                                IEntityViewService<UserView>, IEntityViewServiceAsync<UserView>
{
    // 可添加自定义方法
}

// 实现服务（基类会自动提供所有方法的实现）
public class UserService : EntityService<User, UserView>, IUserService
{
    // 继承所有基础 CRUD 和查询方法
}
```

### 2.6 使用服务

示例代码：

```csharp
// 插入
var user = new User { UserName = "admin", Age = 25, CreateTime = DateTime.Now };
userService.Insert(user);

// 查询
var users = userService.Search(u => u.Age > 18);
var admin = userService.SearchOne(u => u.UserName == "admin");

// 更新
user.Age = 30;
userService.Update(user);

// 删除
userService.Delete(user);

// 异步操作
await userService.InsertAsync(user);
var users = await userService.SearchAsync(u => u.Age > 18);
```

### 2.7 SessionManager 生命周期管理

`SessionManager` 是 LiteOrm 的会话核心，负责管理当前请求/任务的数据库连接与事务上下文。通过静态属性 `SessionManager.Current` 可在任意异步上下文中访问当前会话。

`SessionManager` 以 **Scoped** 方式注册，必须在合适的时机赋值给 `SessionManager.Current` 并在结束时调用 `Dispose()`，以确保数据库连接被正确归还到连接池。

#### ASP.NET Core 应用

需要自定义一个中间件，在每个 HTTP 请求开始时设置 `SessionManager.Current`，并在请求结束时释放会话：

```csharp
public class LiteOrmScopeMiddleware
{
    private readonly RequestDelegate _next;

    public LiteOrmScopeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, SessionManager sessionManager)
    {
        SessionManager.Current = sessionManager;
        try
        {
            await _next(context);
        }
        finally
        {
            SessionManager.Current = null;
            sessionManager.Dispose();  // 归还所有数据库连接至连接池
        }
    }
}
```

在 `Program.cs` 中注册该中间件（应在管道较早位置）：

```csharp
var app = builder.Build();

app.UseMiddleware<LiteOrmScopeMiddleware>();

app.UseRouting();
app.MapControllers();
app.Run();
```

#### Generic Host / Console 应用

在 Generic Host 或控制台应用中，每次独立的工作单元（如后台任务、定时任务、CLI 命令等）都需要手动创建 DI 作用域并管理 `SessionManager` 的生命周期：

```csharp
// 从 Host 服务提供者创建 DI 作用域，获取该作用域内的 SessionManager
using var scope = host.Services.CreateScope();
var sessionManager = scope.ServiceProvider.GetRequiredService<SessionManager>();
SessionManager.Current = sessionManager;
try
{
    // 在此作用域内执行数据库操作
    var users = userService.Search(u => u.Age > 18);
    // ...
}
finally
{
    SessionManager.Current = null;
    sessionManager.Dispose();  // 归还所有数据库连接至连接池
}
```

#### 并行/嵌套子作用域

在同一异步流中若需为子任务隔离独立的数据库连接上下文，可调用 `sessionManager.CreateScope()` 创建子作用域。子作用域会将 `SessionManager.Current` 替换为当前实例的副本，退出 `using` 块时自动释放副本并恢复之前的 `Current`：

```csharp
// sessionManager 已是 Current
using (sessionManager.CreateScope())
{
    // 此作用域内 Current 为独立副本，拥有自己的数据库连接
    var subResult = await someService.QueryAsync();
    // 退出 using 后，副本自动 Dispose 并恢复原来的 Current
}
```

> **注意：** ASP.NET Core 应用若未注册 `SessionManager` 生命周期中间件，`SessionManager.Current` 将不会被自动管理，数据库操作将无法获取有效的会话上下文。

## 3. 文件结构

3.1 项目文件结构：

```
LiteOrm.Common/
├── Attributes/              # 特性定义（Table, Column, ForeignType等）
├── Classes/                 # 工具类（ExprConvert, Util等）
├── DAO/                     # 数据访问接口与扩展
│   ├── IObjectDAO.cs            # 实体DAO接口
│   ├── IObjectDAOAsync.cs       # 实体DAO异步接口
│   ├── IObjectViewDAO.cs        # 视图DAO接口
│   └── LambdaExprExtensions.cs  # Lambda/IQueryable 形式的扩展方法
├── Expr/                    # 见5.1节，表达式系统
├── Service/                 # 服务接口
│   ├── IEntityService.cs
│   ├── IEntityServiceAsync.cs
│   ├── IEntityViewService.cs
│   └── IEntityViewServiceAsync.cs
├── SqlBuilder/              # SQL构建器接口
└── SqlSegment/              # SQL片段（Select/Where/OrderBy等）

LiteOrm/
├── Classes/                 # 核心类
│   ├── SqlGen.cs            # SQL生成器
│   ├── LiteOrmTableSyncInitializer.cs
│   ├── LiteOrmServiceExtensions.cs  # RegisterLiteOrm 扩展方法
│   └── ...
├── DAO/                     # DAO实现
│   ├── DAOBase.cs           # DAO基类
│   ├── ObjectDAO.cs         # 实体DAO实现
│   ├── ObjectViewDAO.cs     # 视图DAO实现
│   ├── DataViewDAO.cs       # DataTable DAO实现
│   └── ...
├── Service/                 # 服务实现
│   ├── EntityService.cs
│   └── EntityViewService.cs
├── DAOContext/              # DAO上下文
│   ├── DAOContext.cs
│   ├── DAOContextPool.cs
│   └── DAOContextPoolFactory.cs
└── SqlBuilder/               # SQL构建器实现
    ├── SqlBuilder.cs        # 默认SQL构建器
    ├── MySqlBuilder.cs      # MySQL
    ├── SqlServerBuilder.cs   # SQL Server
    ├── PostgreSqlBuilder.cs # PostgreSQL
    ├── OracleBuilder.cs      # Oracle
    └── SQLiteBuilder.cs     # SQLite
```

## 4. 基础定义

### 4.1 实体类写法

标准实体类：

```csharp
using LiteOrm.Common;

[Table("Users")]
public class User : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserName")]
    public string? UserName { get; set; }

    [Column("Age")]
    public int Age { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    [Column("DeptId")]
    [ForeignType(typeof(Department), Alias = "Dept")]  // 外键关联到部门表
    public int? DeptId { get; set; }
}
```

### 4.2 视图模型定义

视图模型用于查询操作，可以包含关联表字段：

```csharp
// 部门实体（需先定义外键关联）
[Table("Departments")]
public class Department : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Name")]
    public string Name { get; set; } = string.Empty;
}

// 视图模型继承自实体类
public class UserView : User
{
    // 使用 ForeignType 的 Alias 关联查询部门名称
    // 对应 User.DeptId 上的 [ForeignType(typeof(Department), Alias = "Dept")]
    [ForeignColumn("Dept", Property = "Name")]
    public string? DeptName { get; set; }
}
```

### 4.3 分表支持

实现IArged接口支持分表：

```csharp
[Table("Log_{0}")]  // 表名模板，{0} 由 TableArgs 填充
public class Log : IArged
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Content")]
    public string Content { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    // 自动路由到 Log_202401 格式的表
    string[] IArged.TableArgs => [CreateTime.ToString("yyyyMM")];
}
```

### 4.4 EntityService

#### 4.4.1 IEntityService - 实体服务接口

**文件位置：** `LiteOrm.Common/Service/IEntityService.cs`

```csharp
// 基础 CRUD（定义在 IEntityService<T>）
bool Insert(T entity)
bool Update(T entity)
bool Delete(T entity)
bool UpdateOrInsert(T entity)

// 根据条件删除（使用 LogicExpr，定义在非泛型 IEntityService）
int Delete(LogicExpr expr, params string[] tableArgs)

// 根据 UpdateExpr 更新（定义在非泛型 IEntityService）
int Update(UpdateExpr expr, params string[] tableArgs)

// 根据 ID 删除（定义在非泛型 IEntityService）
bool DeleteID(object id, params string[] tableArgs)

// 批量操作（带事务支持，定义在 IEntityService<T>）
void BatchInsert(IEnumerable<T> entities)
void BatchUpdate(IEnumerable<T> entities)
void BatchUpdateOrInsert(IEnumerable<T> entities)
void BatchDelete(IEnumerable<T> entities)

// 混合批量操作（新增/更新/删除，定义在 IEntityService<T>）
void Batch(IEnumerable<EntityOperation<T>> entities)
```

#### 4.4.2 IEntityViewService - 视图服务接口

**文件位置：** `LiteOrm.Common/Service/IEntityViewService.cs`

```csharp
// 根据 ID 获取对象
T GetObject(object id, params string[] tableArgs)

// 查询实体列表
List<T> Search(Expr expr = null, params string[] tableArgs)

// 查询单个
T SearchOne(Expr expr, params string[] tableArgs)

// 遍历（流式，不缓存到内存）
void ForEach(Expr expr, Action<T> func, params string[] tableArgs)

// 检查是否存在
bool Exists(Expr expr, params string[] tableArgs)
bool ExistsID(object id, params string[] tableArgs)

// 统计数量
int Count(Expr expr = null, params string[] tableArgs)

// 扩展方法（Lambda 形式，源自 LambdaExprExtensions，位于 LiteOrm.Common/DAO/LambdaExprExtensions.cs）
List<T> Search(Expression<Func<T, bool>> expression, string[] tableArgs = null)
List<T> Search(Expression<Func<IQueryable<T>, IQueryable<T>>> expression, string[] tableArgs = null)
T SearchOne(Expression<Func<T, bool>> expression, string[] tableArgs = null)
bool Exists(Expression<Func<T, bool>> expression, params string[] tableArgs)
int Count(Expression<Func<T, bool>> expression, params string[] tableArgs)
```

#### 4.4.3 异步接口

- `IEntityServiceAsync<T>` 是 `IEntityService<T>` 的异步版本，额外提供以下方法：
  - `Task BatchAsync(IEnumerable<EntityOperation<T>> entities, ...)` — 异步混合批量操作
- `IEntityViewServiceAsync<T>` 是 `IEntityViewService<T>` 的异步版本，额外提供以下方法：
  - `Task ForEachAsync(Expr expr, Func<T, Task> func, ...)` — 异步遍历（流式）
  - Lambda 扩展方法：`SearchAsync`、`SearchOneAsync`、`ExistsAsync`、`CountAsync`（位于 `LambdaExprExtensions`）

#### 4.4.4 EntityService - 服务基类

`EntityService<T, TView>` 基类自动实现 `IEntityService<T>` 和 `IEntityViewService<TView>` 及异步接口。

**文件位置：** `LiteOrm/Service/EntityService.cs`

```csharp
// 实体类型与视图类型相同时（简化版本）
public interface IProductService : IEntityService<Product>, IEntityServiceAsync<Product>,
                                    IEntityViewService<Product>, IEntityViewServiceAsync<Product>
{
    // 可添加自定义方法
}

public class ProductService : EntityService<Product>, IProductService
{
    // EntityService<T> 是 EntityService<T, T> 的便利版本
}

// 实体类型与视图类型不同时（推荐，支持关联字段）
public interface IUserService : IEntityService<User>, IEntityServiceAsync<User>,
                                IEntityViewService<UserView>, IEntityViewServiceAsync<UserView>
{
    // 可添加自定义方法
}

public class UserService : EntityService<User, UserView>, IUserService
{
    // 继承所有 CRUD 和查询方法
}
```

`EntityService<T, TView>` 已标注 `[AutoRegister]`，无需自定义子类就可直接通过 DI 以泛型方式使用，适用于不需要扩展自定义方法的简单场景：

```csharp
// 直接注入或从 DI 解析，无需定义任何服务类
var entityService = serviceProvider.GetRequiredService<IEntityService<User>>();
var viewService   = serviceProvider.GetRequiredService<IEntityViewService<UserView>>();

// 异步版本同理
var entityServiceAsync = serviceProvider.GetRequiredService<IEntityServiceAsync<User>>();
var viewServiceAsync   = serviceProvider.GetRequiredService<IEntityViewServiceAsync<UserView>>();
```

### 4.5 各 DAO 的作用说明

部分 DAO 查询方法返回封装结果对象，支持延迟执行和统一的同步/异步消费：

- `EnumerableResult<T>`：由 `ObjectViewDAO.Search` 等查询方法返回，封装了 `DbCommand` 的执行结果，既实现 `IEnumerable<T>` 也实现 `IAsyncEnumerable<T>`，支持如下消费方式：
  - 同步：`.ToList()`、`.FirstOrDefault()`、`.GetResult()`。
  - 异步：`.ToListAsync()`、`.FirstOrDefaultAsync()`、`.GetResultAsync()`、`await foreach`。
  - 流式处理：实现 `IAsyncEnumerable<T>` 可用于 `await foreach` 按行读取，适合大结果集的异步处理。
  - 注意：`EnumerableResult<T>` 的底层 `DbDataReader` 仅能消费一次；若需多次遍历，请先调用 `.ToList()` 或 `.GetResult()` 将结果缓存到内存。

- `DataTableResult`：由 `DataViewDAO.Search` 返回，可通过 `.GetResult()` / `.GetResultAsync()` 获取 `DataTable`，也支持同步与异步调用。

#### 4.5.1 ObjectDAO - 实体数据访问

**文件位置：** `LiteOrm/DAO/ObjectDAO.cs`

**作用**：用于实体的CRUD操作，返回实体对象。

```csharp
// 插入单个实体
bool Insert(T entity)
Task<bool> InsertAsync(T entity, CancellationToken cancellationToken = default)

// 更新单个实体
bool Update(T entity)
Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default)

// 插入或更新（根据主键判断）
UpdateOrInsertResult UpdateOrInsert(T entity)
Task<UpdateOrInsertResult> UpdateOrInsertAsync(T entity, CancellationToken cancellationToken = default)

// 根据实体删除
bool Delete(T entity)
Task<bool> DeleteAsync(T entity, CancellationToken cancellationToken = default)

// 根据条件删除（使用LogicExpr）
int Delete(LogicExpr expr)
Task<int> DeleteAsync(LogicExpr expr, CancellationToken cancellationToken = default)

// 根据ID删除
bool DeleteByKeys(params object[] keys)
Task<bool> DeleteByKeysAsync(object[] keys, CancellationToken cancellationToken = default)

// 批量删除ID
void BatchDeleteByKeys(IEnumerable keys)
Task BatchDeleteByKeysAsync(IEnumerable keys, CancellationToken cancellationToken = default)

// 批量插入
void BatchInsert(IEnumerable<T> entities)
Task BatchInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)

// 批量更新（使用单条UPDATE语句）
void BatchUpdate(IEnumerable<T> entities)
Task BatchUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)

// 批量插入或更新
void BatchUpdateOrInsert(IEnumerable<T> entities)
Task BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)

// 批量删除
void BatchDelete(IEnumerable<T> entities)
Task BatchDeleteAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
```

#### 4.5.2 DataDAO - 按条件更新（不返回实体）

**文件位置：** `LiteOrm/DAO/DataDAO.cs`

**作用**：提供针对单表的批量字段更新操作，无需加载实体对象。

```csharp
// 按条件批量更新指定字段（返回 NonQueryResult，调用 Execute() 或 ExecuteAsync() 执行）
NonQueryResult UpdateAllValues(IEnumerable<KeyValuePair<string, object>> values, LogicExpr expr)

// 按主键更新指定字段
NonQueryResult UpdateValues(IEnumerable<KeyValuePair<string, object>> values, params object[] keys)
```

**示例：**

```csharp
var dataDAO = serviceProvider.GetRequiredService<DataDAO<User>>();

// 将所有 Age > 60 的用户状态置为 inactive
dataDAO.UpdateAllValues(
    new[] { new KeyValuePair<string, object>("Status", "inactive") },
    Expr.Prop("Age") > 60
).Execute();

// 按主键更新单条记录的指定字段
dataDAO.UpdateValues(
    new[] { new KeyValuePair<string, object>("Age", 99) },
    userId
).Execute();
```

#### 4.5.3 DataViewDAO - 视图查询（返回DataTableResult）

**文件位置：** `LiteOrm/DAO/DataViewDAO.cs`

**作用**：返回 DataTable 格式的结果，支持聚合查询和 GroupBy。

```csharp
// 查询返回 DataTableResult
DataTableResult Search(Expr expr)
Task<DataTableResult> SearchAsync(Expr expr, CancellationToken cancellationToken = default)

// 指定字段查询
DataTableResult Search(string[] propertyNames, Expr expr)
Task<DataTableResult> SearchAsync(string[] propertyNames, Expr expr, CancellationToken cancellationToken = default)
```

**注意：** 聚合查询（使用 GroupBy 和聚合函数如 COUNT/SUM/AVG/MAX/MIN）必须使用 DataViewDAO，因为 EntityViewService 不支持 GroupBy。

#### 4.5.4 ObjectViewDAO - 实体视图查询

**文件位置：** `LiteOrm/DAO/ObjectViewDAO.cs`

**作用**：查询视图模型，自动JOIN关联表。

```csharp
// 查询视图模型（自动JOIN关联表）
EnumerableResult<T> Search(Expr expr = null)
Task<EnumerableResult<T>> SearchAsync(Expr expr, CancellationToken cancellationToken = default)

// 根据主键获取对象
EnumerableResult<T> GetObject(params object[] keys)
Task<EnumerableResult<T>> GetObjectAsync(object[] keys, CancellationToken cancellationToken = default)

// 判断对象是否存在
ValueResult<bool> Exists(Expr expr)
Task<ValueResult<bool>> ExistsAsync(Expr expr, CancellationToken cancellationToken = default)

// 统计数量
ValueResult<int> Count(Expr expr)
Task<ValueResult<int>> CountAsync(Expr expr, CancellationToken cancellationToken = default)
```

## 5. Expr详细说明

LiteOrm 的核心是 Expr 表达式系统，Lambda 表达式方式也是先解析为 Expr，再拼接为 SQL。

### 5.1 Expr结构

**文件位置：** `LiteOrm.Common/Expr/`

```
Expr (基类)
├── LogicExpr (逻辑表达式，用于WHERE条件)
│   ├── LogicBinaryExpr (二元比较: ==, >, <, LIKE, IN, IS NULL 等)
│   ├── LogicSet (逻辑组合: AND / OR)
│   ├── NotExpr (NOT 取反)
│   ├── ForeignExpr (EXISTS 子查询)
│   └── LambdaExpr (Lambda 延迟求值表达式)
└── ValueTypeExpr (值类型表达式基类)
    ├── ValueExpr (常量或变量值)
    ├── PropertyExpr (属性/列引用)
    ├── FunctionExpr (函数调用)
    ├── AggregateFunctionExpr (聚合函数: COUNT/SUM/AVG/MAX/MIN)
    ├── ValueBinaryExpr (数学运算: +, -, *, /)
    └── ValueSet (值集合，如 CONCAT / LIST)
```

**运算符重载说明：**

`ValueTypeExpr`（包含 `PropertyExpr`）和 `LogicExpr` 均重载了常用 C# 运算符，可直接用运算符拼接条件而无需调用方法。

| 运算符 | 所在类型 | 返回类型 | 语义 |
| :--- | :--- | :--- | :--- |
| `==` | `ValueTypeExpr` | `LogicExpr` | 等于（自动处理 null 为 IS NULL） |
| `!=` | `ValueTypeExpr` | `LogicExpr` | 不等于 |
| `>` | `ValueTypeExpr` | `LogicExpr` | 大于 |
| `<` | `ValueTypeExpr` | `LogicExpr` | 小于 |
| `>=` | `ValueTypeExpr` | `LogicExpr` | 大于等于 |
| `<=` | `ValueTypeExpr` | `LogicExpr` | 小于等于 |
| `+` `-` `*` `/` | `ValueTypeExpr` | `ValueTypeExpr` | 四则运算 |
| `-`（一元） | `ValueTypeExpr` | `ValueTypeExpr` | 负号 |
| `&` | `LogicExpr` | `LogicExpr` | 逻辑与（AND），任一为 null 则返回另一个 |
| `\|` | `LogicExpr` | `LogicExpr` | 逻辑或（OR），任一为 null 则返回 null |
| `!` | `LogicExpr` | `LogicExpr` | 逻辑非（NOT） |

`ValueTypeExpr` 还提供从 C# 基类型到表达式的隐式转换：`string`、`int`、`long`、`double`、`decimal`、`bool`、`DateTime` 均可直接作为右操作数。

```csharp
// 比较运算符：直接与 C# 常量比较
LogicExpr cond1 = Expr.Prop("Age") > 18;         // 右操作数 int 隐式转换
LogicExpr cond2 = Expr.Prop("Name") == "admin";   // 右操作数 string 隐式转换
LogicExpr cond3 = Expr.Prop("Score") != null;     // null 自动转为 IS NOT NULL

// 逻辑运算符：拼接多个条件
LogicExpr and  = cond1 & cond2;     // AND
LogicExpr or   = cond1 | cond2;     // OR
LogicExpr not  = !cond1;            // NOT

// 四则运算符：用于数学运算的列引用
ValueTypeExpr expr = (Expr.Prop("Price") * 0.9) - Expr.Prop("Discount");  // Price * 0.9 - Discount
LogicExpr     cond = expr > 100;
```

> **注意：** 
>- `LogicExpr` 的 `&` / `|` 运算符用于表示逻辑 `与` / `或` 的 `Expr`，若一个操作数为 null，`&` 返回另一个（即忽略 null 条件），`|` 返回 null（即忽略另一个条件）。
>- `==` 和 `!=` 已重载返回比较相等的 `LogicExpr` 类型而不是 C# 语言中的 `bool` ,因此判定 `Expr` 类型变量是否为空时要用 `is null` 和 `is not null`。
>- 实现 `&&` 和 `||` 运算符需要实现 `LogicExpr` 到 `bool` 的隐式转换，但会造成 `if (expr == null)` 结果错误且但编译并不报错，因此并未实现。

### 5.2 Expr构造方式

#### 5.2.1 Lambda 自动转换

**文件位置：** `LiteOrm.Common/Expr/ExprExtensions.cs`

```csharp
// Lambda 转 Expr（最常用的方式）
LogicExpr expr = Expr.Lambda<User>(u => u.Age > 18 && u.UserName.Contains("admin"));
// 生成 SQL: WHERE (age > 18 AND username LIKE '%admin%')

// 使用 IQueryable 形式（推荐，支持排序和分页）
var users = userService.Search(
    q => q.Where(u => u.Age > 18)
         .OrderBy(u => u.Id)
         .Skip(0)
         .Take(10)
);

// 多条件合并（多个Where自动合并为AND）
var result = userService.Search(
    q => q.Where(u => u.Age > 18)
         .Where(u => u.UserName != null)
         .Where(u => u.UserName.Contains("admin"))
);

// 异步版本
var users = await userService.SearchAsync(
    q => q.Where(u => u.Age > 18)
         .OrderBy(u => u.Id)
         .Skip(0)
         .Take(10)
);
```

#### 5.2.2 手动构建表达式

**文件位置：** `LiteOrm.Common/Expr/Expr.cs`

```csharp
// 属性引用
PropertyExpr prop = Expr.Prop("age");
PropertyExpr prop2 = Expr.Prop(nameof(User.UserName));

// 比较运算
LogicExpr cmp1 = Expr.Prop("age") > 18;
LogicExpr cmp2 = Expr.Prop("username") == "admin";
LogicExpr cmp3 = Expr.Prop("age") >= 18 && Expr.Prop("age") <= 60;

// LIKE/IN/NULL
LogicExpr like = Expr.Prop("username").Contains("admin");
LogicExpr startWith = Expr.Prop("username").StartsWith("admin");
LogicExpr endWith = Expr.Prop("username").EndsWith("admin");
LogicExpr inExpr = Expr.Prop("id").In(1, 2, 3);
LogicExpr notIn = Expr.Prop("id").NotIn(new[] { 4, 5, 6 });
LogicExpr isNull = Expr.Prop("email").IsNull();
LogicExpr isNotNull = Expr.Prop("email").IsNotNull();

// BETWEEN
LogicExpr between = Expr.Between("age", 18, 60);
// 等价写法
LogicExpr between2 = Expr.Prop("age").Between(18, 60);

// 逻辑组合
LogicExpr andExpr = Expr.And(cmp1, like);
LogicExpr orExpr = Expr.Or(cmp1, cmp2);

// EXISTS 子查询（手动构建，传入 LogicExpr）
ForeignExpr existsExpr = Expr.Foreign<Department>(Expr.Prop("Id") == 1);
// 带别名的外键 EXISTS
ForeignExpr existsWithAlias = Expr.Foreign<Department>("Dept", Expr.Prop("Id").IsNotNull());
// 在 Lambda 表达式中使用 Expr.Exists<T>(参见第 6.5 节)

// 聚合函数
AggregateFunctionExpr countExpr = Expr.Prop("id").Count();
AggregateFunctionExpr sumExpr = Expr.Prop("amount").Sum();
AggregateFunctionExpr avgExpr = Expr.Prop("price").Avg();
AggregateFunctionExpr maxExpr = Expr.Prop("score").Max();
AggregateFunctionExpr minExpr = Expr.Prop("score").Min();
```

#### 5.2.3 查询片段表达式 (SqlSegment)

**文件位置：** `LiteOrm.Common/SqlSegment/`

```csharp
// 链式 API（推荐）
var fullQuery = Expr.From<User>()
    .Where(Expr.Prop("Age") > 18)
    .Select(Expr.Prop("Id"), Expr.Prop("UserName"));

// 带排序和分页
var pagedQuery = Expr.From<User>()
    .Where(Expr.Prop("Age") > 18)
    .OrderBy(Expr.Prop("Id"))
    .Section(0, 10);

// 聚合查询（需通过 DataViewDAO 执行）
var aggregateQuery = Expr.From<User>()
    .Where(Expr.Prop("Age") > 18)
    .GroupBy(Expr.Prop("DeptId"))
    .Select(Expr.Prop("DeptId"), Expr.Prop("Id").Count().As("user_count"));
```

### 5.3 序列化与反序列化

`Expr` 对象支持 JSON 序列化与反序列化，适用于日志、配置或跨进程/网络传输场景。框架为 `Expr` 层级类型实现了自定义的 JsonConverter，因此通常可以直接使用 `System.Text.Json`：

```csharp
using System.Text.Json;

// 示例：构造表达式
Expr expr = Expr.From<User>().Where(Expr.Prop("Age") > 18);

// 序列化
string json = JsonSerializer.Serialize<Expr>(expr, new JsonSerializerOptions { WriteIndented = true });

// 反序列化
Expr deserialized = JsonSerializer.Deserialize<Expr>(json);

// 验证（Expr 类型实现了相等比较）
bool equal = expr.Equals(deserialized);
```

注意事项：
- `GenericSqlExpr` 序列化时仅保存 `Key` 与 `Arg`；它不会序列化运行时注册的生成委托。反序列化后依赖于运行时已完成相应的 `GenericSqlExpr.Register` 注册，否则生成 SQL 时会抛出找不到键的异常；
- `LambdaExpr` 序列化时会转为普通 Expr，丢失原始 Lambda 表达式的类型信息和结构；反序列化后无法恢复为 Lambda 表达式，仅作为普通 Expr 使用；
- `FunctionExpr` 等类型已标注自定义 Converter，能保留必要的类型信息与字段；
- 在多版本或跨语言交互时，建议维持表达式模型兼容性，并在反序列化端进行额外校验与错误处理。

## 6. 基本功能

### 6.1 基础 CRUD

```csharp
// 插入
var user = new User { UserName = "admin", Age = 25, CreateTime = DateTime.Now };
userService.Insert(user);

// 更新
user.Age = 30;
userService.Update(user);

// 插入或更新
var result = userService.UpdateOrInsert(user);

// 删除
userService.Delete(user);

// 根据ID删除
userService.DeleteID(1);

// 批量操作
var users = new List<User>
{
    new User { UserName = "user1", Age = 20, CreateTime = DateTime.Now },
    new User { UserName = "user2", Age = 25, CreateTime = DateTime.Now }
};
userService.BatchInsert(users);

// 异步操作
await userService.InsertAsync(user);
await userService.UpdateAsync(user);
await userService.DeleteAsync(user);
```

### 6.2 查询

LiteOrm 提供三种查询条件构造方式，可以单独使用也可以混用：

| 方式 | 类型安全 | 适合场景 | 动态构建 | 特点 |
| :--- | :--- | :--- | :--- | :--- |
| **Lambda** | ✅ 编译期检查 | 常规查询、排序分页 | 有限（条件结构固定时最简洁） | Linq 风格，易读 |
| **Expr** | ❌ 运行时 | 动态条件组合、序列化传输 | 强（可任意拼接）| 灵活强大，适合复杂查询 |
| **ExprString** | ❌ 运行时 | 特殊 SQL 函数、数据库方言 | 弱（插值语法） | 简单直接 |

> Lambda 表达式并不确保生成的 SQL 合法性，在执行时自动转换为 `Expr` 对象，二者最终走相同的 SQL 生成路径，性能无差异。

#### 6.2.1 Lambda 方式

```csharp
// 单一条件
var users = userService.Search(u => u.Age > 18);

// 多条件AND
var users = userService.Search(u => u.Age > 18 && u.UserName.Contains("admin"));

// IN 查询
var users = userService.Search(u => new[] { 1, 2, 3 }.Contains(u.Id));

// IS NULL
var users = userService.Search(u => u.DeptId == null);

// LIKE
var users = userService.Search(u => u.UserName.Contains("admin"));

// 排序和分页
var users = userService.Search(
    q => q.Where(u => u.Age > 18)
         .OrderBy(u => u.Age)
         .ThenByDescending(u => u.Id)
         .Skip(0)
         .Take(10)
);

// 异步版本
var users = await userService.SearchAsync(u => u.Age > 18);
```

#### 6.2.2 Expr 方式

```csharp
// 单一条件
var users = userService.Search(Expr.Prop("Age") > 18);

// 多条件AND
var expr = Expr.Prop("Age") > 18 && Expr.Prop("UserName").Contains("admin");
var users = userService.Search(expr);

// IN 查询
var users = userService.Search(Expr.Prop("Id").In(1, 2, 3));

// IS NULL
var users = userService.Search(Expr.Prop("DeptId").IsNull());

// 异步版本
var users = await userService.SearchAsync(Expr.Prop("Age") > 18);
```

#### 6.2.3 ExprString（Raw SQL）

`.NET 8.0+` 支持 `ExprString` 插值字符串语法，可将 `Expr` 表达式和普通变量值混入 SQL 片段，由框架统一转换为参数化 SQL。`ObjectViewDAO` 和 `DataViewDAO` 均已支持，适合在 `EntityViewService` 内部使用，一般不提供外部调用。

参数处理规则：
- `Expr` 表达式 → 转换为等效 SQL 片段（可以仅插入字段表达式，也可以插入复杂表达式，例如 `WHERE {Expr.Prop("Age") > 18}` 会转化为 `WHERE Age > @p0`，而 `WHERE {Expr.Prop("Age")}} > 18` 都会转化为 `WHERE Age > 18`）
- 普通值（`int`、`string` 等）→ 自动转为命名参数如 `@p0`，防止 SQL 注入（例如`WHERE Age > {18}` 转化为 `WHERE Age > @p0`）

> **注意：** 不要在运行时直接把外部字符串拼接成 SQL，如需提供外部调用建议使用 `GenericSqlExpr`（参见第 7.2 节）的方式。`EnumerableResult<T>` 底层 `DbDataReader` 只能消费一次，若需重复遍历请先调用 `.ToList()`。

**示例 — `ObjectViewDAO`（默认追加 WHERE 片段）：**

```csharp
// 默认模式（isFull = false）：框架自动补全 SELECT ... FROM ...
var ageExpr = Expr.Prop("Age") > 25;

// 同步消费
var users = objectViewDAO.Search($"WHERE {ageExpr} AND UserName LIKE '张%'").ToList();

// 异步消费
var users = await objectViewDAO.Search($"WHERE {ageExpr}").ToListAsync();
// GetResultAsync() 与 ToListAsync() 完全等价
var users = await objectViewDAO.Search($"WHERE {ageExpr}").GetResultAsync();

// 流式处理（IAsyncEnumerable<T>）
await foreach (var user in objectViewDAO.Search($"WHERE {ageExpr}"))
{
    Console.WriteLine(user.UserName);
}

// isFull = true：传入完整 SQL，框架不自动补全
var users = await objectViewDAO.Search(
    $"SELECT Id, UserName FROM Users WHERE {Expr.Prop("Age")} > 18 ORDER BY Id",
    isFull: true).ToListAsync();
```

**示例 — `DataViewDAO`（返回 `DataTableResult`）：**

```csharp
int minAge = 20;

// 同步
DataTable dt = dataViewDAO.Search($"WHERE {Expr.Prop("Age")} > {minAge}").GetResult();

// 异步
DataTable dt = await dataViewDAO.Search($"WHERE {Expr.Prop("Age")} > {minAge}").GetResultAsync();

// isFull = true：可使用 {AllFields}、{From} 占位符（DataViewDAO 会自动替换）
DataTable dt = await dataViewDAO.Search(
    $"SELECT {{AllFields}} FROM {{From}} WHERE {Expr.Prop("Age")} > {minAge} ORDER BY Age DESC",
    isFull: true).GetResultAsync();
```

> **注意：** `ExprString` 语法需要 .NET 8.0 或更高版本。

#### 6.2.4 混合使用

Lambda、`Expr` 和 `ExprString` 可任意组合使用。Lambda 运行时会被解析为 `Expr`，因此三者可直接用 `&` / `|` 运算符拼接。

```csharp
// Lambda 条件 + 动态拼接的 Expr 条件
LogicExpr baseExpr = Expr.Lambda<User>(u => u.Age > 18);
if (deptId.HasValue)
    baseExpr = baseExpr & (Expr.Prop("DeptId") == deptId.Value);
if (!string.IsNullOrEmpty(keyword))
    baseExpr = baseExpr & Expr.Prop("UserName").Contains(keyword);

var users = await userService.SearchAsync(baseExpr);

// IQueryable 形式中嵌入预构建的 Expr 条件
LogicExpr auditFilter = Expr.Lambda<User>(u => u.Status == "active") & Expr.Sql("YearFilter", 2024);

var users = await userService.SearchAsync(
    q => q.Where(auditFilter)
         .Where(u => u.Age > 18)    // 多个 Where 自动合并为 AND
         .OrderByDescending(u => u.Id)
         .Skip(0).Take(20)
);
```

#### 6.3.1 聚合查询

聚合查询需通过 `DataViewDAO` 执行，`EntityViewService` 不支持 `GroupBy`。

```csharp
var dataViewDAO = serviceProvider.GetRequiredService<DataViewDAO<User>>();

var selectExpr = Expr.From<User>()
    .Where(Expr.Prop("Age") > 18)
    .GroupBy(Expr.Prop("DeptId"))
    .Select(Expr.Prop("DeptId"), Expr.Prop("Id").Count().As("user_count"));

// 同步
DataTable dtSync = dataViewDAO.Search(selectExpr).GetResult();

// 异步
DataTable dtAsync = await dataViewDAO.Search(selectExpr).GetResultAsync();
```

#### 6.3.2 关联查询

LiteOrm 的关联查询通过定义实体与视图实现：

```csharp
[Table("Orders")]
public class Order : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserId")]
    [ForeignType(typeof(User))]  // 外键关联到 User
    public int? UserId { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }
}

public class OrderView : Order
{
    [ForeignColumn(typeof(User), Property = "UserName")]  // 从 User 表带出 UserName
    public string? UserName { get; set; }
}

```

查询时自动生成 JOIN：

```csharp
var orders = orderViewService.Search(o => o.Amount > 100);
var orders = await orderViewService.SearchAsync(o => o.Amount > 100);
```

#### 6.3.3 EXISTS 子查询

LiteOrm 支持在 Lambda 表达式中使用 `Expr.Exists<T>` 进行高效的 EXISTS 子查询：

```csharp
// 基础 EXISTS 查询：查询拥有部门的所有用户
var users = userService.Search(u => Expr.Exists<Department>(d => d.Id == u.DeptId));

// EXISTS 与其他条件组合
var users = userService.Search(u => u.Age > 25 && Expr.Exists<Department>(d => d.Id == u.DeptId));

// 复杂的子查询条件
var users = userService.Search(u => Expr.Exists<Department>(d => d.Id == u.DeptId && d.Name == "IT"));

// NOT EXISTS：查询没有部门的用户
var orphans = userService.Search(u => !Expr.Exists<Department>(d => d.Id == u.DeptId));

// 异步版本
var users = await userService.SearchAsync(u => Expr.Exists<Department>(d => d.Id == u.DeptId));
```

EXISTS 仅检查是否存在，不返回关联表数据，性能优于 LEFT JOIN；适合"存在性检查"场景，不适合需要返回关联数据的情况。

### 6.4 分表使用

分表实体需实现 `IArged` 接口，定义完成后，单实体的写操作无需额外处理，`TableArgs` 会自动从实体中取得。批量操作也会自动按分表参数分组分批执行。查询、按条件删除等操作则需显式传入 `tableArgs`。

```csharp
// 定义分表实体（表名模板 Log_{yyyyMM}）
[Table("Log_{0}")]
public class Log : IArged
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Content")]
    public string Content { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    // TableArgs 从实体属性计算，不作为数据库字段
    string[] IArged.TableArgs => [CreateTime.ToString("yyyyMM")];
}

// --- 写操作：无需手动指定表名 ---

// 单条插入：自动路由到 Log_202601
var log = new Log { Content = "登录", CreateTime = new DateTime(2026, 1, 15) };
await logService.InsertAsync(log);

// 单条更新 / 删除：同理自动路由
log.Content = "修改后登录";
await logService.UpdateAsync(log);
await logService.DeleteAsync(log);

// 批量插入：自动按 TableArgs 分组，分别写入 Log_202601 和 Log_202602
var logs = new List<Log>
{
    new Log { Content = "1月日志", CreateTime = new DateTime(2026, 1, 10) },
    new Log { Content = "2月日志", CreateTime = new DateTime(2026, 2, 5) },
};
await logService.BatchInsertAsync(logs);

// --- 查询操作：需显式传入 tableArgs ---

// 查询指定月份的日志
var jan = await logService.SearchAsync(Expr.Prop("Content").Contains("登录"), tableArgs: ["202601"]);

// 按 ID 获取
var one = await logService.GetObjectAsync(42, tableArgs: ["202601"]);

// 数量 / 存在性检查
int count  = await logService.CountAsync(null, tableArgs: ["202601"]);
bool exists = await logService.ExistsIDAsync(42, tableArgs: ["202601"]);

// 按条件删除（需传入 tableArgs）
logService.Delete(Expr.Prop("CreateTime") < new DateTime(2026, 1, 31), "202601");

// 按 ID 删除
logService.DeleteID(42, "202601");
```

`FromExpr` 和 `ForeignExpr` 也支持 `TableArgs`，可在 `Expr` 级别直接指定分表参数，适用于 `DataViewDAO` 聚合查询或手动构建复杂 EXISTS 子查询的场景。

```csharp
// FromExpr + TableArgs：Expr.From<T>() 直接接受 tableArgs 参数
// 适用于通过 DataViewDAO 对分表执行聚合查询
var selectExpr = Expr.From<Log>("202601")       // 指向 Log_202601
    .Where(Expr.Prop("Content").Contains("登录"))
    .GroupBy(Expr.Prop("Content"))
    .Select(Expr.Prop("Content"), Expr.Prop("Id").Count().As("cnt"));

DataTable dt = dataViewDAO.Search(selectExpr).GetResult();

// ForeignExpr + TableArgs：EXISTS 子查询也可指向分表
var existsExpr = Expr.Foreign<Log>(
    Expr.Prop("UserId") == Expr.Prop("User.Id"),   // 关联条件
    "202601");                                      // 指向 Log_202601

var users = userService.Search(existsExpr);

// 带别名的写法
var existsWithAlias = Expr.Foreign<Log>("lg",
    Expr.Prop("UserId") == Expr.Prop("User.Id"),
    "202601");

var users2 = userService.Search(existsWithAlias);
```

### 6.5 事务操作

```csharp
[AutoRegister(Lifetime = ServiceLifetime.Scoped)]
[Intercept(typeof(ServiceInvokeInterceptor))]
public class BusinessService : IBusinessService
{
    private readonly IUserService _userService;
    private readonly ISalesService _salesService;

    public BusinessService(IUserService userService, ISalesService salesService)
    {
        _userService = userService;
        _salesService = salesService;
    }

    [Transaction]  // 声明式事务：抛出异常则自动回滚
    public async Task<bool> CreateOrderAsync(User user, SalesRecord sale)
    {
        user.CreateTime = DateTime.Now;
        await _userService.InsertAsync(user);  // Insert 后 ID 自动回填
        sale.SalesUserId = user.Id;
        await _salesService.InsertAsync(sale);
        return true;
    }
}
```

### 6.6 SQL 调试工具

`SqlGen` 可在不执行查询的情况下将 `Expr` 转换为数据库 SQL，适合调试和日志。

```csharp
var selectExpr = Expr.From<User>()
    .Where(Expr.Prop("Age") > 18)
    .Select(Expr.Prop("Id"), Expr.Prop("UserName"));

var sqlGen = new SqlGen(typeof(User));
var sqlResult = sqlGen.ToSql(selectExpr);
Console.WriteLine(sqlResult);  // 输出 SQL 及参数列表
```

运行期也可通过 `SessionManager.Current.SqlStack` 查看最近执行过的 SQL：

```csharp
foreach (var sql in SessionManager.Current.SqlStack)
{
    Console.WriteLine(sql);
}
```

## 7. 扩展与高级功能

### 7.1 动态注册 Function 和 Handler

对于难以用 Expr 直接表达的 SQL 函数或属性，可通过动态注册 Method/Member Handler 来扩展 Lambda 表达式的解析能力。

```csharp
// 注册指定方法名的处理器（所有类型的同名方法都生效）
LambdaExprConverter.RegisterMethodHandler("JsonValue", (node, converter) =>
{
    // node.Object 是实例，node.Arguments[0] 是路径参数
    return new FunctionExpr("JSON_VALUE",
        converter.Convert(node.Object) as ValueTypeExpr,
        converter.Convert(node.Arguments[0]) as ValueTypeExpr);
});

// 注册指定类型的方法处理器
LambdaExprConverter.RegisterMethodHandler(typeof(string), "PadLeft", (node, converter) =>
{
    return new FunctionExpr("LPAD",
        converter.Convert(node.Object) as ValueTypeExpr,
        converter.Convert(node.Arguments[0]) as ValueTypeExpr,
        converter.Convert(node.Arguments[1]) as ValueTypeExpr);
});

// 注册属性/字段处理器
LambdaExprConverter.RegisterMemberHandler("DayOfYear", (node, converter) =>
    new FunctionExpr("DAYOFYEAR", converter.Convert(node.Expression) as ValueTypeExpr));

// 注册特定类型的成员处理器
LambdaExprConverter.RegisterMemberHandler(typeof(DateTime), "Year", (node, converter) =>
    new FunctionExpr("YEAR", converter.Convert(node.Expression) as ValueTypeExpr));

// 注册后可直接在 Lambda 中使用
var users = userService.Search(u => u.UserName.PadLeft(10, '0') == "0000000admin");
```

### 7.2 GenericSqlExpr

`GenericSqlExpr` 适用于需要直接使用 SQL 片段的场合，可与其他 `Expr` 一起组合使用，采用预注册机制所以可以安全的提供给外部使用。使用分两步：启动时用 `GenericSqlExpr.Register` 注册生成委托，查询时用 `Expr.Sql(key, arg)` 或 `GenericSqlExpr.Get(key, arg)` 获取实例。

**示例：**

```csharp
// === 启动时注册（每个 key 只需注册一次）===

// 单个参数：年份筛选
GenericSqlExpr.Register("YearFilter", (context, builder, @params, arg) =>
{
    string p = @params.Count.ToString();  // 用当前参数总数作为索引
    @params.Add(new KeyValuePair<string, object>(builder.ToParamName(p), (int)arg));
    return $"YEAR(CreateTime) = {builder.ToSqlParam(p)}";
});

// 多个参数：用匿名对象传递
GenericSqlExpr.Register("AgeAndMonthFilter", (context, builder, @params, arg) =>
{
    dynamic d = arg;
    string p1 = @params.Count.ToString();
    @params.Add(new KeyValuePair<string, object>(builder.ToParamName(p1), (int)d.Age));
    string p2 = @params.Count.ToString();
    @params.Add(new KeyValuePair<string, object>(builder.ToParamName(p2), (string)d.Month));
    return $"Age > {builder.ToSqlParam(p1)} AND DATE_FORMAT(CreateTime, '%Y-%m') = {builder.ToSqlParam(p2)}";
});

// === 查询时获取实例 ===

// Expr.Sql(key, arg) 是 GenericSqlExpr.Get(key, arg) 的简化包装
var yearExpr = Expr.Sql("YearFilter", 2024);
var users = userService.Search(yearExpr);

var rangeExpr = Expr.Sql("AgeAndMonthFilter", new { Age = 18, Month = "2024-01" });
var users = await userService.SearchAsync(rangeExpr);

// 也可与其他 Expr 组合使用
var combined = (Expr.Prop("DeptId") == 1) & Expr.Sql("YearFilter", 2024);
var users = userService.Search(combined);
```

### 7.3 自定义 SqlBuilder

通过继承 `SqlBuilder` 并重写相关方法，可为特定数据库定制标识符转义与参数占位符格式，之后注册到 `SqlBuilderFactory` 即可生效。

**示例：**

```csharp
public class CustomSqlBuilder : SqlBuilder
{
    // 自定义标识符转义方式（例如 MySQL 的反引号风格）
    public override string ToSqlName(string name)
        => $"`{name}`";

    // 自定义参数占位符格式
    public override string ToSqlParam(string nativeName)
        => $":{nativeName}";
}

// 注册并关联到指定的 DbConnection 类型
SqlBuilderFactory.Instance.Register(typeof(CustomDbConnection), new CustomSqlBuilder());
```

### 7.4 自定义 IBulkProvider

对于大批量写入，可通过实现 `IBulkProvider` 接口并以 `[AutoRegister(Key = typeof(DbConnectionType))]` 注册来替换默认的逐行插入逻辑（例如使用 `SqlBulkCopy`）。

**示例：**

```csharp
[AutoRegister(Key = typeof(MySqlConnection))]
public class MySqlBulkCopyProvider : IBulkProvider
{  
    public int BulkInsert(DataTable dt, IDbConnection dbConnection, IDbTransaction transaction)
    {
        MySqlBulkCopy bulkCopy = new MySqlBulkCopy(dbConnection as MySqlConnection, transaction as MySqlTransaction);
        bulkCopy.DestinationTableName = dt.TableName;
        bulkCopy.ConflictOption = MySqlBulkLoaderConflictOption.Replace;
        for (int i = 0; i < dt.Columns.Count; i++)
        {
            bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i, dt.Columns[i].ColumnName));
        }
        return bulkCopy.WriteToServer(dt).RowsInserted;
    }
}
```

## 8. 性能

8.1 基于 `LiteOrm.Benchmark` 项目（Windows 11, Intel Core i5-13400F 2.50GHz, .NET 10.0.103）的测试结果：

### 8.2 性能对比概览（BatchCount=100）

| 框架 | 插入性能 (ms) | 更新性能 (ms) | Upsert (ms) | 关联查询 (ms) | 内存分配 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **LiteOrm** | **3,743.9** | **4,684.3** | 5,535.7 | 974.9 | **295.97 KB** |
| FreeSql | 4,358.7 | 4,859.8 | **4,843.1** | 942.3 | 460.62 KB |
| SqlSugar | 4,126.6 | 5,377.7 | 9,355.1 | 1,664.3 | 476.13 KB |
| Dapper | 13,236.3 | 16,492.4 | 18,593.3 | **893.4** | 254.58 KB |
| EF Core | 21,973.8 | 21,571.2 | 22,967.5 | 6,680.8 | 1,965.32 KB |

### 8.3 性能对比概览（BatchCount=1000）

| 框架 | 插入性能 (ms) | 更新性能 (ms) | Upsert (ms) | 关联查询 (ms) | 内存分配 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **LiteOrm** | **10,711.9** | **16,472.2** | 16,733.4 | **6,061.1** | **870.27 KB** |
| FreeSql | 17,707.5 | 30,842.5 | **14,769.0** | 6,520.9 | 4,629.99 KB |
| SqlSugar | 15,775.0 | 35,522.5 | 66,357.1 | 12,304.3 | 4,571.36 KB |
| Dapper | 120,213.5 | 132,356.8 | 136,051.1 | 6,556.1 | 2,476.22 KB |
| EF Core | 169,846.8 | 149,932.5 | 157,037.7 | 12,422.7 | 18,118.07 KB |

### 8.4 性能对比概览（BatchCount=5000）

| 框架 | 插入性能 (ms) | 更新性能 (ms) | Upsert (ms) | 关联查询 (ms) | 内存分配 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **LiteOrm** | **40,268.4** | **68,069.3** | 60,711.4 | **39,060.2** | **4,082.59 KB** |
| FreeSql | 72,488.8 | 133,942.8 | **58,183.2** | 41,220.4 | 23,333.54 KB |
| SqlSugar | 76,643.9 | 194,130.4 | 885,872.8 | 63,744.0 | 23,196.37 KB |
| Dapper | 690,745.5 | 659,912.8 | 677,140.4 | 39,942.4 | 12,349.48 KB |
| EF Core | 824,700.5 | 749,069.8 | 794,845.9 | 49,403.4 | 80,230.09 KB |

> *注：完整测试报告请参考：[LiteOrm 性能评测报告](./LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md).*
---

**文档版本：** 1.1  
**最后更新：** 2026年  
**适用版本：** LiteOrm >=8.0.7