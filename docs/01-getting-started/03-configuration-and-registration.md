# 配置与注册

本文说明 LiteOrm 的基础配置结构、常用配置项和启动注册方式。

> **新手提示**：如果你是第一次配置，建议从最简单的配置开始——只配置一个数据源，使用 SQLite 作为数据库。等跑通基本流程后，再逐步添加多数据源、读写分离等高级配置。

## `appsettings.json` 示例

```json
{
  "LiteOrm": {
    "Default": "main",
    "DataSources": [
      {
        "Name": "main",
        "ConnectionString": "Server=localhost;Database=TestDb;User Id=root;Password=123456;",
        "Provider": "MySqlConnector.MySqlConnection, MySqlConnector",
        "SqlBuilder": null,
        "KeepAliveDuration": "00:10:00",
        "PoolSize": 16,
        "MaxPoolSize": 100,
        "ParamCountLimit": 2000,
        "SyncTable": false,
        "ReadOnlyConfigs": [
          {
            "ConnectionString": "Server=localhost;Database=TestDb_ReadOnly;User Id=root;Password=123456;"
          }
        ]
      }
    ]
  }
}
```

### 各数据库最小配置示例

> 以下是最精简的配置示例，只包含必填字段。你可以直接复制使用，替换其中的连接字符串即可。

**SQL Server：**
```json
{
  "LiteOrm": {
    "Default": "main",
    "DataSources": [
      {
        "Name": "main",
        "ConnectionString": "Server=localhost;Database=MyDb;Trusted_Connection=True;TrustServerCertificate=True;",
        "Provider": "Microsoft.Data.SqlClient.SqlConnection, Microsoft.Data.SqlClient"
      }
    ]
  }
}
```

**MySQL：**
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

**PostgreSQL：**
```json
{
  "LiteOrm": {
    "Default": "main",
    "DataSources": [
      {
        "Name": "main",
        "ConnectionString": "Host=localhost;Database=MyDb;Username=postgres;Password=123456;",
        "Provider": "Npgsql.NpgsqlConnection, Npgsql"
      }
    ]
  }
}
```

**SQLite（推荐新手）：**
```json
{
  "LiteOrm": {
    "Default": "main",
    "DataSources": [
      {
        "Name": "main",
        "ConnectionString": "Data Source=myapp.db",
        "Provider": "Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite"
      }
    ]
  }
}
```

## 配置项说明

| 配置项 | 说明 | 是否必填 | 默认值 |
| --- | --- | --- | --- |
| `Default` | 默认数据源名称。 | 是 | - |
| `DataSources[].Name` | 数据源标识，可被 `[Table(DataSource = ...)]` 引用。 | 是 | - |
| `DataSources[].ConnectionString` | 数据库连接字符串。 | 是 | - |
| `DataSources[].Provider` | 连接类型全名，格式为 `TypeName, AssemblyName`。 | 是 | - |
| `DataSources[].SqlBuilder` | 可选，自定义方言构建器。 | 否 | `null`（自动推断） |
| `DataSources[].KeepAliveDuration` | 连接保活时长。 | 否 | `00:10:00` |
| `DataSources[].PoolSize` | 连接池缓存的最大连接数。 | 否 | `16` |
| `DataSources[].MaxPoolSize` | 最大并发连接数上限。 | 否 | `100` |
| `DataSources[].ParamCountLimit` | 单条 SQL 参数数量限制。 | 否 | `2000` |
| `DataSources[].SyncTable` | 是否自动同步建表（连接池级默认值，可被 `[Table(SyncTable = ...)]` 实体级配置覆盖）。 | 否 | `false` |
| `DataSources[].ReadOnlyConfigs` | 只读库配置，用于读写分离。 | 否 | `[]` |

> **新手建议**：初次使用时，只需配置 `Name`、`ConnectionString`、`Provider` 三个必填项即可，其余使用默认值。

## 注册方式

### 控制台应用

```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()
    .Build();
```

### ASP.NET Core 应用

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();
```

> **注意**：`RegisterLiteOrm()` 必须调用在 `builder.Host` 上（不是 `builder.Services`），因为它需要替换底层的 DI 容器为 Autofac。

### 带选项注册

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterScope = true;
    options.Assemblies = new[] { typeof(MyService).Assembly };
    options.RegisterSqlBuilder("main", new MySqlBuilder());
});
```

