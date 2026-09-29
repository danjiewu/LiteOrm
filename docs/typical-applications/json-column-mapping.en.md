# JSON Columns for Objects and Field-Level Queries

Some fields resist being split into columns: the channel and membership level captured at checkout, free-form product parameters, raw payloads from third-party callbacks. Giving each field its own column means a schema change every time one is added. The usual answer is to serialize the whole structure to JSON, keep it in a single text column, and read individual fields by JSON path when filtering.

LiteOrm splits that work in two: a value converter handles storing and loading the whole JSON blob, and a Lambda member handler handles the SQL expression that reads a single field.

| Goal | Approach |
| --- | --- |
| Store and load the whole JSON blob | Register an "object ↔ JSON text" converter and declare how the type is stored |
| Also filter or sort by fields inside the object | Register a Lambda member fallback for the class, mapped to the `JsonValue` function |
| Field structure is fluid and no class is wanted | Type the property as `JsonNode` and use the built-in indexer plus `GetValue<T>()`; see [Data Mapping](../advanced-topics/data-mapping.en.md#33-jsonnode-mapping-navigation) |

Three requirements follow: storing the blob, filtering by field, and matching a whole object. Every SQL snippet is what actually renders; the dialect is labelled in each one.

## Requirement 1: keep order extension data in one column

**Need**: the `Orders` table has to hold extension data such as channel, membership level, and tags, and more fields will keep arriving; adding a column for each is not worth it.

**Approach** has two steps. First, an ordinary class:

```csharp
using System.Text.Json;

/// <summary>Order extension data, stored as a JSON blob in the Settings column.</summary>
public class OrderSettings
{
    public string? Channel { get; set; }

    public int Level { get; set; }

    public List<string>? Tags { get; set; }
}
```

Then register the storage form and the read/write conversion once, at startup:

```csharp
using LiteOrm;
using LiteOrm.Common;

// Declare the storage form: OrderSettings is treated as JSON in the database
DbValueTypeMap.Set(typeof(OrderSettings), DbValueType.Json);

SqlBuilder.Instance.RegisterDbValueConverter<SqlBuilder, string, OrderSettings>(
    targetType: DbValueType.Json,
    fromDb: json => JsonSerializer.Deserialize<OrderSettings>(json)!,   // read: stored text back to an object
    toDb: value => JsonSerializer.Serialize(value)                      // write: object to stored text
);
```

The entity then declares only the property type:

```csharp
[Table("Orders")]
public class Order
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("OrderNo", AllowNull = true)]
    public string? OrderNo { get; set; }

    // The type is declared through DbValueTypeMap, so the column needs no DbType
    [Column("Settings")]
    public OrderSettings? Settings { get; set; }
}
```

The column is created as a JSON column; each dialect's column type:

| Database | `Settings` column type |
| --- | --- |
| SQLite / SQL Server | `TEXT` |
| MySQL / PostgreSQL | `JSON` |
| Oracle | `CLOB` |

Writing and reading look exactly like any other property:

```csharp
await orderService.InsertAsync(new Order
{
    OrderNo = "SO-1",
    Settings = new OrderSettings { Channel = "APP", Level = 5 }
});
```

```json
// the stored Settings column
{"Channel":"APP","Level":5,"Tags":null}
```

```csharp
var order = await orderService.Search(o => o.OrderNo == "SO-1").FirstOrDefaultAsync();

// already an object on the way out
Console.WriteLine(order!.Settings!.Level);   // 5
```

That is the global-registration route. When only one column needs JSON storage and the global registration is not wanted, implement the conversion as a class deriving from `IDbValueConverter<string, OrderSettings>` and attach it with `ConverterType`; the column still carries `DbType = DbValueType.Json`:

```csharp
using System.Text.Json;
using LiteOrm.Common;

/// <summary>Two-way converter between OrderSettings and JSON text.</summary>
public sealed class OrderSettingsJsonConverter : IDbValueConverter<string, OrderSettings>
{
    Type IDbValueConverter.ValueType => typeof(OrderSettings);

    // Generic versions on the JIT path, non-generic versions on the source-generated (AOT) path
    DbConvertHandler<string, OrderSettings>? IDbValueConverter<string, OrderSettings>.DbReadConverter
        => json => JsonSerializer.Deserialize<OrderSettings>(json)!;

    DbConvertHandler<OrderSettings, object>? IDbValueConverter<string, OrderSettings>.DbWriteConverter
        => value => JsonSerializer.Serialize(value);

    DbConvertHandler? IDbValueConverter.DbReadConverter
        => json => JsonSerializer.Deserialize<OrderSettings>((string)json)!;

    DbConvertHandler? IDbValueConverter.DbWriteConverter
        => value => JsonSerializer.Serialize((OrderSettings)value);
}
```

```csharp
[Column("Settings", DbType = DbValueType.Json, ConverterType = typeof(OrderSettingsJsonConverter))]
public OrderSettings? Settings { get; set; }
```

A column-level converter wins over everything else and masks the global registration. It needs a public parameterless constructor: the framework instantiates it with `Activator.CreateInstance` while resolving column metadata, so components that need injected configuration or keys must use static initialization or environment variables instead. Under AOT these columns must declare `ConverterType` explicitly, since the source generator only emits a read mapping for a column when it sees it, and the global registration alone is not enough; see Option 1 in [Sensitive Data Protection](./sensitive-data-protection.en.md) for the full form.

Key points:

- Registering the converter alone is not enough; the framework also has to know the type is stored as JSON. Use either `DbValueTypeMap.Set(typeof(T), DbValueType.Json)` or `DbType = DbValueType.Json` on the column; with neither, the type falls back to `Object`, the converter is never found, and reads hand back the raw text.
- `TDbType` in `RegisterDbValueConverter` is `string`. `DbValueType.Json` reaches the database as text, the driver reads it with `GetString`, and the converter receives that string.
- The converter never sees null. A null property is sent as `DBNull.Value` uniformly, and reads come back as null or `default`.
- `DbValueTypeMap.Set` is process-wide: once registered, every `OrderSettings` property in the project is treated as JSON, including naked value parameters in `Expr` conditions, which consult the same registry. To touch a single column, use the column-level converter above.
- A deserialization failure throws. With dirty data already in the table, wrap the converter body in `try`, return a default and log, so one bad row cannot take down a whole query.

## Requirement 2: filter by a field inside the object

**Need**: the list page filters orders by "channel = APP" and sorts by membership level, but the level sits inside JSON text where SQL `=` and `>` cannot reach it.

**Approach**: register Lambda member handlers for the class's properties; one call for the type covers them all. Unregistered, `o.Settings!.Channel` is read as a method call, the property name standing in for a function name, and the SQL does not run:

```sql
-- before registration
WHERE Channel("Settings") = 'APP'
```

After registration the member access becomes a JSON accessor function. Passing the type alone installs a member fallback for the whole class, with the path built from the member name inside the handler:

```csharp
using LiteOrm.Common;
using static LiteOrm.Common.Expr;

// Install a member fallback for OrderSettings; the handler builds the JSON path from node.Member.Name
LambdaExprConverter.RegisterMemberHandler(typeof(OrderSettings), handler: (node, converter) =>
    new FunctionExpr(
        "JsonValue",
        converter.Convert(node.Expression!).AsValue(),   // node.Expression is the container, here the Settings column
        Const("$." + node.Member.Name)));                 // JSON path, wrapped in Const so it inlines as a literal
```

The path comes from `node.Member.Name`, matching the keys `JsonSerializer` writes by default. Configure a naming policy (`PropertyNamingPolicy = JsonNamingPolicy.CamelCase`) and the path has to run the member name through the same policy; one letter apart and the query returns nothing.

Passing the type alone installs a **type-level fallback**: it does not enumerate members at registration time, and instead takes over when a member access on that type is resolved. For a member that needs a different path or extra handling, register it separately; an explicitly registered member takes precedence over the type-level fallback, regardless of the order the two are registered in:

```csharp
// Register separately when the stored key differs from the property name (camelCase, for instance);
// it takes precedence over the type-level fallback above
LambdaExprConverter.RegisterMemberHandler(typeof(OrderSettings), nameof(OrderSettings.Channel),
    (node, converter) => new FunctionExpr(
        "JsonValue",
        converter.Convert(node.Expression!).AsValue(),
        Const("$.channel")));
```

From then on the field is used like any other property:

```csharp
var appOrders = await orderService.SearchAsync(
    o => o.Settings!.Channel == "APP" && o.Settings!.Level > 3, cancellationToken: ct);
```

The same condition lands on each dialect's own JSON function:

```sql
-- SQLite
WHERE json_extract("T0"."Settings", '$.Channel') = 'APP'
-- MySQL
WHERE JSON_UNQUOTE(JSON_EXTRACT(`T0`.`Settings`, '$.Channel')) = 'APP'
-- SQL Server / Oracle
WHERE JSON_VALUE("T0"."Settings", '$.Channel') = 'APP'
-- PostgreSQL
WHERE "t0"."settings" ->> '$.Channel' = 'APP'
```

Sorting and projection work too. Folding "column + path" into a small helper keeps the path inline as a literal:

```csharp
using static LiteOrm.Common.Expr;

static FunctionExpr JsonField(string column, string path)
    => new FunctionExpr("JsonValue", Prop(column), Const(path));

var query = Expr.From<Order>()
    .Where(JsonField("Settings", "$.Level") > 3)
    .OrderBy(JsonField("Settings", "$.Channel").Asc())
    .Select(Prop("OrderNo"), JsonField("Settings", "$.Channel"));
```

```sql
SELECT "T0"."OrderNo", json_extract("T0"."Settings", '$.Channel')
FROM "Orders" "T0"
WHERE json_extract("T0"."Settings", '$.Level') > @0
ORDER BY json_extract("T0"."Settings", '$.Channel')
```

Key points:

- Handlers are registered per "type + member name" or per "type". Passing the type alone installs a type-level fallback that takes over when a member access on that type is resolved, symmetric with `RegisterMethodHandler(typeof(SomeType))`; an explicitly registered member (by type or by member name) wins, so change a single member by registering it on its own. Registering by member name alone takes over same-named members of every other type as well; do not do that.
- The type-level fallback applies to collection members too, so `o.Settings!.Tags!.Contains("vip")` renders as `'vip' IN json_extract("Settings", '$.Tags')`, and the SQL is meaningless. Filtering by array contents needs a separate custom function per dialect, registered over that member.
- Handlers only act on members of nested objects. When the container is the Lambda's root parameter (`o => o.OrderNo`), the column path handles it one step earlier, so registering a same-named member handler on the entity type never fires.
- Registration must be finished before the first conversion. The registry is static, so changing it while the process runs makes the same query render different SQL before and after, and a registered member also stops taking part in constant folding.
- Use `Expr.Const("$.Channel")` for the path. The built-in `JsonValue` and `JsonExtract` extension methods treat the path as an ordinary value and produce a bound parameter; SQLite and MySQL accept that, while `JSON_VALUE` on SQL Server and Oracle only accepts a string literal. For cross-database code, build the `FunctionExpr` yourself.
- One level of member access maps to one path segment. For two-level nesting such as `o.Settings.Profile.Level`, write the full path, `Const("$.Profile.Level")`; registering a handler on the intermediate type as well produces nested `JSON_VALUE(JSON_VALUE(...), ...)`, which may not be what you want.
- Numeric fields come back as text (on MySQL, the result of `JSON_UNQUOTE`), so comparisons against numbers rely on each database's implicit conversion, which is enough in practice. To convert explicitly, `.Cast(DbValueType.Int32)` can be applied; each dialect renders the type name it accepts, and on MySQL that is `CAST(... AS SIGNED)`.

## Requirement 3: match a whole object

**Need**: a partner posts back a complete configuration and you have to tell whether the same one is already stored.

**Approach**: compare the property directly; the parameter is serialized to JSON text through the globally registered converter:

```csharp
var exists = await orderService.SearchOneAsync(
    o => o.Settings == new OrderSettings { Channel = "APP", Level = 5, Tags = null });
```

```sql
WHERE "Settings" = @0
-- @0 = {"Channel":"APP","Level":5,"Tags":null}
```

Key points:

- The comparison is textual, so the serialized forms must line up: property order, nulls, number formatting. The `Tags = null` above has to be written out; leaving it off drops a field and the row no longer matches.
- This path goes through naked value parameters: when a parameter's value type is `Default`, the framework resolves the converter by the value's runtime type and only reaches the registered one once `DbValueTypeMap` maps it to JSON. It therefore depends on the global registration from [Requirement 1](#requirement-1-keep-order-extension-data-in-one-column) and does not work with a column-level `ConverterType`.
- Switching the serializer to `Newtonsoft.Json` may change property order and make this form unreliable; use a primary key or field-level conditions instead.

## When not to reach for it

- **Hot filtering or sorting**: every condition parses JSON and cannot use a plain index, which shows at volume. Persist the frequently queried fields as physical columns; MySQL can index a generated column, but that is database-side work.
- **Fields that must join or aggregate across tables**: a JSON path cannot sit in `ON` the way a plain column can. PostgreSQL can index a `jsonb` column with GIN; other databases have no equivalent.
- **Fields that need strong validation**: a JSON column carries no type constraints, so a misspelled property or path surfaces only when a query fails. Stable, constrained fields belong in columns.
- **Range searches over a field**: the extracted value is text, so range comparison relies on each dialect's implicit conversion and behaves differently across databases, which makes such fields poor candidates for JSON storage.

## Related links

- [Back to index](../README.md)
- [Data Mapping and Value Conversion](../advanced-topics/data-mapping.en.md)
- [Lambda Query Guide](../core-usage/lambda-guide.en.md)
- [Expression Extension](../extensibility/expression-extension.en.md)
- [Sensitive Data Protection](./sensitive-data-protection.en.md)
- [AOT Support](../advanced-topics/aot.en.md)
