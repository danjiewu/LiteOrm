# SqlBuilder Development Guide for Domestic / Compatible Databases

This document is intended for **third-party developers** who need to integrate domestic databases or third-party-compatible databases (such as Dameng, KingbaseES, Huawei GaussDB, OceanBase, TiDB, GreatDB, etc.) into their business projects or standalone packages.

LiteOrm already provides out-of-the-box SqlBuilder implementations for these databases that you can use directly. If the default implementation does not meet your version or scenario requirements, this document uses **Dameng (DM)** as a complete example to demonstrate the full workflow from analyzing differences to implementing a subclass and registering it via the public API.

> **Important premise**: Third-party developers **cannot and should not modify** the `SqlBuilderFactory.cs` source code. All custom SqlBuilders are registered through the `RegisterSqlBuilder(...)` public API into the factory's internal `RegisteredSqlBuilders` / `RegisteredSqlBuildersByDataSource` dictionaries. The factory **prioritizes these two dictionaries** when querying; keyword recognition is only a fallback.

## 1. Out-of-the-Box Domestic / Compatible Database Support

LiteOrm ships with the following dialect builders, which can be registered directly by data source name or connection type:

| Builder | Compatible Base | Typical Driver / Connection Type | Auto-matched keywords (fallback only) |
|---------|-----------------|----------------------------------|--------------------------------|
| `DamengBuilder` | `OracleBuilder` | `Dm.DmConnection` (Dm assembly) | `DAMENG`, `DMNET`, `DM.DMCONNECTION` |
| `KingbaseESBuilder` | `PostgreSqlBuilder` | `Kdbndp.KdbndpConnection` | `KINGBASE`, `KDBNDP` |
| `GaussDBBuilder` | `PostgreSqlBuilder` | `Npgsql.NpgsqlConnection` (openGauss compatible) | `GAUSSDB`, `OPENGAUSS` |
| `OceanBaseBuilder` | `MySqlBuilder` | `MySql.Data.MySqlClient` (MySQL compatible mode) | `OCEANBASE` |
| `TiDBBuilder` | `MySqlBuilder` | `MySqlConnector.MySqlConnection` | `TIDB` |
| `GreatDBBuilder` | `MySqlBuilder` | `MySql.Data.MySqlClient` | `GREATDB` |

> These builders all follow the design principle of "inherit from the closest base dialect + override only the differences". In most scenarios, the SQL behavior of domestic databases is already consistent with the corresponding base dialect (Oracle / PostgreSQL / MySQL), so the builder may override only a few methods or none at all.
>
> Even if the default implementation is identical to the base dialect, it is still recommended that you explicitly register the builder to the corresponding data source name via `RegisterSqlBuilder`. **Do not rely on keyword auto-recognition** — the latter is only a fallback mechanism, and driver version changes may cause keywords to no longer match.

## 2. Strategy for Choosing a Base Class

| Target database SQL behavior | Recommended base class |
|------------------------------|------------------------|
| Oracle compatible (Dameng, KingbaseES V8 Oracle mode, GaussDB Oracle compatible mode) | `OracleBuilder` |
| PostgreSQL compatible (KingbaseES default, openGauss / GaussDB, PolarDB-PG) | `PostgreSqlBuilder` |
| MySQL compatible (OceanBase MySQL mode, TiDB, GreatDB, PolarDB-MySQL) | `MySqlBuilder` |
| SQL Server compatible (rare) | `SqlServerBuilder` |
| SQLite compatible | `SQLiteBuilder` |
| Behavior consistent with standard SQL | `SqlBuilder` (base) |

> If the same domestic database provides multiple compatibility modes (for example, KingbaseES V8 supports both Oracle mode and PostgreSQL mode), the **default implementation uses the more common mode** (PostgreSQL mode). For Oracle mode, it is recommended to implement a subclass inheriting from `OracleBuilder` and register it by data source name.

## 3. Dameng (DM) Example: From Analysis to Implementation

### 3.1 Analyze Target Database Differences

