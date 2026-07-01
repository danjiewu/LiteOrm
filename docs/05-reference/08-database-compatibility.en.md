# Database Compatibility Notes

This page summarizes the most common cross-database differences to validate when using LiteOrm. Use it when you are evaluating a new provider, troubleshooting dialect behavior, or deciding whether to extend `SqlBuilder`.

## 1. Supported Databases

LiteOrm includes 11 database-specific `SqlBuilder` implementations (including 6 domestic/compatible databases), plus a generic base class as fallback:

### Mainstream Databases

| Database | SqlBuilder Class | Auto-detect Keyword | Min Version |
|----------|-----------------|---------------------|-------------|
| SQL Server | `SqlServerBuilder` | `SQLCLIENT` | 2012+ |
| MySQL | `MySqlBuilder` | `MYSQL` | 8.0+ |
| Oracle | `OracleBuilder` | `ORACLE` | 12c+ |
| PostgreSQL | `PostgreSqlBuilder` | `NPGSQL` | — |
| SQLite | `SQLiteBuilder` | `SQLITE` | — |

### Domestic / Compatible Databases

| Database | SqlBuilder Class | Inherits From | Auto-detect Keywords |
|----------|-----------------|---------------|----------------------|
| Dameng DM | `DamengBuilder` | `OracleBuilder` | `DAMENG`, `DMNET`, `DM.DMCONNECTION` |
| KingbaseES | `KingbaseESBuilder` | `PostgreSqlBuilder` | `KINGBASE`, `KDBNDP` |
| Huawei GaussDB / openGauss | `GaussDBBuilder` | `PostgreSqlBuilder` | `GAUSSDB`, `OPENGAUSS` |
| OceanBase (MySQL compat) | `OceanBaseBuilder` | `MySqlBuilder` | `OCEANBASE` |
| TiDB | `TiDBBuilder` | `MySqlBuilder` | `TIDB` |
| GreatDB | `GreatDBBuilder` | `MySqlBuilder` | `GREATDB` |

> Domestic database builders are marker subclasses that inherit all behavior from their parent. `DamengBuilder` only overrides `GetAutoIncrementSql()` (returns `IDENTITY(1, 1)`); the other 5 have empty class bodies.

| Other (generic fallback) | `SqlBuilder` (base) | — | — |

### Dialect Auto-Detection

`SqlBuilderFactory.GetSqlBuilder` selects the appropriate `SqlBuilder` by substring-matching the uppercased connection type full name (`providerType.FullName`). **Domestic databases are checked first** to avoid premature matching by generic keywords:

```
 1. DAMENG / DMNET / DM.DMCONNECTION  → DamengBuilder.Instance
 2. KINGBASE / KDBNDP                  → KingbaseESBuilder.Instance
 3. GAUSSDB / OPENGAUSS                → GaussDBBuilder.Instance
 4. OCEANBASE                          → OceanBaseBuilder.Instance
 5. TIDB                               → TiDBBuilder.Instance
 6. GREATDB                            → GreatDBBuilder.Instance
 7. ORACLE                             → OracleBuilder.Instance
 8. MYSQL                              → MySqlBuilder.Instance
 9. SQLITE                             → SQLiteBuilder.Instance
10. SQLCLIENT                          → SqlServerBuilder.Instance (matches both Microsoft.Data.SqlClient and System.Data.SqlClient)
11. NPGSQL                             → PostgreSqlBuilder.Instance
otherwise                               → SqlBuilder.Instance (generic base)
```

You can also manually register a builder via `SqlBuilderFactory.RegisterSqlBuilder(Type, SqlBuilder)` or `RegisterSqlBuilder(string dataSourceName, SqlBuilder)`. Data-source-name registration has the highest priority, followed by type registration, then auto-detection.

### Database Provider Configuration

