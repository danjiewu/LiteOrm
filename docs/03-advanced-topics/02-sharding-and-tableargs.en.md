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

### 3.1 `Orders_{0}`: shard by user suffix

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

This style fits scenarios where **one data source contains many physical tables**, for example:

- `Orders_0`
- `Orders_1`
- ...
- `Orders_9`

On insert, the framework reads `IArged.TableArgs` and routes `UserId = 25` to `Orders_5`. On query, you can explicitly pick the shard with `tableArgs: new[] { "5" }` or `WithArgs("5")`.

### 3.2 `{0}.Orders`: route by user into same-named tables under different databases/schemas

Placeholders are not limited to table suffixes. They can also appear in the `database.table` or `schema.table` position:

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

When `UserId = 25`, `TableArgs[0] = "UserShard_1"`, so the final SQL identifier becomes:

```sql
UserShard_1.Orders
```

This is still **TableArgs routing**. The only difference is that the placeholder is placed in the database/schema position. It works best when:

1. The same connection can already access multiple databases or schemas.
2. All shards share the same table structure.
3. You want to keep using the same dynamic routing APIs: `IArged`, `tableArgs`, `WithArgs(...)`, and `Expr.From<T>(...)`.

> **Important**: `{0}.Orders` depends on provider support for cross-database or cross-schema access on the current connection.  
> If each shard requires a completely different connection string, prefer the `DataSource` approach below instead of pushing the database name into `TableArgs`.

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

> **Security note**: explicit `TableArgs` on a `TableExpr` override the inherited shard arguments currently stored in `SqlBuildContext`.  
> If your upper scope relies on those arguments to enforce tenant, shard, or data-range boundaries, a lower-level `TableExpr` override can bypass that boundary. Use this carefully to avoid out-of-scope data access.

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

The same rule applies when the explicit override is placed deeper in a `TableExpr`, subquery, or association expression: it replaces the inherited context value. In multi-tenant or scoped-data systems, make sure that override is intentional.

## 6. Real-World Sharding Patterns

### 6.1 Directly Specify `TableArgs` in Lambda

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

Suitable for unified control of shard parameters at the caller layer.

### 6.3 Use `Expr.From<T>(...)` to Specify Shard

```csharp
using static LiteOrm.Common.Expr;
var sales = await salesService.SearchAsync(
    From<SalesRecordView>("202411")
        .Where(Prop("Amount") > 100)
        .OrderBy(("Amount", false))
        .Section(0, 3)
);
```

Suitable for combining complex queries, sorting, and pagination.

### 6.4 Use Different Placeholder Positions for Different Dimensions

```csharp
using static LiteOrm.Common.Expr;
var sales = await salesService.SearchAsync(
    From<SalesRecord>("US", "2025")
        .Where(Prop("Amount") > 100)
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
using static LiteOrm.Common.Expr;
var args = new[] { "TenantA", "202501" };

var expr = From<Table1Row>(args)
    // Table2Row in the same scope or a child scope keeps using args
    // unless it explicitly sets its own TableArgs.
    .Where(Exists<Table2Row>(t => true));
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

## 8. Database Routing with `DataSource`

`TableArgs` answers “**which physical table or placeholder-based database name should this operation hit at runtime?**”  
`DataSource` answers “**which configured connection should this entity use by default?**”

### 8.1 Bind an entity to a fixed data source

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

Configuration:

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

Key characteristics:

- `DataSource` is **static metadata** on `[Table(..., DataSource = "...")]`.
- One entity type is fixed to one configured connection.
- It is better for **predefined routing targets**, such as business domains, hot/cold storage, archive databases, or tenant groups that are known in advance.

### 8.2 Dynamic database routing by overriding DAO `DataSource`

If the target database must be chosen at runtime based on the current user, tenant, or request context, `[Table(DataSource = "...")]` is not enough because that metadata is static.

In that case, the better fit is to **override the `DataSource` property in a custom DAO**.

`DAOBase` provides this default implementation:

```csharp
protected virtual string DataSource => TableDefinition.DataSource;
```

You can replace it with a runtime decision:

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

What this means:

1. The entity still maps to the same logical table, such as `Orders`.
2. When the DAO actually acquires a connection, it dynamically returns `OrderDb_0`, `OrderDb_1`, `OrderDb_2`, or `OrderDb_3`.
3. Both `GetDaoContext()` and `SqlBuilder` follow the overridden `DataSource`.

This pattern is especially useful when:

- each shard has a different connection string;
- the routing rule depends on the current user, tenant, request header, or business context;
- the table shape stays the same, but the connection must switch dynamically inside the DAO layer.

> **Boundary**: this is a **DAO-layer** dynamic database-routing pattern.  
> If you are using the generic `EntityService<T>` / `IEntityService<T>` flow, you will usually wrap it with a custom service or factory that chooses the appropriate DAO first.

### 8.3 Combine `DataSource` with `TableArgs`

If a business area already lives in its own data source and still needs internal table sharding, combine both:

```csharp
[Table("Logs_{0}", DataSource = "LogDB")]
public class Log : IArged
{
    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    string[] IArged.TableArgs => new[] { CreateTime.ToString("yyyyMM") };
}
```

This means:

1. First choose the `LogDB` connection.
2. Then, inside that connection, route to `Logs_202603`, `Logs_202604`, and so on.

### 8.4 Comparison: `{0}.table` / `tableArgs` vs `DataSource`

| Approach | Routing granularity | Runtime-dynamic? | Typical form | Best fit |
| --- | --- | --- | --- | --- |
| `Orders_{0}` / `{0}.Orders` + `TableArgs` | table name / database-name placeholder | Yes | `[Table("Orders_{0}")]`, `[Table("{0}.Orders")]` | Per-user, per-month, per-region dynamic routing on the same accessible connection scope |
| `[Table(..., DataSource = "...")]` | configured connection | No (fixed per entity) | `[Table("Orders", DataSource = "OrderDbEast")]` | Fixed split by business domain, hot/cold storage, or known tenant groups |
| override DAO `DataSource` | configured connection | Yes | `protected override string DataSource => ...` | Per-user, per-tenant, or per-request dynamic connection selection |
| `DataSource` + `TableArgs` | choose connection first, then choose table | Partially | `[Table("Logs_{0}", DataSource = "LogDB")]` | A dedicated business database that still needs table sharding inside it |

As a rule of thumb:

- **Need per-call dynamic shard selection by user/time?** Prefer `TableArgs`.
- **Need one entity to always use one connection?** Prefer `DataSource`.
- **Need to switch connections dynamically by current user or tenant?** Prefer a custom DAO that overrides `DataSource`.
- **Need to choose a business database first and then shard inside it?** Combine them.

### 8.5 Read-Write Separation

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
- [Associations](../02-core-usage/06-associations.en.md)
- [Permission Filtering](./06-permission-filtering.en.md)
- [Performance Optimization](./03-performance.en.md)
- [Expression Extension](../04-extensibility/01-expression-extension.en.md)
