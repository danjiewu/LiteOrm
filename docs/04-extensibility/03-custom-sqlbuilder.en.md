# Custom SqlBuilder / Dialect Extension

When the default database dialect is insufficient to cover the target database version or special SQL behavior, you can extend LiteOrm by creating a custom `SqlBuilder`.

## When to Customize SqlBuilder

- The older database version doesn't support the default paging syntax.
- The target database's function names, paging expressions, or SQL fragments are inconsistent with the default implementation.
- You want to uniformly encapsulate compatibility logic for a specific database version.

## Common Extension Points

| Extension Point | Purpose |
|----------------|---------|
| `BuildSelectSql` | Customize the final assembly of query statements. |
| `RegisterFunctionSqlHandler` | Register custom SQL function translations. |
| DataSource-level `RegisterSqlBuilder(...)` | Bind custom dialect to a specified data source. |

## Oracle 11g Paging Example

Oracle 11g doesn't support modern `LIMIT/OFFSET` syntax. You can inherit from `OracleBuilder` and override the paging logic:

```csharp
public class Oracle11gBuilder : OracleBuilder
{
    public readonly static new Oracle11gBuilder Instance = new Oracle11gBuilder();

    public override void BuildSelectSql(ref SqlValueStringBuilder subSelect, ref ValueStringBuilder result)
    {
        // Use ROW_NUMBER() OVER(...) for paging
    }
}
```

For a complete implementation, see [Custom Paging](../03-advanced-topics/05-custom-paging.md).

## Registration Methods

### Register by Data Source

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterSqlBuilder("LegacyOracle", Oracle11gBuilder.Instance);
});
```

### Register by Connection Type

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterSqlBuilder(typeof(OracleConnection), Oracle11gBuilder.Instance);
});
```

## Complete Integration Flow

```csharp
// 1. Write custom dialect
public sealed class LegacyOracleBuilder : OracleBuilder
{
    public static readonly LegacyOracleBuilder Instance = new LegacyOracleBuilder();
}

// 2. Register at startup
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterSqlBuilder("LegacyOracle", LegacyOracleBuilder.Instance);
});

// 3. Point entity or data source to corresponding database in configuration
[Table("Users", DataSource = "LegacyOracle")]
public class User
{
}

// 4. Business layer continues querying in a unified way
var users = await userService.SearchAsync(q => q.Where(u => u.Age >= 18).Skip(0).Take(20));
```

This pattern is suitable for encapsulating compatibility logic in the infrastructure layer, so business code doesn't need to be aware of database version differences.

## Extension Function Handlers

```csharp
MySqlBuilder.Instance.RegisterFunctionSqlHandler("DATE_FORMAT", (ref outSql, expr, context, sqlBuilder, outputParams) =>
{
    outSql.Append("DATE_FORMAT(");
    expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(", ");
    expr.Args[1].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(')');
});
```

If the function comes from Lambda or member extensions, you also need to register the corresponding conversion logic in `LambdaExprConverter`. See [Expression Extension](./01-expression-extension.en.md).

## Design Recommendations

- Prefer reusing existing Builders, only override the differing behavior.
- Try to consolidate database compatibility logic in the dialect layer, don't scatter it into business code.
- Legacy database compatibility usually starts with paging and date functions first.

## Related Links

- [Back to docs hub](../README.md)
- [Custom Paging](../03-advanced-topics/05-custom-paging.en.md)
- [Expression Extension](./01-expression-extension.en.md)
- [Configuration and Registration](../01-getting-started/03-configuration-and-registration.en.md)
