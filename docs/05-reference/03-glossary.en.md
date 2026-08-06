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

## `ConstFilter` / `Column.Constant`

The `Constant` property of `ColumnAttribute`, used to declare fixed filter conditions. Resolved at metadata stage into `TableDefinition.ConstFilter`, and automatically injected into main-table `WHERE` and related-table `JOIN ... ON` during SQL generation. Suited for model-level invariant rules such as enabled-state, fixed partitions, or fixed tenant types; not suited for runtime context like the current user or tenant. See [Permission Filtering](../06-di/02-permission-filtering.en.md).

## `GenericSqlExpr`

A delegate-based dynamic SQL expression (`sealed class GenericSqlExpr : LogicExpr`) that lets you inject custom SQL generation logic without building a full Expr tree. Register a callback delegate via `GenericSqlExpr.Register` and reference it with `Expr.Sql(key, arg)`. Located in the `LiteOrm.Common` namespace.

## `ExprVisitor`

The expression visitor (`static class`) providing multi-mode traversal of `Expr` trees (delegates, `IExprNodeVisitor`, `ExprValidator`). Its static extension method `Validate(this ExprValidator, Expr)` drives whole-tree validation. Located in the `LiteOrm.Common` namespace.

## `ExprValidator`

The expression validator base class (`abstract class`); the `Validate(Expr node)` instance method validates a single node only. Whole-tree validation is driven by `ExprVisitor.Validate(validator, expr)`, which records the failed node to `FailedExpr` automatically. Located in the `LiteOrm.Common` namespace.

## `FunctionExprValidator`

A function-expression validator (`class FunctionExprValidator : ExprValidator`) that controls function-expression usage via the `FunctionPolicy` enum (`AllowAll` / `AllowRegisted` / `Disallow`). Located in the `LiteOrm` namespace.

## `IBulkProvider`

The bulk-write provider interface for database-native bulk imports (e.g. `MySqlBulkCopy`, `SqlBulkCopy`). Assign an implementation to the `BulkProvider` property of the matching `SqlBuilder` to enable it; when unset, batch inserts fall back to regular SQL. Located in the `LiteOrm` namespace.

## `CycleDetector`

An Expr cycle-reference detector (`static class`) exposing `HasCycle` / `FindCycle` / `Detect` methods. It detects circular references in Expr trees using reference equality (`ReferenceEquals`), preventing stack overflows during traversal and conversion. Located in the `LiteOrm.Common` namespace.

## `SqlBuildContext`

The SQL build context, carrying table aliases, scopes, table-name arguments and other state during SQL generation for use by `ISqlBuilder` and the Expr-to-SQL pipeline. DAOs can customize the context by overriding `CreateSqlBuildContext` (e.g. to inject sharding arguments). Located in the `LiteOrm.Common` namespace.
