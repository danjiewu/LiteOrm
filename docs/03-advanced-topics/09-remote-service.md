# 远程服务（LiteOrm.Remote）

LiteOrm 提供完整的远程服务调用方案，由两个独立的 NuGet 包构成：

| 包 | 角色 | 说明 |
|----|------|------|
| `LiteOrm.Remote` | 客户端 | 生成动态代理拦截方法调用，通过 HTTP 转发到服务端 |
| `LiteOrm.Remote.Server` | 服务端 | 接收 HTTP 请求，解析后从 DI 容器解析服务实例并执行 |

客户端与服务端共享 `LiteOrm.Common` 中的 DTO（`RemoteInvocationRequest` / `RemoteInvocationResponse` 等，命名空间 `LiteOrm.Remote`），保证协议一致。`ServiceName` 的生成与解析统一由 `LiteOrm.Common.TypeResolverHelper` 承担（客户端 `TypeResolverHelper.GetName` 生成，服务端 `TypeResolverHelper.FindType` 解析）。

## 一、最简示例

### 服务端

```bash
dotnet add package LiteOrm.Remote.Server
```

```csharp
using LiteOrm.Remote.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();        // 注册 LiteOrm 主框架
builder.Services.AddRemoteServer();    // 注册远程服务端

var app = builder.Build();
app.MapRemoteInvokeEndpoint();         // 映射远程调用端点
app.Run();
```

### 客户端

```bash
dotnet add package LiteOrm.Remote
```

```csharp
using LiteOrm.Remote;

var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrmRemote(opts =>
    {
        opts.RemoteServiceUri = new Uri("http://localhost:5000");
    })
    .Build();
```

### 调用方式与本地服务完全一致

```csharp
using var scope = host.Services.CreateScope();
var userService = scope.ServiceProvider.GetRequiredService<IDemoUserService>();

var user = new DemoUser { UserName = "alice" };
await userService.InsertAsync(user);          // Id 自动回写
Console.WriteLine($"新增用户 Id = {user.Id}");
```

> `AutoRegisterEntityServices` 默认为 `true`，框架自动扫描带 `[Service]` 特性的接口并注册为远程代理，无需手动逐个注册。

---

## 二、前后端物理分离的意义

在传统的单体应用中，数据访问层与应用层运行在同一个进程内，数据库连接串直接暴露在配置文件中。这意味着：

- 任何能访问应用服务器的人都能触达数据库
- 前端 Web 项目与数据库紧耦合，无法独立部署和扩展
- 多端（Web、移动端、桌面端）共享同一套代码时，数据库访问逻辑无法复用

LiteOrm.Remote 通过**远程服务代理**实现了前后端的物理分离：

```mermaid
graph TB
    subgraph 前端层["前端层（Web / 桌面 / 移动端）"]
        subgraph 客户端A["Web 应用"]
            A1["业务代码"]
            A2["远程服务代理<br/>（动态代理）"]
        end
        subgraph 客户端B["桌面应用"]
            B1["业务代码"]
            B2["远程服务代理<br/>（动态代理）"]
        end
    end
    subgraph 后端层["后端数据服务层"]
        C["API 网关 / 数据服务"]
        D["LiteOrm 本地服务<br/>EntityService / DAO"]
        E["数据库"]
    end
    A1 --> A2
    B1 --> B2
    A2 -->|HTTP / JSON| C
    B2 -->|HTTP / JSON| C
    C --> D --> E
```

**核心价值：**

| 价值 | 说明 |
|------|------|
| **数据库不暴露** | 数据库连接串仅存在于后端数据服务层，前端层无法直接访问数据库 |
| **安全隔离** | 前端层只能通过受控的服务接口访问数据，所有查询经过 ExprValidator 验证 |
| **多端复用** | Web、桌面、移动端共享同一套服务接口，后端逻辑统一维护 |
| **独立部署** | 前端层和后端层可独立扩容、独立更新，互不影响 |
| **接口不变** | 业务代码无需改动——本地调用 `userService.InsertAsync(user)` 和远程调用写法完全一致 |

