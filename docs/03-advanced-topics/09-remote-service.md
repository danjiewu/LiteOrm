# 远程服务（LiteOrm.Remote）

LiteOrm 提供完整的远程服务调用方案，由两个独立的 NuGet 包构成：

| 包 | 角色 | 说明 |
|----|------|------|
| `LiteOrm.Remote` | 客户端 | 生成动态代理拦截方法调用，通过 HTTP 转发到服务端 |
| `LiteOrm.Remote.Server` | 服务端 | 接收 HTTP 请求，解析后从 DI 容器解析服务实例并执行 |

客户端与服务端共享 `LiteOrm.Common` 中的 DTO（`RemoteInvocationRequest` / `RemoteInvocationResponse` / `ServiceNameUtil` 等，命名空间 `LiteOrm.Service`），保证协议一致。

## 架构总览

```
┌──────────────────── 客户端进程 ────────────────────┐    ┌──────────────────── 服务端进程 ────────────────────┐
│                                                    │    │                                                    │
│  业务代码                                          │    │   RemoteServiceDispatcher                          │
│    │  userService.InsertAsync(user)                │    │     ├─ ParseRequest(json)                         │
│    ▼                                               │    │     │   ├─ 匹配 ServiceName → Type                  │
│  动态代理（Castle DynamicProxy）                   │    │     │   ├─ 按方法名查找 MethodInfo                  │
│    │  RemoteServiceInvokeInterceptor               │    │     │   └─ 按参数类型反序列化 Arguments              │
│    │    ├─ 构建 RemoteInvocationRequest            │ ──▶│     │                                                │  HTTP
│    │    │   (ServiceName / Method / Arguments)     │    │     ▼                                                │
│    │    ├─ JSON 序列化                             │    │   InvokeAsync                                      │
│    │    └─ IRemoteServiceTransport.InvokeAsync     │    │     ├─ 从 DI 解析服务实例                         │
│    │                  │                            │    │     ├─ 反射调用目标方法                            │
│    │                  ▼                            │    │     ├─ 处理 [ArgumentOut] 回写                   │
│    │            HttpRemoteServiceTransport         │    │     └─ 组装 RemoteInvocationResponse             │
│    │                  │                            │ ◀──│                                                     │
│    │                  ▼                            │    │   JSON 响应                                        │
│    │            反序列化 Response                  │    │                                                     │
│    │            处理 OutArguments 回写             │    │                                                     │
│    ▼                                               │    │                                                     │
│  返回值 / 异常                                     │    │                                                     │
└────────────────────────────────────────────────────┘    └─────────────────────────────────────────────────────┘
```

## 服务端配置

### 1. 安装包

```bash
dotnet add package LiteOrm.Remote.Server
```

### 2. 注册服务并映射端点

在 `Program.cs` / `Startup.cs` 中：

```csharp
using LiteOrm.Remote.Server;

var builder = WebApplication.CreateBuilder(args);

// 注册 LiteOrm 主框架（EntityService 等本地服务）
builder.Host.RegisterLiteOrm();

// 注册远程服务端
builder.Services.AddRemoteService(options =>
{
    options.InvokePath = "api/remote/invoke"; // 默认值，需与客户端一致
    // 可选：自定义服务类型解析器
    // options.ServiceTypeResolver = new DefaultServiceTypeResolver(typeof(IDemoUserService).Assembly);
});

var app = builder.Build();

// 映射远程调用 HTTP 端点
app.MapRemoteInvokeEndpoint();
app.Run();
```

### 服务类型解析器

服务端通过 `IRemoteServiceTypeResolver` 将请求中的 `ServiceName`（类型短名）解析为实际服务接口类型。

| 实现 | 行为 |
|------|------|
| `DefaultServiceTypeResolver` | 扫描所有引用程序集，按类型短名匹配（默认） |
| `DelegateRemoteServiceTypeResolver` | 通过委托自定义解析逻辑 |
| 自定义实现 `IRemoteServiceTypeResolver` | 完全控制解析过程 |

