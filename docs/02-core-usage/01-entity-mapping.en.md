# Entity Mapping and Data Sources

Entities define how LiteOrm maps CLR types to tables, columns, data sources, and sharded table names.

## 1. Basic entity shape

```csharp
[Table("Users")]
public class User
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserName")]
    public string? UserName { get; set; }

    [Column("Age")]
    public int Age { get; set; }
}
```

`ObjectBase` is optional. Use it only when your project benefits from the shared base behavior.

## 2. `[Table]` metadata

```csharp
[Table("Users")]
[Table("Logs_{0}", DataSource = "LogDB")]
```

| Property | Meaning |
|------|---------|
| table name | Physical table name |
| `DataSource` | Named data source for this entity |
| placeholder segments like `{0}` | Slots later filled by `TableArgs` |

## 3. `[Column]` metadata

```csharp
[Column("Id", IsPrimaryKey = true, IsIdentity = true)]
[Column("Profile", DataType = typeof(UserProfile))]
```

Use `DataType` when a property needs serialization into a database column.

## 4. Multi-data-source mapping

```csharp
[Table("Orders", DataSource = "OrderDb")]
public class Order
{
}
```

This keeps the mapping decision in the model instead of repeating it in every query.

## 5. Sharding with `IArged` and `TableArgs`

```csharp
[Table("Logs_{0}")]
public class Log : IArged
{
    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    string[] IArged.TableArgs => new[] { CreateTime.ToString("yyyyMM") };
}
```

When a query or write call provides explicit `TableArgs`, that explicit value takes precedence over the value inferred from `IArged`.

## 6. Modeling advice

- Keep entities focused on persistence mapping.
- Put relationship projection on view models, not on base entities.
- Define primary keys and identity columns up front.
- Decide data source and sharding strategy early so `EntityService`, `ObjectDAO`, and `ObjectViewDAO` all follow the same model rules.

## Related Links

- [Back to English docs hub](../SUMMARY.en.md)
- [View Models and Services](./02-view-models-and-services.en.md)
- [Associations](./05-associations.en.md)
- [Glossary](../05-reference/03-glossary.en.md)
