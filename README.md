# LiteOrm

> A lightweight, high-performance .NET ORM framework

[![NuGet](https://img.shields.io/nuget/v/LiteOrm.svg)](https://www.nuget.org/packages/LiteOrm/)
[![License](https://img.shields.io/github/license/danjiewu/LiteOrm.svg)](LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-LiteOrm-brightgreen)](https://github.com/danjiewu/LiteOrm)

---

## 📖 Language / 语言

**English** | **[中文](./README.zh.md)**

---

## 📚 Documentation

Start with the docs hub, then use the scenario-based reference pages for targeted lookups.

- [Docs Hub (English)](./docs/SUMMARY.en.md)
- [Docs Hub (中文)](./docs/SUMMARY.md)
- [API Index](./docs/05-reference/02-api-index.en.md)
- [Example Index](./docs/05-reference/06-example-index.en.md)
- [Generated SQL Examples](./docs/05-reference/07-sql-examples.en.md)
- [Database Compatibility Notes](./docs/05-reference/08-database-compatibility.en.md)

LiteOrm is a lightweight, high-performance .NET ORM that combines micro-ORM speed with full-ORM ergonomics. It fits projects that need predictable performance while still handling rich SQL scenarios cleanly.

## 🎯 Core Features

- **Ultra-Fast Performance**: Performance close to native Dapper, far exceeding EF Core
- **Multi-Database Support**: Native support for SQL Server, MySQL, Oracle, PostgreSQL, SQLite
- **Flexible Querying**: Multiple query methods via Lambda, `Expr`, or `ExprString`
- **Automatic Associations**: Implement JOIN queries via attributes without manual SQL writing
- **Declarative Transactions**: AOP transaction management via `[Transaction]` attribute
- **Dynamic Sharding**: Table routing via `IArged` interface
- **Async Support**: Complete async/await support
- **Type Safety**: Strong-typed generic interfaces with compile-time type checking

## 📋 Requirements

- **.NET 8.0+** / **.NET Standard 2.0** (.NET Framework 4.6.1+ compatible)
- **Dependencies**: Autofac, Castle.Core
- **Supported databases**: SQL Server 2012+, Oracle 12c+, PostgreSQL, MySQL, SQLite
  > Older database versions may require custom paging. See [Custom Paging](./docs/03-advanced-topics/05-custom-paging.en.md).

## 📦 Installation

```bash
dotnet add package LiteOrm
```

## 🚀 Quick Start

### 1. Configure the connection

In `appsettings.json`:

```json
{
    "LiteOrm": {
        "Default": "DefaultConnection",
        "DataSources": [
            {
                "Name": "DefaultConnection",
                "ConnectionString": "Server=localhost;Database=TestDb;...",
                "Provider": "MySqlConnector.MySqlConnection, MySqlConnector"
            }
        ]
    }
}
```

### 2. Register LiteOrm

In `Program.cs`:

**Console:**
```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()  // Auto initialization
    .Build();
```

**ASP.NET Core:**
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();  // Integration via IHostBuilder extension method
```

### 3. Define an entity

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

### 4. Define a service (optional)

```csharp
// Define view model (for queries, can include related fields)
public class UserView : User { }

public interface IUserService :
    IEntityService<User>, IEntityServiceAsync<User>,
    IEntityViewService<UserView>, IEntityViewServiceAsync<UserView>
{
}

public class UserService : EntityService<User, UserView>, IUserService
{
}
```

### 5. Use the service

You can use a custom service or inject the generic interfaces directly:

```csharp
// Insert
var user = new User { UserName = "admin", Email = "admin@test.com" };
await userService.InsertAsync(user);

// Query
var users = await userService.SearchAsync(u => u.Email.Contains("test"));
var admin = await userService.SearchOneAsync(u => u.UserName == "admin");

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

### Lambda queries

```csharp
// Basic query
var users = await userService.SearchAsync(u => u.Age > 18);

// Sorting
var sorted = await userService.SearchAsync(
    q => q.Where(u => u.Age > 18).OrderBy(u => u.Age)
);

// Pagination
var paged = await userService.SearchAsync(
    q => q.Where(u => u.Status == 1)
          .OrderBy(u => u.Id)
          .Skip(10).Take(20)
);
```

### `Expr` queries

```csharp
// Manually build expressions (supports more complex dynamic conditions)
var expr = Expr.Prop("Age") > 18 & Expr.Prop("Status") == 1;
var users = await userService.SearchAsync(expr);

// IN query
var users = await userService.SearchAsync(
    Expr.Prop("Id").In(1, 2, 3, 4, 5)
);

// LIKE query
var users = await userService.SearchAsync(
    Expr.Prop("UserName").Contains("admin")
);

// Chain complex queries
var query = Expr.From<User>()
    .Where(Expr.Prop("Age") > 18)
    .OrderBy(Expr.Prop("CreateTime"))
    .Section(0, 10);  // LIMIT/OFFSET
var result = await userService.SearchAsync(query);
```

### `ExprString` queries

```csharp
// Use parameterized interpolated strings to prevent SQL injection
int minAge = 18;
var expr = Expr.Prop("Age") > 25;

// `ObjectViewDAO<T>` example
var users = await objectViewDAO.Search($"WHERE {expr} AND Age > {minAge}").ToListAsync();

// `DataViewDAO` example
var dataTable = await dataViewDAO.Search(
    $"SELECT Id, UserName FROM Users WHERE {Expr.Prop("Age")} > {minAge}"
).GetResultAsync();
```

### `EXISTS` subqueries

```csharp
// Check for existence of related data
var result = await userService.SearchAsync(
    q => q.Where(u => Expr.Exists<Order>(o => o.UserId == u.Id))
);
```

### Automatic associations

```csharp
// Define association
[Table("Orders")]
public class Order
{
    [Column("Id", IsPrimaryKey = true)]
    public int Id { get; set; }

    [Column("UserId")]
    [ForeignType(typeof(User))]
    public int UserId { get; set; }
}

// View model with related fields
public class OrderView : Order
{
    [ForeignColumn(typeof(User), Property = "UserName")]
    public string UserName { get; set; }
}

// Automatic JOIN on query
var orders = await orderService.SearchAsync<OrderView>();
```

### Declarative transactions

```csharp
public class BusinessService
{
    private readonly IUserService userService;
    private readonly IOrderService orderService;

    [Transaction]
    public async Task CreateUserWithOrder(User user, Order order)
    {
        await userService.InsertAsync(user);
        order.UserId = user.Id;
        await orderService.InsertAsync(order);
    }
}
```

### Dynamic sharding

```csharp
public class Log : IArged
{
    [Column("Id", IsPrimaryKey = true)]
    public int Id { get; set; }

    [Column("Content")]
    public string Content { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    // Automatically route to Log_202401, Log_202402, etc. by month
    string[] IArged.TableArgs => [CreateTime.ToString("yyyyMM")];
}
```

## ⚡ Performance Benchmarks

Latest comparison test results based on the LiteOrm.Benchmark project (.NET 10.0.4, Linux Ubuntu 24.04 LTS, Intel Xeon Silver 4314 CPU 2.40GHz, MySQL):

### Insert Performance Comparison (ms)

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| **LiteOrm** | **3.98** | **16.39** | **75.62** |
| SqlSugar | 4.33 | 19.12 | 98.15 |
| FreeSql | 4.36 | 18.48 | 85.00 |
| EF Core | 18.50 | 150.35 | 670.19 |
| Dapper | 26.19 | 215.12 | 1,129.57 |

### Update Performance Comparison (ms)

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| **LiteOrm** | **4.84** | **25.36** | **118.70** |
| SqlSugar | 6.39 | 42.62 | 232.66 |
| FreeSql | 5.88 | 40.31 | 175.58 |
| EF Core | 17.26 | 126.44 | 575.32 |
| Dapper | 28.63 | 248.71 | 1,213.51 |

### Upsert Performance Comparison (ms)

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| LiteOrm | 7.54 | 23.72 | 103.52 |
| SqlSugar | 10.36 | 106.11 | 1,741.49 |
| **FreeSql** | **5.53** | **19.11** | **103.06** |
| EF Core | 19.05 | 135.88 | 589.07 |
| Dapper | 29.09 | 247.51 | 1,248.91 |

### Join Query Performance Comparison (ms)

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| **LiteOrm** | **1.36** | 9.35 | 43.94 |
| SqlSugar | 2.29 | 22.10 | 89.97 |
| FreeSql | 1.75 | 9.10 | **43.89** |
| EF Core | 4.93 | 15.62 | 55.16 |
| Dapper | 1.48 | **9.07** | 45.64 |

### Memory Allocation Comparison (1000 rows, KB)

| Framework | Insert | Update | Upsert | Join Query |
|:---|---:|---:|---:|---:|
| **LiteOrm** | **862.82** | **1,189.03** | **1,973.38** | **230.38** |
| SqlSugar | 4,573.59 | 7,679.63 | 35,952.88 | 9,228.26 |
| FreeSql | 4,667.20 | 6,917.50 | 2,256.36 | 866.52 |
| EF Core | 12,503.04 | 9,044.24 | 9,005.39 | 2,198.05 |
| Dapper | 2,476.36 | 3,093.19 | 2,798.36 | 418.43 |

> 📊 For detailed performance benchmark reports, see [LiteOrm.Benchmark](./LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md)

## 📚 Documentation & Resources

For guided reading, start with the docs hub. Use the reference pages below when you need a faster path to a specific topic.

| Resource | Description |
|:---|:---|
| [Documentation Hub](./docs/SUMMARY.en.md) | English docs organized by learning path |
| [中文文档中心](./docs/SUMMARY.md) | Chinese docs hub organized by learning path |
| [API Index](./docs/05-reference/02-api-index.en.md) | Scenario-based API and capability entry points |
| [AI Guide](./docs/05-reference/05-ai-guide.en.md) | Compact appendix for assistants and quick API orientation |
| [Demo Project](./LiteOrm.Demo/) | Main feature demonstration project |
| [Performance Report](./LiteOrm.Benchmark/) | Detailed benchmark reports |
| [Unit Tests](./LiteOrm.Tests/) | Behavior and regression coverage |

## 🤝 Contributing

Found a bug or have an improvement suggestion? Please submit an [Issue](https://github.com/danjiewu/LiteOrm/issues) or [Pull Request](https://github.com/danjiewu/LiteOrm/pulls).

## 📄 License

Released under the [MIT](LICENSE) license.

