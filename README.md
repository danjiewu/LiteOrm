# LiteOrm.Core

LiteOrm.Core 是一个轻量级、高性能的 .NET ORM (对象关系映射) 框架，旨在提供简单、灵活且强大的数据库操作能力。它可以运行在 .NET Standard 2.0 和 .NET 8.0+ 环境下。

## 主要特性

*   **多数据库支持**：原生支持 SQL Server, MySQL (MariaDB), Oracle, 和 SQLite。
*   **灵活的表达式引擎**：基于 `Expr` 的查询构建器，支持复杂的条件组合（And, Or, Not, In, Like 等）。
*   **实体服务模式**：提供统一的 `IEntityService<T>` 和 `IEntityViewService<T>` 接口，封装常用的 CRUD 操作。
*   **异步支持**：所有核心操作均提供基于 `Task` 的异步版本。
*   **声明式映射**：使用 `[Table]`, `[Column]`, `[ForeignType]` 等特性轻松定义实体与数据库表的映射关系。
*   **高性能批量操作**：支持大批量数据的插入、更新和删除。
*   **Autofac 与 ASP.NET Core 集成**：提供便捷的扩展方法，通过 Autofac 实现自动服务注册和拦截。
*   **高级查询支持**：支持子查询、连接查询（Join）、正则匹配等。

## 环境要求

*   .NET 8.0 或更高版本
*   .NET Standard 2.0 (兼容 .NET Framework 4.6.1+)

## 快速入门

### 1. 定义实体

```csharp
[Table("USERS")]
public class User
{
    [Column("ID", IsPrimaryKey = true)]
    public int Id { get; set; }

    [Column("USERNAME")]
    public string UserName { get; set; }

    [Column("EMAIL")]
    public string Email { get; set; }
}
```

### 2. 定义服务接口

```csharp
public interface IUserService : IEntityService<User>, IEntityViewService<User> 
{ 
    // 可以添加自定义业务方法
}

[AutoRegister(ServiceLifetime.Scoped)]
public class UserService : EntityService<User>, IUserService
{
    // 实现自定义业务逻辑
}
```

### 3. 在 ASP.NET Core 中启动配置

在 `Program.cs` 中注册 LiteOrm 服务：

```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm() // 注册 LiteOrm 和 Autofac
    .ConfigureServices(services => {
        // 配置数据库连接等
    })
    .Build();
```

### 4. 使用服务进行查询

```csharp
public class MyController : ControllerBase
{
    private readonly IUserService _userService;

    public MyController(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<IActionResult> GetUser(int id)
    {
        // 使用 Lambda 表达式查询
        var user = await _userService.SearchOneAsync(u => u.Id == id);
        
        // 或者使用 Expr 构建器
        // var user = await _userService.SearchOneAsync(Expr.Property("UserName") == "admin");
        
        return Ok(user);
    }
}
```

## 查询语句示例 (Expr)

LiteOrm 提供了一套强大的表达式构建工具：

```csharp
// 组合条件
Expr condition = (Expr.Property("Age") > 18) & (Expr.Property("Status") == 1);

// IN 查询
var ids = new[] { 1, 2, 3 };
Expr inCondition = Expr.Property("Id").In(ids);

// LIKE 查询
Expr likeCondition = Expr.Property("UserName").Contains("john");

// 执行查询
var results = await service.SearchAsync(condition | inCondition);
```

## 项目结构

*   `LiteOrm.Common`: 核心抽象、映射特性、表达式定义 (`Expr`) 和基础接口。
*   `LiteOrm`: 核心实现、DAO、数据库驱动特定的 SQL 生成器。
*   `LiteOrm.ASPNetCore`: 与 ASP.NET Core 框架的集成支持。
*   `LiteOrm.Test`: 单元测试和示例代码。

## 开源协议

MIT
