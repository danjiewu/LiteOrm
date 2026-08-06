## 导航

本文档是 LiteOrm 主要内容介绍。如需深入学习，请参考以下导航：

### 入门篇 / Getting Started

|中文|English|说明|
|-|-|-|
|[概览](./01-getting-started/01-overview.md)|[Overview](./01-getting-started/01-overview.en.md)|框架介绍、项目组成与适用场景|
|[安装](./01-getting-started/02-installation.md)|[Installation](./01-getting-started/02-installation.en.md)|环境要求与安装（基础库 / DI 扩展双场景）|
|[第一个完整示例（仅基础库）](./01-getting-started/03-first-example.md)|[First Example (Base Only)](./01-getting-started/03-first-example.en.md)|不依赖 DI 扩展的最小可运行示例|
|[第一个完整示例（DI 扩展）](./01-getting-started/05-first-example-di.md)|[First Example (DI Extension)](./01-getting-started/05-first-example-di.en.md)|依赖 `LiteOrm.DependencyInjection`, 支持 AOP 特性|

### 核心使用篇 / Core Usage

|中文|English|说明|
|-|-|-|
|[实体映射](./02-core-usage/01-entity-mapping.md)|[Entity Mapping](./02-core-usage/01-entity-mapping.en.md)|实体定义与映射|
|[视图模型](./02-core-usage/02-view-models-and-services.md)|[View Models](./02-core-usage/02-view-models-and-services.en.md)|视图模型与服务层|
|[CRUD指南](./02-core-usage/03-crud-guide.md)|[CRUD Guide](./02-core-usage/03-crud-guide.en.md)|增删改查操作|
|[查询总览](./02-core-usage/04-query-overview.md)|[Query Overview](./02-core-usage/04-query-overview.en.md)|三种查询方式对比与选型|
|[Lambda 查询](./02-core-usage/05-lambda-guide.md)|[Lambda Guide](./02-core-usage/05-lambda-guide.en.md)|Lambda 过滤、排序、子查询|
|[Expr 使用指南](./02-core-usage/06-expr-guide.md)|[Expr Guide](./02-core-usage/06-expr-guide.en.md)|Expr 构造、组合与语义规则|
|[ExprString 指南](./02-core-usage/07-exprstring-guide.md)|[ExprString Guide](./02-core-usage/07-exprstring-guide.en.md)|插值字符串手写 SQL 与参数化|
|[关联查询](./02-core-usage/08-associations.md)|[Associations](./02-core-usage/08-associations.en.md)|表关联与 JOIN|
|[Lambda 与 Expr 组合](./02-core-usage/09-lambda-expr-mixing.md)|[Lambda \& Expr Mixing](./02-core-usage/09-lambda-expr-mixing.en.md)|在强类型 Lambda 中复用动态 Expr|
|[CTE 指南](./02-core-usage/10-cte-guide.md)|[CTE Guide](./02-core-usage/10-cte-guide.en.md)|公共表表达式与注意事项|

### 高级特性篇 / Advanced Topics

|中文|English|说明|
|-|-|-|
|[分表分库](./03-advanced-topics/02-sharding-and-tableargs.md)|[Sharding](./03-advanced-topics/02-sharding-and-tableargs.en.md)|分表策略与路由|
|[性能](./03-advanced-topics/03-performance.md)|[Performance](./03-advanced-topics/03-performance.en.md)|性能调优建议|
|[窗口函数](./03-advanced-topics/04-window-functions.md)|[Window Functions](./03-advanced-topics/04-window-functions.en.md)|窗口函数支持|
|[自定义分页](./03-advanced-topics/05-custom-paging.md)|[Custom Paging](./03-advanced-topics/05-custom-paging.en.md)|分页方案扩展|
|[安全性](./03-advanced-topics/08-security.md)|[Security](./03-advanced-topics/08-security.en.md)|SQL 注入防护与安全机制|
|[远程服务](./03-advanced-topics/09-remote-service.md)|[Remote Service](./03-advanced-topics/09-remote-service.en.md)|Remote 客户端与服务端使用|

### 扩展开发篇 / Extensibility