> **对比传统方案**：传统方案中，如果 Web 前端和桌面客户端都需要访问数据库，要么各自维护一套数据访问代码（重复且易出错），要么通过 REST API 手动封装（需额外编写 Controller 和 DTO 映射）。LiteOrm.Remote 让服务接口定义本身就成为了 API 协议，无需额外封装层。

---

## 三、原理说明

### 整体架构

```mermaid
graph LR
    subgraph 客户端进程
        A["业务代码<br/>userService.InsertAsync(user)"]
        B["动态代理<br/>Castle DynamicProxy"]
        C["RemoteServiceInvokeInterceptor"]
        C1["构建 RemoteInvocationRequest<br/>ServiceName / Method / Arguments"]
        C2["JSON 序列化"]
        C3["IRemoteServiceTransport.InvokeAsync"]
        D["HttpRemoteServiceTransport"]
        E["反序列化 Response<br/>处理 OutArguments 回写"]
        F["返回值 / 异常"]
    end
    subgraph 服务端进程
        G["RemoteServiceDispatcher"]
        G1["ParseRequest(json)<br/>匹配 ServiceName → Type<br/>按方法名查找 MethodInfo<br/>按参数类型反序列化 Arguments"]
        H["InvokeAsync<br/>从 DI 解析服务实例<br/>反射调用目标方法<br/>处理 ArgumentOut 回写<br/>组装 RemoteInvocationResponse"]
    end
    A --> B --> C
    C --> C1 --> C2 --> C3 --> D
    D -->|HTTP| G
    G --> G1 --> H
    H -->|JSON 响应| D
    D --> E --> F
```

### 客户端流程

1. **DI 解析代理**：业务代码从 DI 容器解析服务接口（如 `IDemoUserService`），获得的是 Castle DynamicProxy 生成的动态代理对象
2. **拦截器介入**：`RemoteServiceInvokeInterceptor` 拦截所有方法调用
3. **构建请求**：将方法签名转换为 `ServiceName`（类型短名）+ `Method`（方法名）+ `Arguments`（序列化参数）
4. **传输调用**：通过 `IRemoteServiceTransport` 将请求发送到服务端
5. **处理响应**：反序列化返回值，处理 `OutArguments` 回写到原始参数对象

### 服务端流程

1. **接收请求**：`RemoteServiceDispatcher.ParseRequest` 解析 JSON 请求
2. **类型匹配**：通过 `IRemoteServiceTypeResolver` 将 `ServiceName` 解析为服务接口 `Type`
3. **方法查找**：按方法名在服务类型（含基接口）中查找 `MethodInfo`，结果缓存在 `ConcurrentDictionary` 中实现 O(1) 查找
4. **参数反序列化**：按方法参数声明类型反序列化 `Arguments`
5. **服务执行**：从 DI 容器解析服务实例，反射调用目标方法
6. **回写处理**：提取 `ArgumentOut` 参数的回写值，组装 `RemoteInvocationResponse`

### 方法查找机制

`RemoteServiceDispatcher.BuildMethodLookup` 遍历服务类型及其所有基接口，构建 `Dictionary<string, MethodInfo>`：

- 优先以 `ServiceMethodAttribute.MethodName` 为键
- 回退到 `MethodInfo.Name`
- 遇到重复键抛出 `AmbiguousMatchException`
- 结果缓存于 `ConcurrentDictionary<Type, Dictionary<string, MethodInfo>>`，后续调用 O(1) 查找

### `$type` 序列化策略

当实参运行时类型与参数声明类型不一致时，以 `{"$type":"实际类型名","$value":<值>}` 结构包装，确保服务端能正确反序列化为实际类型。`Expr` 派生类参数按声明类型序列化（Lambda 在客户端已转换为 `Expr`）。

### Identity 回写机制

