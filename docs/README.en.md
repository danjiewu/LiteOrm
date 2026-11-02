## Documentation Map

> **[⬆ Back to the Website](./index.html)**

This is the main index of the LiteOrm documentation set. For deeper coverage, follow the navigation below:

### Getting Started

|Document|Description|
|-|-|
|[Overview](./getting-started/overview.en.md)|What LiteOrm is, how the project is organized, and the scenarios it fits|
|[Installation](./getting-started/installation.en.md)|Environment requirements and installation (base library and DI extension)|
|[First Example (Base Only)](./getting-started/first-example.en.md)|A minimal runnable example that does not depend on the DI extension|
|[First Example (Manual, No DI)](./getting-started/first-example-manual.en.md)|Build the context by hand with `LiteOrmContext`, no DI container involved|
|[First Example (DI Extension)](./getting-started/first-example-di.en.md)|Depends on `LiteOrm.DependencyInjection` and supports AOP attributes|

### Core Usage

|Document|Description|
|-|-|
|[Entity Mapping](./core-usage/entity-mapping.en.md)|Entity definitions and mapping|
|[View Models](./core-usage/view-models-and-services.en.md)|View models and the service layer|
|[CRUD Guide](./core-usage/crud-guide.en.md)|Create, read, update and delete operations|
|[Query Overview](./core-usage/query-overview.en.md)|The three query styles compared, and how to choose|
|[Lambda Guide](./core-usage/lambda-guide.en.md)|Lambda filtering, sorting and subqueries|
|[Expr Guide](./core-usage/expr-guide.en.md)|Building, composing and reasoning about Expr|
|[ExprString Guide](./core-usage/exprstring-guide.en.md)|Hand-written SQL via interpolated strings, and parameterization|
|[Associations](./core-usage/associations.en.md)|Table relations and JOINs|
|[Lambda & Expr Mixing](./core-usage/lambda-expr-mixing.en.md)|Reusing dynamic Expr inside strongly typed Lambda queries|
|[CTE Guide](./core-usage/cte-guide.en.md)|Common table expressions and the gotchas|

### DI Extension

> This section builds on the `LiteOrm.DependencyInjection` package; install it and register DI first.

|Document|Description|
|-|-|
|[Transactions](./di/transactions.en.md)|Transactions and concurrency control|
|[Logging & Diagnostics](./di/logging.en.md)|ServiceLog, the Log attribute, and slow query logging|
|[Service Authorization](./di/service-authorization.en.md)|`[ServicePermission]` declarations and hooking up `IUserContext`|

### Advanced Topics

|Document|Description|
|-|-|
|[Sharding](./advanced-topics/sharding-and-tableargs.en.md)|Sharding strategy and routing|
|[Performance](./advanced-topics/performance.en.md)|Performance tuning guidance|
|[Window Functions](./advanced-topics/window-functions.en.md)|Window function support|
|[Custom Paging](./advanced-topics/custom-paging.en.md)|Extending the paging strategy|
|[AOT Support](./advanced-topics/aot.en.md)|NativeAOT trimming and source generators|
|[Security](./advanced-topics/security.en.md)|SQL injection protection and other safety mechanisms|
|[Permission Filtering and User Scopes](./advanced-topics/permission-filtering.en.md)|Runtime Expr/GenericSqlExpr, ConstFilter, and choosing a table-routing approach|
|[Remote Service](./advanced-topics/remote-service.en.md)|Using the Remote client and server|
|[Data Mapping](./advanced-topics/data-mapping.en.md)|Value converters, DataReader mapping, AOT differences and custom extensions|

### Extensibility

|Document|Description|
|-|-|
|[Expression Extension](./extensibility/expression-extension.en.md)|Custom expressions|
|[Function Validator](./extensibility/function-validator.en.md)|Function validation|
|[SqlBuilder](./extensibility/custom-sqlbuilder.en.md)|SQL dialect extensions|
|[Expr Serialization Format](./extensibility/expr-serialization.en.md)|Compact vs. normal JSON modes compared|
|[Frontend QueryString](./extensibility/frontend-querystring.en.md)|Driving server-side Expr queries from URL parameters|
|[Frontend Native Expr](./extensibility/frontend-native-expr.en.md)|Posting Expr JSON in the LiteOrm serialization format|
|[Domestic/Compatible Database SqlBuilder Guide](./extensibility/domestic-database-sqlbuilder.en.md)|Integrating DM, KingbaseES, GaussDB, OceanBase, TiDB and GreatDB|
|[Generic Controller](./extensibility/generic-controller.en.md)|Generic controller base classes and dynamic controller generation|

