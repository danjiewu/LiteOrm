# 安装与环境要求

本文介绍 LiteOrm 的运行环境、数据库支持和安装方式。

> **新手提示**：如果你只是想快速体验 LiteOrm，建议使用 SQLite 作为数据库——它不需要安装任何数据库服务，开箱即用。本文末尾提供了 SQLite 的快速上手步骤。

## 环境要求

- `.NET 8.0+`
- `.NET Standard 2.0`（兼容 .NET Framework 4.6.1+）
- 依赖库：`Autofac.Extensions.DependencyInjection`、`Autofac.Extras.DynamicProxy`、`Castle.Core`

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

> 国产数据库继承对应主流数据库的方言行为，自动检测优先匹配。详见 [数据库差异与兼容性说明](../05-reference/08-database-compatibility.md)。

> 对于旧版本数据库，如果默认分页语法不兼容，请参考 [自定义分页](../03-advanced-topics/05-custom-paging.md) 与 [自定义 SqlBuilder / 方言扩展](../04-extensibility/03-custom-sqlbuilder.md)。

### 各数据库 Provider 对照表

> 配置 `appsettings.json` 时，`Provider` 字段需要填写对应数据库驱动的完整类型名。以下是常用数据库的 Provider 配置参考：

| 数据库 | NuGet 包 | Provider 配置值 |
|--------|----------|----------------|
| SQL Server | `Microsoft.Data.SqlClient` | `Microsoft.Data.SqlClient.SqlConnection, Microsoft.Data.SqlClient` |
| SQL Server (旧版) | `System.Data.SqlClient` | `System.Data.SqlClient.SqlConnection, System.Data.SqlClient` |
| MySQL | `MySqlConnector` | `MySqlConnector.MySqlConnection, MySqlConnector` |
| MySQL (旧版) | `MySql.Data` | `MySql.Data.MySqlClient.MySqlConnection, MySql.Data` |
| PostgreSQL | `Npgsql` | `Npgsql.NpgsqlConnection, Npgsql` |
| Oracle | `Oracle.ManagedDataAccess.Core` | `Oracle.ManagedDataAccess.Client.OracleConnection, Oracle.ManagedDataAccess` |
| SQLite | `Microsoft.Data.Sqlite` | `Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite` |

> **注意**：除了安装 `LiteOrm` 包外，你还需要根据使用的数据库安装对应的 NuGet 驱动包（如上表第一列所示）。

## 通过 NuGet 安装

```bash
dotnet add package LiteOrm
```

### 各数据库完整安装命令

**SQL Server 项目：**
```bash
dotnet add package LiteOrm
dotnet add package Microsoft.Data.SqlClient
```

**MySQL 项目：**
```bash
dotnet add package LiteOrm
dotnet add package MySqlConnector
```

**PostgreSQL 项目：**
```bash
dotnet add package LiteOrm
dotnet add package Npgsql
```

**SQLite 项目（推荐新手使用）：**
```bash
dotnet add package LiteOrm
dotnet add package Microsoft.Data.Sqlite
```

## 创建新项目的完整步骤

> 以下是从零开始创建一个使用 LiteOrm 的 ASP.NET Core 项目的完整命令：

```bash
# 1. 创建 Web API 项目
dotnet new webapi -n MyLiteOrmApp
cd MyLiteOrmApp

# 2. 安装 LiteOrm（以 SQLite 为例）
dotnet add package LiteOrm
dotnet add package Microsoft.Data.Sqlite

# 3. 运行项目确认环境正常
dotnet run
```

> 如果你使用 Visual Studio，可以直接通过"管理 NuGet 程序包"搜索 `LiteOrm` 进行安装。

## 安装后的下一步

1. 准备连接字符串和数据源配置。
2. 在宿主启动阶段调用 `RegisterLiteOrm()`。
3. 定义实体、服务或 DAO。
4. 使用 `SearchAsync`、`InsertAsync` 等 API 完成首个示例。

> **SQLite 快速上手**：如果你想用 SQLite 快速体验，连接字符串只需写 `Data Source=myapp.db`，无需安装任何数据库服务。完整示例请参考 [第一个完整示例](./04-first-example.md)。

## 常见安装问题

### 安装后编译报错：找不到 `RegisterLiteOrm` 方法

确保安装了 `LiteOrm` 包（不是 `LiteOrm.Common`），并在代码文件顶部添加 `using LiteOrm;`。

### 运行时提示找不到数据库驱动

检查是否安装了对应数据库的 NuGet 驱动包（如 `Microsoft.Data.SqlClient`、`MySqlConnector` 等），并确认 `appsettings.json` 中的 `Provider` 值与实际安装的包一致。

### .NET Framework 项目能否使用？

可以。LiteOrm 支持 `.NET Standard 2.0`，兼容 .NET Framework 4.6.1 及以上版本。但建议优先使用 .NET 8.0+ 以获得最佳体验。

### 安装后项目体积会很大吗？

不会。LiteOrm 本身非常轻量，核心包只有几百 KB。加上必要的依赖（Autofac、Castle.Core），总体增量在 2-3 MB 左右。

## 相关链接

- [返回目录](../README.md)
- [配置与注册](./03-configuration-and-registration.md)
- [第一个完整示例](./04-first-example.md)
- [配置项速查](../05-reference/01-configuration-reference.md)
