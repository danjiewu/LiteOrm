# 第一个完整示例（仅基础库）

本文通过一个最小可运行示例展示**仅使用 `LiteOrm` 基础库**（不引入 `LiteOrm.DependencyInjection`、Autofac、Castle 动态代理）的典型使用流程：手动初始化、定义实体、插入数据、查询数据和分页查询。

> **适用场景**：控制台应用、批处理脚本、不依赖 DI 容器的项目，或希望对生命周期完全自管理的场景。
>
> 如果你使用 ASP.NET Core 且需要 Autofac 集成、AOP 事务/权限/日志等能力，请参考 [第一个完整示例（DI 版）](./05-first-example-di.md)。

## 0. 项目准备

```bash
dotnet new console -n LiteOrmCoreDemo
cd LiteOrmCoreDemo
dotnet add package LiteOrm
dotnet add package Microsoft.Data.Sqlite
```

> 基础库 `LiteOrm` 会自动携带 `LiteOrm.Common`，无需单独安装。此处以 SQLite 为例，无需额外安装数据库服务。

## 1. 定义实体

```csharp
using LiteOrm.Common;

[Table("Users")]
public class User
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserName")]
    public string? UserName { get; set; }

    [Column("Age")]
    public int Age { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }
}
```

> - `[Table("Users")]`：映射到数据库 `Users` 表。
> - `[Column("Id", IsPrimaryKey = true, IsIdentity = true)]`：主键且自增。
> - 实体类不要求继承 `ObjectBase`，普通 POCO 即可。

## 2. 初始化 LiteOrm

基础库提供两种初始化方式：无需 DI 容器的手动构造（见下），以及纯 MS DI 注册 `AddLiteOrm()`。先看手动构造——数据源配置支持两种方式：代码内手动添加，或从 `appsettings.json` 等 `IConfiguration` 来源读取。

> **注意**：8.1 起，`RegisterLiteOrm()` 已从 `LiteOrm` 基础库移至 `LiteOrm.DependencyInjection` 包（命名空间由 `LiteOrm` 改为 `LiteOrm.DependencyInjection`）。如需 Autofac 集成 / AOP，请使用 `LiteOrm.DependencyInjection` 包中的 `RegisterLiteOrm()`；基础库仅提供 `AddLiteOrm()`（纯 MS DI）。

### 2.1 手动初始化 LiteOrm

#### 方式一：代码内手动配置

```csharp
using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Data.Sqlite;

// 1. 配置数据源
var dataSourceProvider = new DataSourceProvider();
dataSourceProvider.AddDataSource(new DataSourceConfig
{
    Name = "DefaultConnection",
    ConnectionString = "Data Source=LiteOrmDemo.db",
    Provider = typeof(SqliteConnection).AssemblyQualifiedName,
    SyncTable = true  // 自动建表（开发阶段推荐）
});
dataSourceProvider.SetDefaultDataSource("DefaultConnection");

// 2. 创建连接池工厂
var poolFactory = new DAOContextPoolFactory(dataSourceProvider);

// 3. 创建会话管理器并设为当前会话
var sessionManager = new SessionManager(poolFactory);
SessionManager.SetCurrent(() => sessionManager);

// 4. 创建 DAO 和服务
var objectDAO = new ObjectDAO<User>();
var objectViewDAO = new ObjectViewDAO<User>();
var userService = new EntityService<User>(objectDAO, objectViewDAO);
```

#### 方式二：从配置文件读取

基础库内置 `LoadConfiguration` 扩展方法，可直接从 `IConfiguration` 的 `LiteOrm` 节点加载数据源配置，无需逐个调用 `AddDataSource`。

先准备 `appsettings.json`：

```json
{
  "LiteOrm": {
    "Default": "DefaultConnection",
    "DataSources": [
      {
        "Name": "DefaultConnection",
        "ConnectionString": "Data Source=LiteOrmDemo.db",
        "Provider": "Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite",
        "SyncTable": true
      }
    ]
  }
}
```

然后用 `LoadConfiguration` 替代手动 `AddDataSource`：

