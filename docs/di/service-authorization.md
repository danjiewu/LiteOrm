# 服务鉴权

`[ServicePermission]` 是 LiteOrm 内置的方法级访问控制，作用在 Service 层，决定某个服务方法允不允许被调用。它管的是「能不能调用」，「调用之后能看到哪些行」属于行级过滤，见[数据权限](../typical-applications/data-permission.md)。

鉴权由 `ServiceInvokeInterceptor` 在 AOP 拦截阶段完成，校验不过就抛 `ServicePermissionException`，方法体不执行。这项能力依赖 `RegisterLiteOrm()` 提供的接口拦截，纯 `AddLiteOrm()` 和 `LiteOrmContext` 都不带 AOP。

## 1. 校验规则

四条规则按顺序判断，命中即返回：

| 顺序 | 条件 | 结果 |
| --- | --- | --- |
| 1 | 方法和声明类型都没有 `[ServicePermission]` | 放行，不做任何校验 |
| 2 | 声明了 `AllowAnonymous = true` | 放行，不校验身份与角色（即便同时写了 `AllowRoles`） |
| 3 | 容器里没有注册 `IUserContext` | 放行，没有身份来源就不校验角色 |
| 4 | 已注册 `IUserContext` | 要求主体已认证；声明了 `AllowRoles` 时再要求命中其中任一角色 |

第 3 条是兼容设计：没接入用户体系的应用升级后行为不变。一旦注册了 `IUserContext`，第 4 条就生效，此时「没有主体」按未认证处理，会被拒绝。

认证状态取 `IPrincipal.Identity.IsAuthenticated`，角色匹配走 `IPrincipal.IsInRole(role)`。`AllowRoles` 是逗号分隔的字符串，框架按逗号切开，再去掉每项首尾空白。

## 2. 声明权限

特性可以标在方法、类、接口上。标在方法上的优先，方法上没有才看声明类型。

```csharp
public interface IOrderService
{
    // 未声明：不校验
    Task<Order?> GetAsync(long id);

    // 要求已认证，不限角色
    [ServicePermission]
    Task<Order> CreateAsync(Order order);

    // 要求已认证，且角色命中 Admin 或 Manager
    [ServicePermission(AllowRoles = "Admin, Manager")]
    Task ApproveAsync(long id, bool approved);

    // 允许匿名，不校验身份与角色
    [ServicePermission(true)]
    Task<HealthResult> HealthAsync();

    // 只限角色：AllowAnonymous 默认 false，不写即为要求已认证
    [ServicePermission(AllowRoles = "Admin")]
    Task RebuildIndexAsync();
}
```

三种声明的语义差别：

| 写法 | `AllowAnonymous` | 是否要求 `IUserContext` | 行为 |
| --- | --- | --- | --- |
| `[ServicePermission]` | false | 是 | 已注册上下文时要求已认证 |
| `[ServicePermission(AllowRoles = "Admin")]` | false | 是 | 已认证 + 角色命中 |
| `[ServicePermission(true)]` | true | 否 | 直接放行，角色声明被忽略 |

`AllowAnonymous` 是无参构造之外的第一个参数，所以 `[ServicePermission(true, AllowRoles = "Admin")]` 语法合法，角色部分却不生效。这种写法应当避免，容易让人误以为角色起约束作用。

## 3. 提供用户主体

框架不假定用户来源，主体由应用实现 `IUserContext` 提供：

```csharp
public interface IUserContext
{
    IPrincipal? UserPrincipal { get; }
}
```

接口只有一个成员。没有可用主体时返回 `null`，框架按未认证处理。

Web 应用最常见的实现直接从当前请求取主体：

```csharp
using System.Security.Principal;

public sealed class HttpUserContext : IUserContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpUserContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public IPrincipal? UserPrincipal => _accessor.HttpContext?.User;
}
```

注册 `IUserContext` 有下面几种方式，任选一种。

### 3.1 通过 `RegisterLiteOrm` 的选项注册

