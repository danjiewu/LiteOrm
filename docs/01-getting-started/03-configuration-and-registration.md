# 配置与注册

本文说明 LiteOrm 的基础配置结构、常用配置项和启动注册方式。

## `appsettings.json` 示例

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
        "ReadOnlyConfigs": []
      }
    ]
  }
}
```

## 配置项说明

| 配置项 | 说明 |
| --- | --- |
| `Default` | 默认数据源名称。 |
| `DataSources[].Name` | 数据源标识，可被 `[Table(DataSource = ...)]` 引用。 |
| `DataSources[].ConnectionString` | 数据库连接字符串。 |
| `DataSources[].Provider` | 连接类型全名，格式为 `TypeName, AssemblyName`。 |
| `DataSources[].SqlBuilder` | 可选，自定义方言构建器。 |
| `DataSources[].KeepAliveDuration` | 连接保活时长。 |
| `DataSources[].PoolSize` | 连接池缓存的最大连接数。 |
| `DataSources[].MaxPoolSize` | 最大并发连接数上限。 |
| `DataSources[].ParamCountLimit` | 单条 SQL 参数数量限制。 |
| `DataSources[].SyncTable` | 是否自动同步建表。 |
| `DataSources[].ReadOnlyConfigs` | 只读库配置，用于读写分离。 |

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

### 带选项注册

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterScope = true;
    options.Assemblies = new[] { typeof(MyService).Assembly };
    options.RegisterSqlBuilder("DefaultConnection", new MySqlBuilder());
});
```

## 多数据源与读写分离建议

- 在实体上通过 `[Table(DataSource = "...")]` 绑定数据源。
- 读多写少场景可使用 `ReadOnlyConfigs` 配置只读副本。
- 涉及数据库方言差异时，建议显式注册 `SqlBuilder`。

## 常见问题

### `Provider` 应该填写什么？

填写数据库连接对象的完整类型名，例如 `System.Data.SqlClient.SqlConnection, System.Data.SqlClient`。

### 什么时候需要自定义 `SqlBuilder`？

当数据库版本较老、分页语法或函数行为与默认实现不一致时，需要自定义 `SqlBuilder`。

## 相关链接

- [返回目录](../README.md)
- [第一个完整示例](./04-first-example.md)
- [配置项速查](../05-reference/01-configuration-reference.md)
- [自定义 SqlBuilder / 方言扩展](../04-extensibility/03-custom-sqlbuilder.md)

