# 第一个完整示例（仅基础库）

本文通过一个最小可运行示例展示**仅使用** **`LiteOrm`** **基础库**（不引入 `LiteOrm.DependencyInjection`、Autofac、Castle 动态代理）的典型使用流程：使用 `AddLiteOrm()` 初始化、定义实体，并通过泛型实体服务 `EntityService<User>` 完成插入、查询、分页等操作。

> **适用场景**：控制台应用、批处理脚本、单元测试，或希望使用 MS DI 管理生命周期但不需要 AOP 拦截的项目。
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
>
> - `[Column("Id", IsPrimaryKey = true, IsIdentity = true)]`：主键且自增。
>
> - 实体类不要求继承 `ObjectBase`，普通 POCO 即可。

## 2. 初始化 LiteOrm

推荐直接使用基础库内置的 `AddLiteOrm()`（纯 MS DI）完成初始化——它会从 `IConfiguration` 的 `LiteOrm` 节点自动加载数据源、注册核心服务，并支持注册自定义服务，无需手动 new 各种底层对象。

> **注意**：8.1 起，`RegisterLiteOrm()` 已从 `LiteOrm` 基础库移至 `LiteOrm.DependencyInjection` 包（命名空间由 `LiteOrm` 改为 `LiteOrm.DependencyInjection`）。如需 Autofac 集成 / AOP，请使用 `LiteOrm.DependencyInjection` 包中的 `RegisterLiteOrm()`；基础库仅提供 `AddLiteOrm()`（纯 MS DI）。

### 2.1 通过 AddLiteOrm 初始化（推荐）

`AddLiteOrm()` 定义于基础库（`LiteOrm` 命名空间，无需额外安装包），从 `IConfiguration` 的 `LiteOrm` 节点自动加载数据源，并注册 `SessionManager`、DAO、泛型实体服务等核心服务。使用它时无需手动调用 `SessionManager.SetCurrent(...)`——框架在注册 `SessionManager` 时会自动绑定当前作用域实例。

#### 第一步：准备 appsettings.json 配置数据源

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

#### 第二步：调用 AddLiteOrm 注册

```csharp
using LiteOrm;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

// 1. 创建 Host（自动加载 appsettings.json），并在其上注册 LiteOrm
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLiteOrm();

// 2. 构建 Host，其 Services 即为 ServiceProvider
var host = builder.Build();
var serviceProvider = host.Services;
```

> **`AddLiteOrm()`** **注册了哪些服务？**
>
> - 单例：`IDataSourceProvider`（从 `IConfiguration` 的 `LiteOrm` 节点加载）、`DAOContextPoolFactory`、`TableInfoProvider`。
>
> - Scoped：`SessionManager`、泛型 `ObjectDAO<>` / `ObjectViewDAO<>`、`EntityService<>` / `EntityViewService<>`（含 `IObjectDAO<>`、`IEntityService<>` 等接口注册）。
>
> - `AddLiteOrm()` 会在注册 `SessionManager` 时自动绑定 `SessionManager.Current`, 无需手动调用 `SessionManager.SetCurrent(...)`。
>
> - 若 `AutoRegisterServices = true`（默认），还会自动注册带 `[AutoRegister]` 特性的自定义服务与 DAO（非 AOT 模式运行时扫描程序集；AOT 模式由源生成器在编译期登记）。基础示例直接使用泛型实体服务，无需自定义服务。

> **需要安装哪些包？** `AddLiteOrm()` 从 DI 容器中解析 `IConfiguration`。`Host.CreateApplicationBuilder` 会自动加载 `appsettings.json` 并注册配置（含 `IConfiguration`），因此控制台应用只需额外安装 `Microsoft.Extensions.Hosting` 包，无需再手动构建 `ServiceCollection`。

#### 第三步：创建作用域并解析服务

```csharp
// 创建作用域（每个作用域拥有独立的 SessionManager 与 Scoped 服务实例）
using var scope = serviceProvider.CreateScope();
var sp = scope.ServiceProvider;

// 解析泛型实体服务（AddLiteOrm 已注册 EntityService<>）
var userService = sp.GetRequiredService<EntityService<User>>();
```