Before writing code, list the differences between Dameng and the base dialect (Oracle) by referring to the [Difference Cheat Sheet](#6-difference-cheat-sheet-for-other-dialects):

| Difference | Oracle default | Dameng expected |
|------------|-----------------|-----------------|
| Identifier quoting | `"NAME"` (double quotes, uppercased) | Same, follows Oracle behavior |
| Parameter prefix | `:p0` | Same |
| String concatenation | `\|\|` | Same |
| Paging syntax | `OFFSET ... FETCH` (12c+) | Same, Dameng supports `OFFSET ... FETCH` |
| Auto-increment fragment | `GENERATED AS IDENTITY` | **`IDENTITY(start, increment)`** (Dameng-specific inline syntax) |
| EXCEPT keyword | `MINUS` | Same, follows Oracle behavior |
| Boolean type | `NUMBER(1)` | Same |
| Batch update | `MERGE INTO` | Same |
| Identity value return | `RETURNING ... INTO :p` | Same |

Conclusion: Dameng only needs to override `GetAutoIncrementSql(ColumnDefinition)`.

### 3.2 Implement the Builder

**Note**: `DamengBuilder` is already included in LiteOrm's built-in SqlBuilders. The example only demonstrates how to develop a custom SqlBuilder to add database support. In the example below, the `namespace` and `using` belong to **your own project or standalone package**, not the LiteOrm source code. The `DamengBuilder` class name can also be replaced with your own naming (such as `CompanyDamengBuilder`).

Create a new file `YourProject\SqlBuilder\DamengBuilder.cs`:

```csharp
using LiteOrm;          // Reference LiteOrm base class
using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;


namespace YourProject.SqlBuilder
{
    /// <summary>
    /// Dameng (DM) SQL builder.
    /// </summary>
    /// <remarks>
    /// Dameng database (DM7/DM8) SQL syntax is highly compatible with Oracle and is accessed via the Dm driver,
    /// so this builder inherits from <see cref="OracleBuilder"/>. Main differences:
    /// <list type="bullet">
    /// <item>Identity columns use the <c>IDENTITY(start, increment)</c> inline syntax instead of Oracle's GENERATED AS IDENTITY.</item>
    /// <item>Default case-folding strategy is consistent with Oracle (double-quote wrapping, internal uppercasing).</item>
    /// <item>EXCEPT still needs to be translated to MINUS.</item>
    /// <item>Boolean type is mapped to NUMBER(1).</item>
    /// </list>
    /// </remarks>
    public class DamengBuilder : OracleBuilder
    {
        /// <summary>
        /// Gets the singleton instance of <see cref="DamengBuilder"/>.
        /// </summary>
        public static readonly new DamengBuilder Instance = new DamengBuilder();

        /// <summary>
        /// Dameng uses IDENTITY(start, increment) inline syntax to generate identity columns, unlike Oracle's GENERATED AS IDENTITY.
        /// </summary>
        protected override string GetAutoIncrementSql(ColumnDefinition column) => $"IDENTITY({column.IdentityStart}, {column.IdentityIncreasement})";
    }
}
```

**Implementation notes:**

1. **Inherit from `OracleBuilder` rather than `SqlBuilder`**: This lets us reuse all of Oracle's SQL generation logic (`MERGE INTO` batch updates, `MINUS` translation, `RETURNING INTO` identity value return, etc.) and only override the differences.
2. **Provide `public static readonly new XxxBuilder Instance`**: All built-in builders follow the singleton pattern, and the factory returns this singleton.
3. **Only override `GetAutoIncrementSql(ColumnDefinition)`**: This is the only significant difference from Oracle.
4. **Use `protected override` rather than `public override`**: Because the base class's `GetAutoIncrementSql(ColumnDefinition)` is `protected virtual`, keep the access level consistent.

### 3.3 Register the Custom SqlBuilder

After implementing the subclass, you must register it with the factory via the `RegisterSqlBuilder(...)` public API so that the factory can find your builder when generating SQL. Built-in SqlBuilders, by contrast, are matched by keywords.

`RegisterSqlBuilder` has two overloads, each writing to one of the factory's internal dictionaries:

| Overload | Dictionary written | Applicable scenario |
|----------|--------------------|---------------------|
| `RegisterSqlBuilder(string dataSourceName, SqlBuilder sqlBuilder)` | `RegisteredSqlBuildersByDataSource` | Register by data source name (recommended, multi-datasource scenarios) |
| `RegisterSqlBuilder(Type providerType, SqlBuilder sqlBuilder)` | `RegisteredSqlBuilders` | Register by connection type (globally replace the default dialect for that driver) |

Both dictionaries are queried **with priority over keyword recognition**, so writing to them takes effect immediately and is not affected by built-in keyword matching.

#### Register directly during startup configuration

```csharp
using Dm;  // Your target database driver

builder.Host.RegisterLiteOrm(options =>
{
    // Option A: Register by data source name (highest priority, recommended for multi-datasource scenarios)
    options.RegisterSqlBuilder("Dameng", DamengBuilder.Instance);

    // Option B: Register by connection type (globally replace the default dialect for that driver)
    options.RegisterSqlBuilder(typeof(DmConnection), DamengBuilder.Instance);
});
```

#### For multi-datasource scenarios, unified registration by data source name is recommended

To avoid mismatches caused by different drivers sharing a keyword:

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterSqlBuilder("Dameng",   DamengBuilder.Instance);
    options.RegisterSqlBuilder("Kingbase", KingbaseESBuilder.Instance);
    options.RegisterSqlBuilder("Gauss",    GaussDBBuilder.Instance);
});
```

#### Call the factory singleton directly (no DI scenario)

```csharp
SqlBuilderFactory.Instance.RegisterSqlBuilder(typeof(DmConnection), DamengBuilder.Instance);
SqlBuilderFactory.Instance.RegisterSqlBuilder("Dameng", DamengBuilder.Instance);
```

#### Register via configuration file

Specify the builder directly via the `SqlBuilder` field in `appsettings.json`:

```json
{
  "LiteOrm": {
    "Default": "DamengDataSource",
    "DataSources": [
      {
        "Name": "DamengDataSource",
        "ConnectionString": "Server=dm-host:5236;User Id=SYSDBA;Password=SYSDBA001;",
        "Provider": "Dm.DmConnection, Dm",
        "SqlBuilder": "YourProject.SqlBuilder.DamengBuilder, YourProject"
      }
    ]
  }
}
```

- The value format of the `SqlBuilder` field is `fully qualified type name, assembly name`.
- The built-in `DamengBuilder` is located under the `LiteOrm` namespace, so you can fill in `LiteOrm.DamengBuilder, LiteOrm` directly; for custom subclasses, fill in your own namespace and assembly.
- Custom subclasses need to ensure the assembly is loaded (usually by referencing it at startup).

#### Factory resolution order

The four registration methods above all ultimately write to the factory's two internal dictionaries. `SqlBuilderFactory.GetSqlBuilder` queries them in the following priority order (highest to lowest):

1. **By data source name**: `RegisteredSqlBuildersByDataSource[dataSourceName]` — written by `RegisterSqlBuilder(string, SqlBuilder)`
2. **By connection type**: `RegisteredSqlBuilders[providerType]` — written by `RegisterSqlBuilder(Type, SqlBuilder)`
3. **By connection type name keyword**: built-in dialect fallback recognition (only for LiteOrm's built-in driver keywords)
4. **Default**: `SqlBuilder.Instance` (standard SQL)

**Third-party developers should focus on layers 1 and 2**:

- These two layers are the factory's **only two public registration entry points**, and writing to them always takes priority over keyword recognition.
- Even if your driver's FullName does not contain any recognized keywords (for example, a private Dameng driver called `Acme.Db.Connection`), as long as you call `RegisterSqlBuilder(typeof(AcmeConnection), DamengBuilder.Instance)` at startup, the factory will correctly return your builder.
- Layer 3 keyword recognition is only a fallback mechanism for LiteOrm's built-in dialects; third parties should not rely on it, let alone modify it.

> **Conclusion**: Always register explicitly via `RegisterSqlBuilder(...)`. **Do not rely on keyword auto-recognition** — the latter may fail when driver versions change.

### 3.4 Write Unit Tests for the Subclass

If you override a method and change the output semantics (such as Dameng's `GetAutoIncrementSql(ColumnDefinition)` returning `IDENTITY(start, increment)`), it is recommended to add assertions in your own test project. `GetAutoIncrementSql` is `protected`, so verify it indirectly through `BuildCreateTableSql`:

```csharp
[Fact]
public void Dameng_GetAutoIncrementSql_ReturnsIdentitySyntax()
{
    // Build the test context via AttributeTableInfoProvider
    var tableDefinition = CreateProvider(DamengBuilder.Instance).GetTableDefinition(typeof(DamengIdentityModel));
    var sql = DamengBuilder.Instance.BuildCreateTableSql(tableDefinition.Name, tableDefinition.Columns);
    Assert.Contains("IDENTITY(1000, 5)", sql);
    Assert.DoesNotContain("GENERATED AS IDENTITY", sql);
}

