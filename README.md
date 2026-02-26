# LiteOrm

LiteOrm 是一个轻量级、高性能的 .NET ORM (对象关系映射) 框架，旨在提供简单、灵活且高效的数据库操作体验。它原生支持经典三层架构，结合了微 ORM 的性能和完整 ORM 的易用性，特别适合对性能要求高且需要灵活处理复杂 SQL 的场景。

[![NuGet](https://img.shields.io/nuget/v/LiteOrm.svg)](https://www.nuget.org/packages/LiteOrm/)
[![License](https://img.shields.io/github/license/danjiewu/LiteOrm.svg)](LICENSE)

## 主要特性

*   **极速性能**：深度优化反射与元数据处理，在基准测试中性能接近原生 Dapper，远超传统大型 ORM（如 EF Core）。
*   **多数据库原生支持**：内建支持 SQL Server, MySQL (MariaDB), Oracle, PostgreSQL 和 SQLite，支持各方言的高性能分页与函数。
*   **灵活的查询引擎**：基于 `Expr` 的逻辑表达系统，支持 Lambda 自动转换、JSON 序列化、复杂的嵌套条件组合（And/Or/In/Like/Join）。
*   **企业级 AOP 事务**：支持声明式事务（`[Transaction]` 特性），自动平衡跨服务、跨数据源的事务一致性与连接管理。
*   **自动化关联 (Join)**：通过 `[TableJoin]`、 `[ForeignType]`、`[ForeignColumn]` 特性实现无损的表关联查询，自动生成高效 SQL，无需手写 JOIN 语句。
*   **动态分表路由**：原生支持 `IArged` 接口，解决海量数据下的动态水平拆分（分表）路由需求。
*   **高性能批量处理**：预留 `IBulkProvider` 接口，可针对特定数据库采用方式（如 `MySqlBulkCopy` ）极大提高插入效率。
*   **模块化与可扩展性**：支持自定义 SQL 函数 Handler、自定义类型转换器，可适配各种业务特殊的 SQL 方言。
*   **完整的异步支持**：所有操作都提供同步和基于 Task 的异步方法，支持现代异步编程模式。
*   **类型安全**：强类型的泛型接口和方法，提供编译时类型检查，减少运行时错误。

## 环境要求

*   **.NET 8.0 / 10.0** 或更高版本
*   **.NET Standard 2.0** (兼容 .NET Framework 4.6.1+)
*   **支持的数据库**：
    *   Microsoft SQL Server 2012 及以上
    *   MySQL 5.7 及以上 (含 MariaDB)
    *   Oracle 12c 及以上
    *   PostgreSQL 8.4 及以上
    *   SQLite 3.x
    *   其他数据库可通过实现自定义 `SqlBuilder` 进行扩展。   
*   **第三方依赖库**：
    * Autofac
    * Autofac.Extras.DynamicProxy
    * Autofac.Extensions.DependencyInjection
    * Castle.Core
    * Castle.Core.AsyncInterceptor

## 安装

```bash
dotnet add package LiteOrm
```

## 快速链接

- 📖 **[API 参考文档](./docs/LITEORM_API_REFERENCE.md)** - 完整的 API 使用指南
- 🎓 **[演示项目](./LiteOrm.Demo/README.md)** - 6 个核心特性演示程序
- ⚡ **[性能报告](./LiteOrm.Benchmark/)** - 性能基准测试报告
- ✅ **[单元测试](./LiteOrm.Tests/)** - 完整的测试覆盖

## 快速入门

### 1. 映射定义

```csharp
using LiteOrm.Common;

[Table("USERS")]
public class User
{
    [Column("ID", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("USERNAME")]
    public string UserName { get; set; }

    [Column("EMAIL")]
    public string Email { get; set; }
    
    [Column("CREATE_TIME")]
    public DateTime? CreateTime { get; set; }
}
```

### 2. 注入注册 (ASP.NET Core / Generic Host)

在 `Program.cs` 中添加配置：

```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm() // 自动扫描 [AutoRegister] 特性并初始化连接池
    .Build();
```

`appsettings.json` 配置示例：

```json
{
    "LiteOrm": {
        "Default": "DefaultConnection",
        "DataSources": [
            {
                "Name": "DefaultConnection",
                "ConnectionString": "Server=mysql;User ID=ormbench;Password=orm!123;Database=OrmBench;AllowLoadLocalInfile=true;",
                "Provider": "MySqlConnector.MySqlConnection, MySqlConnector",
                "KeepAliveDuration": "00:10:00",
                "PoolSize": 20,
                "MaxPoolSize": 100,
                "ParamCountLimit": 2000,
                "SyncTable": true,
                "ReadOnlyConfigs": [
                    {
                        "ConnectionString": "Server=mysql01;User ID=ormbench;Password=orm!123;Database=OrmBench;AllowLoadLocalInfile=true;"
                    },
                    {
                        "ConnectionString": "Server=mysql02;User ID=ormbench;Password=orm!123;Database=OrmBench;AllowLoadLocalInfile=true;",
                        "PoolSize": 10,
                        "KeepAliveDuration": "00:30:00"
                    }
                ]
            }
        ]
    }
}
```

**配置参数详解：**

| 参数名 | 默认值 | 说明 |
| :--- | :--- | :--- |
| **Default** | - | 默认数据源名称，如果实体未指定数据源则使用此项。 |
| **Name** | - | 必填，数据源名称。 |
| **ConnectionString** | - | 必填，物理连接字符串。 |
| **Provider** | - | 必填，DbConnection 实现类的类型全名（Assembly Qualified Name）。 |
| **PoolSize** | 16 | 基础连接池容量，超过此数量的数据库空闲连接会被释放。 |
| **MaxPoolSize** | 100 | 最大并发连接限制，防止耗尽数据库资源。 |
| **KeepAliveDuration** | 10min | 连接空闲存活时间，超过此时间后空闲连接将被物理关闭。 |
| **ParamCountLimit** | 2000 | 单条 SQL 支持的最大参数个数，批量操作时参数超过此限制会自动分批执行，避免触发 DB 限制。 |
| **SyncTable** | false | 是否在启动时自动检测实体类并尝试同步数据库表结构。 |
| **ReadOnlyConfigs** | - | 只读库配置 |

### ReadOnlyConfigs（只读从库配置）

LiteOrm 支持为每个主数据源配置若干只读从库，用于读写分离、负载均衡或故障切换。只读配置放在对应数据源对象的 `ReadOnlyConfigs` 数组中。

说明：

- `ReadOnlyConfigs`：可选数组，每项为只读数据源配置对象（可为空）。
- 每个只读项至少包含 `ConnectionString`，当只读库与主库使用不同驱动时也可指定 `Provider`。
- LiteOrm 在执行只读操作（例如 SELECT 查询）时会优先选择只读配置，从而减轻主库写入压力并实现读扩展。
- 如果所有只读配置不可用或未配置，LiteOrm 会回退到主数据源的连接。
- 可结合连接池与自定义路由策略实现更复杂的读写分离、负载均衡或高可用策略。

### 3. 自定义服务接口与实现（可选）

```csharp
using LiteOrm.Service;

public interface IUserService : IEntityService<User>, IEntityViewService<UserView>, IEntityServiceAsync<User>, IEntityViewServiceAsync<UserView>
{
    UserView GetByUserName(string userName);
}

public class UserService : EntityService<User,UserView>, IUserService
{
    // 实现自定义方法
    public UserView GetByUserName(string userName)
    {
        return SearchOne(u => u.UserName == userName);
    }
}
```

### 4. 执行查询与操作

```csharp
using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.AspNetCore.Mvc;

public class UserDemoController : ControllerBase
{
    private readonly IUserService userService;
    
    public UserDemoController(IUserService userService)
    {
        this.userService = userService;
    }

    public async Task<IActionResult> Demo()
    {
        // 1. Lambda 异步查询
        var admin = await userService.SearchOneAsync(u => u.UserName == "admin" && u.Id > 0);
        
        // 2. 分页查询（使用Expr方式）
        var page = await userService.SearchAsync(
            Expr.Where<User>(u => u.CreateTime > DateTime.Today.AddDays(-7))
                .OrderBy((nameof(User.Id), false))
                .Section(0, 10)
        );
        
        // 3. 插入新用户
        var newUser = new User
        {
            UserName = "newuser",
            Email = "newuser@example.com",
            CreateTime = DateTime.Now
        };
        await userService.InsertAsync(newUser);
        
        // 4. 更新用户信息
        newUser.Email = "updated@example.com";
        await userService.UpdateAsync(newUser);
        
        // 5. 批量更新
        foreach (var user in page)
        {
            user.Email = user.Email.Replace("@example.com", "@updated.com");
        }
        await userService.BatchUpdateAsync(page);
        
        // 6. 删除用户
        await userService.DeleteAsync(newUser);
        
        return Ok(page);
    }
}
```

## 查询系统 (Expr)

LiteOrm 的核心是其强大的 `Expr` 表达式系统。

### Lambda 自动转换

```csharp
// 自动转换为：WHERE (AGE > 18 AND USERNAME LIKE '%admin%')
Expr expr = Expr.Exp<User>(u => u.Age > 18 && u.UserName.Contains("admin"));
```

### Lambda 表达式分页与排序

LiteOrm 支持使用 `IQueryable` 形式的 Lambda 表达式进行查询，并自动转换为 SQL 分页和排序。

```csharp
// 基础查询
var users = await userService.SearchAsync(
    q => q.Where(u => u.Age > 18)
);

// 排序
var sortedUsers = await userService.SearchAsync(
    q => q.Where(u => u.Age > 18).OrderBy(u => u.Age).ThenByDescending(u => u.Id)
);

// 分页 (Skip/Take)
var pagedUsers = await userService.SearchAsync(
    q => q.Where(u => u.Age > 18)
          .OrderBy(u => u.CreateTime)
          .Skip(10)
          .Take(20)
);

// 多条件合并 (多个 Where 自动合并为 AND)
var multiCondition = await userService.SearchAsync(
    q => q.Where(u => u.Age > 18)
          .Where(u => !string.IsNullOrEmpty(u.UserName))
          .Where(u => u.UserName.Contains("admin"))
);
// 等效于: WHERE (Age > 18 AND UserName IS NOT NULL AND UserName Contains admin)

// EXISTS 子查询
// 查询拥有部门的用户
var usersWithDept = await userService.SearchAsync(
    q => q.Where(u => Expr.Exists<Department>(d => d.Id == u.DeptId))
);

// EXISTS + 其他条件组合
var filteredUsers = await userService.SearchAsync(
    q => q.Where(u => u.Age > 25 && Expr.Exists<Department>(d => d.Id == u.DeptId && d.Name == "IT"))
          .OrderByDescending(u => u.CreateTime)
          .Skip(0).Take(10)
);

// NOT EXISTS
var usersWithoutDept = await userService.SearchAsync(
    q => q.Where(u => !Expr.Exists<Department>(d => d.Id == u.DeptId))
);
```

### 手动构建表达式

```csharp
// 构建复杂表达式：(Age > 18 AND (UserName LIKE '%admin%' OR Email LIKE '%admin%'))
Expr expr = Expr.And(
    Expr.Prop("Age") > 18,
    Expr.Or(
        Expr.Prop("UserName").Contains("admin"),
        Expr.Prop("Email").Contains("admin")
    )
);
```

### JSON 序列化

`Expr` 节点支持直接序列化为 JSON，方便前端动态传递复杂配置化的过滤规则。

### SQL 生成器 (SqlGen)

可以独立于 DAO 使用 `SqlGen` 生成参数化 SQL，方便开发调试：

```csharp
var expr = (Expr.Prop(nameof(User.Age)) > 18) & (Expr.Prop(nameof(User.UserName)).Contains("admin_"));
var res = new SqlGen(typeof(User)).ToSql(expr);
// res.Sql -> (`User`.`Age` > @0 AND `User`.`UserName` LIKE @1 ESCAPE '/')
// res.Params -> [ { "0", 18 }, { "1", "%admin/_%" } ]
```

## 高级特性

### 1. Exists 存在性查询

LiteOrm 支持通过 `Expr.Exists<T>` 进行高效的 SQL EXISTS 子查询。这是一种性能优化的方式，特别适合在只需检查关联数据是否存在，而不需要返回关联数据的场景。

```csharp
// 基础 EXISTS 查询
var result = await userService.SearchAsync(
    q => q.Where(u => Expr.Exists<Department>(d => d.Id == u.DeptId))
);

// EXISTS 与复杂条件组合
var result = await userService.SearchAsync(
    q => q.Where(u => u.Age > 25 && 
                      Expr.Exists<Department>(d => d.Id == u.DeptId && d.Name == "IT") &&
                      Expr.Exists<Department>(d => d.ParentId != null))
);

// NOT EXISTS
var result = await userService.SearchAsync(
    q => q.Where(u => !Expr.Exists<Department>(d => d.Id == u.DeptId))
);
```

**何时使用 EXISTS 而不是 JOIN**：
- ✅ 只检查关联数据是否存在
- ✅ 不需要返回或访问关联表字段
- ✅ 右表（关联表）数据量大，JOIN 可能产生大量临时行
- ❌ 需要返回关联表字段时使用 JOIN 或视图映射

### 2. 自动化关联查询

```csharp
// 定义关联
[Table("Orders")]
public class Order
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    
    [Column("UserId")]
    [ForeignType(typeof(User))]  // ForeignType 放在外键属性上
    public int UserId { get; set; }
    
    [Column("Amount")]
    public decimal Amount { get; set; }
}

// 定义视图模型，包含关联数据
public class OrderView : Order
{
    // 使用 ForeignColumn 直接从关联表获取字段
    [ForeignColumn(typeof(User), Property = "UserName")]
    public string UserName { get; set; }
}

// 查询时自动 JOIN
var orders = await orderService.SearchAsync<OrderView>(o => o.Amount > 100);
// 结果中包含 UserName 字段
```

### 2. 动态分表

```csharp
// 实现 IArged 接口
public class Log : IArged
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    
    [Column("Content")]
    public string Content { get; set; }
    
    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }
    
    // 注意：TableArgs 通过显式接口实现，不作为数据库字段
    // 格式为 Log_{yyyyMM}，根据 CreateTime 自动路由到对应月份表
    string[] IArged.TableArgs => [CreateTime.ToString("yyyyMM")];
}

// 使用分表（无需手动指定表名，自动根据 CreateTime 路由）
var log = new Log
{
    Content = "Test log",
    CreateTime = new DateTime(2026, 1, 15)  // 自动路由到 Log_202601 表
};
await logService.InsertAsync(log);
```

### 3. 声明式事务

```csharp
[Service]
public class BusinessService
{
    private readonly IUserService userService;
    private readonly IOrderService orderService;
    
    public BusinessService(IUserService userService, IOrderService orderService)
    {
        this.userService = userService;
        this.orderService = orderService;
    }
    
    [Transaction] // 自动事务管理
    public async Task CreateUserWithOrder(User user, Order order)
    {
        // 插入用户
        await userService.InsertAsync(user);
        
        // 关联订单
        order.UserId = user.Id;
        await orderService.InsertAsync(order);
        
        // 自动提交事务
    }
}
```

## Demo 示例项目

我们提供了一个完整的示例项目 [LiteOrm.Demo](./LiteOrm.Demo)，涵盖了以下核心特性的演示：

- **表达式系统 (Expr)**：二元/一元、Lambda 转换、JSON 序列化。
- **Lambda 表达式查询**：
  - 基础查询 (Where) 
  - 排序 (OrderBy/OrderByDescending/ThenBy)
  - 分页 (Skip/Take)
- **自动化关联 (Join)**：利用特性实现多级表关联带出。
- **动态分表 (IArged)**：按参数自动路由物理表。
- **声明式事务**：基于 AOP 的无侵入事务控制。


## 性能测试

LiteOrm 在高并发与大规模数据读写场景下表现优异。以下是基于 `LiteOrm.Benchmark` 项目（Windows 11, Intel Core i5-13400F 2.50GHz, .NET 10.0.103）的最新测试结果对比：

### 性能对比概览（BatchCount=100）

| 框架 | 插入性能 (ms) | 更新性能 (ms) | Upsert (ms) | 关联查询 (ms) | 内存分配 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **LiteOrm** | **3,743.9** | **4,684.3** | 5,535.7 | 974.9 | **295.97 KB** |
| FreeSql | 4,358.7 | 4,859.8 | **4,843.1** | 942.3 | 460.62 KB |
| SqlSugar | 4,126.6 | 5,377.7 | 9,355.1 | 1,664.3 | 476.13 KB |
| Dapper | 13,236.3 | 16,492.4 | 18,593.3 | **893.4** | 254.58 KB |
| EF Core | 21,973.8 | 21,571.2 | 22,967.5 | 6,680.8 | 1,965.32 KB |

### 性能对比概览（BatchCount=1000）

| 框架 | 插入性能 (ms) | 更新性能 (ms) | Upsert (ms) | 关联查询 (ms) | 内存分配 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **LiteOrm** | **10,711.9** | **16,472.2** | 16,733.4 | **6,061.1** | **870.27 KB** |
| FreeSql | 17,707.5 | 30,842.5 | **14,769.0** | 6,520.9 | 4,629.99 KB |
| SqlSugar | 15,775.0 | 35,522.5 | 66,357.1 | 12,304.3 | 4,571.36 KB |
| Dapper | 120,213.5 | 132,356.8 | 136,051.1 | 6,556.1 | 2,476.22 KB |
| EF Core | 169,846.8 | 149,932.5 | 157,037.7 | 12,422.7 | 18,118.07 KB |

### 性能对比概览（BatchCount=5000）

| 框架 | 插入性能 (ms) | 更新性能 (ms) | Upsert (ms) | 关联查询 (ms) | 内存分配 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **LiteOrm** | **40,268.4** | **68,069.3** | 60,711.4 | **39,060.2** | **4,082.59 KB** |
| FreeSql | 72,488.8 | 133,942.8 | **58,183.2** | 41,220.4 | 23,333.54 KB |
| SqlSugar | 76,643.9 | 194,130.4 | 885,872.8 | 63,744.0 | 23,196.37 KB |
| Dapper | 690,745.5 | 659,912.8 | 677,140.4 | 39,942.4 | 12,349.48 KB |
| EF Core | 824,700.5 | 749,069.8 | 794,845.9 | 49,403.4 | 80,230.09 KB |

### 各数据量级别最优性能

| 测试项目 | 100 条 | 1000 条 | 5000 条 |
|----------|--------|---------|---------|
| **Insert** | **LiteOrm** (3,743.9 ms) | **LiteOrm** (10,711.9 ms) | **LiteOrm** (40,268.4 ms) |
| **Update** | **LiteOrm** (4,684.3 ms) | **LiteOrm** (16,472.2 ms) | **LiteOrm** (68,069.3 ms) |
| **Upsert** | **FreeSql** (4,843.1 ms) | **FreeSql** (14,769.0 ms) | **FreeSql** (58,183.2 ms) |
| **JoinQuery** | **Dapper** (893.4 ms) | **LiteOrm** (6,061.1 ms) | **LiteOrm** (39,060.2 ms) |

> *注：完整测试报告请参考：[LiteOrm 性能评测报告](./LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md).*

## 模块说明

*   **LiteOrm.Common**: 核心元数据定义、`Expr` 表达式系统、基础工具类。
*   **LiteOrm**: 核心 ORM 逻辑、SQL 构建器实现、DAO 基类、Session/Transaction 管理单元。
*   **LiteOrm.ASPNetCore**: 针对 ASP.NET Core 的扩展支持（待开发）。
*   **LiteOrm.Demo**: 示例项目，涵盖了几乎所有核心特性的代码演示。
*   **LiteOrm.Benchmark**: 性能测试工程，包含与常见 ORM 的对比。
*   **LiteOrm.Tests**: 单元测试项目。
*   **API 参考文档**: [LITEORM_API_REFERENCE.md](./docs/LITEORM_API_REFERENCE.md)

## 贡献与反馈

如果您在使用过程中发现任何问题或有任何改进建议，欢迎提交 [Issue](https://github.com/danjiewu/LiteOrm/issues) 或发起 [Pull Request](https://github.com/danjiewu/LiteOrm/pulls)。


## 项目资源

### 📚 文档中心

| 文档 | 说明 |
|-----|------|
| [API 参考](./docs/LITEORM_API_REFERENCE.md) | 完整的 API 和特性说明 |
| [Demo 使用指南](./LiteOrm.Demo/README.md) | 演示程序使用说明和代码示例 |

### 🎯 核心演示程序

LiteOrm.Demo 包含 6 个核心演示程序，展示框架的主要特性：

| 演示 | 功能 | 位置 |
|-----|------|------|
| ExprTypeDemo | 表达式构造和序列化 | Demos/ExprTypeDemo.cs |
| PracticalQueryDemo | 综合查询实践 | Demos/PracticalQueryDemo.cs |
| ExistsSubqueryDemo | EXISTS 子查询演示 | Demos/ExistsSubqueryDemo.cs |
| TransactionDemo | 事务和业务流程 | Demos/TransactionDemo.cs |
| DataViewDemo | 聚合查询和 GroupBy | Demos/DataViewDemo.cs |
| UpdateExprDemo | 复杂更新操作 | Demos/UpdateExprDemo.cs |

---

## 开源协议

基于 [MIT](LICENSE) 协议发布。


