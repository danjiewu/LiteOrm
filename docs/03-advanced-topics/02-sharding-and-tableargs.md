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

### 2.4 按月分表的完整流程

```csharp
var log = new Log
{
    Level = "ERROR",
    Message = "Payment failed",
    CreateTime = new DateTime(2026, 3, 15)
};

// 写入时使用 IArged.TableArgs => Logs_202603
await logService.InsertAsync(log);

// 查询单个月分表
var marchLogs = await logService.SearchAsync(
    l => l.Level == "ERROR",
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

这里 `{0}`、`{1}` 分别对应 `TableArgs[0]`、`TableArgs[1]`，例如：

```csharp
var args = new[] { "US", "2025" };
// 对应表名：Sales_US_2025
```

把不同维度拆成不同位置占位符后，可以直接传递 `地区 + 年份` 这样的结构化参数，不必手动拼接 `"US_2025"` 之类的字符串。

## 5. 分表查询方式

Service 层通过 `SearchAsync` 的 `tableArgs` 参数指定分表；DAO 层通过 `WithArgs` 方法指定。

### 5.1 Service 查询分表

```csharp
// 通过 tableArgs 参数指定分表
var results = await salesService
    .SearchAsync(s => s.Amount > 1000, tableArgs: new[] { "US", "2025" });
```

### 5.2 DAO 查询分表

```csharp
// 通过 WithArgs 方法指定分表
var results = await salesViewDAO
    .WithArgs("US", "2025")
    .Search(s => s.Amount > 1000)
    .ToListAsync();
```

### 5.3 `TableArgs` 的传递性

`TableArgs` 不只是“当前这一张表”的参数，还具有作用域传递性：

1. 主表一旦指定了 `tableArgs`、`WithArgs(...)` 或 `Expr.From<T>(...)` 的参数，这组参数会进入当前 SQL 作用域。
2. 同作用域中的后续表，以及下级作用域中的子查询 / 关联查询，如果自己没有再显式指定 `TableArgs`，会继续使用主表传递下来的参数。
3. 如果后续表在自己的 `TableExpr` 或关联表达式上再次显式指定了 `TableArgs`，则以显式值覆盖继承值。

这意味着在“同一套分表维度贯穿整条查询链”的场景下，通常只需要在主表指定一次参数，后续表无需重复传参。

### 5.4 批量查询多个分表

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

### 5.5 `IArged` 与 `tableArgs` 覆盖示例

```csharp
var order = new Order
{
    UserId = 25
};

// 插入时自动走 Orders_5
await orderService.InsertAsync(order);

// 查询时显式指定 tableArgs，会覆盖自动推导结果
var archivedOrders = await orderService.SearchAsync(
    o => o.UserId == 25,
    tableArgs: new[] { "archive_5" }
);
```

## 6. 来自 Demo 和测试的真实分表模式

### 6.1 在 Lambda 中直接指定 `TableArgs`

这个写法来自 `LiteOrm.Demo\Demos\ShardingQueryDemo.cs`：

```csharp
var sales = await salesService.SearchAsync(s =>
    s.TableArgs == new[] { "202412" } && s.Amount > 40
);
```

适合“查询固定月份或固定分片”的快速写法。

### 6.2 显式传入 `tableArgs`

```csharp
var sales = await salesService.SearchAsync(
    s => s.Amount > 100,
    tableArgs: new[] { "202411" }
);
```

这个模式和测试中的 `CountAsync(..., tableArgs: ...)` 一样，适合把分表参数放在调用层统一控制。

### 6.3 使用 `Expr.From<T>(...)` 指定分表

```csharp
var sales = await salesService.SearchAsync(
    Expr.From<SalesRecordView>("202411")
        .Where(Expr.Prop("Amount") > 100)
        .OrderBy(("Amount", false))
        .Section(0, 3)
);
```

这个模式同样来自 Demo，适合复杂查询、排序和分页组合使用。

### 6.4 利用不同占位符位置表达不同维度

```csharp
var sales = await salesService.SearchAsync(
    Expr.From<SalesRecord>("US", "2025")
        .Where(Expr.Prop("Amount") > 100)
        .Section(0, 20)
);
```

对于 `[Table("Sales_{0}_{1}")]` 这类表名，`"US"` 会替换 `{0}`，`"2025"` 会替换 `{1}`。

这种写法比手动传 `"US_2025"` 更清晰，也更方便在调用层分别复用地区、年份等维度参数。

### 6.5 不同表错开使用不同占位符

不同表也可以共享同一组 `TableArgs`，但各自使用不同的占位符位置。例如：

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

查询主表时只传一次参数数组：

```csharp
var args = new[] { "TenantA", "202501" };

var expr = Expr.From<Table1Row>(args)
    // 同作用域或下级作用域里的 Table2Row 如果没有单独指定 TableArgs，
    // 会继续使用这组 args。
    .Where(Expr.Exists<Table2Row>(t => true));
```

这时：

- `Table1_{0}` 会使用 `args[0]`，实际表名为 `Table1_TenantA`
- `Table2_{1}` 会使用 `args[1]`，实际表名为 `Table2_202501`

也就是说，可以用**一个数组**给**不同表**传不同参数，每张表只消费自己占位符引用到的位置。这在“租户 + 月份”“业务线 + 区域”等组合场景里能明显减少重复传参代码。

## 7. TableArgs 优先级与继承规则

| 来源                          | 优先级 | 说明                |
| --------------------------- | --- | ----------------- |
| `IArged.TableArgs`          | 自动  | 实体实现接口，插入/更新时自动使用 |
| `tableArgs` 参数 / `WithArgs` | 显式  | 查询时显式指定，覆盖 IArged |

对查询链路来说，还可以再记住下面这条规则：

- **主表先定，后续继承**：主表确定的 `TableArgs` 会传递给同作用域和下级作用域。
- **局部显式优先**：后续表如果单独指定了 `TableArgs`，就以它自己的参数为准。

> **注意**：LiteOrm 并不能自动知道哪些分表存在，跨分表查询需要在应用层遍历可能的分表并合并结果。

## 8. 分库场景

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

## 9. 注意事项

1. **分表键选择**：选择均匀分布的键，避免热点分表
2. **分表数量**：考虑未来扩展，预留足够数量
3. **跨分表查询**：应用层处理合并结果
4. **IArged 实现**：确保 `TableArgs` 在插入前已正确赋值

## 相关链接

- [返回目录](../README.md)
- [关联查询](../02-core-usage/05-associations.md)
- [性能优化](./03-performance.md)
- [表达式扩展](../04-extensibility/01-expression-extension.md)
