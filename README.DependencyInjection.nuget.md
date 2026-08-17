# LiteOrm.DependencyInjection

[![License](https://img.shields.io/github/license/danjiewu/LiteOrm.svg)](https://github.com/danjiewu/LiteOrm/blob/master/LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-LiteOrm-brightgreen)](https://github.com/danjiewu/LiteOrm)

---

## 📖 English Version

LiteOrm.DependencyInjection is the **DI integration layer** of LiteOrm. It switches the host container to **Autofac**, scans `[AutoRegister]` types, applies **Castle DynamicProxy** AOP interception, and explicitly registers the core services (data source provider, SQL builders, DAO context pool, session manager, entity services and DAOs) — so `RegisterLiteOrm()` brings up the whole LiteOrm stack automatically.

### When to Use

- You want to register LiteOrm via `RegisterLiteOrm()` and resolve services from the DI container.
- You need AOP features (`[Transaction]`, `[ServicePermission]`, `[ServiceLog]`, `[Log]`) on your service methods.
- You want automatic `[AutoRegister]` scanning with Autofac lifetimes and interceptors.
- You need automatic database schema synchronization at startup (`SyncTable`).

### Requirements

- **.NET 8.0+** or **.NET Standard 2.0** (.NET Framework 4.6.1+ compatible)
- **Dependencies**: `Autofac`, `Castle.Core`, `Castle.Core.AsyncInterceptor`, `LiteOrm`, `LiteOrm.Common`

### Installation

```bash
dotnet add package LiteOrm.DependencyInjection
```

`LiteOrm.DependencyInjection` transitively references `LiteOrm` and `LiteOrm.Common`, so you do not need to reference them separately.

### Quick Start

Configure your data source in `appsettings.json`:

```json
{
  "LiteOrm": {
    "Default": "main",
    "DataSources": [
      {
        "Name": "main",
        "ConnectionString": "Server=localhost;Database=MyDb;User Id=root;Password=123456;",
        "Provider": "MySqlConnector.MySqlConnection, MySqlConnector"
      }
    ]
  }
}
```

Register LiteOrm in `Program.cs` (must be called on `builder.Host`, because it replaces the underlying DI container with Autofac):

```csharp
using LiteOrm.DependencyInjection;

// Console app
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()
    .Build();

// ASP.NET Core app
var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();
```

With options:

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.Assemblies = new[] { typeof(MyService).Assembly };
    options.RegisterSqlBuilder("main", new MySqlBuilder());
});
```

That is it — `[AutoRegister]` services, entity services (`IEntityService<T>`), view services (`IEntityViewService<T>`) and DAOs (`IObjectDAO<T>`, `IObjectViewDAO<T>`, `IDataViewDAO<T>`) are resolved from the container automatically.

### Highlights

- **Autofac container**: `RegisterLiteOrm()` switches the host to Autofac via `AutofacServiceProviderFactory`; existing `services.AddXxx()` registrations keep working through the MS DI bridge.
- **Deterministic core registration**: `RegisterCoreServices()` explicitly registers `DataSourceProvider`, `SqlBuilderFactory`, `DAOContextPoolFactory`, `SessionManager`, generic entity services and DAOs — no `[AutoRegister]` scanning for core types.
- **Auto-registration**: `RegisterAutoService()` scans assemblies and registers `[AutoRegister]` types with configurable lifetimes, keys, auto-activation, `[InterceptAttribute]` AOP wiring, and automatic `ServiceInvokeInterceptor` for types (or interfaces) carrying `[Service]`.
- **Scope tracking**: scope tracking is always enabled; `RegisterScope()` keeps `SessionManager.Current` pointing at the correct lifetime scope across async contexts.
- **Schema sync**: `LiteOrmCoreInitializer` (a `IHostedService`) auto-creates tables/columns/indexes for entities on startup.
- **Service generation proxies**: `AddServiceGenerator<T>()` creates Castle DynamicProxy interface proxies that resolve their return values from the DI container.

### ❌AOT Support

- `LiteOrm.DependencyInjection` depends on the Autofac container and dynamic proxies, which are not compatible with AOT.

---

## 🔄 Migrating to 8.1.1 (from 8.0.20 or earlier)

Versions **8.1.0** and **8.1.1** introduce several breaking changes. If you are upgrading from **8.0.20 or earlier**, follow these steps:

### 1. Add the new `LiteOrm.DependencyInjection` package

`RegisterLiteOrm()` moved out of the `LiteOrm` core package into the new `LiteOrm.DependencyInjection` package, and the namespace changed from `LiteOrm` to `LiteOrm.DependencyInjection`:

```xml
<PackageReference Include="LiteOrm.DependencyInjection" Version="8.1.1" />
```

```csharp
// Old (8.0.20 or earlier)
using LiteOrm;

