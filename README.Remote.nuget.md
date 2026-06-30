# LiteOrm.Remote

[![License](https://img.shields.io/github/license/danjiewu/LiteOrm.svg)](https://github.com/danjiewu/LiteOrm/blob/master/LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-LiteOrm-brightgreen)](https://github.com/danjiewu/LiteOrm)

---

## 📖 English Version

LiteOrm.Remote is the **client-side** package of LiteOrm's remote service invocation. It generates dynamic proxies for your service interfaces and forwards calls over HTTP to a `LiteOrm.Remote.Server` endpoint, so business code can call remote services exactly like local ones.

### When to Use

- You want to physically separate frontend apps (Web / desktop / mobile) from the database access layer.
- Multiple clients need to share one backend service implementation.
- You need to keep database connection strings off the client machines.

### Requirements

- **.NET 8.0+** / **.NET Standard 2.0** (.NET Framework 4.6.1+ compatible)
- **Dependencies**: `LiteOrm.Common`, `Castle.Core`, `Castle.Core.AsyncInterceptor`
- A running **`LiteOrm.Remote.Server`** endpoint to forward calls to

### Installation

```bash
dotnet add package LiteOrm.Remote
```

### Quick Start

```csharp
using LiteOrm.Remote;

var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrmRemote(opts =>
    {
        opts.RemoteServiceUri = new Uri("http://localhost:5000");
    })
    .Build();
```

`AutoRegisterEntityServices` defaults to `true` — the framework scans interfaces marked with `[Service]` and registers them as remote proxies automatically. No per-interface registration needed.

### Call Remote Services Like Local Ones

```csharp
using var scope = host.Services.CreateScope();
var userService = scope.ServiceProvider.GetRequiredService<IDemoUserService>();

var user = new DemoUser { UserName = "alice" };
await userService.InsertAsync(user);          // Id is written back automatically
Console.WriteLine($"Inserted user Id = {user.Id}");
```

### Highlights

- **Transparent RPC**: dynamic proxies make remote calls look like local interface calls.
- **Auto Identity Write-back**: `ArgumentOutAttribute` carries identity values back from the server.
- **Pluggable Transport**: built-in HTTP transport; swap `IRemoteServiceTransport` for custom transports.
- **Shared Protocol**: DTOs (`RemoteInvocationRequest` / `RemoteInvocationResponse`) live in `LiteOrm.Common`, so client and server stay in sync.

---

## 📖 中文版本

LiteOrm.Remote 是 LiteOrm 远程服务调用的**客户端**包。它为服务接口生成动态代理，通过 HTTP 将调用转发到 `LiteOrm.Remote.Server` 端点，业务代码可以像调用本地服务一样调用远程服务。

### 适用场景

- 需要将前端应用（Web / 桌面 / 移动端）与数据访问层物理隔离。
- 多个客户端需要共享同一套后端服务实现。
- 希望数据库连接串不暴露在客户端机器上。

### 环境要求

- **.NET 8.0+** / **.NET Standard 2.0**（兼容 .NET Framework 4.6.1+）
- **依赖库**：`LiteOrm.Common`、`Castle.Core`、`Castle.Core.AsyncInterceptor`
- 需要一个运行中的 **`LiteOrm.Remote.Server`** 端点接收调用

### 安装

```bash
dotnet add package LiteOrm.Remote
```

### 快速入门

```csharp
using LiteOrm.Remote;

var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrmRemote(opts =>
    {
        opts.RemoteServiceUri = new Uri("http://localhost:5000");
    })
    .Build();
```

`AutoRegisterEntityServices` 默认为 `true`，框架自动扫描带 `[Service]` 特性的接口并注册为远程代理，无需手动逐个注册。

### 像本地服务一样调用远程服务

```csharp
using var scope = host.Services.CreateScope();
var userService = scope.ServiceProvider.GetRequiredService<IDemoUserService>();

var user = new DemoUser { UserName = "alice" };
await userService.InsertAsync(user);          // Id 自动回写
Console.WriteLine($"新增用户 Id = {user.Id}");
```

### 主要特性

- **透明 RPC**：动态代理让远程调用看起来与本地接口调用无异。
- **自动 Identity 回写**：通过 `ArgumentOutAttribute` 把服务端生成的标识值回写到客户端。
- **可插拔传输层**：内置 HTTP 传输；可实现 `IRemoteServiceTransport` 替换为自定义传输。
- **共享协议**：DTO（`RemoteInvocationRequest` / `RemoteInvocationResponse`）位于 `LiteOrm.Common`，客户端与服务端协议天然一致。

---

## 📚 相关资源 / Resources

- [LiteOrm 主仓库 / Main Repository](https://github.com/danjiewu/LiteOrm)
- [远程服务文档 / Remote Service Docs](https://github.com/danjiewu/LiteOrm/blob/master/docs/03-advanced-topics/09-remote-service.md)
- [服务端包 / Server Package: LiteOrm.Remote.Server](https://www.nuget.org/packages/LiteOrm.Remote.Server)
- [Demo 项目 / Demo Project](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo)

## 📄 License

[MIT License](https://github.com/danjiewu/LiteOrm/blob/master/LICENSE)
