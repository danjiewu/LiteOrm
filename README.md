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

- **.NET 8.0+** 或 **.NET 10.0+**
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

```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()  // 自动初始化
    .Build();
```

### 3. 定义服务

```csharp
public interface IUserService : IEntityService<User>, IEntityServiceAsync<User>
{
}

public class UserService : EntityService<User>, IUserService
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

基于 LiteOrm.Benchmark 项目的对比测试（1000 条记录）：

| 框架 | 插入 (ms) | 更新 (ms) | Upsert (ms) | 关联查询 (ms) | 内存分配 |
|:---|:---:|:---:|:---:|:---:|:---:|
| **LiteOrm** | **10,711.9** | **16,472.2** | 16,733.4 | **6,061.1** | **870.27 KB** |
| FreeSql | 17,707.5 | 30,842.5 | **14,769.0** | 6,520.9 | 4,629.99 KB |
| SqlSugar | 15,775.0 | 35,522.5 | 66,357.1 | 12,304.3 | 4,571.36 KB |
| Dapper | 120,213.5 | 132,356.8 | 136,051.1 | 6,556.1 | 2,476.22 KB |
| EF Core | 169,846.8 | 149,932.5 | 157,037.7 | 12,422.7 | 18,118.07 KB |

## 📚 文档与示例

| 资源 | 说明 |
|:---|:---|
| [API 参考](./docs/LITEORM_API_REFERENCE.md) | 完整的 API 文档和配置说明 |
| [Demo 项目](./LiteOrm.Demo/README.md) | 6 个核心特性演示程序 |
| [性能报告](./LiteOrm.Benchmark/) | 详细的性能基准测试报告 |
| [单元测试](./LiteOrm.Tests/) | 完整的测试覆盖 |

## 🤝 贡献与反馈

如发现问题或有改进建议，欢迎提交 [Issue](https://github.com/danjiewu/LiteOrm/issues) 或 [Pull Request](https://github.com/danjiewu/LiteOrm/pulls)。

## 📄 开源协议

基于 [MIT](LICENSE) 协议发布。