> **泛型类型匹配**：`DefaultServiceTypeResolver` 查找开放泛型类型时使用 CLR 泛型名格式 `Foo`1`（baseName + "`" + arity），避免与同名的非泛型类型冲突。

```csharp
// 限制扫描范围到指定程序集
builder.Services.AddRemoteService(options =>
{
    options.ServiceTypeResolver = new DefaultServiceTypeResolver(typeof(IDemoUserService).Assembly);
});

// 或使用工厂（可注入其他 DI 服务）
builder.Services.AddRemoteService(options =>
{
    options.ServiceTypeResolverFactory = sp =>
        new DefaultServiceTypeResolver(typeof(IDemoUserService).Assembly);
});
```

### `[Service]` 特性

服务端默认扫描标记了 `[Service]` 且 `IsService == true` 的接口进行注册。未标记的接口不会暴露为远程服务。

```csharp
[Service]                                        // 暴露为远程服务
public interface IDemoUserService : IEntityServiceAsync<DemoUser>
{
}

[Service(IsService = false)]                     // 显式禁用远程调用
public interface IInternalService
{
}
```

## 客户端配置

### 1. 安装包

```bash
dotnet add package LiteOrm.Remote
```

### 2. 注册远程客户端

```csharp
using LiteOrm.Remote;

var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrmRemote(opts =>
    {
        opts.RemoteServiceUri = new Uri("http://localhost:5000");
        opts.RemoteServicePath = "api/remote/invoke"; // 默认值，需与服务端一致
        opts.AutoRegisterEntityServices = true;       // 自动注册实体服务代理
    })
    .Build();
```

### 配置项一览

`LiteOrmRemoteExtensions.LiteOrmOptions`：

| 属性 | 类型 | 说明 |
|------|------|------|
| `RemoteServiceUri` | `Uri?` | 远程服务基础地址。设置后自动注册基于 `HttpClient` 的 `HttpRemoteServiceTransport` |
| `RemoteServicePath` | `string` | 相对于 `RemoteServiceUri` 的请求路径，默认 `api/remote/invoke` |
| `ConfigureHttpClient` | `Action<HttpClient>?` | 配置内部 `HttpClient`（超时、默认请求头等） |
| `Transport` | `IRemoteServiceTransport?` | 自定义传输层实例。设置后优先于 `RemoteServiceUri` |
| `AutoRegisterEntityServices` | `bool` | 是否自动注册所有实体服务为远程代理，默认 `false` |
| `Assemblies` | `Assembly[]?` | 自定义接口扫描程序集列表，未设置则扫描所有引用程序集 |

> **必填项**：`Transport` 或 `RemoteServiceUri` 至少设置一个，否则注册时抛出 `InvalidOperationException`。

### 自动注册实体服务（`AutoRegisterEntityServices`）

设为 `true` 时完成两步注册：

**第 1 步**：通过 MS DI `AddScoped` 注册 4 个开放泛型接口的具体代理实现类：

| 接口 | 代理类 |
|------|--------|
| `IEntityService<T>` | `RemoteServiceProxy<T>` |
| `IEntityServiceAsync<T>` | `RemoteServiceAsyncProxy<T>` |
| `IEntityViewService<T>` | `RemoteViewServiceProxy<T>` |
| `IEntityViewServiceAsync<T>` | `RemoteViewServiceAsyncProxy<T>` |

**第 2 步**：扫描程序集，将继承自上述泛型接口的自定义接口（如 `IDemoUserService`）注册为远程代理。

启用后可直接从 DI 容器解析任何实体服务接口：

```csharp
using var scope = host.Services.CreateScope();
var userService = scope.ServiceProvider.GetRequiredService<IDemoUserService>();
var genericService = scope.ServiceProvider.GetRequiredService<IEntityServiceAsync<User>>();

// 调用方式与本地服务完全一致
var user = await userService.GetObjectAsync(1);
```

### 手动注册单个服务接口

不启用 `AutoRegisterEntityServices` 时，可逐个注册：