```csharp
builder.Services.AddHttpContextAccessor();

builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterUserContext<HttpUserContext>();          // 泛型，默认 Scoped
});
```

三个重载对应三种来源：

```csharp
options.RegisterUserContext<HttpUserContext>();                          // 泛型 + 生命周期
options.RegisterUserContext(new FixedUserContext(principal));            // 直接给实例（单例）
options.RegisterUserContext(sp => new ScopedUserContext(sp));            // 工厂 + 生命周期
```

泛型重载和工厂重载的第二个参数是 `Lifetime.Singleton / Scoped / Transient`，默认 `Scoped`。实现类型的构造函数依赖（如 `IHttpContextAccessor`）由容器注入。

多次调用会叠加注册，后注册的实现优先被解析。

### 3.2 直接注册到容器

不用 `RegisterLiteOrm` 的选项也行，把 `IUserContext` 当普通服务注册，拦截器通过 `IServiceProvider.GetService<IUserContext>()` 解析：

```csharp
builder.Services.AddScoped<IUserContext, HttpUserContext>();
```

### 3.3 生命周期怎么选

| 宿主形态 | 建议 |
| --- | --- |
| ASP.NET Core 每请求 | `Scoped`（默认），主体随请求变化 |
| 后台任务 / Worker | `Scoped`，主体从任务上下文取；没有请求时要自己约定来源 |
| 主体在进程内固定 | `Singleton` 或实例注册 |

主体来源如果是 `AsyncLocal` 或请求作用域，不要用 `Singleton` 包装访问器，小心读到别的请求的值。

## 4. 异常处理

校验不通过时抛 `ServicePermissionException`，它继承 `ServiceException`。

```csharp
try
{
    await orderService.ApproveAsync(id, true);
}
catch (ServicePermissionException ex)
{
    // "Access to 'OrderService.ApproveAsync' requires one of the roles: Admin, Manager."
}
```

异常消息包含服务名、方法名和缺失的角色。拦截器按 `Warning` 记录它，普通异常按 `Error`，无权访问不会混进错误告警。

Web 层按 `403` 处理：

```csharp
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var feature = context.Features.Get<IExceptionHandlerFeature>();
    if (feature?.Error is ServicePermissionException)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync("当前用户无权访问该操作。");
    }
}));
```

`403` 和 `404` 分开返回，前端才能区分「权限不够」和「记录不存在」。行级过滤导致的查不到仍应是 `404` 或空结果。

## 5. 生效前提

鉴权走的是 AOP 拦截，下面几条必须满足，否则特性不生效，也不会报错。

- 服务类型要被拦截到。带 `[Service]` 特性（`IsService = true`）的类型在自动注册时会应用拦截器；也可以显式声明 `[Intercept(typeof(ServiceInvokeInterceptor))]`。自行注册的服务类型不会自动获得拦截。
- 必须通过接口调用。Castle 生成的是接口代理，直接 `new` 实现类或解析具体类型拿到的实例不带拦截。同类注意事项见[事务](./transactions.md)。
- 方法要是 `public` 的虚方法或接口方法。
- 鉴权只在最外层调用执行一次。嵌套调用（同一拦截器已在处理中）不再校验，内层方法的权限声明只对直接从外部发起的调用起作用。

## 6. 内置接口自带的权限

框架的读写接口本身已经声明了权限，接入 `IUserContext` 后立即生效。

| 接口 | 声明 | 含义 |
| --- | --- | --- |
| `IEntityViewService<T>` / `IEntityViewServiceAsync<T>` | `[ServicePermission(true)]` | 查询匿名放行 |
| `IEntityService<T>` / `IEntityServiceAsync<T>` | `[ServicePermission(false)]` | 写入要求已认证 |

注册 `IUserContext` 之后：

- 直接注入泛型查询接口的读操作不受影响，仍然放行。
- 直接注入泛型写入接口的写操作开始要求已认证主体，无主体时抛 `ServicePermissionException`。

