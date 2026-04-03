# 动态分表分库

LiteOrm 通过 `IArged` 接口支持动态分表，适用于按时间、地区等维度拆分的表。

## 1. IArged 接口

实现 `IArged` 接口，框架在执行 SQL 时自动调用 `TableArgs` 属性获取分表参数。

```csharp
public interface IArged
{
    string[] TableArgs { get; }
}
```

## 2. 按时间分表

### 2.1 定义分表实体

```csharp
[Table("Logs_{0}")]  // {0} 会被 TableArgs 替换
public class Log : IArged
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("Level")]
    public string? Level { get; set; }

    [Column("Message")]
    public string? Message { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    string[] IArged.TableArgs => new[] { CreateTime.ToString("yyyyMM") };
}
```

### 2.2 使用示例

```csharp
var log = new Log
{
    Level = "INFO",
    Message = "User logged in",
    CreateTime = DateTime.Now
};

await logService.InsertAsync(log);
// 自动路由到表 Logs_202603
```

### 2.3 查询时分表

通过 `tableArgs` 参数指定分表：

```csharp
// 通过 tableArgs 参数指定分表
var logs = await logService.SearchAsync(
    l => l.CreateTime >= startTime && l.CreateTime <= endTime,
    tableArgs: new[] { "202603" }
);
```

## 3. 按用户 ID 分表

```csharp
[Table("Orders_{0}")]
public class Order : IArged
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("UserId")]
    public long UserId { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }

    string[] IArged.TableArgs => new[] { (UserId % 10).ToString() };
}
```

## 4. 多维度分表

### 4.1 复合分表键

```csharp
[Table("Sales_{0}_{1}")]
public class SalesRecord : IArged
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("Region")]
    public string? Region { get; set; }

    [Column("Year")]
    public int Year { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }

    string[] IArged.TableArgs => new[] { Region!, Year.ToString() };
}
```

## 5. 分表查询方式

Service 层通过 `SearchAsync` 的 `tableArgs` 参数指定分表；DAO 层通过 `WithArgs` 方法指定。

### 5.1 Service 查询分表

```csharp
// 通过 tableArgs 参数指定分表
var results = await salesService
    .SearchAsync(s => s.Amount > 1000, tableArgs: new[] { "US_2025" });
```

### 5.2 DAO 查询分表

```csharp
// 通过 WithArgs 方法指定分表
var results = await salesViewDAO
    .WithArgs("US_2025")
    .Search(s => s.Amount > 1000)
    .ToListAsync();
```

### 5.3 批量查询多个分表

需要逐个查询后合并结果：

```csharp
// 合并查询多个分表的数据
var allLogs = new List<Log>();
for (int month = 1; month <= 12; month++)
{
    var tableName = $"{month:D2}";  // 01, 02, ... 12（表名 Logs_ 前缀已在 Table 特性中定义）
    var logs = await logService
        .SearchAsync(l => l.Level == "ERROR", tableArgs: new[] { tableName });
    allLogs.AddRange(logs);
}
```

## 6. TableArgs 优先级

| 来源                          | 优先级 | 说明                |
| --------------------------- | --- | ----------------- |
| `IArged.TableArgs`          | 自动  | 实体实现接口，插入/更新时自动使用 |
| `tableArgs` 参数 / `WithArgs` | 显式  | 查询时显式指定，覆盖 IArged |

> **注意**：LiteOrm 并不能自动知道哪些分表存在，跨分表查询需要在应用层遍历可能的分表并合并结果。

## 7. 分库场景

### 7.1 多数据源 + 分表

```csharp
[Table("Logs_{0}", DataSource = "LogDB")]
public class Log : IArged
{
    // ...
}
```

### 7.2 读写分离

```json
{
  "LiteOrm": {
    "Default": "WriteDB",
    "DataSources": [
      {
        "Name": "WriteDB",
        "ConnectionString": "Server=master;...",
        "Provider": "...",
        "ReadOnlyConfigs": [
          {
            "ConnectionString": "Server=replica01;..."
          },
          {
            "ConnectionString": "Server=replica02;...",
            "PoolSize": 10
          }
        ]
      }
    ]
  }
}
```

## 8. 注意事项

1. **分表键选择**：选择均匀分布的键，避免热点分表
2. **分表数量**：考虑未来扩展，预留足够数量
3. **跨分表查询**：应用层处理合并结果
4. **IArged 实现**：确保 `TableArgs` 在插入前已正确赋值

## 9. 下一步

- 关联查询：[关联查询](../05_Associations.md)
- 性能优化：[性能优化](./EXP_Performance.md)
- 表达式扩展：[表达式扩展](./EXP_ExpressionExtension.md)