```csharp
services.AddRemoteService<IUserService>()
        .AddRemoteService<IOrderService>();
```

### 工厂模式（推荐）

定义工厂接口聚合多个业务服务，通过 `AddRemoteServiceGenerator` 一次性注册，并自动扫描工厂返回的所有接口类型：

```csharp
public interface RemoteServiceFactory
{
    IDemoUserService DemoUserService { get; }
    IDemoOrderService DemoOrderService { get; }
    IDemoDepartmentService DemoDepartmentService { get; }
}

// 注册工厂代理，自动扫描并注册所有返回类型为远程代理
services.AddRemoteServiceGenerator<RemoteServiceFactory>();

// 使用
var factory = scope.ServiceProvider.GetRequiredService<RemoteServiceFactory>();
var user = await factory.DemoUserService.GetByUserNameAsync("alice");
```

`AddRemoteServiceGenerator` 会扫描工厂接口的所有属性与方法返回类型，将满足以下条件的类型自动注册为远程代理：
1. 为接口类型；
2. 命名空间不属于 `System`；
3. 未在 DI 容器中注册（避免覆盖手动注册）。

## 参数回写（ArgumentOut）

远程调用中，某些方法需要将服务端计算出的值回写到客户端参数对象（如 `Insert` 后回写自增主键）。LiteOrm 通过 `[ArgumentOut]` 系列特性实现。

### `[IdentityOut]` —— 自增主键回写

`Insert` / `UpdateOrInsert` / `BatchInsert` / `BatchUpdateOrInsert` / `Batch` 方法已标记 `[IdentityOut]`，服务端执行后会将自增主键回写到客户端对象。

```csharp
var user = new User { UserName = "alice" };
await userService.InsertAsync(user);
// user.Id 已被回写
Console.WriteLine($"新增用户 Id = {user.Id}");
```

批量操作使用集合模式 `[IdentityOut(Mode = ArgumentMode.Collection)]`，对集合中每个元素逐项回写：

```csharp
var orders = new List<Order> { /* ... */ };
await orderService.BatchInsertAsync(orders);
// orders 中每个元素的 Id 都已被回写
foreach (var o in orders)
    Console.WriteLine($"OrderNo={o.OrderNo}, Id={o.Id}");
```

### `ArgumentMode` 枚举

| 值 | 说明 |
|----|------|
| `Single`（默认） | 单个参数回写 |
| `Collection` | 遍历 `IEnumerable`/`IList`，对每个元素调用 handler 回写 |

### 自定义回写处理器

实现 `IArgumentOutHandler` 接口，并通过 `[ArgumentOut(typeof(YourHandler), typeof(ReturnType))]` 标记参数：

```csharp
public class MyOutHandler : IArgumentOutHandler
{
    public Type ReturnType { get; }

    public MyOutHandler(Type returnType) { ReturnType = returnType; }

    // 服务端：从执行结果中提取需要回写的值
    public object GenerateReturnValue(object instance, MethodInfo method, object[] args, object result)
    {
        // ...
    }

    // 客户端：将回写值写入参数对象
    public void WriteBack(object argument, object value)
    {
        // ...
    }
}

// 使用
public interface IMyService
{
    Task DoSomethingAsync([ArgumentOut(typeof(MyOutHandler), typeof(string))] MyParam param);
}
```

## 调用示例

### 查询

```csharp
// 按主键查询
var user = await userService.GetObjectAsync(1);

// Lambda 条件查询
var admins = await userService.SearchAsync(u => u.Role == "Admin");

// 自定义方法
var user = await userService.GetByUserNameAsync("alice");

// 存在性检查与计数
bool exists = await userService.ExistsAsync(u => u.UserName == "alice");
int count = await userService.CountAsync(u => u.Role == "Admin");
```

### 写入

