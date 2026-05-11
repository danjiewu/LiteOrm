## 导航

本文档是 LiteOrm 主要内容介绍。如需深入学习，请参考以下导航：

### 入门篇 / Getting Started

| 中文 | English | 说明 |
|-----|---------|------|
| [概览](./01-getting-started/01-overview.md) | [Overview](./01-getting-started/01-overview.en.md) | 框架介绍与适用场景 |
| [安装](./01-getting-started/02-installation.md) | [Installation](./01-getting-started/02-installation.en.md) | 安装步骤与环境配置 |
| [配置](./01-getting-started/03-configuration-and-registration.md) | [Configuration](./01-getting-started/03-configuration-and-registration.en.md) | 服务注册与配置 |
| [示例](./01-getting-started/04-first-example.md) | [First Example](./01-getting-started/04-first-example.en.md) | 完整使用示例 |

### 核心使用篇 / Core Usage

| 中文 | English | 说明 |
|-----|---------|------|
| [实体映射](./02-core-usage/01-entity-mapping.md) | [Entity Mapping](./02-core-usage/01-entity-mapping.en.md) | 实体定义与映射 |
| [视图模型](./02-core-usage/02-view-models-and-services.md) | [View Models](./02-core-usage/02-view-models-and-services.en.md) | 视图模型与服务层 |
| [查询指南](./02-core-usage/03-query-guide.md) | [Query Guide](./02-core-usage/03-query-guide.en.md) | 各种查询方式 |
| [CRUD指南](./02-core-usage/04-crud-guide.md) | [CRUD Guide](./02-core-usage/04-crud-guide.en.md) | 增删改查操作 |
| [关联查询](./02-core-usage/05-associations.md) | [Associations](./02-core-usage/05-associations.en.md) | 表关联与 JOIN |
| [Lambda 与 Expr 组合](./02-core-usage/06-lambda-expr-mixing.md) | [Lambda & Expr Mixing](./02-core-usage/06-lambda-expr-mixing.en.md) | 在强类型 Lambda 中复用动态 Expr |
| [CTE 指南](./02-core-usage/07-cte-guide.md) | [CTE Guide](./02-core-usage/07-cte-guide.en.md) | 公共表表达式与注意事项 |

### 高级特性篇 / Advanced Topics

| 中文 | English | 说明 |
|-----|---------|------|
| [事务](./03-advanced-topics/01-transactions.md) | [Transactions](./03-advanced-topics/01-transactions.en.md) | 事务与并发控制 |
| [分表分库](./03-advanced-topics/02-sharding-and-tableargs.md) | [Sharding](./03-advanced-topics/02-sharding-and-tableargs.en.md) | 分表策略与路由 |
| [性能](./03-advanced-topics/03-performance.md) | [Performance](./03-advanced-topics/03-performance.en.md) | 性能调优建议 |
| [窗口函数](./03-advanced-topics/04-window-functions.md) | [Window Functions](./03-advanced-topics/04-window-functions.en.md) | 窗口函数支持 |
| [自定义分页](./03-advanced-topics/05-custom-paging.md) | [Custom Paging](./03-advanced-topics/05-custom-paging.en.md) | 分页方案扩展 |
| [权限过滤](./03-advanced-topics/06-permission-filtering.md) | [Permission Filtering](./03-advanced-topics/06-permission-filtering.en.md) | 用户范围过滤与访问控制 |
| [日志与诊断](./03-advanced-topics/07-logging.md) | [Logging & Diagnostics](./03-advanced-topics/07-logging.en.md) | ServiceLog、Log 特性与慢查询日志 |
| [安全性](./03-advanced-topics/08-security.md) | [Security](./03-advanced-topics/08-security.en.md) | SQL 注入防护与安全机制 |

### 扩展开发篇 / Extensibility

| 中文 | English | 说明 |
|-----|---------|------|
| [表达式扩展](./04-extensibility/01-expression-extension.md) | [Expression Extension](./04-extensibility/01-expression-extension.en.md) | 自定义表达式 |
| [验证器](./04-extensibility/02-function-validator.md) | [Function Validator](./04-extensibility/02-function-validator.en.md) | 函数验证机制 |
| [SqlBuilder](./04-extensibility/03-custom-sqlbuilder.md) | [SqlBuilder](./04-extensibility/03-custom-sqlbuilder.en.md) | SQL 方言扩展 |
| [Expr 序列化格式](./04-extensibility/04-expr-serialization.md) | [Expr Serialization Format](./04-extensibility/04-expr-serialization.en.md) | JSON 简洁模式与正常模式对比 |
| [前端 QueryString 查询](./04-extensibility/05-frontend-querystring.md) | [Frontend QueryString](./04-extensibility/05-frontend-querystring.en.md) | 用 URL 参数驱动后端 Expr 查询 |
| [前端原生 Expr 查询](./04-extensibility/06-frontend-native-expr.md) | [Frontend Native Expr](./04-extensibility/06-frontend-native-expr.en.md) | 按 LiteOrm 序列化格式提交 Expr JSON |
| [泛型 Controller 与动态生成](./04-extensibility/07-generic-controller.md) | [Generic Controller](./04-extensibility/07-generic-controller.en.md) | 泛型基类 Controller 与动态 Controller 生成 |

### 参考文档 / Reference

| 中文 | English | 说明 |
|-----|---------|------|
| [配置参考](./05-reference/01-configuration-reference.md) | [Config Reference](./05-reference/01-configuration-reference.en.md) | 配置项说明 |
| [API索引](./05-reference/02-api-index.md) | [API Index](./05-reference/02-api-index.en.md) | API 快速索引 |
| [术语表](./05-reference/03-glossary.md) | [Glossary](./05-reference/03-glossary.en.md) | 术语解释 |
| [AI指南](./05-reference/05-ai-guide.md) | [AI Guide](./05-reference/05-ai-guide.en.md) | AI 辅助开发 |
| [示例索引](./05-reference/06-example-index.md) | [Example Index](./05-reference/06-example-index.en.md) | 示例代码索引 |
| [SQL示例](./05-reference/07-sql-examples.md) | [SQL Examples](./05-reference/07-sql-examples.en.md) | SQL 生成示例 |
| [兼容性](./05-reference/08-database-compatibility.md) | [Compatibility](./05-reference/08-database-compatibility.en.md) | 各数据库差异 |

### 相关资源 / Related Resources

| 资源 | Resource |
|-----|----------|
| [Demo 项目](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo) | [Demo project](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo) |
| [源代码](https://github.com/danjiewu/LiteOrm) | [Source code](https://github.com/danjiewu/LiteOrm) |
| [单元测试](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Tests) | [Unit tests](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Tests) |
| [性能报告](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md) | [Benchmark report](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md) |

### 推荐阅读路径

1. 第一次接触 LiteOrm：先看"入门篇"的四篇文档。
2. 准备接入业务项目：继续阅读"核心使用篇"，建立实体、查询、写入和关联的整体认识。
3. 涉及事务、分表、性能或数据库方言差异：继续阅读"高级特性篇"。
4. 需要扩展框架能力：查阅"扩展开发篇"。
5. 需要快速确认配置项、接口名或术语：直接查阅"参考篇"。
