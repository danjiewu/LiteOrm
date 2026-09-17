## 导航

本文档是 LiteOrm 主要内容介绍。如需深入学习，请参考以下导航：

### 入门篇 / Getting Started

|中文|English|说明|
|-|-|-|
|[概览](./getting-started/overview.md)|[Overview](./getting-started/overview.en.md)|框架介绍、项目组成与适用场景|
|[安装](./getting-started/installation.md)|[Installation](./getting-started/installation.en.md)|环境要求与安装（基础库 / DI 扩展双场景）|
|[第一个完整示例（仅基础库）](./getting-started/first-example.md)|[First Example (Base Only)](./getting-started/first-example.en.md)|不依赖 DI 扩展的最小可运行示例|
|[第一个完整示例（手动构造，无 DI）](./getting-started/first-example-manual.md)|[First Example (Manual, No DI)](./getting-started/first-example-manual.en.md)|用 `LiteOrmContext` 手动创建上下文，完全不使用 DI 容器|
|[第一个完整示例（DI 扩展）](./getting-started/first-example-di.md)|[First Example (DI Extension)](./getting-started/first-example-di.en.md)|依赖 `LiteOrm.DependencyInjection`, 支持 AOP 特性|

### 核心使用篇 / Core Usage

|中文|English|说明|
|-|-|-|
|[实体映射](./core-usage/entity-mapping.md)|[Entity Mapping](./core-usage/entity-mapping.en.md)|实体定义与映射|
|[视图模型](./core-usage/view-models-and-services.md)|[View Models](./core-usage/view-models-and-services.en.md)|视图模型与服务层|
|[CRUD指南](./core-usage/crud-guide.md)|[CRUD Guide](./core-usage/crud-guide.en.md)|增删改查操作|
|[查询总览](./core-usage/query-overview.md)|[Query Overview](./core-usage/query-overview.en.md)|三种查询方式对比与选型|
|[Lambda 查询](./core-usage/lambda-guide.md)|[Lambda Guide](./core-usage/lambda-guide.en.md)|Lambda 过滤、排序、子查询|
|[Expr 使用指南](./core-usage/expr-guide.md)|[Expr Guide](./core-usage/expr-guide.en.md)|Expr 构造、组合与语义规则|
|[ExprString 指南](./core-usage/exprstring-guide.md)|[ExprString Guide](./core-usage/exprstring-guide.en.md)|插值字符串手写 SQL 与参数化|
|[关联查询](./core-usage/associations.md)|[Associations](./core-usage/associations.en.md)|表关联与 JOIN|
|[Lambda 与 Expr 组合](./core-usage/lambda-expr-mixing.md)|[Lambda \& Expr Mixing](./core-usage/lambda-expr-mixing.en.md)|在强类型 Lambda 中复用动态 Expr|
|[CTE 指南](./core-usage/cte-guide.md)|[CTE Guide](./core-usage/cte-guide.en.md)|公共表表达式与注意事项|

### DI扩展 / DI Extension

|中文|English|说明|
|-|-|-|
|[事务](./di/transactions.md)|[Transactions](./di/transactions.en.md)|事务与并发控制|
|[权限过滤](./di/permission-filtering.md)|[Permission Filtering](./di/permission-filtering.en.md)|用户范围过滤与访问控制|
|[日志与诊断](./di/logging.md)|[Logging \& Diagnostics](./di/logging.en.md)|ServiceLog、Log 特性与慢查询日志|
|[泛型 Controller 与动态生成](./di/generic-controller.md)|[Generic Controller](./di/generic-controller.en.md)|泛型基类 Controller 与动态 Controller 生成|

### 高级特性篇 / Advanced Topics

|中文|English|说明|
|-|-|-|
|[分表分库](./advanced-topics/sharding-and-tableargs.md)|[Sharding](./advanced-topics/sharding-and-tableargs.en.md)|分表策略与路由|
|[性能](./advanced-topics/performance.md)|[Performance](./advanced-topics/performance.en.md)|性能调优建议|
|[窗口函数](./advanced-topics/window-functions.md)|[Window Functions](./advanced-topics/window-functions.en.md)|窗口函数支持|
|[自定义分页](./advanced-topics/custom-paging.md)|[Custom Paging](./advanced-topics/custom-paging.en.md)|分页方案扩展|
|[AOT 支持](./advanced-topics/aot.md)|[AOT Support](./advanced-topics/aot.en.md)|NativeAOT 裁剪与源生成器|
|[安全性](./advanced-topics/security.md)|[Security](./advanced-topics/security.en.md)|SQL 注入防护与安全机制|
|[远程服务](./advanced-topics/remote-service.md)|[Remote Service](./advanced-topics/remote-service.en.md)|Remote 客户端与服务端使用|
|[数据映射与值转换](./advanced-topics/data-mapping.md)|[Data Mapping](./advanced-topics/data-mapping.en.md)|值转换器、DataReader 映射、AOT 差异与自定义扩展|

### 扩展开发篇 / Extensibility