存量应用在后台任务、定时作业里直接注入 `IEntityService<T>` 写库时容易踩到这里：`IUserContext.UserPrincipal` 在这些场景返回 `null`，写入被拒。三种处理方式，按场景选：

一、给这些场景提供主体。后台任务没有请求上下文，可以配一个固定的系统主体：

```csharp
public sealed class SystemUserContext : IUserContext
{
    private static readonly IPrincipal _system =
        new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "system"), new Claim(ClaimTypes.Role, "System") },
            "System"));

    public IPrincipal? UserPrincipal => _system;
}
```

二、按场景切换来源。请求场景走请求主体，没有请求时回落到系统主体，在同一个 `IUserContext` 里分支：

```csharp
public sealed class AmbientUserContext : IUserContext
{
    private readonly IHttpContextAccessor _accessor;

    public AmbientUserContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public IPrincipal? UserPrincipal =>
        _accessor.HttpContext?.User ?? SystemPrincipal.Instance;
}
```

三、让这些方法匿名。某个写入本来就是公开的（如注册、埋点回写）时，在自定义服务方法上声明 `[ServicePermission(true)]` 即可。框架内置的写入接口改不了，只能靠前两种方式提供主体。

`ClaimsPrincipal` 的 `IsInRole` 按角色声明值精确比较，**大小写敏感**；`GenericPrincipal` 与 `WindowsPrincipal` 忽略大小写。角色名要和主体里的声明完全一致。匹配语义由 `IPrincipal.IsInRole` 决定，框架只负责把声明的角色名传进去。

## 7. 自定义服务上的权限

自定义服务要先满足第 5 节的前提，再声明权限：

```csharp
[Service]
public interface IUserService
{
    Task<User?> GetAsync(int id);

    [ServicePermission(AllowRoles = "Admin")]
    Task DeleteAsync(int id);
}

[AutoRegister(Lifetime = Lifetime.Scoped)]
[Service]
public sealed class UserService : EntityService<User>, IUserService
{
    public UserService(IServiceProvider serviceProvider) : base(serviceProvider) { }

    public Task<User?> GetAsync(int id) => GetObjectAsync(id);

    // 只读：不声明权限，命中第 1 节第 1 条放行
}
```

派生服务不用为继承来的方法重复声明，接口上和实现方法上声明的特性，拦截器都读得到。

## 8. 常见误区

### 8.1 把方法级鉴权当成数据隔离

`[ServicePermission]` 只管「能不能调用」，不管「能操作哪些数据」。通过角色校验的管理员调用 `SearchAsync` 依然能拿到全表数据。行级范围要另外做，见[数据权限](../typical-applications/data-permission.md)和[权限过滤与用户范围控制](../advanced-topics/permission-filtering.md)。

### 8.2 用 `AllowAnonymous` 加角色，以为角色会生效

`AllowAnonymous = true` 是优先级最高的放行，同一特性上的 `AllowRoles` 不校验。要按角色限制就别写 `AllowAnonymous`。

### 8.3 注册了 `IUserContext` 却期望旧行为

第 3 条的兼容放行只在完全没注册时成立。一旦注册，所有带权限声明又没标 `AllowAnonymous` 的方法都开始要求已认证主体，包括框架内置的写入接口。

### 8.4 直接 `new` 服务实例

拦截器只作用于容器解析出的接口代理，手工构造的实例不过拦截，声明什么权限都不生效。

### 8.5 用 `ServicePermissionException` 表示数据不存在

它是权限异常，对应的 HTTP 语义是 `403`。数据不存在走各自服务的空结果或 `404`，两者混用会让前端无法正确提示。

## 相关链接

- [返回目录](../README.md)
- [数据权限](../typical-applications/data-permission.md)
- [权限过滤与用户范围控制](../advanced-topics/permission-filtering.md)
- [日志与诊断](./logging.md)
- [事务](./transactions.md)
- [安全性](../advanced-topics/security.md)