服务端执行 `InsertAsync` 后，`IdentityOutAttribute` 的 `GenerateReturnValue` 从实体副本中提取 Identity 列值，放入 `OutArguments`。客户端拦截器收到响应后调用 `WriteBack`，将值写回业务代码持有的原始对象，保持引用不变。

---

## 四、详细类型与配置说明

### `[Service]` 特性

标记接口为远程服务。`AutoRegisterEntityServices` 默认开启时，框架自动扫描带 `[Service]`（且 `IsService == true`）的接口，通过 `TypeResolverHelper.Register` 注册名称映射并注册为远程代理。

```csharp
[Service]                                        // 暴露为远程服务，自动注册名称映射
public interface IDemoUserService : IEntityServiceAsync<DemoUser>
{
}

[Service(Name = "UserSvc")]                      // 自定义服务名
public interface IUserService
{
}

[Service(IsService = false)]                     // 显式禁用远程调用
public interface IInternalService
{
}
```

### `[ServiceMethod]` 特性

为方法指定自定义名称，用于方法名映射。未标注时使用 `MethodInfo.Name`。

```csharp
public interface IUserService
{
    [ServiceMethod("FindByAccount")]
    Task<User> GetByUserNameAsync(string userName);
}
```

### `TypeResolverHelper` —— 类型名 ↔ 类型双向解析

`LiteOrm.Common.TypeResolverHelper` 是公共工具类，提供类型名与 `Type` 的双向转换，**客户端生成 `ServiceName` 和服务端解析 `ServiceName` 都依赖它**。

#### 核心方法

| 方法 | 说明 |
|------|------|
| `GetName(Type)` | 生成类型可序列化名称。非泛型返回 `Type.Name`；泛型返回 `基名<参数短名1,...>`（去除反引号 arity 后缀，递归处理嵌套泛型） |
| `FindType(string typeName, string? defaultNamespace = null)` | 按名称查找类型 |
| `Register(string name, Type type)` | 注册自定义名称 ↔ 类型映射（**优先级最高**） |
| `Unregister(string name)` | 注销自定义映射 |
| `TryParseGenericServiceName(string)` | 解析泛型服务名为 (基名, 参数名数组)，如 `IEntityService<User>` → `("IEntityService", ["User"])` |

#### `FindType` 解析顺序

1. **自定义注册**（`Register` 注册的映射，优先级最高）
2. **`Type.GetType`**（兼容程序集限定名 `AssemblyQualifiedName` 与全名）
3. **精确全名匹配**（跨程序集遍历 `assembly.GetType(typeName)`）
4. **默认命名空间 + 短名**（当 `defaultNamespace` 已设置且 `typeName` 为短名时，组合为全名精确匹配）
5. **短名扫描**（遍历所有程序集按 `Type.Name` 匹配）

> **泛型类型名**：泛型类型应使用 CLR 名称格式 `Foo`1`（含反引号 arity 后缀），避免与同名的非泛型类型冲突。

### 服务端配置

#### `RemoteServerOptions` 配置项

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `InvokePath` | `string` | `"api/remote/invoke"` | 远程调用 HTTP 端点路径 |
| `JsonSerializerOptions` | `JsonSerializerOptions` | `UnsafeRelaxedJsonEscaping` + 大小写不敏感 | JSON 序列化选项 |
| `ServiceTypeResolver` | `IRemoteServiceTypeResolver` | `DefaultServiceTypeResolver` | 服务类型解析器实例 |
| `ServiceTypeResolverFactory` | `Func<IServiceProvider, IRemoteServiceTypeResolver>?` | `null` | 解析器工厂，优先级高于 `ServiceTypeResolver` |
| `AutoRegisterEntityServices` | `bool` | `true` | 自动扫描带 `[Service]` 特性的接口，通过 `TypeResolverHelper.Register` 注册 |
| `Assemblies` | `Assembly[]?` | `null` | 扫描程序集列表（用于 `AutoRegisterEntityServices`），未设置则扫描所有引用程序集 |

#### `IRemoteServiceTypeResolver` —— 服务类型解析器

服务端通过 `IRemoteServiceTypeResolver` 将请求中的 `ServiceName`（类型短名）解析为实际服务接口类型。

| 实现 | 行为 |
|------|------|
| `DefaultServiceTypeResolver` | 默认实现。未指定命名空间时全程序集按类型短名扫描；指定 `ServiceNamespace`/`ModelNamespace` 后优先按 `命名空间.类型名` 精确匹配，失败再回退全程序集短名扫描 |
| `DelegateRemoteServiceTypeResolver` | 通过委托自定义解析逻辑 |
| 自定义实现 `IRemoteServiceTypeResolver` | 完全控制解析过程 |

```csharp
// 默认：全程序集按类型短名扫描
options.ServiceTypeResolver = new DefaultServiceTypeResolver();

