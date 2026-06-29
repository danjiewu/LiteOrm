# LiteOrm.Remote 使用文档

## 目录

1. [最简样例](#1-最简样例)
2. [前后端物理分离的意义](#2-前后端物理分离的意义)
3. [原理说明](#3-原理说明)
4. [详细类型与配置说明](#4-详细类型与配置说明)
5. [高阶用法](#5-高阶用法)
6. [注意事项](#6-注意事项)
7. [LiteOrm.Remote 的特点和优势](#7-liteormremote-的特点和优势)

---

## 1. 最简样例

### 1.1 服务端

在 ASP.NET Core 项目中，添加对 `LiteOrm.Remote.Server` 的引用，然后注册远程服务端点：

```csharp
var builder = WebApplication.CreateBuilder(args);

// 注册远程服务服务端基础设施
builder.Services.AddRemoteServer(options =>
{
    // 可选：自定义调用路径（默认 api/remote/invoke）
    options.InvokePath = "api/remote/invoke";
});

var app = builder.Build();

// 映射远程调用 HTTP 端点
app.MapRemoteInvokeEndpoint();

app.Run();
```

服务端只需确保业务服务接口的实现类已在 DI 容器中注册（如通过 LiteOrm 的 `AutoRegister` 特性），`RemoteServiceDispatcher` 会自动从容器解析并调用。

### 1.2 客户端

在客户端项目中，添加对 `LiteOrm.Remote` 的引用：

```csharp
var host = Host.CreateDefaultBuilder()
    .RegisterLiteOrmRemote(opts =>
    {
        // 设置远程服务端地址
        opts.RemoteServiceUri = new Uri("https://api.example.com");
        // 自动注册实体服务为远程代理
        opts.AutoRegisterEntityServices = true;
    })
    .Build();

// 直接解析远程服务并使用 —— 代码与本地调用完全一致
var userService = host.Services.GetRequiredService<IDemoUserService>();
var user = new DemoUser { UserName = "alice", DisplayName = "Alice" };
await userService.InsertAsync(user); // Identity 自动回写
Console.WriteLine($"新增用户 Id={user.Id}");

var found = await userService.GetByUserNameAsync("alice");
Console.WriteLine($"查询结果：{found?.DisplayName}");
```

---

## 2. 前后端物理分离的意义

在传统的单体应用中，服务接口定义在同一个进程内，通过 DI 容器直接注入实现类即可完成调用。但当业务规模增长，需要将服务拆分为独立部署的后端时，面临以下挑战：

| 传统方案 | 痛点 |
|---------|------|
| 手动编写 HTTP 调用代码 | 每个接口都需要手写 `HttpClient.PostAsync`、序列化、反序列化、错误处理 |
| gRPC / WebApi 自动生成 | 需要维护 `.proto` 文件或 Swagger 契约，引入额外的构建步骤和工具链 |
| 服务调用代码与业务逻辑耦合 | 接口变更时，调用方需要同步修改大量样板代码 |

**LiteOrm.Remote 的核心理念**：让远程调用对开发者完全透明。你只需定义接口，DI 容器自动注入远程代理，调用方式与本地服务完全一致 —— 无需关心序列化、HTTP 通信、异常传递等底层细节。

关键价值：

- **零契约维护**：无需 `.proto`、无需 Swagger 生成客户端，接口定义即契约
- **代码即文档**：服务接口就是 API 文档，客户端和服务端共享同一套接口定义
- **渐进式拆分**：单体应用可以零修改地将部分服务迁移到远程，调用方代码无需任何改动
- **Identity 自动回写**：`InsertAsync` 后 `Id` 自动回填到客户端对象，就像本地调用一样

---

## 3. 原理说明

### 3.1 整体架构

```
┌──────────────────────────────────────────────────────────────┐
│                         客户端                                │
│                                                              │
│  IDemoUserService proxy                                      │
│       │                                                      │
│       ▼                                                      │
│  RemoteServiceInvokeInterceptor (Castle DynamicProxy)         │
│       │                                                      │
│       ▼                                                      │
│  IRemoteServiceTransport ──HTTP POST──▶ 服务端                │
│  (HttpRemoteServiceTransport)          │                      │
└────────────────────────────────────────┼──────────────────────┘
                                         │
┌────────────────────────────────────────┼──────────────────────┐
│                         服务端          ▼                      │
│                                                              │
│  MapRemoteInvokeEndpoint (POST api/remote/invoke)             │
│       │                                                      │
│       ▼                                                      │
│  RemoteServiceDispatcher.ParseRequest(json)                   │
│       │                                                      │
│       ├── IRemoteServiceTypeResolver.ResolveService(name)     │
│       ├── BuildMethodCache(serviceType) → 查找 MethodInfo     │
│       └── 按参数类型反序列化 Arguments                         │
│       │                                                      │
│       ▼                                                      │
│  IServiceProvider.GetService(serviceType) → 调用方法           │
│       │                                                      │
│       ▼                                                      │
│  RemoteInvocationResponse (JSON 返回)                         │
└──────────────────────────────────────────────────────────────┘
```

### 3.2 核心流程

**客户端发起调用**：

1. 用户从 DI 容器解析 `IDemoUserService`，获得 Castle DynamicProxy 生成的动态代理对象
2. 调用代理对象的任意方法（如 `InsertAsync(user)`）时，由 `RemoteServiceInvokeInterceptor` 拦截
3. 拦截器构建 `RemoteInvocationRequest`，包含：
   - `ServiceName`：服务接口的短名（如 `IDemoUserService`）
   - `Method`：方法信息（`MethodInfo`）
   - `Arguments`：参数列表（过滤掉 `CancellationToken`）
4. 通过 `IRemoteServiceTransport.InvokeAsync()` 将请求序列化为 JSON 并发送到服务端
5. 接收 `RemoteInvocationResponse`，反序列化返回值，处理异常，回写 `[IdentityOut]` 参数

**服务端处理请求**：

1. `MapRemoteInvokeEndpoint` 映射的 HTTP 端点接收 POST 请求
2. `RemoteServiceDispatcher.ParseRequest()` 解析 JSON：
   - 通过 `IRemoteServiceTypeResolver` 将 `ServiceName` 解析为 `Type`
   - 在服务类型上构建 `方法名 → MethodInfo` 查找表（含基接口方法）
   - 按方法参数类型反序列化参数
3. 从 DI 容器获取服务实例，反射调用目标方法
4. 处理返回值（支持 `void`、`Task`、`Task<T>`、同步返回）
5. 构建 `RemoteInvocationResponse`（含 `Result`、`OutArguments`、`Error`）并返回 JSON

### 3.3 方法查找机制

`RemoteServiceDispatcher` 为每个服务类型构建 `ConcurrentDictionary<Type, Dictionary<string, MethodInfo>>` 缓存，实现 O(1) 方法查找：

- 遍历服务类型及其所有基接口的公共实例方法
- `[ServiceMethod]` 标记的方法优先使用 `MethodName` 作为键
- 未标记的方法以 `MethodInfo.Name` 作为键
- 重复键抛出 `AmbiguousMatchException`
- `[ServiceMethod(IsService = false)]` 的方法被排除

### 3.4 类型序列化策略

参数序列化使用 `$type` 包装机制：

- 当实参运行时类型与参数声明类型相同时，直接序列化，不附加类型信息
- 当类型不一致时（如参数声明为 `object` 或基类），以 `{"$type":"实际类型名","$value":<值>}` 结构包装
- `Expr` 派生类（如 `LogicExpr`）直接按声明类型序列化，因为 `Expr` 有内置的 JSON 转换器

### 3.5 Identity 回写机制

对于标记了 `[IdentityOut]` 的参数（如 `InsertAsync([IdentityOut] T entity)`）：

1. 客户端拦截器从 `MethodInfo` 参数上读取 `[ArgumentOut]` 特性，构建回写计划
2. 服务端 `RemoteServiceDispatcher` 执行方法后，通过 `ArgumentOutHandlerResolver` 解析回写处理器，生成回写值放入 `OutArguments`
3. 客户端收到响应后，按回写计划将值写回原始参数对象

支持集合模式（`[IdentityOut(Mode = ArgumentMode.Collection)]`）：批量操作时对集合中每个元素逐项回写。

---

## 4. 详细类型与配置说明

### 4.1 客户端配置：`LiteOrmOptions`

通过 `RegisterLiteOrmRemote(opts => { ... })` 配置：

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `RemoteServiceUri` | `Uri?` | `null` | 远程服务端基础地址。设置后自动注册 `HttpRemoteServiceTransport` |
| `RemoteServicePath` | `string` | `"api/remote/invoke"` | 相对于 `RemoteServiceUri` 的请求路径 |
| `ConfigureHttpClient` | `Action<HttpClient>?` | `null` | 配置默认 `HttpClient`（如超时、请求头） |
| `Transport` | `IRemoteServiceTransport?` | `null` | 自定义传输层实例。设置后优先于 `RemoteServiceUri` |
| `AutoRegisterEntityServices` | `bool` | `false` | 是否自动注册所有实体服务为远程代理 |
| `Assemblies` | `Assembly[]?` | `null` | 扫描的程序集列表。`null` 时扫描所有引用程序集 |

**注意**：`Transport` 和 `RemoteServiceUri` 必须至少设置一个，否则抛出 `InvalidOperationException`。

### 4.2 客户端注册 API

#### `AddRemoteService<TService>` — 手动注册单个服务

```csharp
services.AddRemoteService<IUserService>(Lifetime.Scoped);
```

- 为 `IUserService` 创建无目标接口代理，所有方法调用转发到远程
- 解析后直接返回可调用远程服务的代理实例

#### `AddRemoteServiceGenerator<TService>` — 注册工厂代理

```csharp
services.AddRemoteServiceGenerator<RemoteServiceFactory>();
```

- 为工厂接口创建代理，访问属性时自动从 DI 容器解析对应的远程服务
- 自动扫描工厂接口的所有属性与方法返回类型，将未注册的接口类型自动注册为远程代理
- 自动注册条件：接口类型、命名空间不属于 `System`、未在 DI 容器中注册

#### `AutoRegisterEntityServices` — 自动注册实体服务

设置 `opts.AutoRegisterEntityServices = true` 时，框架自动完成：

1. 注册 4 个开放泛型接口的具体代理实现：
   - `IEntityService<T>` → `RemoteServiceProxy<T>`
   - `IEntityServiceAsync<T>` → `RemoteServiceAsyncProxy<T>`
   - `IEntityViewService<T>` → `RemoteViewServiceProxy<T>`
   - `IEntityViewServiceAsync<T>` → `RemoteViewServiceAsyncProxy<T>`
2. 扫描程序集，将继承自上述泛型接口的自定义接口（如 `IDemoUserService`）注册为远程代理

### 4.3 服务端配置：`RemoteServerOptions`

通过 `AddRemoteServer(options => { ... })` 配置：

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `InvokePath` | `string` | `"api/remote/invoke"` | 远程调用 HTTP 端点路径，需与客户端一致 |
| `JsonSerializerOptions` | `JsonSerializerOptions` | `UnsafeRelaxedJsonEscaping` + 大小写不敏感 | JSON 序列化选项 |
| `ServiceTypeResolver` | `IRemoteServiceTypeResolver` | `DefaultServiceTypeResolver` | 服务类型解析器实例 |
| `ServiceTypeResolverFactory` | `Func<IServiceProvider, IRemoteServiceTypeResolver>?` | `null` | 解析器工厂。优先级高于 `ServiceTypeResolver` |

### 4.4 服务类型解析器：`IRemoteServiceTypeResolver`

服务端通过此接口根据 `ServiceName` 解析目标服务接口类型。

#### `DefaultServiceTypeResolver`（默认）

- 无参构造：全程序集短名扫描
- 带参构造：`new DefaultServiceTypeResolver(serviceNamespace, modelNamespace)`
  - 先按 `命名空间 + "." + 类型名` 精确匹配
  - 失败后回退到全程序集短名扫描
  - 支持开放泛型接口的闭合构造（如 `IEntityService<User>` → `IEntityService<>` + `User`）

#### `DelegateRemoteServiceTypeResolver`（自定义委托）

```csharp
new DelegateRemoteServiceTypeResolver(name =>
{
    // 自定义解析逻辑
    return name switch
    {
        "IUserService" => typeof(IUserService),
        _ => null
    };
});
```

#### 自定义实现

实现 `IRemoteServiceTypeResolver` 接口即可：

```csharp
public class MyResolver : IRemoteServiceTypeResolver
{
    public Type? ResolveService(string serviceName) { /* ... */ }
}
```

### 4.5 传输层：`IRemoteServiceTransport`

```csharp
public interface IRemoteServiceTransport
{
    Task<RemoteInvocationResponse> InvokeAsync(
        RemoteInvocationRequest request,
        CancellationToken cancellationToken = default);
}
```

内置实现：

| 实现类 | 说明 |
|--------|------|
| `HttpRemoteServiceTransport` | 基于 `HttpClient` + `System.Text.Json`，通过 HTTP POST 发送请求 |
| `JsonRemoteServiceTransport`（抽象基类） | 提供 JSON 序列化/反序列化逻辑，子类只需实现 `GetResponseJsonAsync` |

自定义传输层只需实现 `IRemoteServiceTransport`，例如接入 gRPC、消息队列等。

### 4.6 核心消息类型

#### `RemoteInvocationRequest`

```csharp
public sealed class RemoteInvocationRequest
{
    public string ServiceName { get; set; }    // 服务名称
    [JsonIgnore]
    public MethodInfo Method { get; set; }      // 方法信息（不参与序列化）
    public object[] Arguments { get; set; }     // 参数列表
}
```

#### `RemoteInvocationResponse`

```csharp
public sealed class RemoteInvocationResponse
{
    public bool Success { get; set; }                         // 是否成功
    public object Result { get; set; }                        // 返回值
    public SortedList<int, object> OutArguments { get; set; }  // 回写参数
    public RemoteErrorInfo Error { get; set; }                 // 异常信息
}
```

#### `RemoteErrorInfo`

```csharp
public sealed class RemoteErrorInfo
{
    public string Type { get; set; }       // 异常类型全名
    public string Message { get; set; }    // 异常消息
    public string StackTrace { get; set; } // 异常堆栈
}
```

### 4.7 特性标注

| 特性 | 作用 | 适用位置 |
|------|------|----------|
| `[Service]` | 标记接口为远程服务，可设置 `Name` 覆盖默认名称 | 接口 |
| `[ServiceMethod]` | 标记方法为远程服务方法，可设置 `MethodName` 自定义方法键、`IsService` 排除方法 | 方法 |
| `[IdentityOut]` | 标记参数为需要回写的 Identity（如自增主键回填） | 参数 |
| `[IdentityOut(Mode = ArgumentMode.Collection)]` | 集合模式 Identity 回写，对集合中每个元素逐项回写 | 参数 |
| `[ServiceLog]` | 配置日志级别和格式 | 方法/接口 |
| `[ExceptionHook]` | 配置异常处理 Hook | 方法/接口 |

---

## 5. 高阶用法

### 5.1 自定义传输层

当默认的 HTTP+JSON 传输不满足需求时，可以实现自定义传输层：

```csharp
public class GrpcRemoteServiceTransport : IRemoteServiceTransport
{
    public async Task<RemoteInvocationResponse> InvokeAsync(
        RemoteInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        // 使用 gRPC 发送请求
        // ...
    }
}

// 注册
hostBuilder.RegisterLiteOrmRemote(opts =>
{
    opts.Transport = new GrpcRemoteServiceTransport();
});
```

### 5.2 全局异常处理

通过 `RemoteServiceInvokeInterceptor.ExceptionHandling` 静态事件处理远程调用异常：

```csharp
RemoteServiceInvokeInterceptor.ExceptionHandling += (sender, ctx) =>
{
    if (ctx.Exception is ServiceException se && se.Message.Contains("timeout"))
    {
        ctx.Handle(defaultValue); // 返回默认值，不抛出异常
    }
};
```

### 5.3 方法级异常 Hook

通过 `[ExceptionHook]` 特性为特定方法配置异常处理：

```csharp
[Service]
public interface IUserService
{
    [ExceptionHook(typeof(MyExceptionHook))]
    Task<User> GetUserAsync(int id);
}

public class MyExceptionHook : IServiceExceptionHook
{
    public void OnException(ServiceExceptionContext context)
    {
        if (context.Exception is NotFoundException)
        {
            context.Handle(null); // 返回 null 而不是抛异常
        }
    }
}
```

### 5.4 自定义 ServiceName 和方法名

```csharp
[Service(Name = "UserSvc")]  // 自定义服务名
public interface IUserService
{
    [ServiceMethod(MethodName = "FindByName")]  // 自定义方法键
    Task<User> GetByUserNameAsync(string name);

    [ServiceMethod(IsService = false)]  // 排除此方法
    void InternalHelper();
}
```

### 5.5 工厂模式访问远程服务

定义工厂接口，统一管理远程服务入口：

```csharp
public interface RemoteServiceFactory
{
    IDemoUserService DemoUserService { get; }
    IDemoOrderService DemoOrderService { get; }
    IDemoDepartmentService DemoDepartmentService { get; }
}
```

注册：

```csharp
services.AddRemoteServiceGenerator<RemoteServiceFactory>();
```

使用：

```csharp
var factory = sp.GetRequiredService<RemoteServiceFactory>();
var users = await factory.DemoUserService.GetByUserNameAsync("alice");
```

`AddRemoteServiceGenerator` 会自动扫描工厂接口的所有属性/方法返回类型，将未注册的接口类型自动注册为远程代理。

### 5.6 服务端自定义类型解析器

当类型分布在多个命名空间时，可以指定命名空间缩小查找范围：

```csharp
builder.Services.AddRemoteServer(options =>
{
    options.ServiceTypeResolver = new DefaultServiceTypeResolver(
        serviceNamespace: "MyApp.Services",
        modelNamespace: "MyApp.Models"
    );
});
```

或者使用工厂模式注入 DI 依赖：

```csharp
builder.Services.AddRemoteServer(options =>
{
    options.ServiceTypeResolverFactory = sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        return new DefaultServiceTypeResolver(
            config["Remote:ServiceNamespace"],
            config["Remote:ModelNamespace"]
        );
    };
});
```

### 5.7 慢调用监控

`RemoteServiceInvokeInterceptor` 内置慢调用日志：

```csharp
// 设置慢调用阈值（默认 3 秒）
RemoteServiceInvokeInterceptor.SlowQueryThreshold = TimeSpan.FromSeconds(5);

// 设置日志中集合展开的最大长度（默认 10）
RemoteServiceInvokeInterceptor.MaxExpandedLogLength = 20;
```

超过阈值的方法调用会自动以 `Warning` 级别记录日志。

---

## 6. 注意事项

### 6.1 类型可见性

- 服务端和客户端必须共享同一套服务接口定义。建议将接口定义放在独立的共享程序集中，客户端和服务端共同引用
- 实体类型（`DemoUser`、`DemoOrder` 等）也必须对双方可见
- `DefaultServiceTypeResolver` 默认扫描所有已加载程序集，确保类型所在的程序集已被加载

### 6.2 不支持的方法

- **`ForEachAsync` 流式遍历**：远程调用不支持流式返回值，调用会抛出 `NotSupportedException`
- **带 `CancellationToken` 参数的方法**：`CancellationToken` 会被自动过滤且不参与序列化，但会被传递给 `IRemoteServiceTransport.InvokeAsync` 的 `cancellationToken` 参数

### 6.3 序列化注意事项

- 当参数的实际类型与声明类型不一致时（如 `object` 参数传入具体类型），框架使用 `$type` 包装，确保服务端能正确反序列化
- `Expr` 及其派生类（`LogicExpr`、`UpdateExpr` 等）有内置的 JSON 转换器，直接按声明类型序列化
- 服务端和客户端需使用相同的 `JsonSerializerOptions` 配置（默认 `UnsafeRelaxedJsonEscaping` + 大小写不敏感）

### 6.4 方法重载

- 按方法名匹配时，同名方法会导致 `AmbiguousMatchException`
- 使用 `[ServiceMethod(MethodName = "xxx")]` 为每个重载指定唯一的方法键来解决

### 6.5 基接口方法继承

- `RemoteServiceDispatcher.BuildMethodLookup` 会遍历服务类型及其所有基接口，确保继承的方法也能被正确解析
- 客户端使用 `GetServiceType(IInvocation)` 推断最派生的服务接口，避免 `ServiceName` 丢失派生接口信息

### 6.6 Castle DynamicProxy 兼容性

- `CreateInterfaceProxyWithoutTarget` 场景下，`IInvocation.TargetType` 可能为 `null`（当拦截基接口方法时）
- 框架内置了 `_proxyServiceTypeCache` 缓存，从代理对象实现的接口中推断最派生的服务接口

### 6.7 传输层配置

- `Transport` 优先级高于 `RemoteServiceUri`：同时设置时使用 `Transport`
- 自定义传输层需自行处理序列化/反序列化，可继承 `JsonRemoteServiceTransport` 复用 JSON 逻辑

### 6.8 服务生命周期

- `RemoteServiceInvokeInterceptor` 注册为 `Singleton`
- `RemoteServiceGenerateInterceptor` 和 `RemoteServiceDispatcher` 注册为 `Scoped`
- 手动注册远程服务时，默认生命周期为 `Scoped`，可通过 `AddRemoteService<TService>(Lifetime.Singleton)` 调整

---

## 7. LiteOrm.Remote 的特点和优势

### 7.1 零侵入的远程调用

- 调用方代码无需任何修改，从 DI 容器解析服务接口即可使用
- 与本地调用 API 完全一致，支持 `async/await`、`CancellationToken`、`Identity` 回写等所有语义

### 7.2 接口即契约

- 无需 `.proto` 文件、无需 Swagger 代码生成、无需维护额外的契约文件
- 接口定义就是 API 文档，编译期即可发现接口不匹配问题

### 7.3 完整的 Identity 回写

- 支持 `InsertAsync` 后主键自动回填到客户端对象
- 支持批量操作时集合模式逐项回写（`ArgumentMode.Collection`）
- 回写机制可扩展：自定义 `IArgumentOutHandler` 实现

### 7.4 灵活的传输层

- 内置 HTTP+JSON 传输，开箱即用
- 抽象接口 `IRemoteServiceTransport`，可接入 gRPC、消息队列等任意协议
- 自定义传输层只需实现一个方法

### 7.5 智能的类型解析

- 服务端自动方法查找：遍历基接口继承链，支持方法名定位
- `$type` 包装机制：处理多态参数，确保类型信息不丢失
- 方法缓存：`ConcurrentDictionary` 缓存方法查找表，O(1) 性能

### 7.6 内置可观测性

- 调用前后日志记录（含参数、返回值、耗时）
- 慢调用自动告警（可配置阈值）
- 异常日志区分业务异常（`Warning`）和系统异常（`Error`）
- 全局异常事件 + 方法级异常 Hook 双重机制

### 7.7 渐进式架构演进

- 单体应用可以先按本地方式开发，后续将服务接口迁移到远程部署
- 客户端代码无需改动，只需从 `RegisterLiteOrm` 切换到 `RegisterLiteOrmRemote`
- 支持按服务粒度逐步拆分，无需一次性重构

### 7.8 灵活的注册方式

- `AddRemoteService<T>`：手动精确控制每个服务的注册
- `AddRemoteServiceGenerator<T>`：通过工厂接口批量注册，自动扫描属性类型
- `AutoRegisterEntityServices`：全自动扫描，适合快速集成