| Database | NuGet Package | Provider Value |
|----------|---------------|----------------|
| SQL Server | `Microsoft.Data.SqlClient` | `Microsoft.Data.SqlClient.SqlConnection, Microsoft.Data.SqlClient` |
| SQL Server (legacy) | `System.Data.SqlClient` | `System.Data.SqlClient.SqlConnection, System.Data.SqlClient` |
| MySQL | `MySqlConnector` | `MySqlConnector.MySqlConnection, MySqlConnector` |
| MySQL (legacy) | `MySql.Data` | `MySql.Data.MySqlClient.MySqlConnection, MySql.Data` |
| PostgreSQL | `Npgsql` | `Npgsql.NpgsqlConnection, Npgsql` |
| Oracle | `Oracle.ManagedDataAccess.Core` | `Oracle.ManagedDataAccess.Client.OracleConnection, Oracle.ManagedDataAccess` |
| SQLite | `Microsoft.Data.Sqlite` | `Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite` |

> For domestic databases, install the vendor-specific driver and set `Provider` to the fully qualified type name of the vendor's `DbConnection`.

## 2. Dialect Differences in Detail

### 2.1 Paging Syntax

Paging is the biggest compatibility hotspot.

| Database | Paging Method |
|----------|--------------|
| SQL Server 2012+ / Oracle 12c+ / PostgreSQL | `OFFSET ... ROWS FETCH NEXT ... ROWS ONLY` (base default) |
| MySQL / OceanBase / TiDB / GreatDB | `LIMIT [skip,] take` |
| SQLite | `LIMIT take OFFSET skip` |
| Oracle 11g and earlier | Requires `ROW_NUMBER()` nested subquery; custom `SqlBuilder` needed |

> The Demo project includes [Oracle11gBuilder.cs](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo/Demos/Oracle11gBuilder.cs) which extends `OracleBuilder` and overrides `BuildSelectSql` to demonstrate nested paging for Oracle 11g.

Recommended references:
- [Custom paging](../03-advanced-topics/05-custom-paging.en.md)
- [Custom SqlBuilder and dialect extension](../04-extensibility/03-custom-sqlbuilder.en.md)

### 2.2 Type Mapping Differences

Different databases handle .NET types differently. `SqlBuilder` subclasses handle these in `GetDbTypeInternal` / `ConvertToDbValue`:

| Database | Special Handling |
|----------|-----------------|
| Oracle / Dameng | `bool` → `DbType.Byte` (Oracle has no native boolean); `DateTime` → `DbType.Date` |
| SQLite | `DateTime`/`TimeSpan`/`DateTimeOffset` → `DbType.String`; DateTime formatted as `yyyy-MM-dd HH:mm:ss.fff`, DateTimeOffset as `yyyy-MM-dd HH:mm:ss.fff zzz`, TimeSpan using `c` format |
| SQL Server / MySQL / PostgreSQL / others | Standard type mapping, no special conversion |

### 2.3 Auto-increment Primary Key (Identity)

| Database | Identity Method | `SupportBatchInsertWithIdentity` | Notes |
|----------|----------------|--------------------------------|-------|
| SQL Server | `SELECT @@IDENTITY` (single) / `SCOPE_IDENTITY()` (batch) | ✅ `true` | Base class uses `@@IDENTITY` for single-row; SqlServer uses `SCOPE_IDENTITY()` for batch |
| MySQL / OceanBase / TiDB / GreatDB | `LAST_INSERT_ID()` | ✅ `true` | — |
| SQLite | `LAST_INSERT_ROWID()` | ✅ `true` | — |
| PostgreSQL / KingbaseES / GaussDB | `RETURNING` clause | ❌ `false` | Uses SERIAL/BIGSERIAL types |
| Oracle 12c+ / Dameng | `GENERATED AS IDENTITY` + `RETURNING ... INTO :param` | ❌ `false` | `OracleIdentitySourceType.Identity` (default) |
| Oracle 11g / Dameng | Sequence | ❌ `false` | `OracleIdentitySourceType.Sequence`, SQL uses `tablename_seq.nextval` |
| Oracle / Dameng | Custom expression | ❌ `false` | `OracleIdentitySourceType.Expression`, uses `IdentityExpression` |