// 指定命名空间，优先精确匹配、提升解析速度并避免同名类型冲突
options.ServiceTypeResolver = new DefaultServiceTypeResolver(
    serviceNamespace: "MyApp.Services",
    modelNamespace: "MyApp.Models");

// 或使用工厂（可注入其他 DI 服务）
builder.Services.AddRemoteServer(options =>
{
    options.ServiceTypeResolverFactory = sp =>
        new DefaultServiceTypeResolver("MyApp.Services", "MyApp.Models");
});
```

### 客户端配置

#### `LiteOrmOptions` 配置项

| 属性 | 类型 | 说明 |
|------|------|------|
| `RemoteServiceUri` | `Uri?` | 远程服务基础地址。设置后自动注册基于 `HttpClient` 的 `HttpRemoteServiceTransport` |
| `RemoteServicePath` | `string` | 相对于 `RemoteServiceUri` 的请求路径，默认 `api/remote/invoke` |
| `ConfigureHttpClient` | `Action<HttpClient>?` | 配置内部 `HttpClient`（超时、默认请求头等） |
| `Transport` | `IRemoteServiceTransport?` | 自定义传输层实例。设置后优先于 `RemoteServiceUri` |
| `AutoRegisterEntityServices` | `bool` | 是否自动注册所有实体服务为远程代理，同时扫描 `[Service]` 特性接口并通过 `TypeResolverHelper.Register` 注册，默认 `true` |
| `Assemblies` | `Assembly[]?` | 自定义接口扫描程序集列表，未设置则扫描所有引用程序集 |

> **必填项**：`Transport` 或 `RemoteServiceUri` 至少设置一个，否则注册时抛出 `InvalidOperationException`。

### `AutoRegisterEntityServices` 自动注册

服务端和客户端均提供 `AutoRegisterEntityServices` 设置，默认为 `true`。框架自动扫描程序集中标记了 `[Service]`（且 `IsService == true`）的接口：

**客户端**完成两步注册：

**第 1 步**：扫描程序集，将标记了 `[Service]` 的接口：
- 通过 `TypeResolverHelper.Register` 注册到全局名称映射
- 同时注册为远程代理（Castle DynamicProxy），所有方法调用转发到远程服务端

**第 2 步**：通过 MS DI `AddScoped` 注册 4 个开放泛型接口的具体代理实现类：

| 接口 | 代理类 |
|------|--------|
| `IEntityService<T>` | `RemoteServiceProxy<T>` |
| `IEntityServiceAsync<T>` | `RemoteServiceAsyncProxy<T>` |
| `IEntityViewService<T>` | `RemoteViewServiceProxy<T>` |
| `IEntityViewServiceAsync<T>` | `RemoteViewServiceAsyncProxy<T>` |

**服务端**扫描带 `[Service]` 特性的接口，通过 `TypeResolverHelper.Register` 注册名称映射，确保两端 ServiceName 一致。

**注册规则**：
- 若 `[Service(Name = "CustomName")]` 设置了 `Name`，使用该名称注册
- 否则使用 `TypeResolverHelper.GetName(type)` 生成的短名（如 `IDemoUserService`、`IEntityServiceAsync<DemoUser>`）

### 手动注册与工厂模式

`AddRemoteService<TService>()` 用于手动注册任意服务接口为远程代理，**不依赖 `AutoRegisterEntityServices`**，可单独使用，也可与 `AutoRegisterEntityServices` 共存（手动注册优先，自动扫描会跳过已注册的接口）：

```csharp
// 单独使用：逐个注册
services.AddRemoteService<IUserService>()
        .AddRemoteService<IOrderService>();

