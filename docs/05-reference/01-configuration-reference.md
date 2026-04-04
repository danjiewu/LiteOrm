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
| `SyncTable` | `bool` | `false` | 是否自动同步建表，生产环境建议关闭。 |
| `ReadOnlyConfigs` | `array` | `[]` | 只读库配置列表，用于读写分离。 |

## `ReadOnlyConfigs[]`

只读库配置的字段均可选，未填写的字段会自动继承主库配置。

| 字段 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `ConnectionString` | `string` | 必填 | 只读库连接字符串。 |
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
