# LiteOrm

> A lightweight, high-performance .NET ORM framework

[![NuGet](https://img.shields.io/nuget/v/LiteOrm.svg)](https://www.nuget.org/packages/LiteOrm/)
[![License](https://img.shields.io/github/license/danjiewu/LiteOrm.svg)](LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-LiteOrm-brightgreen)](https://github.com/danjiewu/LiteOrm)

<p align="center">
  <img src="docs/assets/logo/logo.svg" alt="LiteOrm logo" width="220">
</p>

***

## 📖 Language / 语言

**English** | **[中文](./README.md)**

***

## 📚 Documentation

LiteOrm combines micro-ORM speed with full-ORM ergonomics. It fits projects that need predictable performance while still handling rich SQL scenarios cleanly.

Start with the **docs hub**, then follow the learning-path navigation to read the full documentation:

**Homepage**: [https://danjiewu.github.io/LiteOrm/](https://danjiewu.github.io/LiteOrm/) ｜ Local docs nav: **[docs/README.md](./docs/README.md)**

- **Getting Started**: install, register, and run your first working example
- **Core Usage**: entity mapping, Expr / query guides, CRUD, associations, mixing Lambda with Expr
- **Advanced Topics**: transactions, sharding, performance, window functions, permission filtering, diagnostics, remote service
- **Extensibility**: expression extension, Expr serialization, frontend QueryString / native Expr integration, domestic database dialects

The quick-start examples live under [docs/getting-started](./docs/getting-started/first-example.en.md); for API lookup see the [API Index](./docs/reference/api-index.en.md).

## 🎯 Core Features

- **Ultra-Fast Performance**: Performance close to native Dapper, far exceeding EF Core
- **Multi-Database Support**: SQL Server, MySQL, Oracle, PostgreSQL, SQLite; built-in Dameng, KingbaseES, Huawei GaussDB / openGauss, OceanBase, TiDB, GreatDB dialects
- **Flexible Querying**: Lambda, `Expr`, and `ExprString` query methods
- **Automatic Associations**: Attribute-based, seamless JOIN queries without manual SQL
- **Declarative Transactions**: AOP transaction management via `[Transaction]`
- **Logging and Diagnostics**: `ServiceLog`, `Log`, and slow-query diagnostics
- **Dynamic Sharding**: Table routing via the `IArged` interface
- **Async Support**: Complete async/await support
- **Type Safety**: Strong-typed generic interfaces with compile-time type checking

## 📋 Requirements

- **.NET 8.0+** / **.NET Standard 2.0** (.NET Framework 4.6.1+ compatible)
- **Dependencies**: Autofac, Castle.Core
- **Supported databases**: SQL Server 2012+, Oracle 12c+, PostgreSQL, MySQL 8.0+, SQLite, Dameng (DM), KingbaseES, Huawei GaussDB / openGauss, OceanBase, TiDB, and GreatDB

  > Older database versions may require custom paging. See [Custom Paging](./docs/advanced-topics/custom-paging.en.md).

## 📦 Packages & Installation

| Package | Description |
|---------|-------------|
| `LiteOrm` | Core library |
| `LiteOrm.DependencyInjection` | DI registration (`RegisterLiteOrm`) and AOP support |
| `LiteOrm.Remote` | Remote client |
| `LiteOrm.Remote.Server` | Remote server |

```bash
dotnet add package LiteOrm
dotnet add package LiteOrm.DependencyInjection   # required for DI registration (RegisterLiteOrm)
```

## 🚀 Quick Start

For configuring the connection, registering LiteOrm, defining entities and services, and running the first example, see:

- [First Full Example (DI Extension)](./docs/getting-started/first-example-di.en.md)
- [First Full Example (Manual, No DI)](./docs/getting-started/first-example-manual.en.md)

## ⚡ Performance Benchmarks

Latest comparison test results based on the LiteOrm.Benchmark project (.NET 10.0.11, Linux Ubuntu 24.04.4 LTS, Intel Xeon Silver 4314 CPU 2.40GHz, MySQL):

### Insert Performance Comparison (ms)

| Framework | 100 rows | 1000 rows | 10000 rows |
|:----------| --------:| ---------:| ----------:|
| **LiteOrm** | **4.71** | **22.13** | **178.12** |
| SqlSugar | 5.92 | 36.64 | 352.86 |
| FreeSql | 6.30 | 41.47 | 332.71 |
| EF Core | 21.13 | 210.10 | 1,837.48 |
| Dapper | 5.28 | 27.84 | 266.28 |

### Update Performance Comparison (ms)

| Framework | 100 rows | 1000 rows | 10000 rows |
|:----------| --------:| ---------:| ----------:|
| **LiteOrm** | **6.02** | **30.77** | **280.36** |
| SqlSugar | 8.31 | 74.03 | 734.61 |
| FreeSql | 8.08 | 67.50 | 570.89 |
| EF Core | 17.31 | 176.71 | 1,374.47 |
| Dapper | 6.37 | 44.00 | 355.83 |

### Upsert Performance Comparison (ms)

| Framework | 100 rows | 1000 rows | 10000 rows |
|:----------| --------:| ---------:| ----------:|
| LiteOrm | 6.89 | **27.72** | **247.92** |
| SqlSugar | 12.83 | 129.97 | 7,042.44 |
| FreeSql | 6.59 | 31.31 | 257.39 |
| EF Core | 19.85 | 221.15 | 1,538.52 |
| Dapper | **5.99** | 33.31 | 327.69 |

### Join Query Performance Comparison (ms)

| Framework | 100 rows | 1000 rows | 10000 rows |
|:----------| --------:| ---------:| ----------:|
| **LiteOrm** | **1.41** | **8.09** | **78.65** |
| SqlSugar | 2.33 | 20.65 | 179.25 |
| FreeSql | 1.94 | 10.37 | 101.45 |
| EF Core | 3.03 | 15.43 | 162.80 |
| Dapper | 1.64 | 10.12 | 98.68 |

### Memory Allocation Comparison (1000 rows, KB)

| Framework | Insert | Update | Upsert | Join Query |
|:----------| ------:| ------:| ------:| ---------:|
| **LiteOrm** | **1,698.87** | **2,324.10** | **2,165.41** | **242.54** |
| SqlSugar | 8,840.12 | 14,922.50 | 41,965.50 | 9,226.28 |
| FreeSql | 11,461.28 | 15,545.64 | 4,582.20 | 1,322.68 |
| EF Core | 31,350.14 | 24,131.62 | 25,801.93 | 9,467.87 |
| Dapper | 5,371.92 | 6,408.03 | 5,915.40 | 669.82 |

> 📊 For detailed performance benchmark reports, see [LiteOrm.Benchmark](./LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md)

## 📚 Documentation & Resources

| Resource | Description |
|:--- |:--- |
| [Documentation Hub](./docs/README.md) | Bilingual docs hub organized by learning path |
| [中文文档中心](./docs/README.md) | 按学习路径组织的中英文文档导航 |
| [API Index](./docs/reference/api-index.en.md) | Scenario-based API and capability entry points |
| [AI Guide](./docs/reference/ai-guide.en.md) | Compact appendix for assistants and quick API orientation |
| [Changelog](./docs/CHANGELOG.en.md) | Functional changes by version |
| [Demo Project](./LiteOrm.Demo/) | Main feature demonstration project |
| [Performance Report](./LiteOrm.Benchmark/) | Detailed benchmark reports |
| [Unit Tests](./LiteOrm.Tests/) | Behavior and regression coverage |

## 🤝 Contributing

Found a bug or have an improvement suggestion? Please submit an [Issue](https://github.com/danjiewu/LiteOrm/issues) or [Pull Request](https://github.com/danjiewu/LiteOrm/pulls).

## 📄 License

Released under the [MIT](LICENSE) license.