# Logging and Diagnostics

LiteOrm writes runtime logs through `Microsoft.Extensions.Logging`. If the host application already has logging providers configured, such as Console, Debug, or Serilog, LiteOrm service-call logs, exception logs, and slow-query logs flow through the same pipeline.

## 1. What LiteOrm logs

Most built-in logging is centered around the service interceptor:

- **invoke logs** for service name, method name, and arguments
- **return logs** for return value and elapsed time
- **exception logs** for arguments and exception details
- **slow-query logs** for calls that exceed the configured threshold, including the SQL stack

The built-in generic service interfaces already opt in to service logging:

```csharp
[ServiceLog(LogLevel = ServiceLogLevel.Debug)]
public interface IEntityServiceAsync<T> : IEntityServiceAsync
{
}
```

So when your application reuses `EntityService` or `EntityViewService`, you usually already have baseline service-call logging.

## 2. Wire host logging first

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

### 2.3 `LoggerFactory` inside `RegisterLiteOrm(options => ...)`

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

`options.LoggerFactory` is mainly used for **framework registration and assembly-scan logging**. Regular service-call logs still use the host DI container's `ILoggerFactory`.

## 3. Using `ServiceLog`

`[ServiceLog]` controls **service method invocation logging** and can be applied to:

- methods
- classes
- interfaces

Its two main settings are:

- `LogLevel`: `ServiceLogLevel.Trace / Debug / Information / Warning / Error / Critical / None`
- `LogFormat`: `LogFormat.None / Args / ReturnValue / Full`

### 3.1 Simplest form

```csharp
[ServiceLog]
public interface IUserService
{
    Task<User?> GetByIdAsync(int id);
}
```

This is equivalent to:

```csharp
[ServiceLog(LogLevel = ServiceLogLevel.Information, LogFormat = LogFormat.Full)]
```

### 3.2 Set a default policy for the whole service

```csharp
[ServiceLog(LogLevel = ServiceLogLevel.Debug, LogFormat = LogFormat.Args)]
public interface IOrderService
{
    Task<Order?> GetAsync(int id);
    Task<IReadOnlyList<Order>> SearchAsync(string keyword);
}
```

This works well during development when you want to see what gets called and with which inputs, but do not want full return bodies.

### 3.3 Override logging for one method

```csharp
public interface IAccountService
{
    [ServiceLog(LogLevel = ServiceLogLevel.Warning, LogFormat = LogFormat.Full)]
    Task<bool> TransferAsync(long fromId, long toId, decimal amount);

    [ServiceLog(LogLevel = ServiceLogLevel.None)]
    Task<string> GetHealthAsync();
}
```

- `TransferAsync` is elevated for audit-worthy operations.
- `GetHealthAsync` disables service logs entirely to avoid noisy high-frequency entries.

### 3.4 `LogFormat` guidance

| Setting | Best for |
|------|----------|
| `None` | Disable invoke/return logs |
| `Args` | Input-focused troubleshooting |
| `ReturnValue` | Result-focused diagnostics |
| `Full` | End-to-end debugging with both inputs and outputs |

## 4. Two common uses of `Log`

`[Log]` and `[Log(false)]` control **which data is allowed into logs**.

## 4.1 On method parameters: hide sensitive inputs

The service interceptor reads `LogAttribute` from method parameters. If a parameter is marked as `[Log(false)]`, LiteOrm replaces its value with `*` in the invocation log.

```csharp
public interface IAuthService
{
    Task<LoginResult> LoginAsync(string userName, [Log(false)] string password);
}
```

That keeps plaintext passwords out of logs.

> `CancellationToken` is excluded by default even without `[Log(false)]`.

## 4.2 On entity properties: keep fields out of object logs

`ObjectBase` implements `ILogable`. When LiteOrm logs an entity object, it prefers `ToLog()`, and `ObjectBase.ToLog()` respects `LogAttribute` on properties.

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

With that setup, `PasswordHash` and `PasswordSalt` stay out of the generated log text.

## 4.3 Customize log text for complex objects

If the default `ObjectBase.ToLog()` output is not enough, a type can implement `ILogable` directly:

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

When such an object is logged by the interceptor, LiteOrm uses `ToLog()`.

## 5. Slow-query and volume controls

`ServiceInvokeInterceptor` exposes two useful static knobs:

```csharp
ServiceInvokeInterceptor.SlowQueryThreshold = TimeSpan.FromSeconds(1);
ServiceInvokeInterceptor.MaxExpandedLogLength = 20;
```

- `SlowQueryThreshold`: emits extra `<Slow>` and `<SlowSQL>` entries when exceeded
- `MaxExpandedLogLength`: limits collection expansion to keep logs from exploding in size

## 6. Recommended practice

1. Put `ServiceLog` on service interfaces so logging stays aligned with service boundaries.
2. Treat passwords, tokens, keys, and sensitive identifiers as opt-out-by-default: use `[Log(false)]` or a custom `ILogable`.
3. Use `Debug + Full` during local troubleshooting, then narrow to `Information` or `Warning` in production.
4. Silence high-frequency, low-value methods with `ServiceLogLevel.None`.

## Related Links

- [Back to docs hub](../README.md)
- [Configuration and Registration](../01-getting-started/03-configuration-and-registration.en.md)
- [Transactions](./01-transactions.en.md)
- [Performance](./03-performance.en.md)
