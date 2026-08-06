# 安装与环境要求

本文介绍 LiteOrm 的运行环境、数据库支持，以及两种安装方式：仅使用基础库（`LiteOrm`）与使用框架集成包（`LiteOrm.DependencyInjection`）。

> **新手提示**：如果你只是想快速体验 LiteOrm，建议使用 SQLite 作为数据库——它不需要安装任何数据库服务，开箱即用。需要宿主级集成（Autofac、AOP）时再引入 `LiteOrm.DependencyInjection`。

## 环境要求

- `.NET 8.0+`
- `.NET Standard 2.0`（兼容 .NET Framework 4.6.1+）
- 数据库驱动包：根据所选数据库安装对应的 NuGet 驱动

> **如何检查 .NET 版本？** 在终端中运行 `dotnet --version`，确保输出为 `8.0.x` 或更高版本。如果尚未安装，请访问 [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download) 下载安装。

## 支持的数据库

**主流数据库：**

- SQL Server 2012+
- MySQL 8.0+
- Oracle 12c+
- PostgreSQL
- SQLite

**国产 / 兼容数据库：**

- 达梦 DM（Oracle 兼容）
- 人大金仓 KingbaseES（PostgreSQL 兼容）
- 华为 GaussDB / openGauss（PostgreSQL 兼容）
- OceanBase（MySQL 兼容）
- TiDB（MySQL 兼容）
- 万里 GreatDB（MySQL 兼容）

> 国产数据库继承对应主流数据库的方言行为，自动检测优先匹配。详见 [数据库差异与兼容性说明](../05-reference/07-database-compatibility.md)。

> 对于旧版本数据库，如果默认分页语法不兼容，请参考 [自定义分页](../03-advanced-topics/05-custom-paging.md) 与 [自定义 SqlBuilder / 方言扩展](../04-extensibility/03-custom-sqlbuilder.md)。

### 各数据库 NuGet 驱动包

> 无论哪种安装方式，都需要根据使用的数据库安装对应的 NuGet 驱动包：

| 数据库 | NuGet 包 |
|--------|----------|
| SQL Server | `Microsoft.Data.SqlClient` |
| SQL Server (旧版) | `System.Data.SqlClient` |
| MySQL | `MySqlConnector` |
| MySQL (旧版) | `MySql.Data` |
| PostgreSQL | `Npgsql` |
| Oracle | `Oracle.ManagedDataAccess.Core` |
| SQLite | `Microsoft.Data.Sqlite` |

> 配置 `appsettings.json` 时，`Provider` 字段需要填写对应数据库驱动的完整类型名，参见[数据库差异与兼容性说明](../05-reference/07-database-compatibility.md)。

## 方式一：仅使用基础库（`LiteOrm`）

适合只需要实体映射、查询、DAO 等核心能力，且自行管理连接与生命周期的场景。此方式**不引入任何 DI 框架**，无需 Autofac 与 Castle 动态代理。

```bash
dotnet add package LiteOrm
dotnet add package Microsoft.Data.Sqlite   # 按数据库选装
```

- 基础库由 `LiteOrm` 与 `LiteOrm.Common` 两个包构成，`LiteOrm` 会自动携带 `LiteOrm.Common`。
- 不提供 `RegisterLiteOrm()`，也不含 AOP 拦截（事务/权限/日志）能力。
- 数据访问通过 `ObjectDAO` / `DataDAO` 等 DAO 类型完成。

## 方式二：使用框架集成包（`LiteOrm.DependencyInjection`）

适合 ASP.NET Core 项目。`LiteOrm.DependencyInjection` 引入 Autofac 容器与 Castle 动态代理，通过 `builder.Host.RegisterLiteOrm()` 一键注册，并启用 AOP 事务/权限/日志。

```bash
dotnet add package LiteOrm
dotnet add package LiteOrm.DependencyInjection    # DI 注册（RegisterLiteOrm）需要
dotnet add package Microsoft.Data.Sqlite   # 按数据库选装
```

## 创建新项目的完整步骤

> 以下是从零开始创建一个使用 LiteOrm.DependencyInjection 的 ASP.NET Core 项目的完整命令：

```bash
# 1. 创建 Web API 项目
dotnet new webapi -n MyLiteOrmApp
cd MyLiteOrmApp

# 2. 安装 LiteOrm（以 SQLite 为例）
dotnet add package LiteOrm
dotnet add package LiteOrm.DependencyInjection
dotnet add package Microsoft.Data.Sqlite

# 3. 运行项目确认环境正常
dotnet run
```

> 如果你使用 Visual Studio，可以直接通过"管理 NuGet 程序包"搜索 `LiteOrm` 进行安装。

## 安装后的下一步

- **仅基础库**：参考 [第一个完整示例（仅基础库）](./03-first-example.md) 进行手动初始化。
- **LiteOrm.DependencyInjection 集成**：在宿主启动阶段调用 `RegisterLiteOrm()`，参考 [配置参考](../05-reference/01-configuration-reference.md) 与 [第一个完整示例（DI 版）](./05-first-example-di.md)。

> **SQLite 快速上手**：如果你想用 SQLite 快速体验，连接字符串只需写 `Data Source=myapp.db`，无需安装任何数据库服务。

## 常见安装问题

### 安装后编译报错：找不到 `RegisterLiteOrm` 方法

确保安装了 `LiteOrm.DependencyInjection` 包（`RegisterLiteOrm()` 定义于该包，仅安装 `LiteOrm` 或 `LiteOrm.Common` 不会提供此方法），并在代码文件顶部添加 `using LiteOrm.DependencyInjection;`。

### 运行时提示找不到数据库驱动

检查是否安装了对应数据库的 NuGet 驱动包（如 `Microsoft.Data.SqlClient`、`MySqlConnector` 等），并确认 `appsettings.json` 中的 `Provider` 值与实际安装的包一致。

### .NET Framework 项目能否使用？

可以。LiteOrm 支持 `.NET Standard 2.0`，兼容 .NET Framework 4.6.1 及以上版本。但建议优先使用 .NET 8.0+ 以获得最佳体验。

### 安装后项目体积会很大吗？

不会。LiteOrm 本身非常轻量，基础包只有几百 KB。加上必要的依赖（Autofac、Castle.Core），总体增量在 2-3 MB 左右。仅使用基础库时不引入 Autofac 与 Castle，体积更小。

## 相关链接

- [返回目录](../README.md)
- [配置参考](../05-reference/01-configuration-reference.md)
- [第一个完整示例（DI 版）](./05-first-example-di.md)
