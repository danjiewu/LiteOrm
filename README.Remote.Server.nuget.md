# LiteOrm.Remote.Server

[![License](https://img.shields.io/github/license/danjiewu/LiteOrm.svg)](https://github.com/danjiewu/LiteOrm/blob/master/LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-LiteOrm-brightgreen)](https://github.com/danjiewu/LiteOrm)

---

## 📖 English Version

LiteOrm.Remote.Server is the **server-side** package of LiteOrm's remote service invocation. It receives HTTP calls dispatched by `LiteOrm.Remote` clients, resolves the target service from the DI container, executes the method, and returns the result (including identity write-back values).

### When to Use

- You want to host a centralized data-access layer that multiple frontend apps can call.
- You need to keep database access and connection strings entirely server-side.
- You want to expose LiteOrm-based `IEntityService<T>` / custom `[Service]` interfaces over HTTP without writing controllers manually.

### Requirements

- **.NET 8.0+** (server-side only; ASP.NET Core based)
- **Dependencies**: `LiteOrm.Common`, `Microsoft.AspNetCore.App`
- Clients must use **`LiteOrm.Remote`** to ensure protocol compatibility

### Installation

```bash
dotnet add package LiteOrm.Remote.Server
```

### Quick Start

```csharp
using LiteOrm.Remote.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();        // Register the LiteOrm core framework
builder.Services.AddRemoteServer();    // Register the remote server

var app = builder.Build();
app.MapRemoteInvokeEndpoint();         // Map the remote invocation endpoint
app.Run();
```

For authenticated access, implement `IRemoteAuthenticationHandler` and register it in DI. If using ASP.NET Core Identity, use the built-in `IdentityRemoteAuthenticationHandler<TUser>`:

```csharp
// Using ASP.NET Core Identity
builder.Services.AddIdentity<MyUser, MyRole>().AddEntityFrameworkStores<MyDbContext>();
builder.Services.AddSingleton<IRemoteAuthenticationHandler, IdentityRemoteAuthenticationHandler<MyUser>>();
builder.Services.AddRemoteServer(options => { options.EnableAuthentication = false; });

// Or implement IRemoteAuthenticationHandler directly for custom auth (JWT, etc.)
builder.Services.AddSingleton<IRemoteAuthenticationHandler, MyAuthHandler>();
builder.Services.AddRemoteServer();
```

That's it — interfaces marked with `[Service]` and registered in DI are now remotely callable.

### How It Works

1. Client sends a `RemoteInvocationRequest` (service name + method + arguments) via HTTP.
2. Server resolves `ServiceName` to a concrete type via `TypeResolverHelper`, gets the instance from DI.
3. Server invokes the method, collects `ArgumentOut` write-back values, and returns a `RemoteInvocationResponse`.
4. Client proxy writes back identity / out parameters and completes the task.

### Highlights

- **Zero-Controller Exposure**: no need to write controllers per service; one endpoint dispatches all `[Service]` interfaces.
- **Ticket-based Authentication**: implement `IRemoteAuthenticationHandler` or use the built-in `IdentityRemoteAuthenticationHandler<TUser>` (obtains `SignInManager<TUser>` from DI). The SignIn/SignOut endpoints are mapped automatically.
- **Identity Write-back**: `ArgumentOutAttribute` + `IdentityArgumentOutHandler` return auto-generated identity values to the client automatically.
- **DI-Integrated**: services are resolved from the ASP.NET Core DI container, so scoped services and transactions work as expected.
- **Shared Protocol**: DTOs live in `LiteOrm.Common`, keeping client/server wire format in sync.

---

## 📖 中文版本

LiteOrm.Remote.Server 是 LiteOrm 远程服务调用的**服务端**包。它接收由 `LiteOrm.Remote` 客户端通过 HTTP 发起的调用，从 DI 容器解析目标服务实例、执行方法并返回结果（包含标识值回写）。

### 适用场景

- 希望集中部署数据访问层，供多个前端应用调用。
- 需要把数据库访问与连接串完全保留在服务端。
- 希望在不手写 Controller 的情况下，通过 HTTP 暴露基于 LiteOrm 的 `IEntityService<T>` 或自定义 `[Service]` 接口。

### 环境要求

- **.NET 8.0+**（仅服务端，基于 ASP.NET Core）
- **依赖库**：`LiteOrm.Common`、`Microsoft.AspNetCore.App`
- 客户端必须使用 **`LiteOrm.Remote`** 以保证协议一致

### 安装

```bash
dotnet add package LiteOrm.Remote.Server
```

### 快速入门

```csharp
using LiteOrm.Remote.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();        // 注册 LiteOrm 主框架
builder.Services.AddRemoteServer();    // 注册远程服务端

var app = builder.Build();
app.MapRemoteInvokeEndpoint();         // 映射远程调用端点
app.Run();
```

如需身份认证，实现 `IRemoteAuthenticationHandler` 并注册到 DI。若使用 ASP.NET Core Identity，可直接使用内置的 `IdentityRemoteAuthenticationHandler<TUser>`：

```csharp
// 使用 ASP.NET Core Identity
builder.Services.AddIdentity<MyUser, MyRole>().AddEntityFrameworkStores<MyDbContext>();
builder.Services.AddSingleton<IRemoteAuthenticationHandler, IdentityRemoteAuthenticationHandler<MyUser>>();
builder.Services.AddRemoteServer(options => { options.EnableAuthentication = false; });

// 或直接实现 IRemoteAuthenticationHandler 进行自定义认证（JWT 等）
builder.Services.AddSingleton<IRemoteAuthenticationHandler, MyAuthHandler>();
builder.Services.AddRemoteServer();
```

完成 — 标记了 `[Service]` 特性并注册到 DI 的接口现在即可被远程调用。

### 工作原理

1. 客户端通过 HTTP 发送 `RemoteInvocationRequest`（服务名 + 方法 + 参数）。
2. 服务端通过 `TypeResolverHelper` 将 `ServiceName` 解析为具体类型，从 DI 获取实例。
3. 服务端执行方法，收集 `ArgumentOut` 回写值，返回 `RemoteInvocationResponse`。
4. 客户端代理回写标识值 / out 参数，完成任务。

### 主要特性

- **零 Controller 暴露**：无需为每个服务编写 Controller；单个端点分发所有 `[Service]` 接口。
- **票据身份认证**：实现 `IRemoteAuthenticationHandler` 或使用内置 `IdentityRemoteAuthenticationHandler<TUser>`（从 DI 获取 `SignInManager<TUser>`）。SignIn/SignOut 端点自动映射。
- **标识值自动回写**：`ArgumentOutAttribute` + `IdentityArgumentOutHandler` 自动把生成的标识值返回给客户端。
- **DI 集成**：服务从 ASP.NET Core DI 容器解析，作用域服务与事务行为符合预期。
- **共享协议**：DTO 位于 `LiteOrm.Common`，客户端 / 服务端传输格式天然一致。

---

## 📚 相关资源 / Resources

- [LiteOrm 主仓库 / Main Repository](https://github.com/danjiewu/LiteOrm)
- [远程服务文档 / Remote Service Docs](https://github.com/danjiewu/LiteOrm/blob/master/docs/03-advanced-topics/09-remote-service.md)
- [客户端包 / Client Package: LiteOrm.Remote](https://www.nuget.org/packages/LiteOrm.Remote)
- [Demo 项目 / Demo Project](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo)

## 📄 License

[MIT License](https://github.com/danjiewu/LiteOrm/blob/master/LICENSE)