```csharp
using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Extensions.Configuration;

// 1. 读取配置文件
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json")
    .Build();

// 2. 通过 LoadConfiguration 从 LiteOrm 节点加载数据源
var dataSourceProvider = new DataSourceProvider();
dataSourceProvider.LoadConfiguration(configuration.GetSection("LiteOrm"));

// 3. 创建连接池工厂
var poolFactory = new DAOContextPoolFactory(dataSourceProvider);

// 4. 创建会话管理器并设为当前会话
var sessionManager = new SessionManager(poolFactory);
SessionManager.SetCurrent(() => sessionManager);

// 5. 创建 DAO 和服务
var objectDAO = new ObjectDAO<User>();
var objectViewDAO = new ObjectViewDAO<User>();
var userService = new EntityService<User>(objectDAO, objectViewDAO);
```

> 使用 `LoadConfiguration` 需额外安装 `Microsoft.Extensions.Configuration` 和 `Microsoft.Extensions.Configuration.Json` 包。基础库本身仅依赖 `Microsoft.Extensions.Configuration.Abstractions`（提供 `IConfiguration` 接口）。

> **逐行解释**：
> - `DataSourceProvider`：管理数据源配置。通过 `AddDataSource` 显式添加，或通过 `LoadConfiguration` 从 `IConfiguration` 批量加载；`SetDefaultDataSource` 或配置节中的 `Default` 键指定默认数据源。
> - `LiteOrmSqlFunctionInitializer.Initialize()`：SQL 函数映射在首次访问 SqlBuilder 时由静态构造函数自动注册，无需手动调用。
> - `DAOContextPoolFactory`：根据数据源配置创建连接池，管理连接的获取与回收。通过构造函数传入 `SessionManager`，DAO 内部通过 `SessionManager.GetDAOContextPool()` 获取连接池以解析提供程序类型。
> - `SessionManager`：管理数据库会话、事务和异步上下文。通过 `SetCurrent` 设置为当前异步上下文的会话。
> - `ObjectDAO<T>` / `ObjectViewDAO<T>`：分别负责增删改和查询的数据访问对象。两者均有无参构造函数，内部通过 `TableInfoProvider.Instance` 获取全局单例，无需手动传入。
> - `EntityService<T>`：封装了 DAO 的业务服务，提供 `InsertAsync`、`SearchAsync`、`UpdateAsync`、`DeleteAsync` 等方法。

### 2.2 通过 AddLiteOrm 注册和获取服务（推荐）

上一节展示了纯手动构造依赖链的方式。如果你希望使用 `Microsoft.Extensions.DependencyInjection`（以下简称 MS DI）来管理服务生命周期，但**不引入 LiteOrm.DependencyInjection / Autofac**，可以直接调用基础库内置的 `AddLiteOrm()` 完成注册。

`AddLiteOrm()` 定义于基础库（`LiteOrm` 命名空间，无需额外安装包），适合需要依赖注入但不需要 AOP 拦截的场景，例如单元测试、轻量级 Web API、或希望按作用域（Scope）管理 `SessionManager` 生命周期的项目。

```csharp
using LiteOrm;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

// 1. 创建 Host（自动加载 appsettings.json），并在其上注册 LiteOrm
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLiteOrm(options =>
{
    options.AutoRegisterServices = true;   // 默认 true：应用 [AutoRegister] 编译期注册
    options.ConfigureServices = s => { /* 追加自定义服务注册 */ };
});

// 2. 构建 Host，其 Services 即为 ServiceProvider
var host = builder.Build();
var serviceProvider = host.Services;

// 3. 将 SessionManager 的解析委托给 ServiceProvider
//    SessionManager.SetCurrent 接受一个工厂委托，
//    在首次访问 SessionManager.Current 时延迟执行并缓存结果
SessionManager.SetCurrent(() => serviceProvider.GetService<SessionManager>());
```

> **`AddLiteOrm()` 注册了哪些服务？**
> - 单例：`IDataSourceProvider`（从 `IConfiguration` 的 `LiteOrm` 节点加载）、`DAOContextPoolFactory`、`TableInfoProvider`。
> - Scoped：`SessionManager`、泛型 `ObjectDAO<>` / `ObjectViewDAO<>`、`EntityService<>` / `EntityViewService<>`（含 `IObjectDAO<>`、`IEntityService<>` 等接口注册）。
> - 若 `AutoRegisterServices = true`（默认），还会应用 `[AutoRegister]` 自定义服务与 DAO 的编译期自动注册。

