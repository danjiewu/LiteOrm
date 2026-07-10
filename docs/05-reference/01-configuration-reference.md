# 配置项速查

本文汇总 LiteOrm 完整配置项、默认值和使用建议，适合作为接入与排障时的速查页。

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
        "ParamCountLimit": 2000,
        "SyncTable": false,
        "ReadOnlyConfigs": [
          {
            "ConnectionString": "Server=localhost;Database=TestDb_ReadOnly;User Id=root;Password=123456;",
            "KeepAliveDuration": "00:15:00",
            "PoolSize": 32,
            "MaxPoolSize": 200,
            "ParamCountLimit": 2000
          }
        ]
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
| `ParamCountLimit` | `int` | `2000` | 单条 SQL 参数数量限制，防止参数过多导致数据库不支持。 |
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

| 数据库 | Provider 示例 |
| --- | --- |
| MySQL | `MySqlConnector.MySqlConnection, MySqlConnector` |
| SQL Server | `System.Data.SqlClient.SqlConnection, System.Data.SqlClient` |
| PostgreSQL | `Npgsql.NpgsqlConnection, Npgsql` |
| Oracle | `Oracle.ManagedDataAccess.Client.OracleConnection, Oracle.ManagedDataAccess` |
| SQLite | `Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite` |

## 建议值

| 场景 | PoolSize | MaxPoolSize |
| --- | --- | --- |
| 一般业务系统 | `16` | `100` |
| 低并发后台任务 | `5` | `20` |
| 高并发写入/批量导入 | `32` | `200` |
| 只读查询为主 | `32` | `200` |

## 相关链接

- [返回目录](../README.md)
- [配置与注册](../01-getting-started/03-configuration-and-registration.md)
- [性能优化](../03-advanced-topics/03-performance.md)
- [API 索引](./02-api-index.md)
