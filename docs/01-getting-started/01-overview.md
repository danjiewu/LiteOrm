# 概述与适用场景

LiteOrm 是一个轻量级、高性能的 .NET ORM 框架。它兼顾了微 ORM 的执行效率和完整 ORM 的建模体验，适合希望保留 SQL 控制力、同时减少重复数据访问代码的项目。

## 适合哪些场景

- 需要比传统 ORM 更接近 SQL 执行过程的业务系统。
- 有多数据源、读写分离、分表分库等需求的中大型系统。
- 希望在 Lambda 查询、动态表达式和原生 SQL 片段之间自由切换。
- 需要保留较高性能，同时不想完全退回到手写 ADO.NET 或 Dapper 拼装层。

## LiteOrm 的核心特点

- 支持 `Lambda`、`Expr`、`ExprString` 三种查询方式。
- 用特性描述实体、外键和视图，完成自动关联查询。
- 同时支持 DAO 风格和 Service 风格的数据访问封装。
- 内置事务、分表、连接池、异步调用和多数据库方言支持。
- 可以通过表达式扩展和 `SqlBuilder` 扩展自定义数据库能力。

## 与常见方案的定位差异

| 方案 | 更擅长的方向 |
| --- | --- |
| EF Core | 迁移、完整生态、约定优先 |
| Dapper | 极简、手写 SQL、最薄抽象 |
| LiteOrm | 性能、表达式扩展、自动关联、灵活 SQL 控制 |

## 推荐的学习顺序

1. 先完成 [安装与环境要求](./02-installation.md)。
2. 再阅读 [配置与注册](./03-configuration-and-registration.md)。
3. 跑通 [第一个完整示例](./04-first-example.md)。
4. 然后进入 [实体映射与数据源](../02-core-usage/01-entity-mapping.md) 和 [查询指南](../02-core-usage/03-query-guide.md)。

## 相关链接

- [返回目录](../SUMMARY.md)
- [文档中心](../SUMMARY.md)
- [API 索引](../05-reference/02-api-index.md)
- [Demo 项目](../../LiteOrm.Demo/)

