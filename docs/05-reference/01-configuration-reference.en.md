# Configuration Reference

This page is a quick lookup for common LiteOrm configuration fields, defaults, and practical recommendations.

## Top-level settings

| Field | Type | Default | Notes |
|------|------|---------|-------|
| `Default` | `string` | - | default data source name |
| `DataSources` | `array` | `[]` | configured data sources |

## `DataSources[]`

| Field | Type | Default | Notes |
|------|------|---------|-------|
| `Name` | `string` | - | data source name |
| `ConnectionString` | `string` | - | database connection string |
| `Provider` | `string` | - | fully qualified connection type |
| `SqlBuilder` | `string` | `null` | optional custom dialect type |
| `KeepAliveDuration` | `TimeSpan` | `00:10:00` | connection keep-alive duration |
| `PoolSize` | `int` | `16` | cached connection count |
| `MaxPoolSize` | `int` | `100` | maximum concurrent connections |
| `ParamCountLimit` | `int` | `2000` | parameter-count limit per SQL statement |
| `SyncTable` | `bool` | `false` | whether to auto-sync table creation |
| `ReadOnlyConfigs` | `array` | `[]` | read-only replicas |

## `ReadOnlyConfigs[]`

| Field | Notes |
|------|-------|
| `ConnectionString` | read-replica connection string |
| `KeepAliveDuration` | optional override of primary setting |
| `PoolSize` / `MaxPoolSize` | optional replica pool sizing |
| `ParamCountLimit` | optional replica-specific parameter limit |

Missing fields inherit from the primary data-source configuration.

## Recommended starting values

- general business systems: `PoolSize = 16`, `MaxPoolSize = 100`
- low-concurrency background jobs: lower `PoolSize` is often enough
- heavy imports or write peaks: increase `MaxPoolSize` only after checking real database capacity

## Related Links

- [Back to English docs hub](../SUMMARY.en.md)
- [Configuration and Registration](../01-getting-started/03-configuration-and-registration.en.md)
- [Performance](../03-advanced-topics/03-performance.en.md)
- [API Index](./02-api-index.en.md)
