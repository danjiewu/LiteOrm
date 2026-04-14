# Dynamic Sharding and Table Routing

LiteOrm supports dynamic table sharding through the `IArged` interface, suitable for tables split by dimensions like time, region, etc.

## 1. IArged Interface

Implement the `IArged` interface, and the framework automatically calls the `TableArgs` property to get table routing parameters when executing SQL.

```csharp
public interface IArged
{
    string[] TableArgs { get; }
}
```

## 2. Time-Based Sharding

### 2.1 Define Sharded Entity

```csharp
[Table("Logs_{0}")]  // {0} will be replaced by TableArgs
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

### 2.2 Usage Example

```csharp
var log = new Log
{
    Level = "INFO",
    Message = "User logged in",
    CreateTime = DateTime.Now
};

await logService.InsertAsync(log);
// Automatically routes to table Logs_202603
```

### 2.3 Sharding in Queries

Specify the shard via the `tableArgs` parameter:

```csharp
// Specify shard via tableArgs parameter
var logs = await logService.SearchAsync(
    l => l.CreateTime >= startTime && l.CreateTime <= endTime,
    tableArgs: new[] { "202603" }
);
```

### 2.4 Complete Monthly Sharding Flow

```csharp
var log = new Log
{
    Level = "ERROR",
    Message = "Payment failed",
    CreateTime = new DateTime(2026, 3, 15)
};

// Uses IArged.TableArgs => Logs_202603 on insert
await logService.InsertAsync(log);

// Query single monthly shard
var marchLogs = await logService.SearchAsync(
    l => l.Level == "ERROR",
    tableArgs: new[] { "202603" }
);
```

## 3. User ID-Based Sharding

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

## 4. Multi-Dimensional Sharding

### 4.1 Composite Sharding Key

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

## 5. Sharded Query Methods

Service layer specifies shards via the `tableArgs` parameter of `SearchAsync`; DAO layer uses the `WithArgs` method.

### 5.1 Service Layer Sharded Query

```csharp
// Specify shard via tableArgs parameter
var results = await salesService
    .SearchAsync(s => s.Amount > 1000, tableArgs: new[] { "US_2025" });
```

### 5.2 DAO Layer Sharded Query

```csharp
// Specify shard via WithArgs method
var results = await salesViewDAO
    .WithArgs("US_2025")
    .Search(s => s.Amount > 1000)
    .ToListAsync();
```

### 5.3 Batch Query Multiple Shards

You need to query each shard individually and merge the results:

```csharp
// Merge query results from multiple shards
var allLogs = new List<Log>();
for (int month = 1; month <= 12; month++)
{
    var tableName = $"{month:D2}";  // 01, 02, ... 12 (Logs_ prefix is already defined in Table attribute)
    var logs = await logService
        .SearchAsync(l => l.Level == "ERROR", tableArgs: new[] { tableName });
    allLogs.AddRange(logs);
}
```

### 5.4 `IArged` vs `tableArgs` Override Example

```csharp
var order = new Order
{
    UserId = 25
};

// Automatically routes to Orders_5 on insert
await orderService.InsertAsync(order);

// Explicitly specifying tableArgs on query overrides auto-derived result
var archivedOrders = await orderService.SearchAsync(
    o => o.UserId == 25,
    tableArgs: new[] { "archive_5" }
);
```

## 6. Real-World Sharding Patterns from Demo and Tests

### 6.1 Directly Specify `TableArgs` in Lambda

This pattern comes from `LiteOrm.Demo\Demos\ShardingQueryDemo.cs`:

```csharp
var sales = await salesService.SearchAsync(s =>
    s.TableArgs == new[] { "202412" } && s.Amount > 40
);
```

Suitable for "querying fixed months or fixed shards" quick写法.

### 6.2 Explicitly Pass `tableArgs`

```csharp
var sales = await salesService.SearchAsync(
    s => s.Amount > 100,
    tableArgs: new[] { "202411" }
);
```

This pattern is the same as `CountAsync(..., tableArgs: ...)` in tests, suitable for unified control of shard parameters at the caller layer.

### 6.3 Use `Expr.From<T>(...)` to Specify Shard

```csharp
var sales = await salesService.SearchAsync(
    Expr.From<SalesRecordView>("202411")
        .Where(Expr.Prop("Amount") > 100)
        .OrderBy(("Amount", false))
        .Section(0, 3)
);
```

This pattern also comes from Demo, suitable for combining complex queries, sorting, and pagination.

## 7. TableArgs Priority

| Source | Priority | Description |
| --- | --- | --- |
| `IArged.TableArgs` | Automatic | Entity implements interface, auto-used on insert/update |
| `tableArgs` parameter / `WithArgs` | Explicit | Explicit on query, overrides IArged |

> **Note**: LiteOrm cannot automatically know which shards exist. Cross-shard queries require iterating through possible shards at the application layer and merging results.

## 8. Multi-Database Scenarios

### 8.1 Multi-DataSource + Sharding

```csharp
[Table("Logs_{0}", DataSource = "LogDB")]
public class Log : IArged
{
    // ...
}
```

### 8.2 Read-Write Separation

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

## 9. Caveats

1. **Shard key selection**: Choose evenly distributed keys to avoid hot shards
2. **Shard count**: Consider future expansion and reserve enough capacity
3. **Cross-shard queries**: Merge results at the application layer
4. **IArged implementation**: Ensure `TableArgs` is correctly assigned before insert

## Related Links

- [Back to docs hub](../README.md)
- [Associations](../02-core-usage/05-associations.en.md)
- [Performance Optimization](./03-performance.en.md)
- [Expression Extension](../04-extensibility/01-expression-extension.en.md)