> **需要安装哪些包？** `AddLiteOrm()` 从 DI 容器中解析 `IConfiguration`。`Host.CreateApplicationBuilder` 会自动加载 `appsettings.json` 并注册配置（含 `IConfiguration`），因此控制台应用只需额外安装 `Microsoft.Extensions.Hosting` 包，无需再手动构建 `ServiceCollection`。

注册完成后，通过创建作用域并解析服务来执行数据库操作：

```csharp
// 创建作用域（每个作用域拥有独立的 SessionManager 实例）
using var scope = serviceProvider.CreateScope();
var sp = scope.ServiceProvider;

// 从 DI 容器解析服务
var userService = sp.GetRequiredService<EntityService<User>>();

// 后续操作与手动构造方式完全一致
var user = new User
{
    UserName = "admin",
    Age = 30,
    CreateTime = DateTime.Now
};
await userService.InsertAsync(user);
Console.WriteLine($"插入成功，自增 Id = {user.Id}");
```

> **作用域与 SessionManager 的关系**：
> `SessionManager` 注册为 Scoped，每个 `CreateScope()`（如每个 Web 请求）创建的作用域会获得独立的 `SessionManager` 实例。但 `SessionManager.SetCurrent` 设置的是**当前异步上下文**（`AsyncLocal`）的会话工厂，它只会在首次访问时执行一次委托并缓存。
>
> 在多作用域场景下（如 Web 请求），每个作用域需要使用各自的 `SessionManager`。建议使用中间件（Filter）在每个请求进入时，将当前请求作用域的 `SessionManager` 设为当前会话：
>
> ```csharp
> // 在每个请求作用域内，将 SessionManager 绑定到当前异步上下文
> app.Use(async (context, next) =>
> {
>     var sp = context.RequestServices;   // 当前请求作用域的 ServiceProvider
>     SessionManager.SetCurrent(() => sp.GetService<SessionManager>());
>     await next();
> });
> ```
>
> 提示：使用 `LiteOrm.DependencyInjection`（Autofac）时无需手写中间件——`RegisterLiteOrm()` 内置作用域跟踪（`RegisterScope`，默认开启），会在每个作用域进入/退出时自动更新当前会话。

应用退出时释放资源：

```csharp
// 释放 Host（自动 Dispose 单例和未释放的 Scoped 服务，包括连接池）
await host.DisposeAsync();
```

## 3. 插入一条数据

```csharp
var user = new User
{
    UserName = "admin",
    Age = 30,
    CreateTime = DateTime.Now
};

await userService.InsertAsync(user);
Console.WriteLine($"插入成功，自增 Id = {user.Id}");
```

> `InsertAsync` 会将实体插入数据库。如果 `Id` 是自增列（`IsIdentity = true`），插入后实体的 `Id` 属性会自动填充。

## 4. 执行查询

```csharp
// 条件查询
var adults = await userService.SearchAsync(u => u.Age >= 18);
Console.WriteLine($"成年用户数量：{adults.Count}");

// 单条查询
var admin = await userService.SearchOneAsync(u => u.UserName == "admin");
Console.WriteLine($"查询到：{admin?.UserName}, Age = {admin?.Age}");
```

## 5. 执行分页

```csharp
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderByDescending(u => u.CreateTime)
          .Skip(0)
          .Take(10)
);
Console.WriteLine($"分页结果：{page.Count} 条");
```

## 6. 完整调用闭环

```csharp
// 1. 插入
var user = new User
{
    UserName = "demo-user",
    Age = 26,
    CreateTime = DateTime.Now
};
await userService.InsertAsync(user);

// 2. 查询
var current = await userService.SearchOneAsync(u => u.Id == user.Id);

// 3. 更新
current!.UserName = "updated-demo-user";
await userService.UpdateAsync(current);

// 4. 统计
var count = await userService.CountAsync(u => u.Age >= 18);

// 5. 判断是否存在
var exists = await userService.ExistsAsync(u => u.UserName == "updated-demo-user");

// 6. 删除
if (exists)
{
    await userService.DeleteAsync(current);
}

Console.WriteLine($"Count={count}, Exists={exists}");
```

## 7. 手动事务

基础库不提供 AOP 声明式事务（`[Transaction]` 特性需要 `LiteOrm.DependencyInjection` 的 Castle 拦截器），但可以通过 `SessionManager` 手动控制：

