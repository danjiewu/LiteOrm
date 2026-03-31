# LiteOrm 使用文档

LiteOrm 是一个轻量级、高性能的 .NET ORM 框架，结合了微 ORM 的性能和完整 ORM 的易用性。

## 文档目录

### 快速入门

| 文档 | 说明 |
|------|------|
| [01_QuickStart.md](./01_QuickStart.md) | 快速入门：安装、配置、第一个示例 |

### 基础指南

| 文档 | 说明 |
|------|------|
| [02_CoreConcepts.md](./02_CoreConcepts.md) | 基础概念：架构、实体、视图、数据源 |
| [03_QueryGuide.md](./03_QueryGuide.md) | 查询指南：Lambda/Expr/ExprString 三种查询方式 |
| [04_CrudGuide.md](./04_CrudGuide.md) | 增删改查：完整操作指南 |

### 专题详解

| 文档 | 说明 |
|------|------|
| [EXP_Transaction.md](./EXP/EXP_Transaction.md) | 事务处理：声明式与手动事务 |
| [EXP_Sharding.md](./EXP/EXP_Sharding.md) | 分表分库：IArged 接口动态路由 |
| [EXP_Performance.md](./EXP/EXP_Performance.md) | 性能优化：连接池、参数化查询 |
| [EXP_ExpressionExtension.md](./EXP/EXP_ExpressionExtension.md) | 表达式扩展：自定义方法与函数 |
| [EXP_WindowFunctions.md](./EXP/EXP_WindowFunctions.md) | 窗口函数：聚合与排序分析 |
| [EXP_FunctionExprValidator.md](./EXP/EXP_FunctionExprValidator.md) | 函数验证器：安全策略控制 |

## 核心特性

- 极速性能：接近原生 Dapper，远超 EF Core
- 多数据库支持：SQL Server、MySQL、Oracle、PostgreSQL、SQLite
- 三种查询方式：Lambda 表达式、Expr 对象、ExprString 插值字符串
- 自动关联查询：通过特性实现无损 JOIN
- 声明式事务：`[Transaction]` 特性 AOP 管理
- 动态分表：`IArged` 接口支持分表路由
- 完整异步支持：async/await
- 强类型安全：泛型接口，编译时检查

## 相关资源

- [GitHub 仓库](https://github.com/danjiewu/LiteOrm)
- [NuGet 包](https://www.nuget.org/packages/LiteOrm/)
- [API 参考](./LITEORM_API_REFERENCE.zh.md)
- [AI 使用指南](./LITEORM_API_GUIDE_FOR_AI.md)
