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

### 3.1 `Orders_{0}`：按用户尾号分表

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

这类写法适合“**同一个数据源里有多张物理表**”的场景，例如：

- `Orders_0`
- `Orders_1`
- ...
- `Orders_9`

写入时框架会读取 `IArged.TableArgs`，把 `UserId = 25` 路由到 `Orders_5`；查询时可以通过 `tableArgs: new[] { "5" }` 或 `WithArgs("5")` 显式指定分表。

### 3.2 `{0}.Orders`：按用户路由到不同库/Schema 下的同名表

占位符不一定只能写在表名后缀里，也可以出现在 `库名.表名` 或 `Schema.表名` 的位置：

```csharp
[Table("{0}.Orders")]
public class UserOrder : IArged
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("UserId")]
    public long UserId { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }

    string[] IArged.TableArgs => new[] { $"UserShard_{UserId % 4}" };
}
```

当 `UserId = 25` 时，`TableArgs[0] = "UserShard_1"`，最终 SQL 标识符会变成：

```sql
UserShard_1.Orders
```

这种模式仍然属于 **TableArgs 路由**，只是把占位符放到了“库/Schema 名称”位置。它适合以下场景：

1. 同一个连接已经可以访问多个库 / Schema。
2. 各分片中的表结构相同，只是前缀库名或 Schema 名不同。
3. 你希望继续沿用 `IArged`、`tableArgs`、`WithArgs(...)`、`Expr.From<T>(...)` 这一整套动态路由方式。

> **注意**：`{0}.Orders` 依赖目标数据库支持当前连接跨库 / 跨 Schema 访问。  
> 如果不同分片必须使用完全不同的连接字符串，应优先使用后文的 `DataSource` 方案，而不是把库名直接写进 `TableArgs`。

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

> **安全提示**：`TableExpr` 上显式指定的 `TableArgs` 会覆盖当前 `SqlBuildContext` 里继承下来的分表参数。  
> 如果你的上层上下文本来依赖这组参数来限制租户、分片或数据范围，那么下层 `TableExpr` 的显式覆盖可能绕开原有边界，使用时要特别注意避免数据越界访问。

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

如果把这种“显式覆盖”放到更深层的 `TableExpr`、子查询或关联表达式里，也会覆盖当前上下文继承值；在多租户或按业务范围隔离的系统里，务必确认这种覆盖是刻意为之。

## 6. 真实分表模式

### 6.1 在 Lambda 中直接指定 `TableArgs`

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

适合把分表参数放在调用层统一控制。

### 6.3 使用 `Expr.From<T>(...)` 指定分表

```csharp
using static LiteOrm.Common.Expr;
var sales = await salesService.SearchAsync(
    From<SalesRecordView>("202411")
        .Where(Prop("Amount") > 100)
        .OrderBy(("Amount", false))
        .Section(0, 3)
);
```

适合复杂查询、排序和分页组合使用。

### 6.4 利用不同占位符位置表达不同维度

```csharp
using static LiteOrm.Common.Expr;
var sales = await salesService.SearchAsync(
    From<SalesRecord>("US", "2025")
        .Where(Prop("Amount") > 100)
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
using static LiteOrm.Common.Expr;
var args = new[] { "TenantA", "202501" };

var expr = From<Table1Row>(args)
    // 同作用域或下级作用域里的 Table2Row 如果没有单独指定 TableArgs，
    // 会继续使用这组 args。
    .Where(Exists<Table2Row>(t => true));
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

## 8. `DataSource` 方式分库

`TableArgs` 解决的是“**同一个实体在运行时该落到哪个物理表 / 哪个库名占位符**”；  
`DataSource` 解决的是“**这个实体默认应该使用哪个连接配置**”。

### 8.1 固定绑定到某个数据源

```csharp
[Table("Orders", DataSource = "OrderDbEast")]
public class EastOrder : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }
}

