# Example Index

This page groups current LiteOrm examples by scenario instead of by chapter. Most examples come from `LiteOrm.Demo` (end-to-end flows) and `LiteOrm.Tests` (edge cases and verifiable patterns).

## 1. End-to-end onboarding

### Minimal path from configuration to CRUD

- Entry doc: [First end-to-end example](../getting-started/first-example-di.en.md)
- Best for: first-time LiteOrm setup when you want a working baseline quickly
- Focus:
  - entity definition
  - service registration
  - insert, query, update, count, and delete in one flow

## 2. Query examples

### Choosing between Lambda, `Expr`, and `ExprString`

- Entry doc: [Query Overview](../core-usage/query-overview.en.md)
- Focus:
  - when to use each query style
  - dynamic condition composition
  - the intended scope of `ExprString`

### `EXISTS`, `Expr.ExistsRelated(...)`, and subqueries

- Entry doc: [Query Overview](../core-usage/query-overview.en.md) | [Lambda Guide](../core-usage/lambda-guide.en.md) | [Expr Guide](../core-usage/expr-guide.en.md)
- Code sources:
  - `LiteOrm.Demo\Demos\ExistsRelatedDemo.cs`
  - `LiteOrm.Tests\ExprEnhancedTests.cs`
  - `LiteOrm.Tests\ServiceTests.cs`
  - `LiteOrm.Tests\LambdaQueryTests.cs`
- Focus:
  - `Expr.Exists<T>(...)`
  - `Expr.ExistsRelated<T>(...)`
  - `NOT ExistsRelated(...)`
  - `IN` subqueries
  - combining relationship filters with ordinary predicates

### Common predicates and set operations

- Entry doc: [Query Overview](../core-usage/query-overview.en.md) | [Lambda Guide](../core-usage/lambda-guide.en.md) | [Expr Guide](../core-usage/expr-guide.en.md)
- Code sources:
  - `LiteOrm.Demo\Demos\PracticalQueryDemo.cs`
  - `LiteOrm.Tests\PracticalQueryTests.cs`
- Focus:
  - `In`
  - `Between`
  - `Like`
  - converting a dynamic DTO into `Expr`

## 3. Write and batch examples

### Batch insert, update, and delete

- Entry doc: [CRUD guide](../core-usage/crud-guide.en.md)
- Code sources:
  - `LiteOrm.Demo\Data\DbInitializer.cs`
  - `LiteOrm.Tests\ServiceTests.cs`
- Focus:
  - `BatchInsertAsync`
  - `BatchUpdateAsync`
  - `BatchDeleteAsync`
  - the full batch-write loop

### Upsert and mixed batch processing

- Entry doc: [CRUD guide](../core-usage/crud-guide.en.md)
- Code sources:
  - `LiteOrm.Tests\ServiceTests.cs`
  - `LiteOrm.Demo\Demos\UpdateExprDemo.cs`
- Focus:
  - `BatchUpdateOrInsertAsync`
  - mixed batches with `EntityOperation<T>`
  - conditional updates with `UpdateExpr`

## 4. Association examples

### Minimal `ForeignType` + `ForeignColumn` flow

- Entry doc: [Associations](../core-usage/associations.en.md)
- Best for: understanding how a single foreign-key relationship maps into view-model fields

### Multi-level associations and `AutoExpand`

- Entry doc: [Associations](../core-usage/associations.en.md)
- Code sources:
  - `LiteOrm.Demo\Models\User.cs`
  - `LiteOrm.Demo\Models\SalesRecord.cs`
  - `LiteOrm.Tests\ServiceTests.cs`
- Focus:
  - `DeptName` / `ParentDeptName`
  - second-level expansion with `AutoExpand = true`
  - sorting and paging on related fields

### Filtering with `Expr.ExistsRelated(...)`

- Entry doc: [Associations](../core-usage/associations.en.md)
- Code sources:
  - `LiteOrm.Demo\Demos\ExistsRelatedDemo.cs`
  - `LiteOrm.Tests\ExprEnhancedTests.cs`
- Focus:
  - forward filtering
  - reverse-path inference
  - combining with ordinary predicates
  - when `Expr.ExistsRelated(...)` is the better fit

## 5. Advanced feature examples

### Transactions