// 或与 AutoRegisterEntityServices 共存：手动注册的接口不会被自动扫描覆盖
services.AddRemoteService<ISpecialService>();
```

| 注册方式 | 适用场景 | 检测方式 |
|----------|---------|----------|
| `AutoRegisterEntityServices` | 自动扫描带 `[Service]` 特性的接口，同时注册名称映射和远程代理 | `[Service]` 特性 |
| `AddRemoteService<TService>()` | 手动注册任意服务接口（含非实体服务） | 显式指定类型 |
| `AddRemoteServiceGenerator<TFactory>()` | 通过工厂聚合多个服务 | 自动扫描工厂返回类型 |

#### 工厂模式

定义工厂接口聚合多个业务服务，通过 `AddRemoteServiceGenerator` 一次性注册，并自动扫描工厂返回的所有接口类型：

```csharp
public interface RemoteServiceFactory
{
    IDemoUserService DemoUserService { get; }
    IDemoOrderService DemoOrderService { get; }
    IDemoDepartmentService DemoDepartmentService { get; }
}

services.AddRemoteServiceGenerator<RemoteServiceFactory>();

var factory = scope.ServiceProvider.GetRequiredService<RemoteServiceFactory>();
var user = await factory.DemoUserService.GetByUserNameAsync("alice");
```

---

## 五、高阶用法

### 参数回写（ArgumentOut）

> 由于远程调用的**引用语义丢失**（参数在服务端是反序列化的新实例），服务端对参数的修改不会自动反映回客户端。`[ArgumentOut]` 系列特性用于声明需要回写的参数，由框架在服务端提取回写值、客户端应用回写值。

#### 工作流程

```mermaid
graph TD
    A["客户端: user.Id = 0"]
    B["发送请求 (含 user)"]
    C["服务端: 执行 Insert<br/>user.Id = 123（服务端副本）"]
    D["服务端: handler.GenerateReturnValue(user) → 123"]
    E["服务端: 响应 OutArguments: {0: 123}"]
    F["客户端: 按 ReturnType (long) 反序列化 123"]
    G["客户端: handler.WriteBack(user, 123)"]
    H["客户端: user.Id = 123（原始对象）"]
    A --> B --> C --> D --> E --> F --> G --> H
```

#### `[IdentityOut]` —— 自增主键回写

直接实现 `IArgumentOutHandler`，服务端返回 Identity 列的当前值，客户端写回。`ReturnType` 固定为 `long`。

```csharp
public interface IEntityServiceAsync<T> where T : class
{
    Task<bool> InsertAsync([IdentityOut] T entity, CancellationToken ct = default);
    Task BatchInsertAsync([IdentityOut(Mode = ArgumentMode.Collection)] IEnumerable<T> entities, CancellationToken ct = default);
}
```

调用后 Id 自动回写：

```csharp
var user = new User { UserName = "alice" };
await userService.InsertAsync(user);
Console.WriteLine($"新增用户 Id = {user.Id}");  // Id 已回写

var orders = new List<Order> { /* ... */ };
await orderService.BatchInsertAsync(orders);
foreach (var o in orders)
    Console.WriteLine($"OrderNo={o.OrderNo}, Id={o.Id}");  // 每个 Id 都已回写