[Table("Orders", DataSource = "OrderDbWest")]
public class WestOrder : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }
}
```

对应配置：

```json
{
  "LiteOrm": {
    "Default": "OrderDbEast",
    "DataSources": [
      {
        "Name": "OrderDbEast",
        "ConnectionString": "Server=east;Database=OrdersEast;...",
        "Provider": "MySqlConnector.MySqlConnection, MySqlConnector"
      },
      {
        "Name": "OrderDbWest",
        "ConnectionString": "Server=west;Database=OrdersWest;...",
        "Provider": "MySqlConnector.MySqlConnection, MySqlConnector"
      }
    ]
  }
}
```

这个方案的关键特点是：

- `DataSource` 是 **静态元数据**，写在 `[Table(..., DataSource = "...")]` 上。
- 同一个实体类型会固定走同一个数据源，不会像 `TableArgs` 那样按单条记录动态切换。
- 它更适合“按业务域、租户组、冷热库、历史库”这类**预先确定路由目标**的分库。

### 8.2 通过重写 DAO 的 `DataSource` 属性实现动态分库

如果你的分库目标是在运行时根据“当前用户 / 当前租户 / 当前请求上下文”决定的，那么仅靠实体上的 `[Table(DataSource = "...")]` 不够，因为它是静态元数据。

这时更合适的方式是：**在自定义 DAO 中重写 `DataSource` 属性**。

`DAOBase` 默认实现如下：

```csharp
protected virtual string DataSource => TableDefinition.DataSource;
```

你可以把它改成运行时返回：

```csharp
[AutoRegister(Lifetime.Scoped)]
public class UserOrderDAO : ObjectDAO<UserOrder>
{
    private readonly IUserContext _userContext;

    public UserOrderDAO(IUserContext userContext)
    {
        _userContext = userContext;
    }

    protected override string DataSource
        => $"OrderDb_{_userContext.UserId % 4}";
}
```

效果是：

1. 实体仍然映射同一个逻辑表 `Orders`。
2. DAO 在真正取连接时，会根据当前上下文动态返回 `OrderDb_0`、`OrderDb_1`、`OrderDb_2`、`OrderDb_3`。
3. `GetDaoContext()` 和 `SqlBuilder` 都会使用这个重写后的 `DataSource`。

这种方式尤其适合：

- 每个分库对应不同连接字符串；
- 路由规则依赖当前登录用户、租户、请求头或业务上下文；
- 你希望“表结构保持一致，但连接在 DAO 层动态切换”。

> **适用边界**：这是 **DAO 层** 的动态分库方案。  
> 如果你走的是通用 `EntityService<T>` / `IEntityService<T>`，通常需要再包一层自定义 Service / Factory，把请求导向对应 DAO。

### 8.3 `DataSource` + `TableArgs` 组合使用

如果一个业务本身就放在独立数据源里，同时还要在这个数据源内部继续分表，可以把两者组合：

```csharp
[Table("Logs_{0}", DataSource = "LogDB")]
public class Log : IArged
{
    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    string[] IArged.TableArgs => new[] { CreateTime.ToString("yyyyMM") };
}
```

这表示：

1. 先固定走 `LogDB` 连接。
2. 再在这个连接内部，根据 `TableArgs` 路由到 `Logs_202603`、`Logs_202604` 这类物理表。

### 8.4 与 `{0}.table` / `tableArgs` 的用法对比

| 方案 | 路由粒度 | 是否运行时动态 | 典型写法 | 更适合什么场景 |
| --- | --- | --- | --- | --- |
| `Orders_{0}` / `{0}.Orders` + `TableArgs` | 表名 / 库名占位符 | 是 | `[Table("Orders_{0}")]`、`[Table("{0}.Orders")]` | 同连接下按用户、月份、地区等维度动态路由 |
| `[Table(..., DataSource = "...")]` | 连接配置 | 否（按实体固定） | `[Table("Orders", DataSource = "OrderDbEast")]` | 按业务域、冷热库、已知租户组固定分库 |
| DAO 重写 `DataSource` | 连接配置 | 是 | `protected override string DataSource => ...` | 按当前用户 / 租户 / 请求上下文动态选库 |
| `DataSource` + `TableArgs` | 先选连接，再选物理表 | 半动态 | `[Table("Logs_{0}", DataSource = "LogDB")]` | 独立业务库内部继续分表 |

可以这样理解：

- **想在一次调用里按用户 / 时间动态选分片**：优先 `TableArgs`。
- **想让某个实体默认永远走某个连接**：优先 `DataSource`。
- **想按当前用户或租户在运行时动态切换连接**：优先自定义 DAO 并重写 `DataSource`。
- **想先选业务库，再在业务库中继续分表**：组合使用。

### 8.5 读写分离

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
- [关联查询](../02-core-usage/06-associations.md)
- [权限过滤](./06-permission-filtering.md)
- [性能优化](./03-performance.md)
- [表达式扩展](../04-extensibility/01-expression-extension.md)