```csharp
// 新增（自增 Id 自动回写）
var user = new User { UserName = "alice", Role = "Admin" };
await userService.InsertAsync(user);

// 更新
user.DisplayName = "Alice Updated";
await userService.UpdateAsync(user);

// 批量新增（集合模式 Id 回写）
var orders = new List<Order> { /* ... */ };
await orderService.BatchInsertAsync(orders);

// 存在则更新、不存在则新增
await departmentService.UpdateOrInsertAsync(dept);

// 按条件删除
int deleted = await userService.DeleteAsync(u => u.UserName == "alice");
```

## 序列化协议

### 请求格式（`RemoteInvocationRequest`）

```json
{
  "ServiceName": "IDemoUserService",
  "Method": "InsertAsync",
  "Arguments": [
    { "UserName": "alice", "Role": "Admin", "Id": 0 }
  ]
}
```

- `ServiceName`：服务接口类型短名（泛型类型为 `基名<参数短名1,...>`，如 `IEntityServiceAsync<User>`）
- `Method`：方法名
- `Arguments`：参数数组（不含 `CancellationToken`，由传输层透传）

**参数序列化规则**：

1. 实参运行时类型与参数声明类型相同，或参数声明类型为 `Expr` 派生类 → 直接序列化，无额外类型信息
2. 类型不一致 → 以 `{"$type":"实际类型名","$value":<值>}` 结构包装

### 响应格式（`RemoteInvocationResponse`）

成功响应：

```json
{
  "Success": true,
  "Result": { /* 返回值 */ },
  "OutArguments": {
    "0": 123
  }
}
```

失败响应：

```json
{
  "Success": false,
  "Error": {
    "ErrorType": "System.InvalidOperationException",
    "ErrorMessage": "...",
    "ErrorStackTrace": "..."
  }
}
```

## 传输层

### 默认 HTTP 传输（`HttpRemoteServiceTransport`）

```csharp
opts.RemoteServiceUri = new Uri("http://localhost:5000");
opts.RemoteServicePath = "api/remote/invoke";
opts.ConfigureHttpClient = client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("X-Api-Key", "...");
};
```

### 自定义传输

实现 `IRemoteServiceTransport` 接口，并通过 `Transport` 属性注册：

```csharp
public class MyTransport : IRemoteServiceTransport
{
    public Task<RemoteInvocationResponse> InvokeAsync(
        RemoteInvocationRequest request, CancellationToken cancellationToken)
    {
        // 自定义传输逻辑（gRPC、消息队列等）
    }
}

opts.Transport = new MyTransport();
```

## 注意事项与限制

1. **`ForEachAsync` 不支持远程调用**：流式遍历需要持续返回数据，远程协议不支持，会抛出 `NotSupportedException`
2. **`CancellationToken` 透传**：取消令牌不参与序列化，通过传输层端到端传递
3. **客户端与服务端必须注册相同的 `TableInfoProvider.Default`**：`IdentityArgumentOutHandler` 通过 `TableInfoProvider.Default` 解析 Identity 列，无反射回退
4. **`ServiceName` 一致性**：客户端和服务端使用相同的类型短名生成 `ServiceName`，确保两端服务接口类型可被正确匹配
5. **泛型服务接口**：`DefaultServiceTypeResolver` 使用 CLR 名格式 `Foo`1` 查找开放泛型，避免与非泛型同名类型冲突

## 与本地服务的对比

| 维度 | 本地服务 | 远程服务 |
|------|---------|---------|
| 注册方式 | `RegisterLiteOrm` 自动扫描 `[Service]` | `RegisterLiteOrmRemote` + 代理注册 |
| 调用方式 | 直接反射调用 | 动态代理拦截 + HTTP 转发 |
| 事务 | `[Transaction]` AOP | 不支持跨进程事务 |
| `ForEachAsync` | 流式遍历 | 抛出 `NotSupportedException` |
| 参数回写 | 直接修改对象 | 通过 `OutArguments` 序列化回写 |
| 异常传播 | 原始异常 | `RemoteInvocationResponse.Error` 携带异常信息 |

## 参考示例

完整的客户端演示代码见 [RemoteServiceDemo.cs](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo/Demos/RemoteServiceDemo.cs)，覆盖了 13 种典型操作场景。