```csharp
sessionManager.BeginTransaction();
try
{
    await userService.InsertAsync(new User { UserName = "user1", Age = 20, CreateTime = DateTime.Now });
    await userService.InsertAsync(new User { UserName = "user2", Age = 25, CreateTime = DateTime.Now });
    sessionManager.Commit();
}
catch
{
    sessionManager.Rollback();
    throw;
}
```

## 8. 资源释放

基础库场景下，`SessionManager` 和 `DAOContextPoolFactory` 持有数据库连接，使用完毕后需要释放：

```csharp
// 应用退出时
sessionManager.Dispose();
poolFactory.Dispose();

// 如果使用了 SyncTable=true，数据库文件会自动创建
// 如果是 SQLite in-memory（Data Source=:memory:），连接关闭后数据丢失
```

## 9. 基础库与 LiteOrm.DependencyInjection 的能力对比

| 能力 | 仅基础库 (`LiteOrm`) | 宿主集成 (`LiteOrm.DependencyInjection`) |
|------|----------------------|--------------------------------|
| 实体映射 / CRUD / 查询 | ✅ | ✅ |
| 手动事务 | ✅ `SessionManager.BeginTransaction()` | ✅ |
| 声明式事务 `[Transaction]` | ❌ | ✅ AOP 拦截 |
| 权限过滤 `[ServicePermission]` | ❌ | ✅ AOP 拦截 |
| 自动日志 `[ServiceLog]` / `[Log]` | ❌ | ✅ AOP 拦截 |
| DI 容器注册 | ✅ `AddLiteOrm()`（MS DI，见上文） | ✅ `RegisterLiteOrm()`（Autofac） |
| 配置文件绑定 | ✅ `LoadConfiguration` 或 `AddLiteOrm()` 读取 `IConfiguration` | ✅ `appsettings.json` 自动绑定 |
| 批量导入 `IBulkProvider` | ✅ 直接设置 `SqlBuilder.BulkProvider` | ✅ 直接设置 `SqlBuilder.BulkProvider` |

> 如果你后续需要 AOP 能力，可以从基础库平滑迁移到宿主集成（`LiteOrm.DependencyInjection`），实体定义和 DAO/Service 用法完全一致。

## 10. 新手常见问题

### 问题一：`SQLite Error 1: 'no such table: Users'`

**原因**：数据库中没有 `Users` 表。

**解决方法**：在数据源配置中设置 `SyncTable = true`，让 LiteOrm 自动根据实体定义创建表（开发环境推荐）。或手动执行建表 SQL。

### 问题二：`Object reference not set to instance` 或 `SessionManager.Current` 为 null

**原因**：忘记调用 `SessionManager.SetCurrent(() => sessionManager)`。

**解决方法**：确保在创建服务实例之前调用 `SessionManager.SetCurrent(() => sessionManager)`，否则 DAO 在执行 SQL 时无法获取数据库连接。

### 问题三：`Function 'XXX' is not supported` 异常

**原因**：SQL 函数映射现已自动注册，正常情况下不应出现此异常。如果遇到此异常，说明该函数不在内置映射中。

**解决方法**：通过 `sqlBuilder.RegisterFunctionSqlHandler(...)` 手动注册该函数的 SQL 处理器。

## 运行验证清单

- [ ] `dotnet build` 编译通过，无错误。
- [ ] 初始化代码中调用了 `SessionManager.SetCurrent(...)`（手动构造或 ServiceProvider 方式）。
- [ ] 使用 ServiceProvider 方式时，`SessionManager` 注册为 Scoped，且在进入作用域时调用了 `SetCurrent`。
- [ ] 实体类使用了 `[Table]` 和 `[Column]` 特性标注。
- [ ] 插入和查询操作返回了预期的结果。
- [ ] 应用退出前释放了 `ServiceProvider`（或 `SessionManager`）和 `DAOContextPoolFactory`。

## 相关链接

- [返回目录](../README.md)
- [安装](./02-installation.md)
- [配置参考](../05-reference/01-configuration-reference.md)
- [第一个完整示例（DI 版）](./05-first-example-di.md)
- [实体映射与数据源](../02-core-usage/01-entity-mapping.md)
- [查询总览](../02-core-usage/04-query-overview.md)
- [CRUD 指南](../02-core-usage/03-crud-guide.md)
