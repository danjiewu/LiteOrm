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

## 📚 相关资源 / Resources

- [LiteOrm 主仓库 / Main Repository](https://github.com/danjiewu/LiteOrm)
- [配置与注册文档 / Configuration & Registration Docs](https://github.com/danjiewu/LiteOrm/blob/master/docs/reference/configuration-reference.md)
- [8.1 升级指南 / 8.1 Upgrade Guide](https://github.com/danjiewu/LiteOrm/blob/master/docs/upgrade-guides/upgrade-guide-8.1.md)
- [Demo 项目 / Demo Project](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo)

## 📄 License

[MIT License](https://github.com/danjiewu/LiteOrm/blob/master/LICENSE)