> The `OracleIdentitySourceType` enum has three members: `Identity` (default, Oracle 12c+), `Sequence`, and `Expression` (custom SQL expression).

### 2.4 String Concatenation

| Database | Concat Method |
|----------|--------------|
| SQL Server | `+` operator |
| PostgreSQL / SQLite / Oracle / Dameng | `||` operator |
| MySQL / OceanBase / TiDB / GreatDB | `CONCAT(...)` function (base default) |

### 2.5 Identifier Quoting and Parameter Prefix

| Database | Identifier Quoting | Param Prefix | Case Handling |
|----------|-------------------|-------------|---------------|
| SQL Server | `"name"` (double quotes) | `@` | No conversion |
| MySQL / OceanBase / TiDB / GreatDB | `` `name` `` (backticks) | `@` | No conversion |
| Oracle / Dameng | `"NAME"` (double quotes) | `:` | Uppercase |
| PostgreSQL / KingbaseES / GaussDB | `"name"` (double quotes) | `@` | Lowercase |
| SQLite | `"name"` (double quotes) | `@` | No conversion |

### 2.6 Set Operations

| Database | EXCEPT Syntax |
|----------|--------------|
| Oracle / Dameng | `MINUS` |
| Others | `EXCEPT` (base default) |

### 2.7 Batch Update

| Database | Batch Update Method |
|----------|---------------------|
| SQL Server | `UPDATE T SET ... FROM table T INNER JOIN (VALUES ...) AS S(...) ON ...` |
| MySQL / OceanBase / TiDB / GreatDB | `UPDATE table T INNER JOIN (SELECT ... UNION ALL ...) S ON ... SET ...` |
| Oracle / Dameng | `MERGE INTO ... USING (SELECT ... FROM DUAL UNION ALL ...) ON (...) WHEN MATCHED THEN UPDATE SET ...` |
| PostgreSQL / KingbaseES / GaussDB | `UPDATE table u SET ... FROM (VALUES ...) AS v(...) WHERE u.key = v.k0` |
| SQLite | `WITH batch_data(...) AS (VALUES (...)) UPDATE table SET col = (SELECT ... FROM batch_data WHERE ...) WHERE EXISTS (...)` |

### 2.8 Batch Insert

| Database | Batch Insert Method |
|----------|---------------------|
| Oracle / Dameng | `INSERT INTO ... SELECT ... FROM DUAL UNION ALL SELECT ...` |
| Others | Standard `INSERT INTO ... VALUES (...), (...), ...` (base default) |

## 3. Bulk Write Capabilities (IBulkProvider)

LiteOrm supports high-performance bulk writes via the `IBulkProvider` interface, but **the core library does not include any built-in implementations**—only the interface and factory are provided.

| Database | Common Approach | Built-in Implementation |
|----------|----------------|------------------------|
| SQL Server | `SqlBulkCopy` | None (implement `IBulkProvider` yourself) |
| MySQL | `MySqlBulkCopy` | None (Demo includes a [MySqlBulkCopyProvider](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo/Demos/MySqlBulkInsertProvider.cs) example) |
| Oracle | Batch `INSERT` | None |
| PostgreSQL | `COPY` command | None |
| SQLite | Batch `INSERT` | None |

`BulkProviderFactory` resolves `IBulkProvider` instances registered with `[AutoRegister(Key = typeof(ConnectionType))]` by connection type. Returns `null` when no provider is registered, causing `BatchInsertAsync` to fall back to row-by-row inserts.

> To enable high-performance bulk writes, implement `IBulkProvider` and register it with `[AutoRegister(Key = typeof(MySqlConnection))]`.

## 4. Parameter Limits

