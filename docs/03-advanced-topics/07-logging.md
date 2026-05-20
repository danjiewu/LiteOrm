# 日志与诊断

LiteOrm 基于 `Microsoft.Extensions.Logging` 输出运行日志。只要宿主应用已经配置了日志提供程序（如 Console、Debug、Serilog），LiteOrm 的服务调用日志、异常日志和慢查询日志就会进入同一套日志管道。

## 1. LiteOrm 会记录什么

默认日志能力主要集中在 Service 层拦截器：

- **调用开始日志**：记录服务名、方法名和参数。
- **调用结束日志**：记录返回值和执行耗时。
- **异常日志**：记录参数、异常信息和当前调用上下文。
- **慢查询日志**：当调用耗时超过阈值时，额外输出慢方法和对应 SQL 栈。

其中，`IEntityService` / `IEntityViewService` 这些通用接口已经默认标记了：

```csharp
[ServiceLog(LogLevel = ServiceLogLevel.Debug)]
public interface IEntityServiceAsync<T> : IEntityServiceAsync
{
}
```

这意味着你在业务里直接复用 `EntityService` / `EntityViewService` 时，通常已经具备基础服务日志。

## 2. 先把宿主日志接好

### 2.1 ASP.NET Core / Web

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Host.RegisterLiteOrm();
```

### 2.2 Console / Worker

```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
    })
    .RegisterLiteOrm()
    .Build();
```

### 2.3 `RegisterLiteOrm(options => ...)` 中的 `LoggerFactory`

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.LoggerFactory = LoggerFactory.Create(logging =>
    {
        logging.AddConsole();
        logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
    });
});
```

这里的 `options.LoggerFactory` 主要用于 **框架注册与程序集扫描阶段** 的日志输出；真正的服务调用日志仍然走宿主 DI 中的 `ILoggerFactory`。

## 3. `ServiceLog` 的用法

`[ServiceLog]` 用来控制**服务方法调用日志**的级别和输出内容，可标记在：

- 方法
- 类
- 接口

它有两个关键参数：

- `LogLevel`：对应 `ServiceLogLevel.Trace / Debug / Information / Warning / Error / Critical / None`
- `LogFormat`：对应 `LogFormat.None / Args / ReturnValue / Full`

### 3.1 最简单的方式

```csharp
[ServiceLog]
public interface IUserService
{
    Task<User?> GetByIdAsync(int id);
}
```

默认等价于：

```csharp
[ServiceLog(LogLevel = ServiceLogLevel.Information, LogFormat = LogFormat.Full)]
```

### 3.2 给整个服务统一设定日志级别

```csharp
[ServiceLog(LogLevel = ServiceLogLevel.Debug, LogFormat = LogFormat.Args)]
public interface IOrderService
{
    Task<Order?> GetAsync(int id);
    Task<IReadOnlyList<Order>> SearchAsync(string keyword);
}
```

这个写法适合开发期排查“调用了什么、参数是什么”，但又不想输出完整返回值。

### 3.3 在单个方法上覆盖日志策略

```csharp
public interface IAccountService
{
    [ServiceLog(LogLevel = ServiceLogLevel.Warning, LogFormat = LogFormat.Full)]
    Task<bool> TransferAsync(long fromId, long toId, decimal amount);

    [ServiceLog(LogLevel = ServiceLogLevel.None)]
    Task<string> GetHealthAsync();
}
```

- `TransferAsync` 会以更高等级输出，便于关键业务审计。
- `GetHealthAsync` 可直接关闭服务日志，避免高频噪声。

### 3.4 `LogFormat` 选择建议

| 配置 | 适用场景 |
|------|----------|
| `None` | 彻底关闭该方法的调用前后日志 |
| `Args` | 只关心入参，不关心返回体 |
| `ReturnValue` | 只关心执行结果 |
| `Full` | 同时看参数和返回值，适合联调和问题定位 |

## 4. `Log` 特性的两种常见应用

`[Log]` / `[Log(false)]` 主要用来控制**哪些数据可以进入日志**。

## 4.1 用在方法参数上：屏蔽敏感参数

Service 拦截器会读取方法参数上的 `LogAttribute`。如果显式写成 `[Log(false)]`，日志里该参数会被替换为 `*`。

```csharp
public interface IAuthService
{
    Task<LoginResult> LoginAsync(string userName, [Log(false)] string password);
}
```

这样记录服务调用时，不会把明文密码直接写进日志。

> `CancellationToken` 在框架中默认不会展开记录，即使不写 `[Log(false)]` 也会被排除。

## 4.2 用在实体属性上：避免对象日志泄露字段

`ObjectBase` 实现了 `ILogable`。当服务日志记录实体对象时，会优先调用 `ToLog()`；而 `ObjectBase.ToLog()` 会读取属性上的 `LogAttribute`。

```csharp
[Table("Users")]
public class User : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserName")]
    public string UserName { get; set; } = string.Empty;

    [Column("PasswordHash")]
    [Log(false)]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("PasswordSalt")]
    [Log(false)]
    public string PasswordSalt { get; set; } = string.Empty;
}
```

这样在记录 `User` 对象时，`PasswordHash`、`PasswordSalt` 不会出现在日志文本里。

## 4.3 自定义复杂对象的日志内容

如果默认的 `ObjectBase.ToLog()` 还不够，可以让类型自己实现 `ILogable`：

```csharp
public class PaymentRequest : ILogable
{
    public string CardNo { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    public string ToLog()
    {
        var tail = CardNo.Length >= 4 ? CardNo[^4..] : CardNo;
        return $"CardNo:****{tail}, Amount:{Amount}";
    }
}
```