```

> **依赖**：`IdentityOutAttribute` 通过 `TableInfoProvider.Default` 解析 Identity 列，客户端与服务端均需注册（`LiteOrm` 主库的 `LiteOrmCoreInitializer` 会自动初始化）。

#### `[CopyableOut]` —— 整体回写

适用于实现了 `ICopyable` 接口的参数类型。服务端直接返回参数对象本身，客户端通过 `ICopyable.CopyFrom` 整体复制到原始对象。

```csharp
public class CopyableUser : ICopyable
{
    public long Id { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }

    public void CopyFrom(object other)
    {
        var src = (CopyableUser)other;
        Id = src.Id;
        Name = src.Name;
        CreatedAt = src.CreatedAt;
    }
}

public interface ICopyableUserService
{
    Task CreateAsync([CopyableOut(typeof(CopyableUser))] CopyableUser user);
}
```

#### `ArgumentMode` 枚举

| 值 | 说明 | `ReturnType` 含义 |
|----|------|-------------------|
| `Single`（默认） | 单个参数回写 | 回写值的类型 |
| `Collection` | 遍历 `IEnumerable`/`IList`，逐项调用 handler | **单个元素**的回写值类型（框架自动包装为 `List<ReturnType>` 序列化） |

#### 自定义回写处理器

实现 `IArgumentOutHandler` 接口（位于 `LiteOrm.Common` 命名空间），通过 `[ArgumentOut(typeof(YourHandler), typeof(ReturnType))]` 标记参数：

```csharp
using LiteOrm.Common;

public class TimestampOutHandler : IArgumentOutHandler
{
    public Type ReturnType { get; }

    // 构造函数必须接受 Type 参数（框架将 attribute.ReturnType 传入）
    public TimestampOutHandler(Type returnType) { ReturnType = returnType; }

    // 服务端：从参数对象提取需要回传的值（注意：参数是服务端反序列化的副本）
    public object GenerateReturnValue(object argument)
    {
        var entity = (MyEntity)argument;
        return entity.UpdatedAt;   // 返回服务端生成的时间戳
    }

    // 客户端：将回写值应用到原始参数对象（保持引用不变）
    public void WriteBack(object originalArg, object returnValue)
    {
        var entity = (MyEntity)originalArg;
        entity.UpdatedAt = (DateTime)returnValue;
    }
}

// 使用
public interface IMyService
{
    Task InsertAsync([ArgumentOut(typeof(TimestampOutHandler), typeof(DateTime))] MyEntity entity);
}
```

**处理器实例化规则**（由 `ArgumentOutHandlerResolver` 处理）：

1. 若特性自身直接实现 `IArgumentOutHandler`（如 `[IdentityOut]`、`[CopyableOut]`），使用特性实例本身
2. 否则优先从 DI 容器解析 `HandlerType`
3. DI 无法解析时，通过 `(Type returnType)` 构造函数创建，将 `ReturnType` 作为参数传入（无无参构造回退）

> **注意**：`GenerateReturnValue` 的参数是**服务端反序列化生成的副本**，对它的修改不会影响客户端。回写只能通过返回值 + `WriteBack` 完成。

### 自定义传输层

#### `IRemoteServiceTransport` 接口

所有传输层实现的基础接口，只定义一个方法：

```csharp
public interface IRemoteServiceTransport
{
    Task<RemoteInvocationResponse> InvokeAsync(
        RemoteInvocationRequest request, CancellationToken cancellationToken = default);
}
```

#### `JsonRemoteServiceTransport` 抽象基类（推荐基类）

位于 `LiteOrm.Remote` 命名空间，基于 `System.Text.Json` 完成请求/响应的序列化与反序列化，**自定义传输层优先继承此类**，只需实现一个抽象方法：

```csharp
public abstract class JsonRemoteServiceTransport : IRemoteServiceTransport
{
    // 已实现：序列化 request → 调用 GetResponseJsonAsync → 反序列化 response
    public async Task<RemoteInvocationResponse> InvokeAsync(
        RemoteInvocationRequest request, CancellationToken cancellationToken = default);

