# 配置参考

本文汇总 LiteOrm 完整配置项、默认值、注册方式和使用建议，适合作为接入与排障时的速查页。使用 `LiteOrm.DependencyInjection` 时，配置通过 `appsettings.json` 声明，启动时由 `RegisterLiteOrm()` 自动完成 DI 绑定、DAO 注册和方言解析；仅使用基础库时，可通过 `LoadConfiguration` 或 `AddLiteOrm()` 读取同一份配置。

> **新手提示**：如果你是第一次配置，建议从最简单的配置开始——只配置一个数据源，使用 SQLite 作为数据库。初次使用时只需配置 `Name`、`ConnectionString`、`Provider` 三个必填项，其余使用默认值。等跑通基本流程后，再逐步添加多数据源、读写分离等高级配置。

## 完整配置示例

```json
{
  "LiteOrm": {
    "Default": "DefaultConnection",
    "DataSources": [
      {
        "Name": "DefaultConnection",
        "ConnectionString": "Server=localhost;Database=TestDb;User Id=root;Password=123456;",
        "Provider": "MySqlConnector.MySqlConnection, MySqlConnector",
        "SqlBuilder": null,
        "KeepAliveDuration": "00:10:00",
        "PoolSize": 16,
        "MaxPoolSize": 100,
        "ParamCountLimit": 1000,
        "SyncTable": false,
        "ReadOnlyConfigs": [
          {
            "ConnectionString": "Server=localhost;Database=TestDb_ReadOnly;User Id=root;Password=123456;",
            "KeepAliveDuration": "00:15:00",
            "PoolSize": 32,
            "MaxPoolSize": 200,
            "ParamCountLimit": 1000
          }
        ]
      }
    ]
  }
}
```

## 各数据库最小配置示例

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

## 顶层配置

| 字段 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `Default` | `string` | 必填 | 默认数据源名称，与 `DataSources[].Name` 对应。 |
| `DataSources` | `array` | 必填 | 数据源配置列表，至少需要配置一个数据源。 |

## `DataSources[]`

| 字段 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `Name` | `string` | 必填 | 数据源名称，用于 `[Table(DataSource = "...")]` 绑定。 |
| `ConnectionString` | `string` | 必填 | 数据库连接字符串。 |
| `Provider` | `string` | 必填 | 连接类型全名，格式为 `TypeName, AssemblyName`。 |
| `SqlBuilder` | `string` | `null` | 自定义 SQL 构建器类型全名，不填则使用默认构建器。 |
| `KeepAliveDuration` | `TimeSpan` | `00:10:00` | 连接保活时长，格式为 `HH:mm:ss`。 |
| `PoolSize` | `int` | `16` | 缓存连接数，控制连接池预热数量。 |
| `MaxPoolSize` | `int` | `100` | 最大并发连接数上限。 |
| `ParamCountLimit` | `int` | `1000` | 单条 SQL 参数数量限制，防止参数过多导致数据库不支持。 |
| `SyncTable` | `bool` | `false` | 是否自动同步建表，生产环境建议关闭。连接池级默认值，可被 `[Table(SyncTable = ...)]` 实体级配置或 `DatabaseSync.OnTableSyncing` 事件覆盖。 |
| `ReadOnlyConfigs` | `array` | `[]` | 只读库配置列表，用于读写分离。 |

### 实体级同步覆盖（`[Table(SyncTable = ...)]`）

除连接池级开关外，可在实体类上通过 `[Table]` 特性的 `SyncTable` 属性声明**实体级同步模式**，枚举 `SyncTableMode` 取值如下：

| 取值 | 说明 |
| --- | --- |
| `Default` | 默认值，沿用数据源级别的 `SyncTable` 配置，不进行覆盖。 |
| `Never` | 该实体永不自动建表，即使数据源开启了 `SyncTable`。 |
| `Always` | 该实体始终自动建表，即使数据源关闭了 `SyncTable`。 |

```csharp
// 该表始终自动建表，无视数据源 SyncTable=false
[Table("Logs", SyncTable = SyncTableMode.Always)]
public class Log { ... }

// 该表永不自动建表，即使数据源开启了 SyncTable
[Table("Legacy", SyncTable = SyncTableMode.Never)]
public class Legacy { ... }
```

### 动态同步判定（`OnTableSyncing` 事件）

`SyncTable` 判定的优先级从高到低依次为：`OnTableSyncing` 事件订阅者 > `[Table(SyncTable = ...)]` 实体级配置（`Never` / `Always`）> 连接池级 `SyncTable`。若仍需更动态的控制（例如基于运行时条件），可订阅 `DAOContextPool.DatabaseSync` 的 `OnTableSyncing` 事件：