[Table("DamengIdentityModels")]
private class DamengIdentityModel
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, IdentityStart = 1000, IdentityIncreasement = 5, AllowNull = false)]
    public int Id { get; set; }
}
```

For the `CreateProvider` helper method, refer to the existing implementation in `LiteOrm.Tests\SqlBuilderTests.cs` (construct `ISqlBuilderFactory` and `IDataSourceProvider` via Moq, then instantiate `AttributeTableInfoProvider`).

If your dialect has unique function translations (such as Dameng's `SYSDATE` or `TO_DATE` behavior differences), register function handlers following the [Expression Extension](./01-expression-extension.en.md) document and write dedicated tests.

## 4. Inheritance Lookup Mechanism for Function Handlers (SqlFunction)

In addition to overriding `virtual` methods, LiteOrm provides a lighter-weight extension point: **function handlers (FunctionSqlHandler)**. They allow you to register translation logic for a specific function name (such as `DATE_FORMAT` or `SYSDATE`) without creating a subclass or overriding a method. See [Expression Extension](./01-expression-extension.en.md) for details.

This section focuses on the **inheritance chain lookup mechanism** — the key to how custom subclasses cooperate with parent class function handlers.

### 4.1 Lookup Order

When `SqlBuilder.BuildFunctionSql` generates function SQL, it starts from **the most derived type of the current instance** and walks up the inheritance chain layer by layer to find registered handlers. The **first match wins**:

```
Current instance type (e.g., DamengBuilder)  →  look up handlers registered on DamengBuilder
    ↓ miss