    // 子类只需实现：发送 JSON 字符串到远端，返回响应 JSON 字符串
    public abstract Task<string> GetResponseJsonAsync(
        string requestJson, CancellationToken cancellationToken = default);

    // 已实现：按方法返回类型解析响应（含 Result 类型反序列化、OutArguments 解析）
    protected virtual RemoteInvocationResponse ParseResponse(
        string json, MethodInfo method, JsonSerializerOptions options);
}
```

**内置序列化配置**：`UnsafeRelaxedJsonEscaping` + `PropertyNameCaseInsensitive = true`。

**继承示例**（基于 named pipe）：

```csharp
public class NamedPipeTransport : JsonRemoteServiceTransport
{
    private readonly string _pipeName;
    public NamedPipeTransport(string pipeName) => _pipeName = pipeName;

    public override async Task<string> GetResponseJsonAsync(
        string requestJson, CancellationToken cancellationToken = default)
    {
        using var client = new NamedPipeClientStream(".", _pipeName);
        await client.ConnectAsync(cancellationToken);
        var bytes = Encoding.UTF8.GetBytes(requestJson);
        await client.WriteAsync(bytes.AsMemory(0, bytes.Length), cancellationToken);
        // 读取响应 JSON ...
        return responseJson;
    }
}

opts.Transport = new NamedPipeTransport("liteorm-remote");
```

#### 默认 HTTP 传输（`HttpRemoteServiceTransport`）

`JsonRemoteServiceTransport` 的内置子类，基于 `HttpClient`：

```csharp
opts.RemoteServiceUri = new Uri("http://localhost:5000");
opts.RemoteServicePath = "api/remote/invoke";
opts.ConfigureHttpClient = client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("X-Api-Key", "...");
};
```

#### 完全自定义传输

直接实现 `IRemoteServiceTransport`（不继承 `JsonRemoteServiceTransport`），需自行处理序列化：

```csharp
public class MyTransport : IRemoteServiceTransport
{
    public Task<RemoteInvocationResponse> InvokeAsync(
        RemoteInvocationRequest request, CancellationToken cancellationToken)
    {
        // 需自行完成 request 序列化、传输、response 反序列化
    }
}

