# LiteOrm

> A lightweight, high-performance .NET ORM framework

[![NuGet](https://img.shields.io/nuget/v/LiteOrm.svg)](https://www.nuget.org/packages/LiteOrm/)
[![License](https://img.shields.io/github/license/danjiewu/LiteOrm.svg)](LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-LiteOrm-brightgreen)](https://github.com/danjiewu/LiteOrm)

---

## 📖 Language / 语言

**English** | **[中文](./README.zh.md)**

---

LiteOrm is a lightweight, high-performance .NET ORM framework that combines the speed of micro-ORMs with the ease of use of full-featured ORMs, perfect for scenarios requiring high performance and flexible complex SQL handling.

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

- **.NET 8.0+**
- **.NET Standard 2.0** (.NET Framework 4.6.1+ compatible)
- **Dependencies**: Autofac, Castle.Core

## 📦 Installation

```bash
dotnet add package LiteOrm
```

## 🚀 Quick Start

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

### 3. Define Entity

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

### 4. Define Service (Optional)

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

### 5. Use Service

You can use either custom services or directly use generic services:

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

### Lambda Queries

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

### Expr Expression Queries

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

### ExprString Queries (.NET 8.0+)

```csharp
// Use parameterized interpolated strings to prevent SQL injection
int minAge = 18;
var expr = Expr.Prop("Age") > 25;

// ObjectViewDAO example
var users = await objectViewDAO.Search($"WHERE {expr} AND Age > {minAge}").ToListAsync();

// DataViewDAO example
var dataTable = await dataViewDAO.Search(
    $"SELECT Id, UserName FROM Users WHERE {Expr.Prop("Age")} > {minAge}"
).GetResultAsync();
```

### EXISTS Subqueries

```csharp
// Check for existence of related data
var result = await userService.SearchAsync(
    q => q.Where(u => Expr.Exists<Order>(o => o.UserId == u.Id))
);
```

### Automatic Associations

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

### Declarative Transactions

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

### Dynamic Sharding

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

Latest comparison test results based on the LiteOrm.Benchmark project (.NET 10.0.3, MySQL):

### Insert Performance Comparison (ms)

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| **LiteOrm** | **3.74** | **10.71** | **40.27** |
| FreeSql | 4.36 | 17.71 | 72.49 |
| SqlSugar | 4.13 | 15.78 | 76.64 |
| Dapper | 13.24 | 120.21 | 690.75 |
| EF Core | 21.97 | 169.85 | 824.70 |

### Update Performance Comparison (ms)

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| **LiteOrm** | **4.68** | **16.47** | **68.07** |
| FreeSql | 4.86 | 30.84 | 133.94 |
| SqlSugar | 5.38 | 35.52 | 194.13 |
| Dapper | 16.49 | 132.36 | 659.91 |
| EF Core | 21.57 | 149.93 | 749.07 |

### Upsert Performance Comparison (ms)

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| LiteOrm | 5.54 | 16.73 | 60.71 |
| **FreeSql** | **4.84** | **14.77** | **58.18** |
| SqlSugar | 9.36 | 66.36 | 885.87 |
| Dapper | 18.59 | 136.05 | 677.14 |
| EF Core | 22.97 | 157.04 | 794.85 |

### Join Query Performance Comparison (ms)

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| LiteOrm | 0.97 | **6.06** | **39.06** |
| **FreeSql** | 0.94 | 6.52 | 41.22 |
| SqlSugar | 1.66 | 12.30 | 63.74 |
| **Dapper** | **0.89** | 6.56 | 39.94 |
| EF Core | 6.68 | 12.42 | 49.40 |

### Memory Allocation Comparison (1000 rows, KB)

| Framework | Insert | Update | Upsert | Join Query |
|:---|---:|---:|---:|---:|
| **LiteOrm** | **870.27** | **1,514.47** | **2,138.49** | 628.98 |
| FreeSql | 4,629.99 | 6,877.24 | 2,239.19 | 854.07 |
| SqlSugar | 4,571.36 | 7,677.75 | 35,952.45 | 9,226.19 |
| Dapper | 2,476.22 | 3,094.99 | 2,798.43 | **415.97** |
| EF Core | 18,118.07 | 15,149.28 | 14,803.48 | 2,198.79 |

> 📊 For detailed performance benchmark reports, see [LiteOrm.Benchmark](./LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md)

## 📚 Documentation & Resources

| Resource | Description |
|:---|:---|
| [API Reference](./LITEORM_API_REFERENCE.en.md) | Complete API documentation and configuration instructions |
| [Demo Project](./LiteOrm.Demo/) | Main feature demonstration program |
| [Performance Report](./LiteOrm.Benchmark/) | Detailed performance benchmark reports |
| [Unit Tests](./LiteOrm.Tests/) | Complete test coverage |

## 🤝 Contributing

Found a bug or have an improvement suggestion? Please submit an [Issue](https://github.com/danjiewu/LiteOrm/issues) or [Pull Request](https://github.com/danjiewu/LiteOrm/pulls).

## 📄 License

Released under the [MIT](LICENSE) license.
