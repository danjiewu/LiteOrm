# LiteOrm

LiteOrm 是一个轻量级、高性能的 .NET ORM 框架。结合了微 ORM 的性能和完整 ORM 的易用性，特别适合对性能要求高且需要灵活处理复杂 SQL 的场景。

[![NuGet](https://img.shields.io/nuget/v/LiteOrm.svg)](https://www.nuget.org/packages/LiteOrm/)
[![License](https://img.shields.io/github/license/danjiewu/LiteOrm.svg)](LICENSE)

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

### 1. 定义实体

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

### 2. 配置连接

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

### 3. 定义服务

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

### 4. 使用服务

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

基于 LiteOrm.Benchmark 项目的最新对比测试结果（.NET 10.0.3, MySQL）：

### 插入性能对比（ms）

| 框架 | 100 条 | 1000 条 | 5000 条 |
|:---|---:|---:|---:|
| **LiteOrm** | **3.74** | **10.71** | **40.27** |
| FreeSql | 4.36 | 17.71 | 72.49 |
| SqlSugar | 4.13 | 15.78 | 76.64 |
| Dapper | 13.24 | 120.21 | 690.75 |
| EF Core | 21.97 | 169.85 | 824.70 |

### 更新性能对比（ms）

| 框架 | 100 条 | 1000 条 | 5000 条 |
|:---|---:|---:|---:|
| **LiteOrm** | **4.68** | **16.47** | **68.07** |
| FreeSql | 4.86 | 30.84 | 133.94 |
| SqlSugar | 5.38 | 35.52 | 194.13 |
| Dapper | 16.49 | 132.36 | 659.91 |
| EF Core | 21.57 | 149.93 | 749.07 |

### Upsert 性能对比（ms）

| 框架 | 100 条 | 1000 条 | 5000 条 |
|:---|---:|---:|---:|
| LiteOrm | 5.54 | 16.73 | 60.71 |
| **FreeSql** | **4.84** | **14.77** | **58.18** |
| SqlSugar | 9.36 | 66.36 | 885.87 |
| Dapper | 18.59 | 136.05 | 677.14 |
| EF Core | 22.97 | 157.04 | 794.85 |

### 关联查询性能对比（ms）

| 框架 | 100 条 | 1000 条 | 5000 条 |
|:---|---:|---:|---:|
| LiteOrm | 0.97 | **6.06** | **39.06** |
| **FreeSql** | 0.94 | 6.52 | 41.22 |
| SqlSugar | 1.66 | 12.30 | 63.74 |
| **Dapper** | **0.89** | 6.56 | 39.94 |
| EF Core | 6.68 | 12.42 | 49.40 |

### 内存分配对比（1000 条数据，KB）

| 框架 | 插入 | 更新 | Upsert | 关联查询 |
|:---|---:|---:|---:|---:|
| **LiteOrm** | **870.27** | **1,514.47** | **2,138.49** | 628.98 |
| FreeSql | 4,629.99 | 6,877.24 | 2,239.19 | 854.07 |
| SqlSugar | 4,571.36 | 7,677.75 | 35,952.45 | 9,226.19 |
| Dapper | 2,476.22 | 3,094.99 | 2,798.43 | **415.97** |
| EF Core | 18,118.07 | 15,149.28 | 14,803.48 | 2,198.79 |

> 📊 详细的性能基准报告请参考 [LiteOrm.Benchmark](./LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md)

## 📚 文档与示例

| 资源 | 说明 |
|:---|:---|
| [API 参考](./LITEORM_API_REFERENCE.md) | 完整的 API 文档和配置说明 |
| [Demo 项目](./LiteOrm.Demo/) | 主要特性演示程序 |
| [性能报告](./LiteOrm.Benchmark/) | 详细的性能基准测试报告 |
| [单元测试](./LiteOrm.Tests/) | 完整的测试覆盖 |

## 🤝 贡献与反馈

如发现问题或有改进建议，欢迎提交 [Issue](https://github.com/danjiewu/LiteOrm/issues) 或 [Pull Request](https://github.com/danjiewu/LiteOrm/pulls)。

## 📄 开源协议

基于 [MIT](LICENSE) 协议发布。