|中文|English|说明|
|-|-|-|
|[表达式扩展](./04-extensibility/01-expression-extension.md)|[Expression Extension](./04-extensibility/01-expression-extension.en.md)|自定义表达式|
|[验证器](./04-extensibility/02-function-validator.md)|[Function Validator](./04-extensibility/02-function-validator.en.md)|函数验证机制|
|[SqlBuilder](./04-extensibility/03-custom-sqlbuilder.md)|[SqlBuilder](./04-extensibility/03-custom-sqlbuilder.en.md)|SQL 方言扩展|
|[Expr 序列化格式](./04-extensibility/04-expr-serialization.md)|[Expr Serialization Format](./04-extensibility/04-expr-serialization.en.md)|JSON 简洁模式与正常模式对比|
|[前端 QueryString 查询](./04-extensibility/05-frontend-querystring.md)|[Frontend QueryString](./04-extensibility/05-frontend-querystring.en.md)|用 URL 参数驱动后端 Expr 查询|
|[前端原生 Expr 查询](./04-extensibility/06-frontend-native-expr.md)|[Frontend Native Expr](./04-extensibility/06-frontend-native-expr.en.md)|按 LiteOrm 序列化格式提交 Expr JSON|
| [国产/兼容数据库 SqlBuilder 开发指南](./04-extensibility/08-domestic-database-sqlbuilder.md)|[Domestic/Compatible Database SqlBuilder Guide](./04-extensibility/08-domestic-database-sqlbuilder.en.md)|达梦、人大金仓、GaussDB、OceanBase、TiDB、GreatDB 接入指南|

### DI扩展 / DI Extension


|中文|English|说明|
|-|-|-|
|[事务](./06-di/01-transactions.md)|[Transactions](./06-di/01-transactions.en.md)|事务与并发控制|
|[权限过滤](./06-di/02-permission-filtering.md)|[Permission Filtering](./06-di/02-permission-filtering.en.md)|用户范围过滤与访问控制|
|[日志与诊断](./06-di/03-logging.md)|[Logging \& Diagnostics](./06-di/03-logging.en.md)|ServiceLog、Log 特性与慢查询日志|
|[泛型 Controller 与动态生成](./06-di/04-generic-controller.md)|[Generic Controller](./06-di/04-generic-controller.en.md)|泛型基类 Controller 与动态 Controller 生成|

### 参考文档 / Reference

|中文|English|说明|
|-|-|-|
|[配置参考](./05-reference/01-configuration-reference.md)|[Config Reference](./05-reference/01-configuration-reference.en.md)|配置项说明|
|[API索引](./05-reference/02-api-index.md)|[API Index](./05-reference/02-api-index.en.md)|API 快速索引|
|[术语表](./05-reference/03-glossary.md)|[Glossary](./05-reference/03-glossary.en.md)|术语解释|
|[AI指南](./05-reference/04-ai-guide.md)|[AI Guide](./05-reference/04-ai-guide.en.md)|AI 辅助开发|
|[示例索引](./05-reference/05-example-index.md)|[Example Index](./05-reference/05-example-index.en.md)|示例代码索引|
|[SQL示例](./05-reference/06-sql-examples.md)|[SQL Examples](./05-reference/06-sql-examples.en.md)|SQL 生成示例|
|[兼容性](./05-reference/07-database-compatibility.md)|[Compatibility](./05-reference/07-database-compatibility.en.md)|各数据库差异|

### 相关资源 / Related Resources

|资源|Resource|
|-|-|
|[Demo 项目](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo)|[Demo project](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo)|
|[源代码](https://github.com/danjiewu/LiteOrm)|[Source code](https://github.com/danjiewu/LiteOrm)|
|[单元测试](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Tests)|[Unit tests](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Tests)|
|[性能报告](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md)|[Benchmark report](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md)|
|[变更日志](./CHANGELOG.md)|[Changelog](./CHANGELOG.en.md)|
|[8.1 升级指南](./upgrade-guides/01-upgrade-guide-8.1.md)|[8.1 Upgrade Guide](./upgrade-guides/01-upgrade-guide-8.1.en.md)|

### 推荐阅读路径

1. 第一次接触 LiteOrm：先看"入门篇"的[概览](./01-getting-started/01-overview.md)与[安装](./01-getting-started/02-installation.md)。
2. 配置与第一个示例：根据项目类型选择[仅基础库](./01-getting-started/03-first-example.md)或 [DI 扩展](./01-getting-started/05-first-example-di.md)。
3. 准备接入业务项目：继续阅读"核心使用篇"，建立实体、查询、写入和关联的整体认识。
4. 使用 `LiteOrm.DependencyInjection` 集成（Autofac、AOP）：先阅读[配置参考](./05-reference/01-configuration-reference.md)，再了解"DI扩展"中的事务、权限过滤等特性。
5. 涉及分表、性能或数据库方言差异：继续阅读"高级特性篇"。
6. 需要扩展框架能力：查阅"扩展开发篇"。
7. 需要快速确认配置项、接口名或术语：直接查阅"参考篇"。