这类对象被写入服务日志时，会优先使用 `ToLog()` 的结果。

## 5. `ExceptionHook`：在服务异常时追加处理逻辑

`[ExceptionHook]` 适合放在**服务方法、类或接口**上，为 Service 拦截器补充“异常发生时还要做什么”的逻辑，例如：

- 记录业务告警
- 补充审计信息
- 把特定异常转换成约定返回值

它对应的 hook 类型必须实现 `IServiceExceptionHook`：

```csharp
public class OrderExceptionHook : IServiceExceptionHook
{
    public void OnException(ServiceExceptionContext context)
    {
        // 可读取异常、方法名、参数、SQL 栈等上下文
    }
}
```

然后在服务上声明：

```csharp
[ExceptionHook(typeof(OrderExceptionHook), Mode = ServiceExceptionHookMode.Notify)]
public interface IOrderService
{
    Task SubmitAsync(long id);
}
```

### 5.1 触发时机

当服务方法抛出异常时，`ServiceInvokeInterceptor` 会按下面顺序处理：

1. 创建 `ServiceExceptionContext`
2. 依次执行当前方法/类/接口上声明的 `ExceptionHook`
3. 再触发全局 `ServiceInvokeInterceptor.ExceptionHandling` 事件
4. 如果仍未标记为已处理，则继续抛出原异常

这意味着 `ExceptionHook` 更适合做**局部、贴近业务方法的异常扩展**；而 `ExceptionHandling` 事件更适合做**全局统一兜底**。

### 5.2 `Notify` 和 `Handle` 的区别

`ExceptionHookAttribute.Mode` 有两种模式：

| 模式 | 含义 |
|------|------|
| `Notify` | 只通知，不允许把异常标记为已处理 |
| `Handle` | 允许调用 `context.Handle(...)`，把异常转成正常返回结果 |

#### `Notify`：只观察，不吞异常

```csharp
[ExceptionHook(typeof(NotifyOnlyHook), Mode = ServiceExceptionHookMode.Notify)]
public void ThrowWithNotifyHook()
{
    throw new InvalidOperationException("notify");
}
```

这种模式适合做告警、埋点、补充日志。异常仍会继续抛出。

> 如果 `Notify` 模式下仍调用了 `context.Handle(...)`，框架会抛出 `InvalidOperationException`，防止“配置成只通知，实际却吞掉异常”的歧义行为。

#### `Handle`：显式把异常转成结果

```csharp
[ExceptionHook(typeof(HandleHook), Mode = ServiceExceptionHookMode.Handle)]
public int GetStatus()
{
    throw new InvalidOperationException("handle");
}

public class HandleHook : IServiceExceptionHook
{
    public void OnException(ServiceExceptionContext context)
    {
        context.Handle(123);
    }
}
```

当 hook 调用 `context.Handle(123)` 后，拦截器会把这次调用视为“已处理”，并直接返回 `123`。

异步方法同样适用；对 `Task<T>`，返回值会按 `T` 的类型构造。

### 5.3 `ServiceExceptionContext` 里能拿到什么

`OnException(ServiceExceptionContext context)` 中通常会用到这些信息：

- `context.Exception`：原始异常
- `context.ServiceName` / `context.MethodName`：当前服务与方法
- `context.Arguments` / `context.LogArguments`：原始参数与日志参数
- `context.SessionID`：当前会话 ID
- `context.SqlStack`：当前 SQL 栈

因此它既能做日志增强，也能做“按异常类型决定是否降级返回”的判断。

### 5.4 注册建议

`ServiceInvokeInterceptor` 通过 DI 解析 hook 类型，因此 `ExceptionHook` 对应的实现类应注册到容器中。仓库里的常见写法是：

```csharp
[AutoRegister(Lifetime.Scoped, typeof(IServiceExceptionHook))]
public class OrderExceptionHook : IServiceExceptionHook
{
    public void OnException(ServiceExceptionContext context)
    {
    }
}
```

如果 hook 没有注册，框架会尝试按类型创建实例；但只要 hook 依赖其他服务，仍建议显式走 DI 注册。

## 6. 慢查询与日志量控制

`ServiceInvokeInterceptor` 还提供了两个常用静态参数：

```csharp
ServiceInvokeInterceptor.SlowQueryThreshold = TimeSpan.FromSeconds(1);
ServiceInvokeInterceptor.MaxExpandedLogLength = 20;
```

- `SlowQueryThreshold`：超过阈值会额外输出 `<Slow>` 和 `<SlowSQL>` 日志。
- `MaxExpandedLogLength`：控制集合参数/结果的展开上限，避免日志过大。

## 7. 推荐实践

1. 给业务服务接口加 `ServiceLog`，把日志策略放在 Service 边界，而不是控制器里零散打印。
2. 密码、令牌、密钥、身份证号等敏感值，一律用 `[Log(false)]` 或自定义 `ILogable` 脱敏。
3. 开发阶段可用 `Debug + Full`，生产环境更建议按业务重要性收敛到 `Information` 或 `Warning`。
4. `ExceptionHook` 适合做方法级告警、补偿和异常转结果；跨服务统一策略更适合放在全局 `ExceptionHandling` 事件。
5. 高频、低价值的方法可用 `ServiceLogLevel.None` 降噪。

## 相关链接

- [返回目录](../README.md)
- [配置与注册](../01-getting-started/03-configuration-and-registration.md)
- [事务处理](./01-transactions.md)
- [性能优化](./03-performance.md)
