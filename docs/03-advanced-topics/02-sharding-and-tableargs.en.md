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

Here, `{0}` and `{1}` map to `TableArgs[0]` and `TableArgs[1]`. For example:

```csharp
var args = new[] { "US", "2025" };
// Resolves to table name: Sales_US_2025
```

By assigning different dimensions to different placeholder positions, you can pass structured parameters such as `region + year` directly instead of manually concatenating strings like `"US_2025"`.

## 5. Sharded Query Methods

Service layer specifies shards via the `tableArgs` parameter of `SearchAsync`; DAO layer uses the `WithArgs` method.

### 5.1 Service Layer Sharded Query

```csharp
// Specify shard via tableArgs parameter
var results = await salesService
    .SearchAsync(s => s.Amount > 1000, tableArgs: new[] { "US", "2025" });
```

### 5.2 DAO Layer Sharded Query

```csharp
// Specify shard via WithArgs method
var results = await salesViewDAO
    .WithArgs("US", "2025")
    .Search(s => s.Amount > 1000)
    .ToListAsync();
```

### 5.3 `TableArgs` Propagation

`TableArgs` are not limited to just "the current table". They propagate through scopes:

1. Once the main table specifies `tableArgs`, `WithArgs(...)`, or `Expr.From<T>(...)`, those arguments enter the current SQL scope.
2. Later tables in the same scope, and nested tables in child scopes such as subqueries or association expressions, reuse those propagated arguments if they do not explicitly specify their own `TableArgs`.
3. If a later table explicitly sets `TableArgs` on its own `TableExpr` or association expression, that explicit value overrides the inherited one.

So when the same sharding dimensions apply across the whole query chain, you usually only need to specify the arguments once on the main table.

### 5.4 Batch Query Multiple Shards

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

### 5.5 `IArged` vs `tableArgs` Override Example

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

Suitable for quickly querying a fixed month or a fixed shard.

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

### 6.4 Use Different Placeholder Positions for Different Dimensions

```csharp
var sales = await salesService.SearchAsync(
    Expr.From<SalesRecord>("US", "2025")
        .Where(Expr.Prop("Amount") > 100)
        .Section(0, 20)
);
```

For `[Table("Sales_{0}_{1}")]`, `"US"` replaces `{0}` and `"2025"` replaces `{1}`.

This is clearer than passing a single concatenated string such as `"US_2025"`, and it makes region, year, and other dimensions easier to reuse independently at the call site.

### 6.5 Let Different Tables Use Different Placeholder Positions

Different tables can also share the same `TableArgs` array while consuming different placeholder positions. For example:

```csharp
[Table("Table1_{0}")]
public class Table1Row
{
}

[Table("Table2_{1}")]
public class Table2Row
{
}
```

Pass the argument array only once on the main table:

```csharp
var args = new[] { "TenantA", "202501" };

var expr = Expr.From<Table1Row>(args)
    // Table2Row in the same scope or a child scope keeps using args
    // unless it explicitly sets its own TableArgs.
    .Where(Expr.Exists<Table2Row>(t => true));
```

Then:

- `Table1_{0}` uses `args[0]`, so the resolved table name is `Table1_TenantA`
- `Table2_{1}` uses `args[1]`, so the resolved table name is `Table2_202501`

In other words, **one array** can feed **different tables** with different parameters, and each table only consumes the placeholder positions it references. This is especially useful for combinations such as `tenant + month` or `business-line + region`.

## 7. TableArgs Priority and Inheritance

| Source | Priority | Description |
| --- | --- | --- |
| `IArged.TableArgs` | Automatic | Entity implements interface, auto-used on insert/update |
| `tableArgs` parameter / `WithArgs` | Explicit | Explicit on query, overrides IArged |

For query chains, keep this additional rule in mind:

- **Main table first, later tables inherit**: `TableArgs` determined by the main table propagate to tables in the same scope and in child scopes.
- **Local explicit values win**: if a later table explicitly sets its own `TableArgs`, those values override the inherited ones.

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
