## Documentation Map

> **[⬆ Back to the Website](./index.html)**

This is the main index of the LiteOrm documentation set. For deeper coverage, follow the navigation below:

### Getting Started

|Document|Description|
|-|-|
|[Overview](./getting-started/overview.md)|What LiteOrm is, how the project is organized, and the scenarios it fits|
|[Installation](./getting-started/installation.md)|Environment requirements and installation (base library and DI extension)|
|[First Example (Base Only)](./getting-started/first-example.md)|A minimal runnable example that does not depend on the DI extension|
|[First Example (Manual, No DI)](./getting-started/first-example-manual.md)|Build the context by hand with `LiteOrmContext`, no DI container involved|
|[First Example (DI Extension)](./getting-started/first-example-di.md)|Depends on `LiteOrm.DependencyInjection` and supports AOP attributes|

### Core Usage

|Document|Description|
|-|-|
|[Entity Mapping](./core-usage/entity-mapping.md)|Entity definitions and mapping|
|[View Models](./core-usage/view-models-and-services.md)|View models and the service layer|
|[CRUD Guide](./core-usage/crud-guide.md)|Create, read, update and delete operations|
|[Query Overview](./core-usage/query-overview.md)|The three query styles compared, and how to choose|
|[Lambda Guide](./core-usage/lambda-guide.md)|Lambda filtering, sorting and subqueries|
|[Expr Guide](./core-usage/expr-guide.md)|Building, composing and reasoning about Expr|
|[ExprString Guide](./core-usage/exprstring-guide.md)|Hand-written SQL via interpolated strings, and parameterization|
|[Associations](./core-usage/associations.md)|Table relations and JOINs|
|[Lambda & Expr Mixing](./core-usage/lambda-expr-mixing.md)|Reusing dynamic Expr inside strongly typed Lambda queries|
|[CTE Guide](./core-usage/cte-guide.md)|Common table expressions and the gotchas|

### DI Extension

> This section builds on the `LiteOrm.DependencyInjection` package; install it and register DI first.

|Document|Description|
|-|-|
|[Transactions](./di/transactions.md)|Transactions and concurrency control|
|[Logging & Diagnostics](./di/logging.md)|ServiceLog, the Log attribute, and slow query logging|
|[Service Authorization](./di/service-authorization.md)|`[ServicePermission]` declarations and hooking up `IUserContext`|

### Advanced Topics

|Document|Description|
|-|-|
|[Sharding](./advanced-topics/sharding-and-tableargs.md)|Sharding strategy and routing|
|[Performance](./advanced-topics/performance.md)|Performance tuning guidance|
|[Window Functions](./advanced-topics/window-functions.md)|Window function support|
|[Custom Paging](./advanced-topics/custom-paging.md)|Extending the paging strategy|
|[AOT Support](./advanced-topics/aot.md)|NativeAOT trimming and source generators|
|[Security](./advanced-topics/security.md)|SQL injection protection and other safety mechanisms|
|[Permission Filtering and User Scopes](./advanced-topics/permission-filtering.md)|Runtime Expr/GenericSqlExpr, ConstFilter, and choosing a table-routing approach|
|[Remote Service](./advanced-topics/remote-service.md)|Using the Remote client and server|
|[Data Mapping](./advanced-topics/data-mapping.md)|Value converters, DataReader mapping, AOT differences and custom extensions|

### Extensibility

|Document|Description|
|-|-|
|[Expression Extension](./extensibility/expression-extension.md)|Custom expressions|
|[Function Validator](./extensibility/function-validator.md)|Function validation|
|[SqlBuilder](./extensibility/custom-sqlbuilder.md)|SQL dialect extensions|
|[Expr Serialization Format](./extensibility/expr-serialization.md)|Compact vs. normal JSON modes compared|
|[Frontend QueryString](./extensibility/frontend-querystring.md)|Driving server-side Expr queries from URL parameters|
|[Frontend Native Expr](./extensibility/frontend-native-expr.md)|Posting Expr JSON in the LiteOrm serialization format|
|[Domestic/Compatible Database SqlBuilder Guide](./extensibility/domestic-database-sqlbuilder.md)|Integrating DM, KingbaseES, GaussDB, OceanBase, TiDB and GreatDB|
|[Generic Controller](./extensibility/generic-controller.md)|Generic controller base classes and dynamic controller generation|

### Use Cases

|Document|Description|
|-|-|
|[Tenant Isolation](./typical-applications/tenant-isolation.md)|Tenant context, runtime conditions and fixed filters, sharding|
|[Data Permissions](./typical-applications/data-permission.md)|Query filtering, scoped writes, object-level checks and role fallbacks|
|[Soft Deletes and Historical Data](./typical-applications/soft-delete-and-archive.md)|Fixed-slice read paths, soft-delete writes, unique constraints and archiving|
|[Audit and Change Tracking](./typical-applications/audit-and-change-tracking.md)|Entity events, field diffs, transaction boundaries and call logs for auditing|
|[Sensitive Data Protection](./typical-applications/sensitive-data-protection.md)|Encrypted column storage, global custom-type registration, blind indexes and key management|
|[JSON Columns for Objects and Field-Level Queries](./typical-applications/json-column-mapping.md)|Whole-object JSON columns, converter registration, field-to-JSON-function mapping and field-level queries|
|[Computed Columns in Practice](./typical-applications/computed-columns.md)|Three worked examples — tier discounts, listing status bits, cross-table display names — with generated SQL|
|[Concurrency and Read/Write Splitting](./typical-applications/concurrency-and-read-write-splitting.md)|Timestamp optimistic concurrency, transaction boundaries, read-only replicas and consistency|

### Reference

|Document|Description|
|-|-|
|[Config Reference](./reference/configuration-reference.md)|Configuration options|
|[API Index](./reference/api-index.md)|Quick API lookup|
|[Glossary](./reference/glossary.md)|Terminology|
|[AI Guide](./reference/ai-guide.md)|AI-assisted development|
|[Example Index](./reference/example-index.md)|Index of sample code|
|[SQL Examples](./reference/sql-examples.md)|Generated SQL examples|
|[Compatibility](./reference/database-compatibility.md)|Differences between databases|

### Related Resources

|Resource|
|-|
|[Demo project](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo)|
|[Source code](https://github.com/danjiewu/LiteOrm)|
|[Unit tests](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Tests)|
|[Benchmark report](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md)|
|[Changelog](./CHANGELOG.md)|
|[8.1 Upgrade Guide](./upgrade-guides/upgrade-guide-8.1.md)|

### Recommended Reading Path

1. New to LiteOrm: start with [Overview](./getting-started/overview.md) and [Installation](./getting-started/installation.md) in Getting Started.
2. Configuration and a first app: pick [Base Only](./getting-started/first-example.md), [Manual (No DI)](./getting-started/first-example-manual.md), or [DI Extension](./getting-started/first-example-di.md) depending on your project.
3. About to adopt it in a real project: continue through Core Usage to build up the full picture of entities, queries, writes and associations.
4. Integrating via `LiteOrm.DependencyInjection` (Autofac, AOP): read the [Config Reference](./reference/configuration-reference.md) first, then the transactions, logging and service authorization topics in DI Extension.
5. Sharding, performance or dialect differences: continue with Advanced Topics.
6. Extending the framework itself: see Extensibility.
7. Landing a concrete business scenario (tenant isolation, data permissions, soft delete and archiving, auditing, sensitive field encryption, JSON columns for objects, concurrency and read/write splitting): see Use Cases — every topic ships a requirement description plus copy-paste-ready code.
8. Need to confirm a configuration key, an interface name or a term: go straight to Reference.