Parent type (e.g., OracleBuilder)           →  look up handlers registered on OracleBuilder
    ↓ miss
Grandparent type (e.g., SqlBuilder)         →  look up handlers registered on SqlBuilder
    ↓ all miss
Default function mapping table (e.g., IndexOf → CHARINDEX)
```

**Core conclusions**:

- **Handlers registered on the subclass itself have the highest priority.**
- **Handlers registered on the parent class also take effect**, with priority after those registered on the subclass — when the subclass hasn't registered, it automatically falls back to the parent class.
- Only when all are missed does it use the default function mapping table (such as `IndexOf→CHARINDEX`, `Now→CURRENT_TIMESTAMP`).

### 4.2 Practical Implications

This means you can **inherit from an existing built-in builder and only add your own function handlers, without re-registering the parent class's existing handlers**:

```csharp
using LiteOrm;
using LiteOrm.Common;
using static LiteOrm.Common.Expr;

public class MyDamengBuilder : DamengBuilder
{
    public static readonly new MyDamengBuilder Instance = new MyDamengBuilder();

    public MyDamengBuilder()
    {
        // Only register Dameng-specific function translations
        // For example, translate GETDATE to SYSDATE
        this.RegisterFunctionSqlHandler("GETDATE", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, SqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
        {
            outSql.Append("SYSDATE");
        });
    }
}
```

After this code runs:

- `GETDATE` → hits the handler registered on `MyDamengBuilder`, outputs `SYSDATE`.
- `Now` → `MyDamengBuilder` hasn't registered, walks the inheritance chain, hits the handler registered on the `SqlBuilder` base class (outputs `CURRENT_TIMESTAMP`).
- `IndexOf` → all miss, uses the default mapping table (outputs `CHARINDEX`).

### 4.3 Two Ways to Override a Parent Class Handler

If a handler registered on the parent class does not meet your needs, there are two override approaches:

| Approach | Description | Impact scope |
|----------|-------------|--------------|
| Register a new handler with the **same function name** in the subclass constructor | Subclass matches and returns directly without querying the parent | Only the subclass and its descendants |
| Override the `BuildFunctionSql` method | Fully customize the lookup logic | The subclass and its descendants |

> Since lookup walks the inheritance chain from child to parent layer by layer, a handler registered on a subclass **is never shadowed by a same-named handler on the parent class**. This is consistent with `virtual` method override semantics.

### 4.4 When the Lookup Chain Terminates

The lookup continues up to (and including) the `SqlBuilder` base class. If a handler should only take effect for a specific derived class, you can check `sqlBuilder.GetType()` inside the handler to decide whether to handle it; if not, skip and continue the lookup. However, the recommended approach is to register directly in the corresponding derived class's constructor, so the lookup chain matches naturally.

## 5. Verification and Troubleshooting

### 5.1 Recommended Verification Checklist

After integrating Dameng (or any new domestic database), verify in the following order:

1. **Basic CRUD**: Confirm that identity column inserts correctly return auto-increment values (Dameng uses `RETURNING INTO`).
2. **Sorting and paging**: The SQL generated by `Skip(...).Take(...)` is executable on the target database version.
3. **Batch insert / batch update**: Dameng uses `MERGE INTO`; verify the parameter order of primary key columns and updatable columns.
4. **DDL verification**: The `IDENTITY(start, increment)` syntax generated by `BuildCreateTableSql` is accepted by the Dameng executor.
5. **Function translation**: If you have custom Lambda translations (such as `DateTime.Now` → `SYSDATE`), verify them separately.

### 5.2 Inspect Generated SQL First, Then ORM Code

When troubleshooting compatibility issues, follow this order:

1. Confirm the target database version (DM7 / DM8 behavior differs slightly).
2. View the actual generated SQL via logs to confirm whether the dialect was matched. See [Logging and Diagnostics](../03-advanced-topics/07-logging.en.md).
3. If you find the generated SQL is Oracle dialect (such as `GENERATED AS IDENTITY`) rather than Dameng dialect (`IDENTITY(start, increment)`), it means the factory did not match your registered builder. First check whether `RegisterSqlBuilder(...)` was called at startup, then verify the data source name / connection type is consistent with the runtime.
4. If necessary, register explicitly to bypass keyword recognition:

   ```csharp
   options.RegisterSqlBuilder(typeof(DmConnection), DamengBuilder.Instance);
   ```

### 5.3 Case-Folding Strategy Notes

Dameng's default identifier case-folding strategy is consistent with Oracle:

- Identifiers wrapped in double quotes are treated as **uppercase** (`"USER"` → stored internally as `USER`).
- Identifiers without double quotes are automatically uppercased by Dameng.

If your table / column names are actually stored in lowercase in Dameng (for example, a database migrated from MySQL):

- Use all-lowercase or quoted names on the entity `[Table]` / `[Column]`;
- Or implement a subclass inheriting from `DamengBuilder`, override `ToSqlName` to use a "double quotes + lowercase" strategy, and register it to the corresponding data source by data source name.

## 6. Difference Cheat Sheet for Other Dialects

The following table summarizes the differences between the 6 built-in domestic / compatible databases and their base dialects, for reference when integrating a new database:

| Database | Base class | Main differences | Methods overridden |
|----------|-----------|-------------------|---------------------|
| Dameng DM | `OracleBuilder` | Auto-increment column uses `IDENTITY(start, increment)` | Override `GetAutoIncrementSql` |
| KingbaseES | `PostgreSqlBuilder` | None (default is fully consistent with PG) | None |
| Huawei GaussDB / openGauss | `PostgreSqlBuilder` | None (default is fully consistent with PG) | None |
| OceanBase (MySQL mode) | `MySqlBuilder` | None (default is fully consistent with MySQL) | None |
| TiDB | `MySqlBuilder` | Auto-increment column distributed semantics differ (no continuity guarantee) | None (behavioral difference absorbed by the driver layer) |
| GreatDB | `MySqlBuilder` | None (default is fully consistent with MySQL) | None |

**Additional notes:**

- **KingbaseES V8 Oracle mode**: The default `KingbaseESBuilder` uses PostgreSQL mode; if you use Oracle compatibility mode, it is recommended to implement a `KingbaseESOracleBuilder : OracleBuilder` and register it by data source name.
- **OceanBase Oracle mode**: The default `OceanBaseBuilder` uses MySQL mode; if you use Oracle compatibility mode, it is recommended to implement an `OceanBaseOracleBuilder : OracleBuilder` and register it by data source name.
- **TiDB `AUTO_RANDOM`**: If you use `AUTO_RANDOM` primary keys, the auto-increment value is not a continuous integer. The `LAST_INSERT_ID()` returned by `BuildBatchIdentityInsertSql` is for reference only; do not rely on continuous IDs in business logic. You can override `BuildBatchIdentityInsertSql` in a subclass to disable this return or adjust it to something like `ROW_COUNT()`.
- **GaussDB distributed**: If you use the distributed version of GaussDB, DDL differences involving distribution keys / distributed tables may require appending a `DISTRIBUTE BY ...` clause in `BuildCreateTableSql`. Implement this via a subclass.

## 7. FAQ

### Q1: I configured a Dameng connection, but the generated SQL is still Oracle dialect (such as `GENERATED AS IDENTITY`). Why?

**A:** It means the factory did not match your registered builder. Possible reasons:

1. `RegisterSqlBuilder(...)` was not called at startup, or was called later than the first query.
2. The registered data source name / connection type is inconsistent with what is actually used at runtime.
3. You relied on keyword auto-recognition, but your driver's FullName does not contain any of the built-in keywords (`DAMENG`, `DMNET`, `DM.DMCONNECTION`).

Solution: **Do not rely on keyword recognition**, register explicitly:

```csharp
options.RegisterSqlBuilder(typeof(DmConnection), DamengBuilder.Instance);
// or
options.RegisterSqlBuilder("YourDataSourceName", DamengBuilder.Instance);
```

### Q2: Are Dameng and Oracle completely the same in paging?

**A:** DM8 fully supports the `OFFSET ... FETCH` syntax, consistent with Oracle 12c+. For DM7 or older versions, you may need to downgrade to the `ROW_NUMBER() OVER(...)` nested subquery syntax. Refer to the [Oracle 11g custom paging example](../03-advanced-topics/05-custom-paging.en.md) to implement a `Dameng7Builder : DamengBuilder` and register it by data source name.

### Q3: The IDs returned by batch insert on TiDB are not continuous. How to handle this?

**A:** TiDB's auto-increment ID only guarantees uniqueness in distributed scenarios, not continuity. The `LAST_INSERT_ID()` returned by `BuildBatchIdentityInsertSql` is the ID of the first row; subsequent row IDs are not necessarily consecutive. Business logic should avoid inferring other rows by ID after `BatchInsert`; if you need to get all inserted IDs, switch to looping single inserts and collecting the return values, or use a business natural key.

### Q4: How are boolean types handled in domestic databases?

**A:** By default, `OracleBuilder` / `DamengBuilder` maps `bool` to `NUMBER(1)` (0/1). The `PostgreSqlBuilder` family (KingbaseES, GaussDB) natively supports the `BOOLEAN` type. The `MySqlBuilder` family (OceanBase, TiDB, GreatDB) natively supports `TINYINT(1)`. If you need a different mapping (such as Dameng's `BIT`), override `GetDbTypeInternal` and `GetSqlTypeDefinition`.

### Q5: Why are the built-in KingbaseESBuilder / GaussDBBuilder / OceanBaseBuilder / TiDBBuilder / GreatDBBuilder internally empty?

**A:** This is **intentional design**. These databases are fully consistent with their corresponding base dialects in common SQL syntax, but keeping them as independent types serves two purposes:

1. It allows the factory's keyword recognition to route to the correct dialect (instead of matching the generic `SqlBuilder.Instance`);
2. It provides a mounting base class for you to add version-specific extension points later (for example, to support TiDB's `AUTO_RANDOM`, just inherit from `TiDBBuilder` and override methods — it won't affect regular MySQL users).

If you find an uncovered difference between a domestic database and its base dialect, it is recommended to implement a subclass and register it by data source name:

```csharp
public class MyKingbaseESBuilder : KingbaseESBuilder
{
    public static readonly new MyKingbaseESBuilder Instance = new MyKingbaseESBuilder();