opts.Transport = new MyTransport();
```

### 序列化机制

> **重要**：远程服务调用**完全依赖对输入参数和返回值的 JSON 序列化**。客户端参数对象被序列化为 JSON 后传输，服务端反序列化重建参数对象；返回值与回写值同样经过序列化往返。这意味着：

| 约束 | 说明 |
|------|------|
| **引用语义丢失** | 参数对象在服务端是反序列化生成的新实例，对它的修改不会自动反映回客户端。需要回写时必须使用 `[ArgumentOut]` 特性 |
| **循环引用不支持** | `System.Text.Json` 默认不支持循环引用，参数/返回值对象图须为树形 |
| **类型必须可序列化** | 参数与返回值类型必须为公开类型、有无参构造函数、公共属性可读写。私有字段与只读集合不参与序列化 |
| **`CancellationToken` 不序列化** | 取消令牌作为调用上下文由传输层端到端透传，不出现在 `Arguments` 中 |
| **`Expr` 参数按声明类型序列化** | 业务代码中写的 Lambda（如 `u => u.Age > 18`）由 `LambdaExprConverter.ToLogicExpr` **在客户端进程内先转换为 `Expr` 派生类**（如 `LogicExpr`），再作为 `Expr` 类型参数序列化传输，服务端按声明类型反序列化重建表达式树。`Expression<Func<T,bool>>` 本身不会被序列化 |

#### 请求格式（`RemoteInvocationRequest`）

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

#### 响应格式（`RemoteInvocationResponse`）

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

- `Result`：返回值。客户端拦截器按方法返回类型二次反序列化（如 `Task<User>` 反序列化为 `User`）
- `OutArguments`：参数回写字典，键为参数在 `Arguments` 列表中的索引（字符串形式），值为回写值（客户端按 `IArgumentOutHandler.ReturnType` 反序列化）

失败响应：

```json
{
  "Success": false,
  "Error": {
    "Type": "System.InvalidOperationException",
    "Message": "...",
    "StackTrace": "..."
  }
}
```

### 调用示例

#### 查询

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

#### 写入

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

---

## 六、注意事项

1. **`ForEachAsync` 不支持远程调用**：流式遍历需要持续返回数据，远程协议不支持，会抛出 `NotSupportedException`
2. **`CancellationToken` 透传**：取消令牌不参与序列化，通过传输层端到端传递
3. **客户端与服务端必须注册相同的 `TableInfoProvider.Default`**：`IdentityArgumentOutHandler` 通过 `TableInfoProvider.Default` 解析 Identity 列，无反射回退
4. **`ServiceName` 一致性**：客户端和服务端使用相同的类型短名生成 `ServiceName`。两端均启用 `AutoRegisterEntityServices` 时框架自动保证一致；手动注册自定义名称时，两端必须同时调用 `TypeResolverHelper.Register`
5. **泛型服务接口**：`DefaultServiceTypeResolver` 使用 CLR 名格式 `Foo`1` 查找开放泛型，避免与非泛型同名类型冲突
6. **基接口方法继承**：`RemoteServiceDispatcher.BuildMethodLookup` 会遍历服务类型及其所有基接口，确保基接口声明的方法（如 `IEntityServiceAsync<T>.InsertAsync`）可被正确调用。遇到重复方法键时抛出 `AmbiguousMatchException`
7. **Castle DynamicProxy 兼容性**：拦截从基接口继承的方法时，`IInvocation.TargetType` 可能返回 `null`，框架使用 `GetServiceType(IInvocation)` 解析最派生的服务接口

### 与本地服务的对比

| 维度 | 本地服务 | 远程服务 |
|------|---------|---------|
| 注册方式 | `RegisterLiteOrm` 自动扫描 `[Service]` | `RegisterLiteOrmRemote` + 代理注册 |
| 调用方式 | 直接反射调用 | 动态代理拦截 + HTTP 转发 |
| 事务 | `[Transaction]` AOP | 不支持跨进程事务 |
| `ForEachAsync` | 流式遍历 | 抛出 `NotSupportedException` |
| 参数回写 | 直接修改对象 | 通过 `OutArguments` 序列化回写 |
| 异常传播 | 原始异常 | `RemoteInvocationResponse.Error` 携带异常信息 |

---

## 七、LiteOrm.Remote 的特点与优势

| 特点 | 说明 |
|------|------|
| **零侵入** | 业务代码无需任何改动——本地调用与远程调用写法完全一致，只需切换注册方式 |
| **接口即契约** | 服务接口定义本身就是 API 协议，无需额外编写 Controller、DTO 映射或 OpenAPI 文档 |
| **Identity 自动回写** | `[IdentityOut]` 特性自动处理自增主键回写，批量插入也支持集合模式回写 |
| **灵活的传输层** | 内置 HTTP 传输，可通过继承 `JsonRemoteServiceTransport` 快速实现 named pipe、gRPC 等自定义传输 |
| **智能类型解析** | `$type` 包装策略自动处理参数类型多态；`TypeResolverHelper` 支持自定义服务名注册 |
| **O(1) 方法查找** | `RemoteServiceDispatcher` 缓存方法查找表，避免每次调用的反射开销 |
| **自动注册** | `AutoRegisterEntityServices` 默认开启，扫描 `[Service]` 特性自动完成名称映射和代理注册 |
| **渐进式演进** | 可从单体应用（`RegisterLiteOrm`）平滑演进到前后端分离（`RegisterLiteOrmRemote`），服务接口定义不变 |

> 完整的客户端演示代码见 [RemoteServiceDemo.cs](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo/Demos/RemoteServiceDemo.cs)，覆盖了 13 种典型操作场景。
