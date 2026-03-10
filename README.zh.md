# LiteOrm

> 一个轻量级、高性能的 .NET ORM 框架

[![NuGet](https://img.shields.io/nuget/v/LiteOrm.svg)](https://www.nuget.org/packages/LiteOrm/)
[![License](https://img.shields.io/github/license/danjiewu/LiteOrm.svg)](LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-LiteOrm-brightgreen)](https://github.com/danjiewu/LiteOrm)

---

## 📖 Language / 语言

**[English](./README.md)** | **中文** 

---

LiteOrm 是一个轻量级、高性能的 .NET ORM 框架。结合了微 ORM 的性能和完整 ORM 的易用性，特别适合对性能要求高且需要灵活处理复杂 SQL 的场景。

## 🎯 核心特性

- **极速性能**：性能接近原生 Dapper，远超 EF Core
- **多数据库支持**：原生支持 SQL Server、MySQL、Oracle、PostgreSQL、SQLite
- **灵活查询**：支持基于 Lambda、`Expr` 或 `ExprString` 的多种查询方式
- **自动关联**：通过特性实现无损的 JOIN 查询，无需手写 SQL
- **声明式事务**：`[Transaction]` 特性实现 AOP 事务管理
- **动态分表**：`IArged` 接口支持分表路由
- **异步支持**：完整的 async/await 支持
- **类型安全**：强类型泛型接口，编译时类型检查

## 📋 环境要求

- **.NET 8.0+**
- **.NET Standard 2.0**（兼容 .NET Framework 4.6.1+）
- **依赖库**：Autofac、Castle.Core

## 📦 安装

```bash
dotnet add package LiteOrm
```

## 🚀 快速入门

### 1. 配置连接

在 `appsettings.json` 中：

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
### 2. 注册 LiteOrm

在 `Program.cs` 中注册：

**Console：**
```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()  // 自动初始化
    .Build();
```

**ASP.NET Core：**
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();  // 通过 IHostBuilder 扩展方法集成
```

### 3. 定义实体

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

### 4. 定义服务 (可选）

```csharp
// 定义视图模型（用于查询，可包含关联字段）
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

### 5. 使用服务

可使用自定义的服务，也可直接使用泛型服务 ：

```csharp
// 插入
var user = new User { UserName = "admin", Email = "admin@test.com" };
await userService.InsertAsync(user);

// 查询
var users = await userService.SearchAsync(u => u.Email.Contains("test"));
var admin = await userService.SearchOneAsync(u => u.UserName == "admin");

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

## 💡 常见特性

### Lambda 查询

```csharp
// 基础查询
var users = await userService.SearchAsync(u => u.Age > 18);

// 排序
var sorted = await userService.SearchAsync(
    q => q.Where(u => u.Age > 18).OrderBy(u => u.Age)
);

// 分页
var paged = await userService.SearchAsync(
    q => q.Where(u => u.Status == 1)
          .OrderBy(u => u.Id)
          .Skip(10).Take(20)
);
```

### Expr 表达式查询

```csharp
// 手动构建表达式（支持更复杂的动态条件）
var expr = Expr.Prop("Age") > 18 & Expr.Prop("Status") == 1;
var users = await userService.SearchAsync(expr);

// IN 查询
var users = await userService.SearchAsync(
    Expr.Prop("Id").In(1, 2, 3, 4, 5)
);

// LIKE 查询
var users = await userService.SearchAsync(
    Expr.Prop("UserName").Contains("admin")
);

// 链式构建复杂查询
var query = Expr.From<User>()
    .Where(Expr.Prop("Age") > 18)
    .OrderBy(Expr.Prop("CreateTime"))
    .Section(0, 10);  // LIMIT/OFFSET
var result = await userService.SearchAsync(query);
```

### ExprString 查询 (.NET 8.0+)

```csharp
// 使用参数化插值字符串，防止 SQL 注入
int minAge = 18;
var expr = Expr.Prop("Age") > 25;

// ObjectViewDAO 示例
var users = await objectViewDAO.Search($"WHERE {expr} AND Age > {minAge}").ToListAsync();

// DataViewDAO 示例
var dataTable = await dataViewDAO.Search(
    $"SELECT Id, UserName FROM Users WHERE {Expr.Prop("Age")} > {minAge}"
).GetResultAsync();
```

### EXISTS 子查询

```csharp
// 检查关联数据存在性
var result = await userService.SearchAsync(
    q => q.Where(u => Expr.Exists<Order>(o => o.UserId == u.Id))
);
```

### 自动关联查询

```csharp
// 定义关联
[Table("Orders")]
public class Order
{
    [Column("Id", IsPrimaryKey = true)]
    public int Id { get; set; }

    [Column("UserId")]
    [ForeignType(typeof(User))]
    public int UserId { get; set; }
}

// 视图模型包含关联字段
public class OrderView : Order
{
    [ForeignColumn(typeof(User), Property = "UserName")]
    public string UserName { get; set; }
}

// 查询时自动 JOIN
var orders = await orderService.SearchAsync<OrderView>();
```

### 声明式事务

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

### 动态分表

```csharp
public class Log : IArged
{
    [Column("Id", IsPrimaryKey = true)]
    public int Id { get; set; }

    [Column("Content")]
    public string Content { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    // 自动按月份路由到 Log_202401、Log_202402 等表
    string[] IArged.TableArgs => [CreateTime.ToString("yyyyMM")];
}
```

## ⚡ 性能基准

基于 LiteOrm.Benchmark 项目的最新对比测试结果（.NET 10.0.3, Linux Ubuntu 24.04 LTS, Intel Xeon Silver 4314 CPU 2.40GHz, MySQL）：

### 插入性能对比（ms）

| 框架 | 100 条 | 1000 条 | 5000 条 |
|:---|---:|---:|---:|
| **LiteOrm** | **4.16** | **12.21** | **54.27** |
| SqlSugar | 4.37 | 19.30 | 100.73 |
| FreeSql | 5.00 | 21.09 | 90.11 |
| EF Core | 20.18 | 150.28 | 663.25 |
| Dapper | 26.15 | 216.81 | 1,124.73 |

### 更新性能对比（ms）

| 框架 | 100 条 | 1000 条 | 5000 条 |
|:---|---:|---:|---:|
| **LiteOrm** | **5.55** | **20.20** | **94.34** |
| SqlSugar | 6.23 | 44.90 | 243.68 |
| FreeSql | 6.38 | 39.87 | 204.43 |
| EF Core | 18.92 | 133.85 | 574.62 |
| Dapper | 28.37 | 243.36 | 1,209.28 |

### Upsert 性能对比（ms）

| 框架 | 100 条 | 1000 条 | 5000 条 |
|:---|---:|---:|---:|
| **FreeSql** | **5.22** | **17.51** | 91.97 |
| LiteOrm | 6.02 | 19.06 | **80.58** |
| SqlSugar | 10.43 | 108.56 | 1,784.72 |
| EF Core | 21.04 | 137.46 | 571.41 |
| Dapper | 28.58 | 242.39 | 1,211.96 |

### 关联查询性能对比（ms）

| 框架 | 100 条 | 1000 条 | 5000 条 |
|:---|---:|---:|---:|
| **FreeSql** | **1.43** | 9.21 | **42.80** |
| LiteOrm | 1.45 | 9.67 | 43.11 |
| Dapper | 1.45 | **8.76** | 45.37 |
| SqlSugar | 2.32 | 24.25 | 95.99 |
| EF Core | 5.04 | 14.10 | 54.02 |

### 内存分配对比（1000 条数据，KB）

| 框架 | 插入 | 更新 | Upsert | 关联查询 |
|:---|---:|---:|---:|---:|
| **LiteOrm** | **873.32** | **1,202.67** | **1,987.09** | **238.35** |
| Dapper | 2,476.27 | 3,093.32 | 2,799.01 | 418.43 |
| SqlSugar | 4,573.20 | 7,679.01 | 35,951.71 | 9,228.01 |
| FreeSql | 4,633.33 | 6,880.93 | 2,250.44 | 856.68 |
| EF Core | 16,708.90 | 13,450.93 | 13,629.90 | 2,203.24 |

> 📊 详细的性能基准报告请参考 [LiteOrm.Benchmark](./LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md)

## 📚 文档与示例

| 资源 | 说明 |
|:---|:---|
| [API 参考](./LITEORM_API_REFERENCE.zh.md) | 完整的 API 文档和配置说明 |
| [Demo 项目](./LiteOrm.Demo/) | 主要特性演示程序 |
| [性能报告](./LiteOrm.Benchmark/) | 详细的性能基准测试报告 |
| [单元测试](./LiteOrm.Tests/) | 完整的测试覆盖 |

## 🤝 贡献与反馈

如发现问题或有改进建议，欢迎提交 [Issue](https://github.com/danjiewu/LiteOrm/issues) 或 [Pull Request](https://github.com/danjiewu/LiteOrm/pulls)。

## 📄 开源协议

基于 [MIT](LICENSE) 协议发布。

