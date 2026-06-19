# Database Compatibility Notes

This page summarizes the most common cross-database differences to validate when using LiteOrm. Use it when you are evaluating a new provider, troubleshooting dialect behavior, or deciding whether to extend `SqlBuilder`.

## 1. Current coverage

The current docs and README files explicitly cover these primary databases:

- SQL Server
- MySQL
- Oracle
- PostgreSQL
- SQLite

## 2. Common compatibility hotspots

### 2.1 Paging syntax

Paging is usually the biggest compatibility hotspot.

- newer SQL Server versions typically use `OFFSET ... FETCH`
- older Oracle versions often need `ROW_NUMBER()` or nested paging queries
- MySQL / PostgreSQL / SQLite usually use `LIMIT/OFFSET`

If the target database has special paging rules, start with:

- [Custom paging](../03-advanced-topics/05-custom-paging.en.md)
- [Custom SqlBuilder and dialect extension](../04-extensibility/03-custom-sqlbuilder.en.md)

### 2.2 String and date functions

The same business intent can map to different function names across databases. Common examples include:

- date formatting
- string concatenation
- date truncation or arithmetic
- current-time functions

If you register custom Lambda translations, always validate that the generated SQL still matches the target dialect.

Recommended references:

- [Expression extension](../04-extensibility/01-expression-extension.en.md)
- `LiteOrm.Demo\Demos\DateFormatDemo.cs`

### 2.3 Bulk write capabilities

Bulk-write behavior depends heavily on the underlying driver and provider implementation.

- SQL Server commonly uses `SqlBulkCopy`
- MySQL commonly uses `MySqlBulkCopy` or an equivalent high-throughput import path
- other databases may rely on ordinary batch SQL or a custom `IBulkProvider`

That means the final performance of `BatchInsertAsync` depends on both LiteOrm and the configured `IBulkProvider`.

### 2.4 Parameter limits

Different databases and drivers tolerate different numbers of SQL parameters, so `ParamCountLimit` matters.

Watch for cases such as:

- very large `IN (...)` lists
- oversized multi-row inserts
- update statements with many generated parameters

Typical mitigations:

- reduce batch size
- split work into smaller submissions
- adjust `ParamCountLimit`

Recommended references:

- [Configuration reference](./01-configuration-reference.en.md)
- [Performance](../03-advanced-topics/03-performance.en.md)

## 3. How docs map to compatibility work

| Capability | Compatibility-sensitive point | Recommendation |
|------|-------------------------------|----------------|
| paging | dialect syntax varies the most | validate generated paging SQL first; customize `SqlBuilder` if needed |
| window functions | older databases may not support them | confirm database version before enabling them |
| custom functions | names and argument shapes vary | implement database-specific translation through expression extension |
| bulk import | depends on driver and provider support | prefer native bulk capabilities when available |
| sharding | depends mainly on naming rules | standardize `TableArgs` conventions early |

## 4. Practical validation checklist

### 4.1 Validate these first on a new database

1. basic CRUD
2. sorting and paging
3. association queries
4. batch insert
5. one custom function or expression extension

These five checks cover most dialect differences that surface early.

### 4.2 For older databases, check paging first

If the target environment is an older Oracle deployment or another database with unusual paging rules, validate sorting + paging queries before anything else.

### 4.3 Inspect generated SQL before framework code

When troubleshooting compatibility issues, a practical order is:

1. confirm the database version and driver
2. inspect the generated SQL for dialect mismatches
3. then decide whether expression extensions or a custom `SqlBuilder` are needed

## 5. When to implement a custom `SqlBuilder`

Consider a custom `SqlBuilder` when:

- paging SQL is incompatible with the target database version
- function translation needs a shared rewrite layer
- certain SQL fragments require a database-specific implementation
- you want one place to register infrastructure-level dialect behavior

Recommended starting points:

- [Custom paging](../03-advanced-topics/05-custom-paging.en.md)
- [Custom SqlBuilder and dialect extension](../04-extensibility/03-custom-sqlbuilder.en.md)

## 6. A practical compatibility strategy

If you are migrating between databases or supporting more than one at the same time, a pragmatic strategy is:

- keep tutorial and service-layer usage uniform
- isolate compatibility differences in `SqlBuilder` and expression extensions
- verify database-sensitive behavior with demos or integration tests

That keeps business-layer code more stable over time.

## Related links

- [Back to English docs hub](../README.md)
- [Example Index](./06-example-index.en.md)
- [Generated SQL Examples](./07-sql-examples.en.md)
- [Custom paging](../03-advanced-topics/05-custom-paging.en.md)
- [Custom SqlBuilder and dialect extension](../04-extensibility/03-custom-sqlbuilder.en.md)
