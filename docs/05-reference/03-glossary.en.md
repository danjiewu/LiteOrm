# Glossary

## `Expr`

LiteOrm's structured expression model for SQL-shaped queries, updates, functions, and segments.

## `LogicExpr`

The condition-oriented part of the expression system, used for comparisons, boolean composition, `IN`, `EXISTS`, and similar predicates.

## `UpdateExpr`

An expression structure for conditional updates, commonly built with `Expr.Update<T>()`.

## `ExprString`

An interpolated-string way to build small SQL fragments. Use it for localized custom SQL, not as a replacement for normal query composition.

## `RawSql`

An `ExprString` helper marker type (an independent `readonly struct`, not inheriting from `Expr`) used exclusively to splice **dynamic values unsuitable for parameterization** verbatim into interpolated strings. Typical scenarios: `LIMIT`/`OFFSET` integer values, `ASC`/`DESC` sort direction, dynamic column names. Its content **bypasses parameterization** — when inlining dynamic values, the caller must validate first: numeric values via range validation (e.g. non-negative integers), string/token values via whitelist; never splice unvalidated user input. Purely static SQL text can be written directly in the `ExprString` literal — no `RawSql` needed. It is not scanned by `ExprValidator` and does not support Expr JSON round-trip. For reusable fragments that need runtime parameterization, use `GenericSqlExpr` instead.

## `ObjectDAO<T>`

The write-oriented DAO for entity operations such as insert, update, delete, and batching.

## `ObjectViewDAO<T>`

The query-oriented DAO for typed search, projection, associations, and result shaping.

## `EntityService<T>` / `EntityService<T, TView>`

The service-layer entry point that typically hosts business workflows, transactions, and combinations of multiple DAO calls.

## `ForeignType`

Property-level relationship metadata that usually represents a single-column foreign-key path.

## `TableJoin`

Type-level relationship metadata for explicit or reusable joins, especially useful for composite keys or stable aliases.

## `ForeignColumn`

A projected field on a view model that reads a specific property from a related table.

## `AutoExpand`

A relationship-path extension mechanism that makes deeper related paths available for later resolution. It does not force joins by itself.

## `IArged` / `TableArgs`

The sharding mechanism used to fill placeholders in table names at runtime.

## `SqlBuilder`

The dialect builder that converts LiteOrm expressions into executable SQL for a specific database flavor.