> **作用域与 SessionManager 的关系**：
> `SessionManager` 注册为 Scoped，每个 `CreateScope()`（如每个 Web 请求）创建的作用域会获得独立的 `SessionManager` 实例。`AddLiteOrm()` 在注册 `SessionManager` 时已自动绑定 `SessionManager.Current`（解析到当前作用域实例），因此无需手动调用 `SetCurrent` 或编写中间件。
>
> 提示：使用 `LiteOrm.DependencyInjection`（Autofac）时同样无需配置——`RegisterLiteOrm()` 自动启用作用域跟踪，会在每个作用域进入/退出时自动更新当前会话。

应用退出时释放资源：

```csharp
// 释放 Host（自动 Dispose 单例和未释放的 Scoped 服务，包括连接池）
await host.DisposeAsync();
```

### 2.2 注册自定义服务的两种方式（可选）

基础示例直接使用泛型 `EntityService<>`，无需自定义服务。如果你的项目需要把业务方法封装成服务，可通过以下两种方式将自定义服务注册到容器：

#### 方式一：`[AutoRegister]` 自动注册（推荐）

给自定义服务实现类加上 `[AutoRegister]` 特性，`AddLiteOrm()` 在 `AutoRegisterServices = true`（默认）时会自动扫描并注册。特性支持指定生命周期、注册策略与键：

```csharp
[AutoRegister(Lifetime.Scoped, RegisterPolicy.All)]         // 注册实现类自身 + 接口（默认）
public class UserAppService : IUserAppService { /* ... */ }

[AutoRegister(Lifetime.Transient, RegisterPolicy.Interface)] // 仅注册接口
public class ReportService : IReportService { /* ... */ }

[AutoRegister(Lifetime.Singleton, RegisterPolicy.Self)]      // 仅注册实现类自身
public class CacheHelper { /* ... */ }
```

> `Lifetime`：`Singleton` / `Scoped` / `Transient`；`RegisterPolicy`：`All`（自身 + 接口，默认）/ `Interface`（仅接口）/ `Self`（仅自身）。依赖 Scoped 服务的自定义服务应使用 `Scoped` 或 `Transient` 生命周期。

#### 方式二：`options.ConfigureServices` 手动注册

`LiteOrmOptions.ConfigureServices` 在核心服务注册完成后执行，可在此注册任意自定义服务，与普通 MS DI 注册一致：

```csharp
builder.Services.AddLiteOrm(options =>
{
    options.ConfigureServices = services =>
    {
        services.AddScoped<IUserAppService, UserAppService>();
        services.AddSingleton<ICacheService, CacheService>();
    };
});
```

> 两种方式可混用。`ConfigureServices` 中的注册在 `[AutoRegister]` 自动注册之后执行，同一类型的注册以后者为准。

## 3. 完整调用闭环（插入、查询、分页）

下面用一个闭环示例依次演示插入、条件查询、单条查询、分页、更新、统计、存在性判断与删除：

```csharp
// 1. 插入
var user = new User
{
    UserName = "demo-user",
    Age = 26,
    CreateTime = DateTime.Now
};
await userService.InsertAsync(user);
Console.WriteLine($"插入成功，自增 Id = {user.Id}");

// 2. 条件查询
var adults = await userService.SearchAsync(u => u.Age >= 18);
Console.WriteLine($"成年用户数量：{adults.Count}");

// 3. 单条查询
var current = await userService.SearchOneAsync(u => u.Id == user.Id);
Console.WriteLine($"查询到：{current?.UserName}, Age = {current?.Age}");

// 4. 分页查询
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderByDescending(u => u.CreateTime)
          .Skip(0)
          .Take(10)
);
Console.WriteLine($"分页结果：{page.Count} 条");

// 5. 更新
current!.UserName = "updated-demo-user";
await userService.UpdateAsync(current);

// 6. 统计
var count = await userService.CountAsync(u => u.Age >= 18);

// 7. 判断是否存在
var exists = await userService.ExistsAsync(u => u.UserName == "updated-demo-user");

// 8. 删除
if (exists)
{
    await userService.DeleteAsync(current);
}

Console.WriteLine($"Count={count}, Exists={exists}");
```