Different databases and drivers tolerate different numbers of SQL parameters, so `ParamCountLimit` matters.

Watch for cases such as:
- Very large `IN (...)` lists
- Oversized multi-row inserts
- Update statements with many generated parameters

Typical mitigations:
- Reduce batch size
- Split work into smaller submissions
- Adjust `ParamCountLimit`

Recommended references:
- [Configuration reference](./01-configuration-reference.en.md)
- [Performance](../03-advanced-topics/03-performance.en.md)

## 5. Test Coverage

| Database | SqlBuilder | Unit Tests | Demo Config | Driver Reference |
|----------|-----------|------------|-------------|-----------------|
| SQLite | ✅ | ✅ | ✅ | ✅ |
| MySQL | ✅ | ✅ | ✅ | ✅ |
| Oracle | ✅ | ✅ | ✅ | ✅ |
| PostgreSQL / KingbaseES / GaussDB | ✅ | ❌ | ❌ | ❌ |
| SQL Server | ✅ | ❌ | ❌ | ❌ |
| Dameng / OceanBase / TiDB / GreatDB | ✅ | ❌ | ❌ | ❌ |

> The test, demo, and benchmark projects only configure MySQL, SQLite, and Oracle. Other database `SqlBuilder` implementations exist in the core library but are not covered by automated tests. When adopting these databases, prioritize validating paging and bulk operations.

## 6. How Docs Map to Compatibility Work

| Capability | Compatibility-Sensitive Point | Recommendation |
|------------|-------------------------------|----------------|
| Paging | Dialect syntax varies the most | Validate generated paging SQL first; customize `SqlBuilder` if needed |
| Window functions | Older databases may not support them | Confirm database version before enabling them |
| Custom functions | Names and argument shapes vary | Implement database-specific translation through expression extension |
| Bulk import | Depends on driver and provider support | Prefer native bulk capabilities; implement `IBulkProvider` yourself |
| Sharding | Depends mainly on naming rules | Standardize `TableArgs` conventions early |

## 7. Practical Validation Checklist

### 7.1 Validate these first on a new database

1. Basic CRUD
2. Sorting and paging
3. Association queries
4. Batch insert
5. One custom function or expression extension

These five checks cover most dialect differences that surface early.

### 7.2 For older databases, check paging first

If the target environment is an older Oracle deployment (11g and earlier), or another database with unusual paging rules, validate sorting + paging queries before anything else.

### 7.3 Inspect generated SQL before framework code

When troubleshooting compatibility issues, a practical order is:

1. Confirm the database version and driver
2. Inspect the generated SQL for dialect mismatches
3. Then decide whether expression extensions or a custom `SqlBuilder` are needed

## 8. When to Implement a Custom `SqlBuilder`

Consider a custom `SqlBuilder` when:

- Paging SQL is incompatible with the target database version (e.g. Oracle 11g)
- Function translation needs a shared rewrite layer
- Certain SQL fragments require a database-specific implementation
- You want one place to register infrastructure-level dialect behavior

Recommended starting points:
- [Custom paging](../03-advanced-topics/05-custom-paging.en.md)
- [Custom SqlBuilder and dialect extension](../04-extensibility/03-custom-sqlbuilder.en.md)

## 9. A Practical Compatibility Strategy

If you are migrating between databases or supporting more than one at the same time, a pragmatic strategy is:

- Keep tutorial and service-layer usage uniform
- Isolate compatibility differences in `SqlBuilder` and expression extensions
- Verify database-sensitive behavior with demos or integration tests

That keeps business-layer code more stable over time.

## Related Links

- [Back to English docs hub](../README.md)
- [Example Index](./06-example-index.en.md)
- [Generated SQL Examples](./07-sql-examples.en.md)
- [Custom paging](../03-advanced-topics/05-custom-paging.en.md)
- [Custom SqlBuilder and dialect extension](../04-extensibility/03-custom-sqlbuilder.en.md)