// New (8.1.1)
using LiteOrm.DependencyInjection;
```

The `RegisterLiteOrm()` method signature is unchanged, so your call sites do not need modification.

### 2. Update `BulkProvider` usage (if you have custom providers)

`BulkProviderFactory`, `BulkProviderAttribute` and the `[AutoRegister(Key = ...)]` convention were removed. Assign your `IBulkProvider` directly to the `SqlBuilder.BulkProvider` property. `GetSqlBuilder(typeof(MySqlConnection))` returns `MySqlBuilder.Instance`, so set it directly:

```csharp
// Old: look up by connection type via the factory (removed)
var provider = services.GetRequiredService<BulkProviderFactory>().GetProvider(dbConnection.GetType());

// New: set it directly on the SqlBuilder
MySqlBuilder.Instance.BulkProvider = new MySqlBulkCopyProvider();
```

### Upgrading to v8.1.1 (from v8.1.0 or lower)

If you are already on **v8.1.0** and upgrading to **v8.1.1**, the changes are:

- **DAO constructors now require a `SessionManager`.** `DAOBase`, `ObjectDAO<T>`, `ObjectViewDAO<T>`, `DataDAO<T>` and `DataViewDAO<T>` take a `SessionManager` parameter and no longer depend on the static `SessionManager.Current`. Under DI (`RegisterLiteOrm()` / `AddLiteOrm()`) the container resolves it automatically — no code change. When constructing DAOs manually, pass the session:

  ```csharp
  // Old (v8.1.0 and lower)
  var dao = new ObjectDAO<User>();

  // New (v8.1.1)
  var dao = new ObjectDAO<User>(sessionManager);
  ```

  Custom DAOs deriving from a DAO base class must forward it in their constructor: `public MyDAO(SessionManager sessionManager) : base(sessionManager) { }`.

- **`LiteOrmOptions.RegisterScope` has been removed.** Scope tracking is always enabled automatically — remove any `options.RegisterScope = ...` assignment.

- **`AddLiteOrm()` binds `SessionManager.Current` automatically** per scope, so no middleware or manual `SessionManager.SetCurrent(...)` is needed.

### 3. FAQ

**Q1: `IEntityService<T>` cannot be resolved from DI after upgrading?**

Make sure the host uses `RegisterLiteOrm()` (from `LiteOrm.DependencyInjection`). Core types (`EntityService<T>`, `ObjectDAO<T>`, ...) are no longer registered via `[AutoRegister]` scanning; they are registered explicitly by `RegisterCoreServices()`.

**Q2: My custom services are still resolved by an interface without specifying `ServiceTypes`?**

Yes. `[AutoRegister]`'s `Policy` defaults to `RegisterPolicy.All`, which registers both the implementation type itself and its non-`System.*` interfaces. Interface-injected custom services need no explicit `Policy`. Use `RegisterPolicy.Interface` for interface-only or `Self` for self-only registration.

**Q3: Do my existing MS DI `IServiceCollection` registrations still work?**

Yes. `RegisterLiteOrm()` bridges MS DI through `AutofacServiceProviderFactory`; existing `services.AddXxx()` registrations remain valid.

See the full [8.1 Upgrade Guide](https://github.com/danjiewu/LiteOrm/blob/master/docs/upgrade-guides/01-upgrade-guide-8.1.en.md) for details.

---

## 📖 中文版本

LiteOrm.DependencyInjection 是 LiteOrm 的 **DI 集成层**。它将宿主容器切换为 **Autofac**，扫描 `[AutoRegister]` 类型，应用 **Castle DynamicProxy** AOP 拦截，并显式注册核心服务（数据源提供程序、SQL 方言构建器、DAO 连接池工厂、会话管理器、实体服务与 DAO）——调用 `RegisterLiteOrm()` 即可一键拉起完整的 LiteOrm 技术栈。

### 适用场景

- 需要通过 `RegisterLiteOrm()` 注册 LiteOrm，并从 DI 容器解析服务。
- 需要服务方法上的 AOP 能力（`[Transaction]`、`[ServicePermission]`、`[ServiceLog]`、`[Log]`）。
- 需要基于 Autofac 生命周期与拦截器的 `[AutoRegister]` 自动扫描注册。
- 需要启动时自动同步数据库表结构（`SyncTable`）。

### 环境要求

- **.NET 8.0+** 或 **.NET Standard 2.0**（兼容 .NET Framework 4.6.1+）
- **依赖库**：`Autofac`、`Castle.Core`、`Castle.Core.AsyncInterceptor`、`LiteOrm`、`LiteOrm.Common`

### 安装

```bash
dotnet add package LiteOrm.DependencyInjection
```

`LiteOrm.DependencyInjection` 传递引用 `LiteOrm` 与 `LiteOrm.Common`，无需重复声明。

### 快速入门

在 `appsettings.json` 中配置数据源：

```json
{
  "LiteOrm": {
    "Default": "main",
    "DataSources": [
      {
        "Name": "main",
        "ConnectionString": "Server=localhost;Database=MyDb;User Id=root;Password=123456;",
        "Provider": "MySqlConnector.MySqlConnection, MySqlConnector"
      }
    ]
  }
}
```

在 `Program.cs` 中注册 LiteOrm（必须调用在 `builder.Host` 上，因为它需要替换底层 DI 容器为 Autofac）：

```csharp
using LiteOrm.DependencyInjection;

