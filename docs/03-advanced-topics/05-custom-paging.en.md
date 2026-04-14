# Custom Paging Implementation Examples

This document shows how to implement custom paging strategies in LiteOrm, using Oracle 11g as an example.

## Why Custom Paging is Needed

When the target database version is old, or the default dialect cannot satisfy the current SQL specification, you need custom paging logic. The most typical scenarios are:

- Older versions of Oracle, SQL Server, etc. do not support modern paging syntax.
- Need to uniformly support multiple database historical versions.
- Need to centralize paging differences in the dialect layer, rather than branching judgments in business code.

## 1. Custom Paging Implementation

Below is the complete implementation of `Oracle11gBuilder`, which inherits from `OracleBuilder` and overrides the paging logic:

```csharp
public class Oracle11gBuilder : OracleBuilder
{
    /// <summary>
    /// Gets the singleton instance of <see cref="Oracle11gBuilder"/>, suitable for Oracle 11g and above.
    /// </summary>
    public readonly static new Oracle11gBuilder Instance = new Oracle11gBuilder();

    /// <summary>
    /// Assembles the structured SQL fragment into the final SELECT statement (Oracle implementation).
    /// Uses ROW_NUMBER() OVER(...) double-layered nested subquery for paging, compatible with all Oracle versions.
    /// </summary>
    public override void BuildSelectSql(ref SqlValueStringBuilder subSelect, ref ValueStringBuilder result)
    {
        bool hasPaging = subSelect.Take > 0;

        if (hasPaging)
        {
            // Outer: filter ROW_NUMBER() range
            result.Append("SELECT * FROM (\n");
        }

        // Inner: actual data query
        result.Append("SELECT ");
        result.Append(subSelect.Select.AsSpan());

        if (hasPaging)
        {
            // Inner: calculate ROW_NUMBER(), move ORDER BY to OVER clause
            result.Append(",ROW_NUMBER() OVER (ORDER BY ");
            if (subSelect.OrderBy.Length > 0)
                result.Append(subSelect.OrderBy.AsSpan());
            else
                result.Append('1');
            result.Append(") AS \"RN__\"");
        }

        if (subSelect.From.Length > 0)
        {
            result.Append(" \nFROM ");
            result.Append(subSelect.From.AsSpan());
        }

        if (subSelect.Where.Length > 0)
        {
            result.Append(" \nWHERE ");
            result.Append(subSelect.Where.AsSpan());
        }

        if (subSelect.GroupBy.Length > 0)
        {
            result.Append(" \nGROUP BY ");
            result.Append(subSelect.GroupBy.AsSpan());
        }

        if (subSelect.Having.Length > 0)
        {
            result.Append(" \nHAVING ");
            result.Append(subSelect.Having.AsSpan());
        }

        if (hasPaging)
        {
            // Close inner subquery, provide alias for outer layer reference
            result.Append("\n) \"__T\"\n");
            // Filter by ROW_NUMBER() range (1-based, skip items after, take total items)
            result.Append("WHERE \"RN__\" > ");
            result.Append(subSelect.Skip.ToString());
            result.Append(" AND \"RN__\" <= ");
            result.Append((subSelect.Skip + subSelect.Take).ToString());
        }
        else
        {
            if (subSelect.OrderBy.Length > 0)
            {
                result.Append(" \nORDER BY ");
                result.Append(subSelect.OrderBy.AsSpan());
            }
        }
    }
}
```

## 2. Implementation Principles

### 2.1 Paging Mechanism

Oracle 11g does not support `LIMIT` and `OFFSET` syntax, so `ROW_NUMBER() OVER()` function must be used to implement paging:

1. **Inner query**: Calculate row number `RN__` for each row
2. **Outer query**: Filter results based on row number range

### 2.2 Core Logic

- **Without paging**: Generate standard SELECT statement directly
- **With paging**:
  - Add `ROW_NUMBER() OVER(ORDER BY ...) AS RN__` in the inner query
  - Move the ORDER BY clause into the OVER clause
  - In the outer query, filter the result range based on the `RN__` field

## 3. Usage

### 3.1 Register Custom SqlBuilder

There are three ways to register a custom `Oracle11gBuilder`:

#### Method 1: Globally Replace Default Oracle Builder

```csharp
// Register at application startup
using Oracle.ManagedDataAccess.Client;

// Register by connection type
SqlBuilderFactory.Instance.RegisterSqlBuilder(typeof(OracleConnection), Oracle11gBuilder.Instance);

// Or register by data source name
SqlBuilderFactory.Instance.RegisterSqlBuilder("OracleDataSource", Oracle11gBuilder.Instance);
```

#### Method 2: Register via RegisterLiteOrm Options (Recommended)

```csharp
// Specify custom SqlBuilder when registering LiteOrm
using Oracle.ManagedDataAccess.Client;
using System.Reflection;

var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm(options =>
    {
        // Register by data source name
        options.RegisterSqlBuilder("OracleDataSource", Oracle11gBuilder.Instance);

        // Or register by connection type (global replacement)
        options.RegisterSqlBuilder(typeof(OracleConnection), Oracle11gBuilder.Instance);
    })
    .Build();
```

#### Method 3: Specify via Configuration File (Recommended)

Specify the custom SqlBuilder type name directly via the `SqlBuilder` field in `appsettings.json`:

```json
{
    "LiteOrm": {
        "Default": "OracleDataSource",
        "DataSources": [
            {
                "Name": "OracleDataSource",
                "ConnectionString": "Data Source=ORCL;User Id=user;Password=pass;",
                "Provider": "Oracle.ManagedDataAccess.Client.OracleConnection, Oracle.ManagedDataAccess",
                "SqlBuilder": "YourNamespace.Oracle11gBuilder, YourAssembly",
                "PoolSize": 20,
                "MaxPoolSize": 100
            }
        ]
    }
}
```

