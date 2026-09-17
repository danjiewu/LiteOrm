# LiteOrm

> 一个轻量级、高性能的 .NET ORM 框架

[![NuGet](https://img.shields.io/nuget/v/LiteOrm.svg)](https://www.nuget.org/packages/LiteOrm/)
[![License](https://img.shields.io/github/license/danjiewu/LiteOrm.svg)](LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-LiteOrm-brightgreen)](https://github.com/danjiewu/LiteOrm)

***

## 📖 Language / 语言

**[English](./README.en.md)** | **中文**

***

## 📚 文档导航

LiteOrm 兼顾微型 ORM 的执行效率和完整 ORM 的易用性，适合对性能敏感且又需要灵活处理复杂 SQL 的业务场景。

建议从**文档中心**进入，按学习路径阅读完整文档：

**[文档中心](https://danjiewu.github.io/LiteOrm/)**（[docs/README.md](./docs/README.md) 本地导航）

- **入门篇**：快速完成安装、注册和第一个可运行示例
- **核心使用篇**：实体映射、Expr / 查询指南、CRUD、关联查询、Lambda 与 Expr 组合
- **高级特性篇**：事务、分表、性能、窗口函数、权限过滤、日志诊断、远程服务
- **扩展开发篇**：表达式扩展、Expr 序列化、前端 `QueryString` / 原生 `Expr` 查询接入、国产数据库方言

快速入门示例见 [docs/getting-started](./docs/getting-started/first-example.md)，各 API 速查见 [API 索引](./docs/reference/api-index.md)。

## 🎯 核心特性

- **极速性能**：性能接近原生 Dapper，远超 EF Core
- **多数据库支持**：SQL Server、MySQL、Oracle、PostgreSQL、SQLite；内置达梦、人大金仓、华为 GaussDB、OceanBase、TiDB、GreatDB 等国产 / 兼容数据库方言
- **灵活查询**：Lambda、`Expr`、`ExprString` 三种查询方式
- **自动关联**：通过特性实现无损的 JOIN 查询，无需手写 SQL
- **声明式事务**：`[Transaction]` 特性实现 AOP 事务管理
- **日志与诊断**：`ServiceLog`、`Log` 特性及慢查询日志
- **动态分表**：`IArged` 接口支持分表路由
- **异步支持**：完整的 async/await 支持
- **类型安全**：强类型泛型接口，编译时类型检查

## 📋 环境要求

- **.NET 8.0+** / **.NET Standard 2.0**（兼容 .NET Framework 4.6.1+）
- **依赖库**：Autofac、Castle.Core
- **支持的数据库**：SQL Server 2012+、Oracle 12c+、PostgreSQL、MySQL 8.0+、SQLite、达梦（DM）、人大金仓（KingbaseES）、华为 GaussDB / openGauss、OceanBase、TiDB、GreatDB

  > 目标数据库版本较旧时可能需要自定义分页，参见[自定义分页](./docs/advanced-topics/custom-paging.md)。

## 📦 包与安装

| 包 | 说明 |
|----|------|
| `LiteOrm` | 核心库 |
| `LiteOrm.DependencyInjection` | DI 注册（`RegisterLiteOrm`）与 AOP 支持 |
| `LiteOrm.Remote` | Remote 客户端 |
| `LiteOrm.Remote.Server` | Remote 服务端 |

```bash
dotnet add package LiteOrm
dotnet add package LiteOrm.DependencyInjection   # DI 注册（RegisterLiteOrm）需要引用该包
```

## 🚀 快速开始

配置连接、注册 LiteOrm、定义实体与服务，以及首个可运行示例，见：

- [第一个完整示例（DI 扩展）](./docs/getting-started/first-example-di.md)
- [第一个完整示例（手动构造，无 DI）](./docs/getting-started/first-example-manual.md)

## ⚡ 性能基准

基于 LiteOrm.Benchmark 项目的最新对比测试结果（.NET 10.0.11, Linux Ubuntu 24.04.4 LTS, Intel Xeon Silver 4314 CPU 2.40GHz, MySQL）：

### 插入性能对比（ms）

| 框架          |    100 条 |    1000 条 |   10000 条 |
|:---------- | -------: | --------: | --------: |
| **LiteOrm** | **4.71** | **22.13** | **178.12** |
| SqlSugar    |     5.92 |     36.64 |     352.86 |
| FreeSql     |     6.30 |     41.47 |     332.71 |
| EF Core     |    21.13 |    210.10 |   1,837.48 |
| Dapper      |     5.28 |     27.84 |     266.28 |

### 更新性能对比（ms）

| 框架          |    100 条 |    1000 条 |    10000 条 |
|:---------- | -------: | --------: | ---------: |
| **LiteOrm** | **6.02** | **30.77** | **280.36** |
| SqlSugar    |     8.31 |     74.03 |     734.61 |
| FreeSql     |     8.08 |     67.50 |     570.89 |
| EF Core     |    17.31 |    176.71 |   1,374.47 |
| Dapper      |     6.37 |     44.00 |     355.83 |

### Upsert 性能对比（ms）

| 框架          |    100 条 |    1000 条 |    10000 条 |
|:---------- | -------: | --------: | ---------: |
| LiteOrm     |     6.89 | **27.72** | **247.92** |
| SqlSugar    |    12.83 |    129.97 |  7,042.44 |
| FreeSql     |     6.59 |     31.31 |     257.39 |
| EF Core     |    19.85 |    221.15 |   1,538.52 |
| Dapper      | **5.99** |     33.31 |     327.69 |

### 关联查询性能对比（ms）

| 框架          |    100 条 |   1000 条 |   10000 条 |
|:---------- | -------: | -------: | --------: |
| **LiteOrm** | **1.41** | **8.09** | **78.65** |
| SqlSugar    |     2.33 |    20.65 |    179.25 |
| FreeSql     |     1.94 |    10.37 |    101.45 |
| EF Core     |     3.03 |    15.43 |    162.80 |
| Dapper      |     1.64 |    10.12 |     98.68 |

### 内存分配对比（1000 条数据，KB）

| 框架          |         插入 |           更新 |       Upsert |       关联查询 |
|:---------- | ---------: | -----------: | -----------: | ---------: |
| **LiteOrm** | **1,698.87** | **2,324.10** | **2,165.41** | **242.54** |
| SqlSugar    |   8,840.12 |    14,922.50 |   41,965.50 |  9,226.28 |
| FreeSql     |  11,461.28 |    15,545.64 |    4,582.20 |  1,322.68 |
| EF Core     |  31,350.14 |    24,131.62 |   25,801.93 |  9,467.87 |
| Dapper      |   5,371.92 |     6,408.03 |    5,915.40 |    669.82 |

> 📊 详细的性能基准报告见 [LiteOrm.Benchmark](./LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md)

## 📚 文档与示例

| 资源 | 说明 |
|:--- |:--- |
| [文档中心](./docs/README.md) | 按学习路径组织的中英文文档导航 |
| [English Docs Hub](./docs/README.md) | Bilingual docs hub organized by learning path |
| [API 索引](./docs/reference/api-index.md) | 按使用场景整理的接口与能力入口 |
| [AI 使用指南](./docs/reference/ai-guide.md) | 面向 AI 和快速查阅场景的附录 |
| [变更日志](./docs/CHANGELOG.md) | 按版本号记录的功能性变更 |
| [Demo 项目](./LiteOrm.Demo/) | 主要特性的演示工程 |
| [性能报告](./LiteOrm.Benchmark/) | 详细的性能基准测试报告 |
| [单元测试](./LiteOrm.Tests/) | 行为与回归测试覆盖 |

## 🤝 贡献与反馈

如发现问题或有改进建议，欢迎提交 [Issue](https://github.com/danjiewu/LiteOrm/issues) 或 [Pull Request](https://github.com/danjiewu/LiteOrm/pulls)。

## 📄 开源协议

基于 [MIT](LICENSE) 协议发布。