    // Override difference methods
}

// Register
options.RegisterSqlBuilder("Kingbase", MyKingbaseESBuilder.Instance);
```

### Q6: Can I modify the `SqlBuilderFactory.cs` source code to add my own keywords?

**A:** Not recommended. Third-party modifications to LiteOrm source code create a maintenance burden (re-merging conflicts during upgrades) and are unnecessary — builders registered to the dictionaries via `RegisterSqlBuilder(...)` **always take priority over** keyword recognition. If you need to support an unrecognized driver, just register it explicitly at startup:

```csharp
options.RegisterSqlBuilder(typeof(YourCustomConnection), YourBuilder.Instance);
```

## 8. Summary of the Extension Workflow

When integrating a new domestic / compatible database, follow these steps:

1. **Determine the compatible base class**: Based on the target database's SQL behavior, choose the closest base class from `OracleBuilder` / `PostgreSqlBuilder` / `MySqlBuilder` / `SqlServerBuilder` / `SQLiteBuilder` / `SqlBuilder`.
2. **List the differences**: Refer to the [Difference Cheat Sheet](#6-difference-cheat-sheet-for-other-dialects) and compare with the base dialect to list the methods that need to be overridden.
3. **Implement the subclass**: Create a new `XxxBuilder.cs` in your own project or standalone package, override only the differing methods, and provide a `public static readonly new XxxBuilder Instance` singleton.
4. **Register the SqlBuilder**: Write to the `RegisteredSqlBuilders` / `RegisteredSqlBuildersByDataSource` dictionaries via the `RegisterSqlBuilder(...)` public API. **No need to modify LiteOrm source code**. See [3.3 Register the Custom SqlBuilder](#33-register-the-custom-sqlbuilder).
5. **Add tests**: Write assertions in your own test project to verify that the output of overridden methods meets expectations; for common behavior, refer to the existing parameterized test pattern in LiteOrm.Tests.
6. **Configure the data source**: Register the data source and specify the `SqlBuilder` in `appsettings.json` or the `RegisterLiteOrm` options.
7. **Verification checklist**: Run through the business scenarios following the [5.1 Recommended Verification Checklist](#51-recommended-verification-checklist).

## Related Links

- [Back to docs hub](../README.md)
- [Custom SqlBuilder / Dialect Extension](./03-custom-sqlbuilder.en.md)
- [Custom Paging Implementation Example](../03-advanced-topics/05-custom-paging.en.md)
- [Database Differences and Compatibility Notes](../05-reference/08-database-compatibility.en.md)
- [Expression Extension](./01-expression-extension.en.md)
- [Configuration and Registration](../01-getting-started/03-configuration-and-registration.en.md)
- [Logging and Diagnostics](../03-advanced-topics/07-logging.en.md)
