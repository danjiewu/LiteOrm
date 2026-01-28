# LiteOrm

LiteOrm 是一个轻量级、高性能的 .NET ORM (对象关系映射) 框架，旨在提供简单、灵活且高效的数据库操作体验。它结合了微 ORM 的性能和完整 ORM 的易用性，特别适合对性能要求极高且需要灵活处理复杂 SQL 的场景。

[![NuGet](https://img.shields.io/nuget/v/LiteOrm.svg)](https://www.nuget.org/packages/LiteOrm/)
[![License](https://img.shields.io/github/license/danjiewu/LiteOrm.svg)](LICENSE)

## 主要特性

*   **极速性能**：深度优化反射与元数据处理，在基准测试中性能接近原生 Dapper，远超传统大型 ORM（如 EF Core）。
*   **多数据库原生支持**：内建支持 SQL Server, MySQL (MariaDB), Oracle, 和 SQLite，支持各方言的高性能分页与函数。
*   **灵活的查询引擎**：基于 `Expr` 的逻辑表达系统，支持 Lambda 自动转换、JSON 序列化、复杂的嵌套条件组合（And/Or/In/Like/Join）。
*   **企业级 AOP 事务**：支持声明式事务（`[Transaction]` 特性），自动平衡跨服务、跨数据源的事务一致性与连接管理。
*   **自动化关联 (Join)**：通过 `[ForeignColumn]` 特性实现无损的表关联查询，自动生成高效 SQL，无需手写 JOIN 语句。
*   **动态分表路由**：原生支持 `IArged` 接口，解决海量数据下的动态水平拆分（分表）路由需求。
*   **高性能批量处理**：预留针对特定数据库的 `IBulkProvider` 接口，可通过 `MySqlBulkCopy` 等方式极大提高插入效率，支持万级数据快速导入。
*   **模块化与可扩展性**：支持自定义 SQL 函数 Handler、自定义类型转换器，可完美适配各种业务特殊的 SQL 方言。

## 环境要求

*   **.NET 8.0 / 10.0** 或更高版本
*   **.NET Standard 2.0** (兼容 .NET Framework 4.6.1+)

## 安装

```bash
dotnet add package LiteOrm
```

## 快速入门 

### 1. 映射定义

```csharp
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
    "ConnectionStrings": [
      {
        "Name": "DefaultConnection",
        "ConnectionString": "Server=localhost;Database=demo;Uid=root;Pwd=p@ssword;",
        "Provider": "MySqlConnector.MySqlConnection, MySqlConnector",
        "PoolSize": 20,
        "MaxPoolSize": 100
      }
    ]
  }
}
```

### 3. 执行查询与操作

```csharp
public class MyService(IEntityService<User> userService)
{
    public async Task Demo()
    {
        // 1. Lambda 异步查询
        var admin = await userService.SearchOneAsync(u => u.UserName == "admin" && u.Id > 0);
        
        // 2. 分页查询
        var page = await userService.SearchSectionAsync(u => u.CreateTime > DateTime.Today.AddDays(-7), 
                                                        new PageSection(0, 10, Sorting.Desc(nameof(User.Id))));
                                                        
        // 3. 批量更新部分字段
        await userService.UpdateValuesAsync(new Dictionary<string, object> { ["STATUS"] = 1 }, 
                                           u => u.Id < 100);
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

### JSON 序列化
`Expr` 节点支持直接序列化为 JSON，方便前端动态传递复杂配置化的过滤规则。

### SQL 生成器 (SqlGen)
可以独立于 DAO 使用 `SqlGen` 生成参数化 SQL：
```csharp
var res = new SqlGen(typeof(User)).ToSql(u => u.Id == 123);
// res.Sql -> SELECT ... FROM USERS WHERE ID = @0
```

## 性能测试

LiteOrm 在高并发与大规模数据读写场景下表现优异。以下是基于 `LiteOrm.Benchmark` 项目在本地环境下的部分测试结果参考：

| 框架 | 1000条插入 (ms) | 1000条更新 (ms) | 关联查询 (ms) | 内存分配 |
| :--- | :--- | :--- | :--- | :--- |
| **LiteOrm** | **~15ms** | **~25ms** | **~8ms** | **极低** |
| Dapper | ~14ms | ~24ms | ~12ms | 极低 |
| SqlSugar | ~35ms | ~48ms | ~22ms | 中 |
| EF Core | ~120ms | ~180ms | ~45ms | 高 |

> *注：测试基于 MySQL 8.0 物理连接，由 `LiteOrm.Benchmark` 项目生成。*

## 模块说明

*   **LiteOrm.Common**: 核心元数据定义、`Expr` 表达式系统、基础工具类。
*   **LiteOrm**: 核心 ORM 逻辑、SQL 构建器实现、DAO 基类、Session/Transaction 管理单元。
*   **LiteOrm.ASPNetCore**: 针对 ASP.NET Core 的扩展支持，提供声明式事务 AOP 拦截器。
*   **LiteOrm.Demo**: 详尽的示例项目，涵盖了几乎所有核心特性的代码演示。
*   **LiteOrm.Benchmark**: 性能测试工程，包含与常见 ORM 的对比。

## 贡献与反馈

如果您在使用过程中发现任何问题或有任何改进建议，欢迎提交 [Issue](https://github.com/danjiewu/LiteOrm/issues) 或发起 [Pull Request](https://github.com/danjiewu/LiteOrm/pulls)。

## 开源协议

基于 [MIT](LICENSE) 协议发布。


