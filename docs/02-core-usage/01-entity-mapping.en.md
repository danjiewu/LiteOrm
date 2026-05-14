# Entity Mapping and Data Sources

Entity classes are the mapping foundation between LiteOrm and database tables. This article introduces core rules for entity definition, table-column mapping, multiple data sources, and sharding parameters.

## Basic Entity Structure

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

    [Column("DeptId")]
    public int? DeptId { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }
}
```

> `ObjectBase` is an optional base class. You can use LiteOrm perfectly fine without inheriting from it.

## `[Table]` Attribute

```csharp
[Table("Users")]
[Table("Logs_{0}", DataSource = "LogDB")]
```

| Parameter | Description |
|-----------|-------------|
| `Name` | Database table name, supports placeholder for sharding. |
| `DataSource` | Specifies the data source for the current entity. |

## `[Column]` Attribute

```csharp
[Column("Id", IsPrimaryKey = true, IsIdentity = true)]
[Column("Profile", DataType = typeof(UserProfile))]
```

| Parameter | Description |
|-----------|-------------|
| `Name` | Database column name. |
| `IsPrimaryKey` | Whether it is a primary key. |
| `IsIdentity` | Whether it is an identity column. |
| `DataType` | Serialization type, used for complex object storage. |

## `[PropertyOrder]` Attribute

Controls the ordering of entity properties in database operations (e.g., table creation, SQL column list generation).

```csharp
[Table("Users")]
public class User
{
    [PropertyOrder(1)]
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [PropertyOrder(2)]
    [Column("UserName")]
    public string? UserName { get; set; }

    [PropertyOrder(After = nameof(DeptId))]
    [Column("Age")]
    public int Age { get; set; }

    [PropertyOrder(0)]
    [Column("DeptId")]
    public int? DeptId { get; set; }
}
```

| Parameter | Description |
|-----------|-------------|
| `Order` | Sort priority. Lower values come first. Default is 0. Within the same topological level, properties with smaller Order values are placed first. |
| `After` | Specifies a property name; indicates the current property should be placed after it. |
| `Before` | Specifies a property name; indicates the current property should be placed before it. |

> **Sorting Rule**: Properties are first sorted by Before/After topological dependencies, then within the same level by Order value ascending, and finally by original declaration order. An `InvalidOperationException` is thrown when circular dependencies are detected.

## Multi-DataSource Mapping

If there are multiple data sources in the project, you can explicitly mark them on the entity:

```csharp
[Table("Orders", DataSource = "OrderDb")]
public class Order
{
}
```

This way, all default read/write operations for this entity will use the `OrderDb` data source.

## Sharding Parameters and `IArged`

When the table name contains placeholders, you can provide dynamic sharding parameters via `IArged`:

```csharp
[Table("Logs_{0}")]
public class Log : IArged
{
    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    string[] IArged.TableArgs => new[] { CreateTime.ToString("yyyyMM") };
}
```

For more details, see [Sharding and TableArgs](../03-advanced-topics/02-sharding-and-tableargs.en.md).

## Modeling Recommendations

- Keep entities simple; avoid cramming too much business logic into entities.
- Metadata like primary keys, identity columns, and data sources should be clearly defined at the model layer from the start.
- For fields that need association queries, prefer using view models; don't pollute basic entities.
- When dealing with cross-database or legacy database compatibility, confirm the corresponding dialect behavior in advance.

## Related Links

- [Back to docs hub](../README.md)
- [View Models and Services](./02-view-models-and-services.en.md)
- [Associations](./05-associations.en.md)
- [Glossary](../05-reference/03-glossary.en.md)