// 控制台应用
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()
    .Build();

// ASP.NET Core 应用
var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();
```

带选项注册：

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.Assemblies = new[] { typeof(MyService).Assembly };
    options.RegisterSqlBuilder("main", new MySqlBuilder());
});
```

完成——`[AutoRegister]` 服务、实体服务（`IEntityService<T>`）、视图服务（`IEntityViewService<T>`）与 DAO（`IObjectDAO<T>`、`IObjectViewDAO<T>`、`IDataViewDAO<T>`）均可从容器自动解析。

### 主要特性

- **Autofac 容器**：`RegisterLiteOrm()` 通过 `AutofacServiceProviderFactory` 切换为 Autofac；已有的 `services.AddXxx()` 注册经 MS DI 桥接后仍然有效。
- **确定性核心注册**：`RegisterCoreServices()` 显式注册 `DataSourceProvider`、`SqlBuilderFactory`、`DAOContextPoolFactory`、`SessionManager`、泛型实体服务与 DAO，核心类型不再依赖 `[AutoRegister]` 扫描。
- **自动注册**：`RegisterAutoService()` 扫描程序集，按可配置的生命周期、Key、自动激活、`[InterceptAttribute]` AOP 装配，以及带 `[Service]` 特性的类型（自动应用 `ServiceInvokeInterceptor`）注册 `[AutoRegister]` 类型。
- **作用域跟踪**：作用域跟踪，自动保证异步上下文中 `SessionManager.Current` 始终指向正确的生命周期作用域。
- **表结构同步**：`LiteOrmCoreInitializer`（`IHostedService`）在启动时自动为实体创建表 / 列 / 索引。
- **服务生成代理**：`AddServiceGenerator<T>()` 创建 Castle DynamicProxy 接口代理，返回值自动从 DI 容器解析。

### ❌AOT 支持

- `LiteOrm.DependencyInjection` 依赖于Autofac容器、动态代理等特性，不支持 AOT。

---

## 🔄 迁移到 8.1.1（从 8.0.20 及以下版本）

**8.1.0 / 8.1.1** 引入了若干破坏性变更。如果你正从 **8.0.20 及以下版本**升级，请按以下步骤操作：

### 1. 引用新的 `LiteOrm.DependencyInjection` 包

`RegisterLiteOrm()` 从 `LiteOrm` 核心包移至新增的 `LiteOrm.DependencyInjection` 包，命名空间由 `LiteOrm` 改为 `LiteOrm.DependencyInjection`：

```xml
<PackageReference Include="LiteOrm.DependencyInjection" Version="8.1.1" />
```