### Use Cases

|Document|Description|
|-|-|
|[Tenant Isolation](./typical-applications/tenant-isolation.en.md)|Tenant context, runtime conditions and fixed filters, sharding|
|[Data Permissions](./typical-applications/data-permission.en.md)|Query filtering, scoped writes, object-level checks and role fallbacks|
|[Soft Deletes and Historical Data](./typical-applications/soft-delete-and-archive.en.md)|Fixed-slice read paths, soft-delete writes, unique constraints and archiving|
|[Audit and Change Tracking](./typical-applications/audit-and-change-tracking.en.md)|Entity events, field diffs, transaction boundaries and call logs for auditing|
|[Sensitive Data Protection](./typical-applications/sensitive-data-protection.en.md)|Encrypted column storage, global custom-type registration, blind indexes and key management|
|[JSON Columns for Objects and Field-Level Queries](./typical-applications/json-column-mapping.en.md)|Whole-object JSON columns, converter registration, field-to-JSON-function mapping and field-level queries|
|[Computed Columns in Practice](./typical-applications/computed-columns.en.md)|Three worked examples — tier discounts, listing status bits, cross-table display names — with generated SQL|
|[Concurrency and Read/Write Splitting](./typical-applications/concurrency-and-read-write-splitting.en.md)|Timestamp optimistic concurrency, transaction boundaries, read-only replicas and consistency|
|[Projection on Demand](./typical-applications/query-projection.en.md)|Four requirements comparing entity-first projection with query-time projection: list pages, exports, detail pages and grouped aggregation|
|[Complex Query Examples](./typical-applications/complex-queries.en.md)|One query JSON including sorting and paging handed directly to `Search`, extracting the filter for counting, orders for a department and its descendants, last 7 days as function vs. parameter, tree SQL via `GenericSqlExpr` and a custom `FunctionExpr`, count and page from one filter, saved/reloaded condition JSON, an equivalent Lambda entry that builds a Query object and passes it in, and SqlGen debugging|

### Reference

|Document|Description|
|-|-|
|[Config Reference](./reference/configuration-reference.en.md)|Configuration options|
|[API Index](./reference/api-index.en.md)|Quick API lookup|
|[Glossary](./reference/glossary.en.md)|Terminology|
|[AI Guide](./reference/ai-guide.en.md)|AI-assisted development|
|[Example Index](./reference/example-index.en.md)|Index of sample code|
|[SQL Examples](./reference/sql-examples.en.md)|Generated SQL examples|
|[Compatibility](./reference/database-compatibility.en.md)|Differences between databases|

### Related Resources

|Resource|
|-|
|[Demo project](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo)|
|[Source code](https://github.com/danjiewu/LiteOrm)|
|[Unit tests](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Tests)|
|[Benchmark report](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md)|
|[Changelog](./CHANGELOG.en.md)|
|[8.1 Upgrade Guide](./upgrade-guides/upgrade-guide-8.1.en.md)|

### Recommended Reading Path

1. New to LiteOrm: start with [Overview](./getting-started/overview.en.md) and [Installation](./getting-started/installation.en.md) in Getting Started.
2. Configuration and a first app: pick [Base Only](./getting-started/first-example.en.md), [Manual (No DI)](./getting-started/first-example-manual.en.md), or [DI Extension](./getting-started/first-example-di.en.md) depending on your project.
3. About to adopt it in a real project: continue through Core Usage to build up the full picture of entities, queries, writes and associations.
4. Integrating via `LiteOrm.DependencyInjection` (Autofac, AOP): read the [Config Reference](./reference/configuration-reference.en.md) first, then the transactions, logging and service authorization topics in DI Extension.
5. Sharding, performance or dialect differences: continue with Advanced Topics.
6. Extending the framework itself: see Extensibility.
7. Landing a concrete business scenario (tenant isolation, data permissions, soft delete and archiving, auditing, sensitive field encryption, JSON columns for objects, concurrency and read/write splitting, projection on demand, complex queries with department trees, last-7-days filtering, dynamic sorting/paging and saved conditions): see Use Cases — every topic ships a requirement description plus copy-paste-ready code.
8. Need to confirm a configuration key, an interface name or a term: go straight to Reference.
