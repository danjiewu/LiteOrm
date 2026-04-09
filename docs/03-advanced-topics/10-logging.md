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

## 5. 慢查询与日志量控制

`ServiceInvokeInterceptor` 还提供了两个常用静态参数：

```csharp
ServiceInvokeInterceptor.SlowQueryThreshold = TimeSpan.FromSeconds(1);
ServiceInvokeInterceptor.MaxExpandedLogLength = 20;
```

- `SlowQueryThreshold`：超过阈值会额外输出 `<Slow>` 和 `<SlowSQL>` 日志。
- `MaxExpandedLogLength`：控制集合参数/结果的展开上限，避免日志过大。

## 6. 推荐实践

1. 给业务服务接口加 `ServiceLog`，把日志策略放在 Service 边界，而不是控制器里零散打印。
2. 密码、令牌、密钥、身份证号等敏感值，一律用 `[Log(false)]` 或自定义 `ILogable` 脱敏。
3. 开发阶段可用 `Debug + Full`，生产环境更建议按业务重要性收敛到 `Information` 或 `Warning`。
4. 高频、低价值的方法可用 `ServiceLogLevel.None` 降噪。

## 相关链接

- [返回目录](../README.md)
- [配置与注册](../01-getting-started/03-configuration-and-registration.md)
- [事务处理](./01-transactions.md)
- [性能优化](./03-performance.md)