- Entry doc: [Transactions](../di/transactions.en.md)
- Code source:
  - `LiteOrm.Demo\Demos\TransactionDemo.cs`
- Focus:
  - declarative transactions
  - rollback on failure
  - wrapping a business workflow

### `timestamp` optimistic concurrency

- Entry doc: [CRUD Guide](../core-usage/crud-guide.en.md)
- Code sources:
  - `LiteOrm.Tests\ObjectDAOTests.cs`
  - `LiteOrm.Tests\Models\TestTimestampUser.cs`
- Focus:
  - `[Column(..., IsTimestamp = true)]`
  - `ObjectDAO<T>.Update(entity, timestamp)`
  - `ObjectDAO<T>.UpdateAsync(entity, timestamp)`
  - `false` as the concurrency-conflict result

### Sharding and `TableArgs`

- Entry doc: [Sharding and TableArgs](../advanced-topics/sharding-and-tableargs.en.md)
- Code sources:
  - `LiteOrm.Demo\Demos\ShardingQueryDemo.cs`
  - `LiteOrm.Demo\Models\SalesRecord.cs`
- Focus:
  - `IArged`
  - `TableArgs`
  - overriding shard arguments per query
  - month-based table routing in read/write flows

### Performance tuning and bulk providers

- Entry doc: [Performance](../advanced-topics/performance.en.md)
- Code sources:
  - `LiteOrm.Demo\Data\DbInitializer.cs`
  - `LiteOrm.Demo\Demos\MySqlBulkInsertProvider.cs` (implementation class: `MySqlBulkCopyProvider`)
  - `LiteOrm.Tests\ServiceTests.cs`
- Focus:
  - bulk initialization
  - `SearchAs<T>` projections
  - `ExistsAsync` vs `CountAsync`
  - real `IBulkProvider` implementations

### Window functions

- Entry doc: [Window functions](../advanced-topics/window-functions.en.md)
- Code source:
  - `LiteOrm.Demo\Demos\WindowFunctionDemo.cs`
- Focus:
  - registering window functions
  - aggregate window queries
  - mapping ranking and analytics results

### Custom paging

- Entry doc: [Custom paging](../advanced-topics/custom-paging.en.md)
- Best for: older databases whose paging syntax is not compatible with the default dialect behavior

## 6. Extensibility examples

### NativeAOT

- Entry doc: [NativeAOT Support](../advanced-topics/aot.en.md)
- Source: `LiteOrm.AotDemo\Program.cs` (an end-to-end example covering full CRUD + `SearchAs`/`SearchOneAs`)
- Best for: publishing without a JIT; verifying AOT compatibility of source-generated table metadata and DataReader mappings

### Expression extension

- Entry doc: [Expression extension](../extensibility/expression-extension.en.md)
- Code source:
  - `LiteOrm.Demo\Demos\DateFormatDemo.cs`
- Focus:
  - registering method translation
  - date-formatting examples
  - the Lambda-to-SQL extension flow

### Function validator

- Entry doc: [Function expression validator](../extensibility/function-validator.en.md)
- Focus:
  - whitelist policies
  - security boundaries
  - combining validation with expression extension

### Custom `SqlBuilder`

- Entry doc: [Custom SqlBuilder and dialect extension](../extensibility/custom-sqlbuilder.en.md)
- Focus:
  - dialect extension entry points
  - shared override points for custom paging
  - registration and integration flow

## 7. Recommended reading order

If you want to move from simple to advanced examples, this is a practical sequence:

1. [First end-to-end example](../getting-started/first-example-di.en.md)
2. [CRUD guide](../core-usage/crud-guide.en.md)
3. [Query Overview](../core-usage/query-overview.en.md)
4. [Lambda Guide](../core-usage/lambda-guide.en.md)
5. [Expr Guide](../core-usage/expr-guide.en.md)
6. [ExprString Guide](../core-usage/exprstring-guide.en.md)
7. [Associations](../core-usage/associations.en.md)
8. [Transactions](../di/transactions.en.md)
9. [Sharding and TableArgs](../advanced-topics/sharding-and-tableargs.en.md)
10. [Performance](../advanced-topics/performance.en.md)
11. [Expression extension](../extensibility/expression-extension.en.md)

## Related links

- [Back to English docs hub](../README.md)
- [API Index](./api-index.en.md)