|中文|English|说明|
|-|-|-|
|[表达式扩展](./extensibility/expression-extension.md)|[Expression Extension](./extensibility/expression-extension.en.md)|自定义表达式|
|[验证器](./extensibility/function-validator.md)|[Function Validator](./extensibility/function-validator.en.md)|函数验证机制|
|[SqlBuilder](./extensibility/custom-sqlbuilder.md)|[SqlBuilder](./extensibility/custom-sqlbuilder.en.md)|SQL 方言扩展|
|[Expr 序列化格式](./extensibility/expr-serialization.md)|[Expr Serialization Format](./extensibility/expr-serialization.en.md)|JSON 简洁模式与正常模式对比|
|[前端 QueryString 查询](./extensibility/frontend-querystring.md)|[Frontend QueryString](./extensibility/frontend-querystring.en.md)|用 URL 参数驱动后端 Expr 查询|
|[前端原生 Expr 查询](./extensibility/frontend-native-expr.md)|[Frontend Native Expr](./extensibility/frontend-native-expr.en.md)|按 LiteOrm 序列化格式提交 Expr JSON|
| [国产/兼容数据库 SqlBuilder 开发指南](./extensibility/domestic-database-sqlbuilder.md)|[Domestic/Compatible Database SqlBuilder Guide](./extensibility/domestic-database-sqlbuilder.en.md)|达梦、人大金仓、GaussDB、OceanBase、TiDB、GreatDB 接入指南|

### 应用场景 / Use Cases

|中文|English|说明|
|-|-|-|
|[多租户隔离](./typical-applications/tenant-isolation.md)|[Tenant Isolation](./typical-applications/tenant-isolation.en.md)|租户上下文、运行时条件与固定筛选、分表分库|
|[数据权限](./typical-applications/data-permission.md)|[Data Permissions](./typical-applications/data-permission.en.md)|查询过滤、范围写入、对象级校验与角色兜底|
|[软删除与历史数据](./typical-applications/soft-delete-and-archive.md)|[Soft Deletes and Historical Data](./typical-applications/soft-delete-and-archive.en.md)|固定切片读路径、软删除写入、唯一约束与归档|
|[审计与变更追踪](./typical-applications/audit-and-change-tracking.md)|[Audit and Change Tracking](./typical-applications/audit-and-change-tracking.en.md)|实体事件、字段 diff、审计落库的事务边界与调用日志|
|[敏感字段加密与脱敏](./typical-applications/sensitive-data-protection.md)|[Sensitive Data Protection](./typical-applications/sensitive-data-protection.en.md)|列级转换器、盲索引、掩码列与密钥管理|
|[并发控制与读写分离](./typical-applications/concurrency-and-read-write-splitting.md)|[Concurrency and Read/Write Splitting](./typical-applications/concurrency-and-read-write-splitting.en.md)|时间戳乐观并发、事务边界、只读副本与读写一致性|

### 参考文档 / Reference

|中文|English|说明|
|-|-|-|
|[配置参考](./reference/configuration-reference.md)|[Config Reference](./reference/configuration-reference.en.md)|配置项说明|
|[API索引](./reference/api-index.md)|[API Index](./reference/api-index.en.md)|API 快速索引|
|[术语表](./reference/glossary.md)|[Glossary](./reference/glossary.en.md)|术语解释|
|[AI指南](./reference/ai-guide.md)|[AI Guide](./reference/ai-guide.en.md)|AI 辅助开发|
|[示例索引](./reference/example-index.md)|[Example Index](./reference/example-index.en.md)|示例代码索引|
|[SQL示例](./reference/sql-examples.md)|[SQL Examples](./reference/sql-examples.en.md)|SQL 生成示例|
|[兼容性](./reference/database-compatibility.md)|[Compatibility](./reference/database-compatibility.en.md)|各数据库差异|

### 相关资源 / Related Resources

|资源|Resource|
|-|-|
|[Demo 项目](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo)|[Demo project](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo)|
|[源代码](https://github.com/danjiewu/LiteOrm)|[Source code](https://github.com/danjiewu/LiteOrm)|
|[单元测试](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Tests)|[Unit tests](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Tests)|
|[性能报告](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md)|[Benchmark report](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md)|
|[变更日志](./CHANGELOG.md)|[Changelog](./CHANGELOG.en.md)|
|[8.1 升级指南](./upgrade-guides/upgrade-guide-8.1.md)|[8.1 Upgrade Guide](./upgrade-guides/upgrade-guide-8.1.en.md)|

### 推荐阅读路径

1. 第一次接触 LiteOrm：先看"入门篇"的[概览](./getting-started/overview.md)与[安装](./getting-started/installation.md)。
2. 配置与第一个示例：根据项目类型选择[仅基础库](./getting-started/first-example.md)、[手动构造（无 DI）](./getting-started/first-example-manual.md)或 [DI 扩展](./getting-started/first-example-di.md)。
3. 准备接入业务项目：继续阅读"核心使用篇"，建立实体、查询、写入和关联的整体认识。
4. 使用 `LiteOrm.DependencyInjection` 集成（Autofac、AOP）：先阅读[配置参考](./reference/configuration-reference.md)，再了解"DI扩展"中的事务、权限过滤等特性。
5. 涉及分表、性能或数据库方言差异：继续阅读"高级特性篇"。
6. 需要扩展框架能力：查阅"扩展开发篇"。
7. 落地具体业务场景（多租户隔离、数据权限、软删除与归档、审计、敏感字段加密、并发与读写分离）：查阅"应用场景"，每个场景都有需求描述与可直接抄的代码。
8. 需要快速确认配置项、接口名或术语：直接查阅"参考篇"。
