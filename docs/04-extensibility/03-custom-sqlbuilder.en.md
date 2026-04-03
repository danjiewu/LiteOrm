# Custom SqlBuilder and Dialect Extension

Use a custom `SqlBuilder` when the target database or version needs SQL that differs from LiteOrm's default dialect behavior.

## 1. When to customize `SqlBuilder`

- legacy paging syntax
- database-specific function names
- compatibility logic you want to centralize below the service layer

## 2. Common extension points

| Extension point | Use |
|------|-----|
| `BuildSelectSql` | final select assembly and paging behavior |
| `RegisterFunctionSqlHandler` | custom SQL translation for functions |
| `RegisterSqlBuilder(...)` | bind a dialect override to a data source or connection type |

## 3. Example pattern

```csharp
public sealed class LegacyOracleBuilder : OracleBuilder
{
    public static readonly LegacyOracleBuilder Instance = new LegacyOracleBuilder();
}

builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterSqlBuilder("LegacyOracle", LegacyOracleBuilder.Instance);
});
```

This keeps legacy-database behavior inside infrastructure code while `EntityService`, `ObjectDAO`, and `ObjectViewDAO` keep the same calling style.

## 4. Function SQL handlers

If you add a new `Expr` or Lambda translation, register the SQL output in the builder:

```csharp
MySqlBuilder.Instance.RegisterFunctionSqlHandler("DATE_FORMAT", ...);
```

Pair this with `LambdaExprConverter` registration when the function should also be reachable from Lambda syntax.

## 5. Design advice

- start from an existing builder and override only the differences
- keep business code unaware of database-version quirks
- solve paging and function compatibility in one place

## Related Links

- [Back to English docs hub](../SUMMARY.en.md)
- [Custom Paging](../03-advanced-topics/05-custom-paging.en.md)
- [Expression Extension](./01-expression-extension.en.md)
- [Configuration and Registration](../01-getting-started/03-configuration-and-registration.en.md)
