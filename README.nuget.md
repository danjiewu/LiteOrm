# LiteOrm

[![License](https://img.shields.io/github/license/danjiewu/LiteOrm.svg)](https://github.com/danjiewu/LiteOrm/blob/master/LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-LiteOrm-brightgreen)](https://github.com/danjiewu/LiteOrm)

---

## 📖 English Version

LiteOrm is a lightweight, high-performance .NET ORM that combines micro-ORM speed (near Dapper) with full-ORM ergonomics — ideal when you need predictable performance and flexible SQL composition.

### Core Features

- **Multi-database**: native support for SQL Server, MySQL, Oracle, PostgreSQL, SQLite; built-in dialects for Dameng (DM), KingbaseES, Huawei GaussDB/openGauss, OceanBase, TiDB, GreatDB
- **Flexible querying**: Lambda, `Expr`, or `ExprString` styles, all converging on one expression tree
- **Automatic associations**: JOIN queries via attributes, no manual SQL
- **Declarative transactions**: `[Transaction]` AOP transactions
- **Dynamic sharding**: table routing via the `IArged` interface
- **Full async support** and strong-typed generic interfaces

### Requirements

- **.NET 8.0+** or **.NET Standard 2.0** (.NET Framework 4.6.1+ compatible)

### Installation

```bash
dotnet add package LiteOrm
```

`LiteOrm` transitively references `LiteOrm.Common`.

### Quick Start

**No DI container at all**, using `LiteOrmContext`:

```csharp
using LiteOrm;
using LiteOrm.Common;
using Microsoft.Data.Sqlite;

// Register data sources, then create a session and build DAOs from it
using var liteOrm = new LiteOrmContext()
    .AddDataSource<SqliteConnection>("main", "Data Source=LiteOrmDemo.db", @default: true, syncTable: true);
using var session = liteOrm.CreateSession();
var userDao = new ObjectDAO<User>(session);
var viewDao = new ObjectViewDAO<User>(session);

var user = new User { UserName = "admin", Age = 18, CreateTime = DateTime.Now };
await userDao.InsertAsync(user);

var loaded = await viewDao.GetObject(user.Id).FirstOrDefaultAsync();
var adults = await viewDao.Search(Expr.Prop(nameof(User.Age)) > 18).ToListAsync();
```

`AddDataSource<TConnection>` accepts named parameters that map onto `DataSourceConfig` — `connectionString`, `@default`, `syncTable`, `sqlBuilder`, `poolSize`, `maxPoolSize`, etc., anything omitted uses the config default. Chain one call per extra data source; all options are fixed at this step and must be set before the first `CreateSession()`. This path registers no services, never reads `IConfiguration`, and never touches `SessionManager.Current`; data-source routing is decided by the entity's `[Table(DataSource = "...")]`. See [First Full Example (Manual, No DI)](docs/01-getting-started/04-first-example-manual.en.md). For DI, see [Quick Start (DI Integration)](#quick-start-di-integration) below.

### Quick Start (DI Integration)

With a DI container, use `AddLiteOrm()` (plain MS DI, no Autofac / AOP, built into the base library):

```csharp
using LiteOrm;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);   // loads appsettings.json
builder.Services.AddLiteOrm();                       // reads the "LiteOrm" config section

var host = builder.Build();
using var scope = host.Services.CreateScope();
var userService = scope.ServiceProvider.GetRequiredService<EntityService<User>>();

var user = new User { UserName = "admin", Age = 18 };
await userService.InsertAsync(user);
var users = await userService.SearchAsync(u => u.Age > 18);
```

Data sources are loaded from the `LiteOrm` section of `IConfiguration`; use the `AddLiteOrm(options => ...)` overload to tune registration (`AutoRegisterServices` defaults to true, plus a `ConfigureServices` callback). `AddLiteOrm()` registers the LiteOrm core services (`SessionManager`, `DAOContextPoolFactory`, etc.). For full **automatic session management, declarative transactions, logging**, use the [**LiteOrm.DependencyInjection**](https://www.nuget.org/packages/LiteOrm.DependencyInjection) package.

### AOT Support

- The **net8.0 / net10.0** targets are AOT-compatible (`IsAotCompatible`), and the libraries work under NativeAOT and full trimming.
- `Expr` trees are serialized via the source-generated **`ExprJsonSerializerContext`** (registered through `[JsonConverter]` on `Expr`), so JSON round-trips of expressions require no reflection and are NativeAOT-safe.
- When building with `PublishAot=true` / trimming enabled, the bundled source generator (`LiteOrm.Generators`) emits registration code at compile time for entity types, `SqlBuilder`/`DbConnection` types, DataReader mapping delegates and property accessors.
- Runtime reflection-based paths are used only in the JIT fallback mode; AOT mode uses pre-registered converters and generators.

### Key Features

- **Lambda / Expr / ExprString** — pick the style that fits: strongly-typed lambdas for daily filters, dynamic `Expr` trees for query builders, `ExprString` for DAO-side SQL.
- **Automatic associations** — `[ForeignType]` / `[ForeignColumn]` project joined fields onto view models without writing JOINs.
- **Declarative transactions** — `[Transaction]` on a service method.
- **Dynamic sharding** — implement `IArged.TableArgs` to route to physical tables.

### Documentation & Resources

- [Docs Hub (EN/中文)](https://github.com/danjiewu/LiteOrm/blob/master/docs/README.md)
- [GitHub Repository](https://github.com/danjiewu/LiteOrm) — source code & issue tracking
- [Demo Project](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo)

### License

[MIT License](https://github.com/danjiewu/LiteOrm/blob/master/LICENSE)

---

## 📖 中文版本

LiteOrm 是一个轻量级、高性能的 .NET ORM 框架，兼顾微型 ORM 的执行效率（接近 Dapper）与完整 ORM 的易用性，适合对性能敏感且需要灵活拼装 SQL 的场景。

### 核心特性

- **多数据库支持**：原生支持 SQL Server、MySQL、Oracle、PostgreSQL、SQLite；内置达梦、人大金仓、华为 GaussDB / openGauss、OceanBase、TiDB、GreatDB 等国产 / 兼容数据库方言
- **灵活查询**：支持基于 Lambda、`Expr`、`ExprString` 的多种查询方式，统一收敛到同一套表达式树
- **自动关联**：通过特性实现无损的 JOIN 查询，无需手写 SQL
- **声明式事务**：`[Transaction]` 特性实现 AOP 事务管理
- **动态分表**：`IArged` 接口支持分表路由
- **完整异步支持**，并提供强类型泛型接口

### 环境要求

- **.NET 8.0+** 或 **.NET Standard 2.0**（兼容 .NET Framework 4.6.1+）

### 安装

```bash
dotnet add package LiteOrm
```

`LiteOrm` 会自动携带 `LiteOrm.Common`。

### 快速入门

完全不使用 DI 容器，直接用 `LiteOrmContext`：

```csharp
using LiteOrm;
using LiteOrm.Common;
using Microsoft.Data.Sqlite;

// 创建上下文并登记数据源，再由会话构造 DAO
using var liteOrm = new LiteOrmContext()
    .AddDataSource<SqliteConnection>("main", "Data Source=LiteOrmDemo.db", @default: true, syncTable: true);
using var session = liteOrm.CreateSession();
var userDao = new ObjectDAO<User>(session);
var viewDao = new ObjectViewDAO<User>(session);

var user = new User { UserName = "admin", Age = 18, CreateTime = DateTime.Now };
await userDao.InsertAsync(user);

var loaded = await viewDao.GetObject(user.Id).FirstOrDefaultAsync();
var adults = await viewDao.Search(Expr.Prop(nameof(User.Age)) > 18).ToListAsync();
```

`AddDataSource<TConnection>` 的命名参数与 `DataSourceConfig` 一一对应——`connectionString`、`@default`、`syncTable`、`sqlBuilder`、`poolSize`、`maxPoolSize` 等，未填的按配置默认值；多个数据源就链式多调一次，所有设置都定在这一步，且必须在首次 `CreateSession()` 之前。这条线路不注册任何服务、不读取 `IConfiguration`、不设置 `SessionManager.Current`，数据源由实体的 `[Table(DataSource = "...")]` 决定。完整示例见 [第一个完整示例（手动构造，无 DI）](docs/01-getting-started/04-first-example-manual.md)。需要 DI 时见下方 [快速入门（DI 集成）](#快速入门di-集成)。

### 快速入门（DI 集成）

使用 DI 容器时，调用基础库内置的 `AddLiteOrm()`（纯 MS DI，无 Autofac / AOP）：

```csharp
using LiteOrm;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);   // 自动加载 appsettings.json
builder.Services.AddLiteOrm();                       // 读取 "LiteOrm" 配置节点

var host = builder.Build();
using var scope = host.Services.CreateScope();
var userService = scope.ServiceProvider.GetRequiredService<EntityService<User>>();

var user = new User { UserName = "admin", Age = 18 };
await userService.InsertAsync(user);
var users = await userService.SearchAsync(u => u.Age > 18);
```

数据源从 `IConfiguration` 的 `LiteOrm` 节点加载；需要调整注册行为时改用 `AddLiteOrm(options => ...)` 重载，可设置 `AutoRegisterServices`（默认 true）与 `ConfigureServices`。`AddLiteOrm()` 自动注册 LiteOrm 核心服务（`SessionManager`、`DAOContextPoolFactory` 等）。如需要更全面的**自动会话管理、声明式事务、日志等**，请使用 [**LiteOrm.DependencyInjection**](https://www.nuget.org/packages/LiteOrm.DependencyInjection) 包。

### AOT 支持

- **net8.0 / net10.0** 目标为 AOT 兼容（`IsAotCompatible`），库可在 NativeAOT 与完全裁剪（Trim）下正常工作。
- `Expr` 表达式树通过源生成的 **`ExprJsonSerializerContext`** 序列化（经 `Expr` 上的 `[JsonConverter]` 自动注册），表达式的 JSON 序列化不依赖反射，天然兼容 NativeAOT。
- 使用 `PublishAot=true` 或启用裁剪构建时，内置源生成器（`LiteOrm.Generators`）会在编译期生成实体类型、`SqlBuilder` / `DbConnection` 类型、DataReader 映射委托与属性访问器的注册代码。
- 反射式路径仅在 JIT 回退模式下使用；AOT 模式下使用预注册的转换器与生成器。

### 常见特性

- **Lambda / Expr / ExprString**——按场景选择：强类型 Lambda 适合日常筛选，`Expr` 表达式适合动态条件拼装，`ExprString` 用于 DAO 层 SQL。
- **自动关联**——通过 `[ForeignType]` / `[ForeignColumn]` 把联表字段投影到视图模型，无需手写 JOIN。
- **声明式事务**——服务方法上标注 `[Transaction]` 即可。
- **动态分表**——实现 `IArged.TableArgs` 路由到物理表。

### 文档与资源

- [文档中心（EN/中文）](https://github.com/danjiewu/LiteOrm/blob/master/docs/README.md)
- [GitHub 仓库](https://github.com/danjiewu/LiteOrm) — 源代码与问题跟踪
- [Demo 项目](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo)

### 开源协议

[MIT 协议](https://github.com/danjiewu/LiteOrm/blob/master/LICENSE)
