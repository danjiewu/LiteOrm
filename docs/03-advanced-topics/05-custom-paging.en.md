# Custom Paging

This page shows how to move paging differences into a custom `SqlBuilder`, using Oracle 11g as the main example.

## 1. Why custom paging exists

Older databases may not support the paging syntax expected by a default dialect. Instead of branching in application code, keep `Skip` / `Take` in business code and translate it inside the dialect layer.

## 2. Oracle 11g example

```csharp
public class Oracle11gBuilder : OracleBuilder
{
    public static readonly new Oracle11gBuilder Instance = new Oracle11gBuilder();

    public override void BuildSelectSql(ref SqlValueStringBuilder subSelect, ref ValueStringBuilder result)
    {
        // wrap the query and generate ROW_NUMBER() OVER (...) AS "RN__"
        // then filter by the requested skip/take range
    }
}
```

The usual Oracle 11g approach is:

1. build the inner query
2. add `ROW_NUMBER() OVER (...)`
3. filter on the generated `RN__` column in the outer query

## 3. Registration options

### Recommended startup registration

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterSqlBuilder("OracleDataSource", Oracle11gBuilder.Instance);
});
```

### Registration by connection type

```csharp
options.RegisterSqlBuilder(typeof(OracleConnection), Oracle11gBuilder.Instance);
```

### Registration from configuration

Set `DataSources[].SqlBuilder` to the fully qualified builder type name.

## 4. Query usage stays the same

```csharp
var page = await userService.SearchAsync(
    q => q.Where(u => u.Status == 1)
          .OrderBy(u => u.Id)
          .Skip(20)
          .Take(20)
);
```

You can also use `Expr.Section(...)` or DAO-based paging. The point is that callers keep the same API surface.

## 5. Compatibility notes

- Ensure the target database version really needs the override
- Review ordering carefully because paging without a stable order is rarely safe
- Keep paging SQL differences inside `SqlBuilder`, not inside services

## Related Links

- [Back to English docs hub](../SUMMARY.en.md)
- [Custom SqlBuilder and Dialect Extension](../04-extensibility/03-custom-sqlbuilder.en.md)
- [Configuration and Registration](../01-getting-started/03-configuration-and-registration.en.md)
- [Database Compatibility Notes](../05-reference/08-database-compatibility.en.md)