### 完整的 Program.cs 示例

> 以下是一个完整的 ASP.NET Core 项目 `Program.cs` 示例，展示了 LiteOrm 注册的典型位置：

```csharp
using LiteOrm;

var builder = WebApplication.CreateBuilder(args);

// 添加控制器服务
builder.Services.AddControllers();

// 注册 LiteOrm（必须在 builder.Host 上调用）
builder.Host.RegisterLiteOrm();

var app = builder.Build();

app.MapControllers();
app.Run();
```

## 日志集成

LiteOrm 的运行日志接入 `Microsoft.Extensions.Logging`。服务调用日志、异常日志和慢查询日志会跟随宿主应用的日志提供程序一起输出。

### 宿主日志配置示例

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Host.RegisterLiteOrm();
```

### `options.LoggerFactory` 的作用

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

- 宿主 DI 中的 `ILoggerFactory`：用于常规 Service 调用日志。
- `options.LoggerFactory`：主要用于框架注册和程序集扫描阶段的输出。

详细用法参见：[日志与诊断](../03-advanced-topics/07-logging.md)

## 多数据源与读写分离建议

- 在实体上通过 `[Table(DataSource = "...")]` 绑定数据源。
- 读多写少场景可使用 `ReadOnlyConfigs` 配置只读副本：
  - 默认情况下，查询/视图类 API 会优先使用只读连接。
  - 同一个 `Session` 内，首次选中的只读副本会被缓存并复用（避免每次查询都重新轮询）。
  - 在事务中，为保证一致性，读取会强制回落到主库连接。
  - 未配置只读副本时，读取会自动回落主库连接。
- 涉及数据库方言差异时，建议显式注册 `SqlBuilder`。

## 常见问题

### `Provider` 应该填写什么？

填写数据库连接对象的完整类型名，例如 `System.Data.SqlClient.SqlConnection, System.Data.SqlClient`。

### 什么时候需要自定义 `SqlBuilder`？

当数据库版本较老、分页语法或函数行为与默认实现不一致时，需要自定义 `SqlBuilder`。

### 新手常见配置错误

> 以下是初学者在配置阶段最容易遇到的问题：

**1. 把 `RegisterLiteOrm()` 写在了 `builder.Services` 上**

错误写法：`builder.Services.RegisterLiteOrm();` ❌

正确写法：`builder.Host.RegisterLiteOrm();` ✅

原因：LiteOrm 需要替换宿主级别的 DI 容器为 Autofac，所以必须在 `IHostBuilder` 上调用。

**2. `Provider` 格式写错**

错误写法：`"Provider": "SqlConnection"` ❌（缺少命名空间和程序集名）

正确写法：`"Provider": "Microsoft.Data.SqlClient.SqlConnection, Microsoft.Data.SqlClient"` ✅

格式必须是 `完整类型名, 程序集名`（注意中间是逗号+空格）。

**3. 连接字符串中的特殊字符未转义**

如果连接字符串中包含反斜杠（如 Windows 路径），在 JSON 中需要使用双反斜杠 `\\` 或正斜杠 `/`：

```json
"ConnectionString": "Data Source=C:\\data\\myapp.db"
```

**4. 忘记安装数据库驱动包**

只安装了 `LiteOrm` 包，但没有安装对应数据库的 NuGet 驱动包（如 `Microsoft.Data.Sqlite`、`MySqlConnector` 等），运行时会抛出 `TypeLoadException`。

**5. 配置了多个数据源但 `Default` 指向了不存在的名称**

`Default` 的值必须与某个 `DataSources[].Name` 完全匹配，否则框架无法确定默认使用哪个数据源。

### 如何验证配置是否正确？

启动应用后，观察控制台输出。如果看到类似 `LiteOrm initialized successfully` 的日志，说明配置正确。如果出现异常，请检查：

1. 连接字符串是否能正常连接数据库（可以用数据库管理工具先测试）。
2. `Provider` 类型名是否与安装的 NuGet 包一致。
3. 数据库服务是否已启动。

## 相关链接

- [返回目录](../README.md)
- [第一个完整示例](./04-first-example.md)
- [配置项速查](../05-reference/01-configuration-reference.md)
- [自定义 SqlBuilder / 方言扩展](../04-extensibility/03-custom-sqlbuilder.md)