```csharp
// 旧（8.0.20 及以下版本）
using LiteOrm;

// 新（8.1.1）
using LiteOrm.DependencyInjection;
```

`RegisterLiteOrm()` 方法签名不变，调用方式无需改动。

### 2. 更新 `BulkProvider` 使用方式（如有自定义实现）

`BulkProviderFactory`、`BulkProviderAttribute` 与 `[AutoRegister(Key = ...)]` 标记方式均已移除。将 `IBulkProvider` 直接设置到 `SqlBuilder.BulkProvider` 属性。`GetSqlBuilder(typeof(MySqlConnection))` 返回的就是 `MySqlBuilder.Instance`，直接对其设置即可：

```csharp
// 旧：通过工厂按连接类型查找（已移除）
var provider = services.GetRequiredService<BulkProviderFactory>().GetProvider(dbConnection.GetType());

// 新：直接设置到 SqlBuilder.BulkProvider
MySqlBuilder.Instance.BulkProvider = new MySqlBulkCopyProvider();
```

### 升级到 8.1.1（从 8.1.0 及更低版本）

如果你已处于 **v8.1.0**，升级到 **v8.1.1** 的变更如下：

- **DAO 构造函数现在需要 `SessionManager`。** `DAOBase`、`ObjectDAO<T>`、`ObjectViewDAO<T>`、`DataDAO<T>`、`DataViewDAO<T>` 构造函数接收 `SessionManager` 参数，不再依赖静态 `SessionManager.Current`。使用 `RegisterLiteOrm()` / `AddLiteOrm()` 时由 DI 容器自动解析，无需改动；手动构造 DAO 时需传入会话：

  ```csharp
  // 旧（v8.1.0 及更低）
  var dao = new ObjectDAO<User>();

  // 新（v8.1.1）
  var dao = new ObjectDAO<User>(sessionManager);
  ```

  自定义 DAO 若继承自 DAO 基类，构造函数需传入并转发：`public MyDAO(SessionManager sessionManager) : base(sessionManager) { }`。

- **`LiteOrmOptions.RegisterScope` 选项已移除。** 作用域跟踪始终自动启用，请删除 `options.RegisterScope = ...` 赋值。

- **`AddLiteOrm()` 自动按作用域绑定 `SessionManager.Current`**，无需中间件或手动 `SessionManager.SetCurrent(...)`。

### 3. 常见问题

**Q1：升级后 `IEntityService<T>` 无法从 DI 解析？**

确认宿主使用了 `RegisterLiteOrm()`（来自 `LiteOrm.DependencyInjection`）。核心类型（`EntityService<T>`、`ObjectDAO<T>` 等）不再通过 `[AutoRegister]` 扫描注册，而是由 `RegisterCoreServices()` 显式注册。

**Q2：我的业务服务未显式指定 `ServiceTypes`，还能通过接口解析吗？**

可以。`[AutoRegister]` 的 `Policy` 默认值为 `RegisterPolicy.All`，会同时注册实现类型自身及其非 `System.*` 命名空间接口，依赖接口注入的服务无需显式声明 `Policy`。需要仅注册接口时用 `RegisterPolicy.Interface`，仅注册自身时用 `Self`。

**Q3：原来用 MS DI 的 `IServiceCollection` 注册的服务还能用吗？**

可以。`RegisterLiteOrm()` 内部使用 `AutofacServiceProviderFactory` 桥接 MS DI，已有的 `services.AddXxx()` 注册仍然有效。

详细说明见完整 [8.1 升级指南](https://github.com/danjiewu/LiteOrm/blob/master/docs/upgrade-guides/01-upgrade-guide-8.1.md)。

---

## 📚 相关资源 / Resources

- [LiteOrm 主仓库 / Main Repository](https://github.com/danjiewu/LiteOrm)
- [配置与注册文档 / Configuration & Registration Docs](https://github.com/danjiewu/LiteOrm/blob/master/docs/06-di/01-configuration-and-registration.md)
- [8.1 升级指南 / 8.1 Upgrade Guide](https://github.com/danjiewu/LiteOrm/blob/master/docs/upgrade-guides/01-upgrade-guide-8.1.md)
- [Demo 项目 / Demo Project](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo)

## 📄 License

[MIT License](https://github.com/danjiewu/LiteOrm/blob/master/LICENSE)
