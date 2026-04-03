# 配置项速查

本文汇总 LiteOrm 常用配置项、默认值和使用建议，适合作为接入与排障时的速查页。

## 顶层配置

| 字段 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `Default` | `string` | - | 默认数据源名称。 |
| `DataSources` | `array` | `[]` | 数据源配置列表。 |

## `DataSources[]`

| 字段 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `Name` | `string` | - | 数据源名称。 |
| `ConnectionString` | `string` | - | 数据库连接字符串。 |
| `Provider` | `string` | - | 连接类型全名。 |
| `SqlBuilder` | `string` | `null` | 自定义 SQL 构建器类型。 |
| `KeepAliveDuration` | `TimeSpan` | `00:10:00` | 连接保活时长。 |
| `PoolSize` | `int` | `16` | 缓存连接数。 |
| `MaxPoolSize` | `int` | `100` | 最大并发连接数。 |
| `ParamCountLimit` | `int` | `2000` | SQL 参数数量限制。 |
| `SyncTable` | `bool` | `false` | 是否自动同步建表。 |
| `ReadOnlyConfigs` | `array` | `[]` | 只读库配置。 |

## `ReadOnlyConfigs[]`

| 字段 | 说明 |
| --- | --- |
| `ConnectionString` | 只读库连接串；未填写的字段继承主库配置。 |
| `KeepAliveDuration` | 可覆盖主库保活设置。 |
| `PoolSize` / `MaxPoolSize` | 可单独控制只读连接池。 |
| `ParamCountLimit` | 可单独控制参数上限。 |

## 建议值

- 一般业务系统：`PoolSize = 16`，`MaxPoolSize = 100`
- 低并发后台任务：可降为 `PoolSize = 5`
- 批量导入或高峰期写入：视数据库承载能力调大 `MaxPoolSize`

## 相关链接

- [返回目录](../SUMMARY.md)
- [配置与注册](../01-getting-started/03-configuration-and-registration.md)
- [性能优化](../03-advanced-topics/03-performance.md)
- [API 索引](./02-api-index.md)

