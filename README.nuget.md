# LiteOrm

[![NuGet](https://img.shields.io/nuget/v/LiteOrm.svg)](https://www.nuget.org/packages/LiteOrm/)
[![License](https://img.shields.io/github/license/danjiewu/LiteOrm.svg)](https://github.com/danjiewu/LiteOrm/blob/master/LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-LiteOrm-brightgreen)](https://github.com/danjiewu/LiteOrm)

---

<a id="english-version"></a>

## 📖 Language / 语言

**English** | **[中文](#中文版本)**

A lightweight, high-performance .NET ORM framework that combines the speed of micro-ORMs with the ease of use of full-featured ORMs. Perfect for scenarios requiring high performance and flexible complex SQL handling.

### Table of Contents
- [Core Features](#core-features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Key Features](#key-features)
- [Documentation & Resources](#documentation--resources)
- [Contributing](#contributing)
- [License](#license)

## 🎯 Core Features

<a id="core-features"></a>

- **Ultra-Fast Performance**: Performance close to native Dapper, far exceeding EF Core
- **Multi-Database Support**: Native support for SQL Server, MySQL, Oracle, PostgreSQL, SQLite
- **Flexible Querying**: Multiple query methods via Lambda, `Expr`, or `ExprString`
- **Automatic Associations**: Implement JOIN queries via attributes without manual SQL writing
- **Declarative Transactions**: AOP transaction management via `[Transaction]` attribute
- **Dynamic Sharding**: Table routing via `IArged` interface
- **Async Support**: Complete async/await support
- **Type Safety**: Strong-typed generic interfaces with compile-time type checking

## 📋 Requirements

<a id="requirements"></a>

- **.NET 8.0+** or **.NET Standard 2.0** (.NET Framework 4.6.1+)
- **Dependencies**: Autofac, Castle.Core

## 📦 Installation

<a id="installation"></a>

```bash
dotnet add package LiteOrm
```

---

## 🚀 Quick Start

<a id="quick-start"></a>

### 1. Configure Connection

In `appsettings.json`:

```json
{
    "LiteOrm": {
        "Default": "DefaultConnection",
        "DataSources": [
            {
                "Name": "DefaultConnection",
                "ConnectionString": "Server=localhost;Database=TestDb;...",
                "Provider": "MySqlConnector.MySqlConnection, MySqlConnector",
                "PoolSize": 20,
                "MaxPoolSize": 100
            }
        ]
    }
}
```

In `Program.cs`:

**Console:**
```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()
    .Build();
```

**ASP.NET Core:**
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();
```

### 2. Define Entity

```csharp
using LiteOrm.Common;

[Table("Users")]
public class User
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserName")]
    public string UserName { get; set; }

    [Column("Email")]
    public string Email { get; set; }

    [Column("CreateTime")]
    public DateTime? CreateTime { get; set; }
}
```

### 3. Define Service（optional）

```csharp
public interface IUserService :
    IEntityService<User>, IEntityServiceAsync<User>
{
}

public class UserService : EntityService<User>, IUserService
{
}
```

### 4. Use Service

```csharp
// Insert
var user = new User { UserName = "admin", Email = "admin@test.com" };
await userService.InsertAsync(user);

// Query
var users = await userService.SearchAsync(u => u.Email.Contains("test"));

// Update
user.Email = "newemail@test.com";
await userService.UpdateAsync(user);

// Delete
await userService.DeleteAsync(user);

// Pagination
var page = await userService.SearchAsync(
    q => q.Where(u => u.CreateTime > DateTime.Today)
          .OrderByDescending(u => u.CreateTime)
          .Skip(0).Take(10)
);
```

## 💡 Key Features

<a id="key-features"></a>

### Lambda Queries

```csharp
var users = await userService.SearchAsync(u => u.Age > 18);
```

### Expr Expression Queries

```csharp
var expr = Expr.Prop("Age") > 18 & Expr.Prop("Status") == 1;
var users = await userService.SearchAsync(expr);
```

### ExprString Queries (.NET 8.0+)

```csharp
int minAge = 18;
var expr = Expr.Prop("Age") > 25;
var users = await objectViewDAO.Search(
    $"WHERE {expr} AND Age > {minAge}"
).ToListAsync();
```

### Automatic Associations

```csharp
[Table("Orders")]
public class Order
{
    [Column("Id", IsPrimaryKey = true)]
    public int Id { get; set; }

    [Column("UserId")]
    [ForeignType(typeof(User))]
    public int UserId { get; set; }
}

public class OrderView : Order
{
    [ForeignColumn(typeof(User), Property = "UserName")]
    public string UserName { get; set; }
}

var orders = await orderService.SearchAsync<OrderView>();
```

### Declarative Transactions

```csharp
[Transaction]
public async Task CreateUserWithOrder(User user, Order order)
{
    await userService.InsertAsync(user);
    order.UserId = user.Id;
    await orderService.InsertAsync(order);
}
```

### Dynamic Sharding

```csharp
public class Log : IArged
{
    [Column("Id", IsPrimaryKey = true)]
    public int Id { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    string[] IArged.TableArgs => [CreateTime.ToString("yyyyMM")];
}
```

## 📚 Documentation & Resources

<a id="documentation--resources"></a>

- **[API Reference](https://github.com/danjiewu/LiteOrm/blob/master/LITEORM_API_REFERENCE.en.md)** - Complete API documentation
- **[GitHub Repository](https://github.com/danjiewu/LiteOrm)** - Source code and issues
- **[Demo Project](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo)** - Feature demonstrations
- **[Performance Report](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Benchmark)** - Detailed benchmarks

## 🤝 Contributing

<a id="contributing"></a>

Found a bug? Have a suggestion? Please open an [Issue](https://github.com/danjiewu/LiteOrm/issues) or [Pull Request](https://github.com/danjiewu/LiteOrm/pulls).

## 📄 License

<a id="license"></a>

[MIT License](https://github.com/danjiewu/LiteOrm/blob/master/LICENSE)

---
<a id="中文版本"></a>

## 📖 Language / 语言

**中文** | **[English](#english-version)**

LiteOrm 是一个轻量级、高性能的 .NET ORM 框架。结合了微 ORM 的性能和完整 ORM 的易用性，特别适合对性能要求高且需要灵活处理复杂 SQL 的场景。

### 目录
- [核心特性](#核心特性)
- [环境要求](#环境要求)
- [安装](#安装)
- [快速入门](#快速入门)
- [常见特性](#常见特性)
- [相关资源](#相关资源)
- [贡献与反馈](#贡献与反馈)
- [开源协议](#开源协议)

### 🎯 核心特性

<a id="核心特性"></a>

- **极速性能**：性能接近原生 Dapper，远超 EF Core
- **多数据库支持**：原生支持 SQL Server、MySQL、Oracle、PostgreSQL、SQLite
- **灵活查询**：支持基于 Lambda、`Expr` 或 `ExprString` 的多种查询方式
- **自动关联**：通过特性实现无损的 JOIN 查询，无需手写 SQL
- **声明式事务**：`[Transaction]` 特性实现 AOP 事务管理
- **动态分表**：`IArged` 接口支持分表路由
- **异步支持**：完整的 async/await 支持
- **类型安全**：强类型泛型接口，编译时类型检查

### 📋 环境要求

<a id="环境要求"></a>

- **.NET 8.0+** 或 **.NET Standard 2.0**（兼容 .NET Framework 4.6.1+）
- **依赖库**：Autofac、Castle.Core

### 📦 安装

<a id="安装"></a>

```bash
dotnet add package LiteOrm
```

---

### 🚀 快速入门

<a id="快速入门"></a>

#### 1. 配置连接

`appsettings.json`:
```json
{
    "LiteOrm": {
        "Default": "DefaultConnection",
        "DataSources": [
            {
                "Name": "DefaultConnection",
                "ConnectionString": "Server=localhost;Database=TestDb;...",
                "Provider": "MySqlConnector.MySqlConnection, MySqlConnector",
                "PoolSize": 20,
                "MaxPoolSize": 100
            }
        ]
    }
}
```

`Program.cs`:

**控制台应用：**
```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()
    .Build();
```

**ASP.NET Core 应用：**
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();
```

#### 2. 定义实体

```csharp
using LiteOrm.Common;

[Table("Users")]
public class User
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserName")]
    public string UserName { get; set; }

    [Column("Email")]
    public string Email { get; set; }

    [Column("CreateTime")]
    public DateTime? CreateTime { get; set; }
}
```

#### 3. 定义服务（可选）

```csharp
public interface IUserService :
    IEntityService<User>, IEntityServiceAsync<User>
{
}

public class UserService : EntityService<User>, IUserService
{
}
```

#### 4. 使用服务

```csharp
// 插入
var user = new User { UserName = "admin", Email = "admin@test.com" };
await userService.InsertAsync(user);

// 查询
var users = await userService.SearchAsync(u => u.Email.Contains("test"));

// 更新
user.Email = "newemail@test.com";
await userService.UpdateAsync(user);

// 删除
await userService.DeleteAsync(user);

// 分页
var page = await userService.SearchAsync(
    q => q.Where(u => u.CreateTime > DateTime.Today)
          .OrderByDescending(u => u.CreateTime)
          .Skip(0).Take(10)
);
```

### 💡 常见特性

<a id="常见特性"></a>

- **Lambda 查询**：直观的 Lambda 表达式查询
- **Expr 表达式**：灵活的动态条件构建
- **ExprString 查询**：参数化字符串查询
- **自动关联**：无损的 JOIN 查询
- **声明式事务**：基于特性的 AOP 事务
- **动态分表**：自动分表路由

### 📚 相关资源

<a id="相关资源"></a>

- **[API 参考](https://github.com/danjiewu/LiteOrm/blob/master/LITEORM_API_REFERENCE.md)** - 完整的 API 文档
- **[GitHub 仓库](https://github.com/danjiewu/LiteOrm)** - 源代码和问题跟踪
- **[Demo 项目](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo)** - 功能演示
- **[性能报告](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Benchmark)** - 详细的性能基准

### 🤝 贡献与反馈

<a id="贡献与反馈"></a>

欢迎提交 [Issue](https://github.com/danjiewu/LiteOrm/issues) 或 [Pull Request](https://github.com/danjiewu/LiteOrm/pulls)。

### 📄 开源协议

<a id="开源协议"></a>

[MIT 协议](https://github.com/danjiewu/LiteOrm/blob/master/LICENSE)

[回到顶部](#liteorm)
