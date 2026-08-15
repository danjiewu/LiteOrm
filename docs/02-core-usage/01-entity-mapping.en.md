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
[Table("Logs", SyncTable = SyncTableMode.Always)]
```

| Parameter | Description |
|-----------|-------------|
| `Name` | Database table name, supports placeholder for sharding. |
| `DataSource` | Specifies the data source for the current entity. |
| `SyncTable` | Entity-level table-structure sync mode, enum `SyncTableMode` (`Default` / `Never` / `Always`), defaults to `Default`. When set to `Never` or `Always`, it overrides the data-source-level `SyncTable` config. |

## `[Column]` Attribute

```csharp
[Column("Id", IsPrimaryKey = true, IsIdentity = true)]
[Column("Age", DbType = DbValueType.Int32)]
[Column("Tags", DbType = DbValueType.Array)]      // PostgreSQL text[]
[Column("Meta", DbType = DbValueType.Jsonb)]      // PostgreSQL jsonb
```

> `DbType` is of the `DbValueType` enum type, defaulting to `DbValueType.Default` (meaning "not specified — inferred from the property type").

| Parameter | Description |
|-----------|-------------|
| `ColumnName` | Database column name (positional constructor parameter). |
| `IsPrimaryKey` | Whether it is a primary key. |
| `IsIdentity` | Whether it is an identity column. |
| `IdentityStart` | Identity column start value, default `1`. Only takes effect on databases that support start value (SQL Server, Dameng, Oracle); MySQL via table-level `AUTO_INCREMENT = n` option; SQLite does not support customization. |
| `IdentityIncreasement` | Identity column increment value, default `1`. Only takes effect on databases that support increment (SQL Server, Dameng, Oracle); MySQL requires session variable `auto_increment_increment`; SQLite does not support customization. |
| `DbType` | Database column type (`DbValueType` enum), defaults to `DbValueType.Default` (inferred from the property type). `Json`/`Jsonb` denote JSON/JSONB columns, and `Array` denotes an array column. |
| `Expression` | Computed column expression (non-actual column); reference other properties of the same entity via `{PropertyName}`, or write a dialect-specific raw SQL fragment. |
| `ColumnMode` | Column operation mode (`ColumnMode` enum), defaults to `Full`. Set to `ColumnMode.Computed` for computed columns. |

### Array Columns (PostgreSQL)

Collection-typed properties (`int[]`, `string[]`, `List<T>`, etc.) are inferred as `DbValueType.Array` when no `DbType` is specified. Native-array dialects such as PostgreSQL emit array column types (`integer[]`, `text[]`); other dialects fall back to text-JSON storage:

```csharp
[Table("Products")]
public class Product
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Tags")]
    public string[]? Tags { get; set; }   // inferred as DbValueType.Array → PostgreSQL text[]

    [Column("Scores")]
    public int[]? Scores { get; set; }    // → PostgreSQL integer[]
}
```

### JSON / JSONB Columns

```csharp
[Table("Products")]
public class Product
{
    [Column("Meta", DbType = DbValueType.Json)]     // text JSON (portable across databases)
    public string? Meta { get; set; }

    [Column("Attributes", DbType = DbValueType.Jsonb)]  // PostgreSQL jsonb
    public string? Attributes { get; set; }
}
```

> JSON columns serialize complex objects to JSON strings on write and deserialize them back to the property type on read.

### Computed Columns (Non-Actual Columns)

A computed column does not create a physical database column and is excluded from inserts/updates; queries return the value via `Expression`, and references to the property in query conditions also render the expression. Within the expression, use `{PropertyName}` to reference other properties of the same entity — placeholders are rendered as column names (with the required quoting and table qualification). You may also write a dialect-specific raw SQL fragment.

```csharp
[Table("Users")]
public class User
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("FirstName")]
    public string? FirstName { get; set; }

    [Column("LastName")]
    public string? LastName { get; set; }

    // Computed column: no physical column; SELECT returns (FirstName || ' ' || LastName), WHERE renders the expression too
    [Column("FullName", Expression = "{FirstName} || ' ' || {LastName}", ColumnMode = ColumnMode.Computed)]
    public string? FullName { get; set; }
}
```

- **No physical column**: skipped by `CREATE TABLE` / `ALTER TABLE ADD COLUMN`, and not written on insert/update.
- **Expression result**: the default SELECT renders `({expr}) AS "PropertyName"` and the read result is mapped back to the property.
- **Query conditions**: `SearchAsync(u => u.FullName == "John Smith")` produces `WHERE ("FirstName" || ' ' || "LastName") = @0`.
- Setting `Expression` alone (without `ColumnMode.Computed`) is also treated as a computed column; declaring `ColumnMode = ColumnMode.Computed` is recommended.
- The expression is dialect-specific (the example uses SQLite/PostgreSQL `||`; MySQL uses `CONCAT(...)`).

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
- [Associations](./08-associations.en.md)
- [Glossary](../05-reference/03-glossary.en.md)