**Note**:
- The value format of the `SqlBuilder` field is `FullTypeName, AssemblyName`
- This approach is more flexible and doesn't require manually registering SqlBuilder in code
- Suitable for scenarios where you need to dynamically specify different SqlBuilder implementations in configuration

### 3.2 Usage Examples

#### Basic Paged Query

```csharp
// Using service layer
var pageResult = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderBy(u => u.Id)
          .Skip(10).Take(20)
);

// Using DAO directly
var users = await objectViewDAO.Search(
    Expr.From<User>()
        .Where(u => u.Age >= 18)
        .OrderBy(nameof(User.Id))
        .Section(10, 20) // Skip 10 items, take 20 items
).ToListAsync();
```

#### Complex Condition Paging

```csharp
var query = Expr.From<User>()
    .Where(Expr.Prop("Age") > 18 & Expr.Prop("DeptId").In(1, 2, 3))
    .OrderByDescending("CreateTime")
    .Section(0, 10); // First page, 10 records

var result = await userService.SearchAsync(query);
```

### 3.3 Complete Flow from Integration to Query

```csharp
// 1. Define custom Builder
public class Oracle11gBuilder : OracleBuilder
{
    public static readonly Oracle11gBuilder Instance = new Oracle11gBuilder();
}

// 2. Register to LiteOrm
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterSqlBuilder("OracleDataSource", Oracle11gBuilder.Instance);
});

// 3. Use paging API normally, no need to rewrite queries in business code
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderBy(u => u.Id)
          .Skip(20)
          .Take(20)
);
```

The key point of this pattern is: paging differences are only handled in `SqlBuilder`, while the business layer maintains a unified `Skip/Take`写法.

## 4. Generated SQL Examples

### 4.1 Non-Paged Query

```sql
SELECT "T0"."ID", "T0"."USERNAME", "T0"."AGE", "T0"."CREATETIME"
FROM "USERS" "T0"
WHERE "T0"."AGE" >= :0
ORDER BY "T0"."ID"
```

### 4.2 Paged Query

```sql
SELECT * FROM (
SELECT "T0"."ID", "T0"."USERNAME", "T0"."AGE", "T0"."CREATETIME",ROW_NUMBER() OVER (ORDER BY "T0"."ID") AS "RN__"
FROM "USERS" "T0"
WHERE "T0"."AGE" >= :0
) "__T"
WHERE "__T"."RN__" > 10 AND "__T"."RN__" <= 30
```

**Note**:
- The generated SQL adds alias "T0" to the main table by default and uses this alias to qualify all column names
- Parameter names use pure numeric format (e.g., :0, :1, etc.) to avoid parameter name conflicts
- Column and table names are automatically formatted based on database type:
  - Oracle: wrapped in double quotes and converted to uppercase (e.g., "USERS", "ID")
  - SQL Server: wrapped in square brackets (e.g., [Users], [Id])
  - MySQL: wrapped in backticks (e.g., `users`, `id`)
  - PostgreSQL: wrapped in double quotes and converted to lowercase (e.g., "users", "id")
- This formatting ensures SQL compatibility and correctness across different databases

## 5. Performance Optimization Tips

1. **Index optimization**: Ensure appropriate indexes on ORDER BY fields
2. **Reduce data transfer**: Select only necessary columns, avoid SELECT *
3. **Set reasonable page sizes**: Adjust Take value according to actual needs
4. **Use bound parameters**: Avoid SQL injection and improve performance

## 6. Compatibility Notes

- **Oracle 11g+**: Fully compatible
- **Oracle 10g**: Ensure ROW_NUMBER() function is available
- **Other databases**: Need to implement corresponding custom builders

## 7. Extending to Other Databases

You can refer to the `Oracle11gBuilder` implementation to create custom paging strategies for other databases:

### 7.1 SQL Server 2008 and Below

```csharp
public class SqlServer2008Builder : SqlServerBuilder
{
    public readonly static new SqlServer2008Builder Instance = new SqlServer2008Builder();

    public override void BuildSelectSql(ref SqlValueStringBuilder subSelect, ref ValueStringBuilder result)
    {
        // Implement TOP + ROW_NUMBER() paging
        // ...
    }
}
```

### 7.2 PostgreSQL

```csharp
public class CustomPostgreSqlBuilder : PostgreSqlBuilder
{
    public readonly static new CustomPostgreSqlBuilder Instance = new CustomPostgreSqlBuilder();

    public override void BuildSelectSql(ref SqlValueStringBuilder subSelect, ref ValueStringBuilder result)
    {
        // Implement custom paging logic
        // ...
    }
}
```

## 8. FAQ

### 8.1 Paged Query Performance Issues

**Problem**: Large data volume paging queries are slow

**Solution**:
- Ensure indexes exist on ORDER BY fields
- Consider using covering indexes
- For very large tables, consider using keyset pagination (cursor-based paging)

### 8.2 Sorting Issues

**Problem**: Paged results sort incorrectly

**Solution**:
- Ensure ORDER BY clause contains unique fields
- When no ORDER BY is specified, `ORDER BY 1` is used as the default sort

## 9. Summary

By implementing a custom `SqlBuilder`, you can provide optimal paging strategies for different database versions and scenarios, thereby improving query performance and compatibility. LiteOrm's modular design makes this extension very simple and intuitive.

## Related Links

- [Back to docs hub](../README.md)
- [SqlBuilder and Dialect Extension](../04-extensibility/03-custom-sqlbuilder.en.md)
- [Configuration and Registration](../01-getting-started/03-configuration-and-registration.en.md)
- [Compatibility Notes](../05-reference/08-database-compatibility.en.md)