```csharp
var pool = poolFactory.GetPool("SQLite");

// 场景一：连接池开启同步，但仅对 User 表生效，其余跳过
pool.SyncTable = true;
pool.DatabaseSync.OnTableSyncing += (sender, e) =>
{
    e.ShouldSync = e.ObjectType == typeof(User);
};

// 场景二：连接池关闭同步，但对 Log 表开绿灯
pool.SyncTable = false;
pool.DatabaseSync.OnTableSyncing += (sender, e) =>
{
    if (e.ObjectType == typeof(Log)) e.ShouldSync = true;
};
```

事件参数 `TableSyncingEventArgs` 携带：

| 属性 | 说明 |
| --- | --- |
| `ObjectType` | 待同步的实体类型。 |
| `TableName` | 解析后的表名（已应用 `tableArgs`，可用于分表场景判定）。 |
| `ShouldSync` | 是否同步，默认值为实体级 `[Table(SyncTable = ...)]`（`Never`/`Always` 覆盖连接池配置，`Default` 时回退到连接池级 `SyncTable`），订阅者可覆盖此决策。 |

> 判定逻辑封装在 `DatabaseSync.ShouldSyncTable` 中，`EnsureTable` / `EnsureTableAsync` 在执行 DDL 前调用。无订阅者时回退到实体级 `[Table(SyncTable = ...)]`（若为 `Default` 则进一步回退到连接池级 `SyncTable`）。

## `ReadOnlyConfigs[]`

只读库至少应提供 `ConnectionString`；其余连接池相关字段未填写时会自动继承主库配置。

| 字段 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `ConnectionString` | `string` | 必填 | 只读库连接字符串；为空时不会创建只读连接池。 |
| `KeepAliveDuration` | `TimeSpan` | 继承主库 | 连接保活时长，格式为 `HH:mm:ss`。 |
| `PoolSize` | `int` | 继承主库 | 只读库连接池缓存数量。 |
| `MaxPoolSize` | `int` | 继承主库 | 只读库最大并发连接数。 |
| `ParamCountLimit` | `int` | 继承主库 | 只读库单条 SQL 参数数量限制。 |

## 常见 Provider 值

各数据库（含国产/兼容数据库）的 `Provider` 配置值，请参见[数据库差异与兼容性说明](./07-database-compatibility.md)。

## 建议值

| 场景 | PoolSize | MaxPoolSize |
| --- | --- | --- |
| 一般业务系统 | `16` | `100` |
| 低并发后台任务 | `5` | `20` |
| 高并发写入/批量导入 | `32` | `200` |
| 只读查询为主 | `32` | `200` |

## 注册方式（`LiteOrm.DependencyInjection`）

> `RegisterLiteOrm()` 定义于 `LiteOrm.DependencyInjection` 包。使用前需执行 `dotnet add package LiteOrm.DependencyInjection` 并添加 `using LiteOrm.DependencyInjection;`。**注意**：`RegisterLiteOrm()` 必须调用在 `builder.Host` 上（不是 `builder.Services`），因为它需要将底层 DI 容器替换为 Autofac。

### 控制台 / Worker 应用

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

### 带选项注册

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.Assemblies = new[] { typeof(MyService).Assembly };
    options.RegisterSqlBuilder("main", new MySqlBuilder());
});
```

### 完整的 Program.cs 示例

> 以下是一个完整的 ASP.NET Core 项目 `Program.cs` 示例，展示了 LiteOrm 注册的典型位置：

```csharp
using LiteOrm.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 添加控制器服务
builder.Services.AddControllers();

// 注册 LiteOrm（必须在 builder.Host 上调用）
builder.Host.RegisterLiteOrm();

var app = builder.Build();

app.MapControllers();
app.Run();
```

> 如果你不需要 Autofac / AOP，可改用基础库自带的 `AddLiteOrm()`（纯 MS DI，在 `builder.Services` 上调用），参见[第一个完整示例（仅基础库）](../01-getting-started/03-first-example.md)。

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

填写数据库连接对象的完整类型名，例如 `Microsoft.Data.SqlClient.SqlConnection, Microsoft.Data.SqlClient`。

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
- [第一个完整示例（DI 版）](../01-getting-started/05-first-example-di.md)
- [事务](../06-di/01-transactions.md)
- [日志与诊断](../06-di/03-logging.md)
- [性能优化](../03-advanced-topics/03-performance.md)
- [API 索引](./02-api-index.md)