> - `InsertAsync`：将实体插入数据库。如果 `Id` 是自增列（`IsIdentity = true`），插入后实体的 `Id` 属性会自动填充。
>
> - `SearchAsync`：返回集合；`SearchOneAsync`：返回单条，无匹配时返回 `null`。
>
> - `SearchAsync` 的查询构建器支持 `Where` / `OrderByDescending` / `Skip` / `Take` 组合实现分页排序。

## 4. 基础库与 LiteOrm.DependencyInjection 的能力对比

| 能力                            | 仅基础库 (`LiteOrm`)                                           | 宿主集成 (`LiteOrm.DependencyInjection`) |
| ----------------------------- | ---------------------------------------------------------- | ------------------------------------ |
| 实体映射 / CRUD / 查询              | ✅                                                          | ✅                                    |
| 手动事务                          | ✅ `SessionManager.BeginTransaction()`                      | ✅                                    |
| 声明式事务 `[Transaction]`         | ❌                                                          | ✅ AOP 拦截                             |
| 权限过滤 `[ServicePermission]`    | ❌                                                          | ✅ AOP 拦截                             |
| 自动日志 `[ServiceLog]` / `[Log]` | ❌                                                          | ✅ AOP 拦截                             |
| DI 容器注册                       | ✅ `AddLiteOrm()`（MS DI，见上文）                                | ✅ `RegisterLiteOrm()`（Autofac）       |
| 配置文件绑定                        | ✅ `LoadConfiguration` 或 `AddLiteOrm()` 读取 `IConfiguration` | ✅ `appsettings.json` 自动绑定            |
| 批量导入 `IBulkProvider`          | ✅ 直接设置 `SqlBuilder.BulkProvider`                           | ✅ 直接设置 `SqlBuilder.BulkProvider`     |

> 如果你后续需要 AOP 能力，可以从基础库平滑迁移到宿主集成（`LiteOrm.DependencyInjection`），实体定义和 DAO/Service 用法完全一致。

## 5. 新手常见问题

### 问题一：`SQLite Error 1: 'no such table: Users'`

**原因**：数据库中没有 `Users` 表。

**解决方法**：在数据源配置中设置 `SyncTable = true`，让 LiteOrm 自动根据实体定义创建表（开发环境推荐）。或手动执行建表 SQL。

### 问题二：`Object reference not set to instance` 或 `SessionManager.Current` 为 null

**原因**：手动构造方式下忘记调用 `SessionManager.SetCurrent(() => sessionManager)`（使用 `AddLiteOrm()` 时会自动绑定，不会出现此问题）。

**解决方法**：手动构造场景下，确保在创建服务实例之前调用 `SessionManager.SetCurrent(() => sessionManager)`；使用 `AddLiteOrm()` 时无需手动调用，框架会自动绑定。否则 DAO 在执行 SQL 时无法获取数据库连接。

### 问题三：`Function 'XXX' is not supported` 异常

**原因**：SQL 函数映射现已自动注册，正常情况下不应出现此异常。如果遇到此异常，说明该函数不在内置映射中。

**解决方法**：通过 `sqlBuilder.RegisterFunctionSqlHandler(...)` 手动注册该函数的 SQL 处理器。

## 运行验证清单

- [ ] `dotnet build` 编译通过，无错误。

- [ ] 手动构造方式下已调用 `SessionManager.SetCurrent(...)`；使用 `AddLiteOrm()` 时自动绑定，无需手动调用。

- [ ] 实体类使用了 `[Table]` 和 `[Column]` 特性标注。

- [ ] 插入、查询和分页操作返回了预期的结果。

- [ ] 应用退出前调用了 `await host.DisposeAsync()` 释放资源（连接池等）。

## 相关链接

- [返回目录](../README.md)

- [安装](./02-installation.md)

- [配置参考](../05-reference/01-configuration-reference.md)

- [第一个完整示例（DI 版）](./05-first-example-di.md)

- [实体映射与数据源](../02-core-usage/01-entity-mapping.md)

- [查询总览](../02-core-usage/04-query-overview.md)

- [CRUD 指南](../02-core-usage/03-crud-guide.md)
