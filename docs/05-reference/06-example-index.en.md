# Example Index

This page groups current LiteOrm examples by scenario instead of by chapter. Most examples come from `LiteOrm.Demo` (end-to-end flows) and `LiteOrm.Tests` (edge cases and verifiable patterns).

## 1. End-to-end onboarding

### Minimal path from configuration to CRUD

- Entry doc: [First end-to-end example](../01-getting-started/04-first-example.en.md)
- Best for: first-time LiteOrm setup when you want a working baseline quickly
- Focus:
  - entity definition
  - service registration
  - insert, query, update, count, and delete in one flow

## 2. Query examples

### Choosing between Lambda, `Expr`, and `ExprString`

- Entry doc: [Query guide](../02-core-usage/03-query-guide.en.md)
- Focus:
  - when to use each query style
  - dynamic condition composition
  - the intended scope of `ExprString`

### `EXISTS`, `Expr.ExistsRelated(...)`, and subqueries

- Entry doc: [Query guide](../02-core-usage/03-query-guide.en.md)
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

- Entry doc: [Query guide](../02-core-usage/03-query-guide.en.md)
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

- Entry doc: [CRUD guide](../02-core-usage/04-crud-guide.en.md)
- Code sources:
  - `LiteOrm.Demo\Data\DbInitializer.cs`
  - `LiteOrm.Tests\ServiceTests.cs`
- Focus:
  - `BatchInsertAsync`
  - `BatchUpdateAsync`
  - `BatchDeleteAsync`
  - the full batch-write loop

### Upsert and mixed batch processing

- Entry doc: [CRUD guide](../02-core-usage/04-crud-guide.en.md)
- Code sources:
  - `LiteOrm.Tests\ServiceTests.cs`
  - `LiteOrm.Demo\Demos\UpdateExprDemo.cs`
- Focus:
  - `BatchUpdateOrInsertAsync`
  - mixed batches with `EntityOperation<T>`
  - conditional updates with `UpdateExpr`

## 4. Association examples

### Minimal `ForeignType` + `ForeignColumn` flow

- Entry doc: [Associations](../02-core-usage/05-associations.en.md)
- Best for: understanding how a single foreign-key relationship maps into view-model fields

### Multi-level associations and `AutoExpand`

- Entry doc: [Associations](../02-core-usage/05-associations.en.md)
- Code sources:
  - `LiteOrm.Demo\Models\User.cs`
  - `LiteOrm.Demo\Models\SalesRecord.cs`
  - `LiteOrm.Tests\ServiceTests.cs`
- Focus:
  - `DeptName` / `ParentDeptName`
  - second-level expansion with `AutoExpand = true`
  - sorting and paging on related fields

### Filtering with `Expr.ExistsRelated(...)`

- Entry doc: [Associations](../02-core-usage/05-associations.en.md)
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

- Entry doc: [Transactions](../03-advanced-topics/01-transactions.en.md)
- Code source:
  - `LiteOrm.Demo\Demos\TransactionDemo.cs`
- Focus:
  - declarative transactions
  - rollback on failure
  - wrapping a business workflow

### Sharding and `TableArgs`

- Entry doc: [Sharding and TableArgs](../03-advanced-topics/02-sharding-and-tableargs.en.md)
- Code sources:
  - `LiteOrm.Demo\Demos\ShardingQueryDemo.cs`
  - `LiteOrm.Demo\Models\SalesRecord.cs`
- Focus:
  - `IArged`
  - `TableArgs`
  - overriding shard arguments per query
  - month-based table routing in read/write flows

### Performance tuning and bulk providers

- Entry doc: [Performance](../03-advanced-topics/03-performance.en.md)
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

- Entry doc: [Window functions](../03-advanced-topics/04-window-functions.en.md)
- Code source:
  - `LiteOrm.Demo\Demos\WindowFunctionDemo.cs`
- Focus:
  - registering window functions
  - aggregate window queries
  - mapping ranking and analytics results

### Custom paging

- Entry doc: [Custom paging](../03-advanced-topics/05-custom-paging.en.md)
- Best for: older databases whose paging syntax is not compatible with the default dialect behavior

## 6. Extensibility examples

### Expression extension

- Entry doc: [Expression extension](../04-extensibility/01-expression-extension.en.md)
- Code source:
  - `LiteOrm.Demo\Demos\DateFormatDemo.cs`
- Focus:
  - registering method translation
  - date-formatting examples
  - the Lambda-to-SQL extension flow

### Function validator

- Entry doc: [Function expression validator](../04-extensibility/02-function-validator.en.md)
- Focus:
  - whitelist policies
  - security boundaries
  - combining validation with expression extension

### Custom `SqlBuilder`

- Entry doc: [Custom SqlBuilder and dialect extension](../04-extensibility/03-custom-sqlbuilder.en.md)
- Focus:
  - dialect extension entry points
  - shared override points for custom paging
  - registration and integration flow

## 7. Recommended reading order

If you want to move from simple to advanced examples, this is a practical sequence:

1. [First end-to-end example](../01-getting-started/04-first-example.en.md)
2. [Query guide](../02-core-usage/03-query-guide.en.md)
3. [CRUD guide](../02-core-usage/04-crud-guide.en.md)
4. [Associations](../02-core-usage/05-associations.en.md)
5. [Transactions](../03-advanced-topics/01-transactions.en.md)
6. [Sharding and TableArgs](../03-advanced-topics/02-sharding-and-tableargs.en.md)
7. [Performance](../03-advanced-topics/03-performance.en.md)
8. [Expression extension](../04-extensibility/01-expression-extension.en.md)

## Related links

- [Back to English docs hub](../README.md)
- [API Index](./02-api-index.en.md)
