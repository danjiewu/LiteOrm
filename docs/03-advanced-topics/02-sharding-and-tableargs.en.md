# Sharding and `TableArgs`

LiteOrm uses `IArged` and `TableArgs` to fill placeholders in table names at runtime. This supports month-based tables, user-bucket tables, and other deterministic sharding patterns.

## 1. `IArged`

```csharp
public interface IArged
{
    string[] TableArgs { get; }
}
```

If an entity implements `IArged`, LiteOrm can infer the target shard during writes.

## 2. Time-based sharding

```csharp
[Table("Logs_{0}")]
public class Log : IArged
{
    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    string[] IArged.TableArgs => new[] { CreateTime.ToString("yyyyMM") };
}
```

Explicit shard selection during queries:

```csharp
var marchLogs = await logService.SearchAsync(
    l => l.Level == "ERROR",
    tableArgs: new[] { "202603" }
);
```

## 3. Service, DAO, and `Expr` styles

### Service API

```csharp
var results = await salesService.SearchAsync(
    s => s.Amount > 1000,
    tableArgs: new[] { "US", "2025" }
);
```

### DAO API

```csharp
var results = await salesViewDAO
    .WithArgs("US", "2025")
    .Search(s => s.Amount > 1000)
    .ToListAsync();
```

### `Expr.From<T>(...)`

```csharp
var sales = await salesService.SearchAsync(
    Expr.From<SalesRecordView>("202411")
        .Where(Expr.Prop("Amount") > 100)
        .OrderBy(("Amount", false))
        .Section(0, 3)
);
```

## 4. Multi-dimensional shard keys

`[Table("Sales_{0}_{1}")]` is valid when the model needs more than one placeholder. The order of values in `TableArgs` must match the placeholder order in the table name.

## 5. Precedence rules

| Source | Typical use | Precedence |
|------|-------------|------------|
| `IArged.TableArgs` | inferred shard for insert/update | automatic |
| explicit `tableArgs` or `WithArgs(...)` | query-time override | higher |

Explicit shard arguments override the value that would otherwise come from `IArged`.

## 6. Multi-data-source scenarios

Sharding and data-source routing can be combined:

```csharp
[Table("Logs_{0}", DataSource = "LogDB")]
public class Log : IArged
{
}
```

You can also pair this with `ReadOnlyConfigs` when shard reads may use replicas.

## 7. Practical limits

- LiteOrm does not discover shard tables automatically.
- Cross-shard queries are usually implemented by iterating candidate shards and merging results in application code.
- Choose shard keys that distribute load evenly and remain stable over time.

## Related Links

- [Back to English docs hub](../SUMMARY.en.md)
- [Associations](../02-core-usage/05-associations.en.md)
- [Performance](./03-performance.en.md)
- [Expression Extension](../04-extensibility/01-expression-extension.en.md)
