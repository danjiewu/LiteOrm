# 第一个完整示例

本文通过一个最小可运行示例展示 LiteOrm 的典型使用流程：定义实体、注册服务、插入数据、查询数据和分页查询。

## 1. 定义实体

```csharp
using LiteOrm.Common;

[Table("Users")]
public class User
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserName")]
    public string? UserName { get; set; }

    [Column("Age")]
    public int Age { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    [Column("DeptId")]
    public int? DeptId { get; set; }
}
```

## 2. 定义服务

```csharp
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<User>, IEntityViewServiceAsync<User>
{ }

public class UserService : EntityService<User>, IUserService
{ }
```

如果你的项目暂时不准备定义自定义服务，也可以直接注入框架提供的泛型服务接口，后面的完整闭环里会同时演示两种写法。

## 3. 准备配置文件

```json
{
  "LiteOrm": {
    "Default": "DefaultConnection",
    "DataSources": [
      {
        "Name": "DefaultConnection",
        "ConnectionString": "Server=localhost;Database=TestDb;User Id=root;Password=123456;",
        "Provider": "MySqlConnector.MySqlConnection, MySqlConnector"
      }
    ]
  }
}
```

## 4. 注册 LiteOrm

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();
```

## 5. 插入一条数据

```csharp
var user = new User
{
    UserName = "admin",
    Age = 30,
    CreateTime = DateTime.Now,
    DeptId = 1
};

await userService.InsertAsync(user);
```

## 6. 执行查询

```csharp
var adults = await userService.SearchAsync(u => u.Age >= 18);
var admin = await userService.SearchOneAsync(u => u.UserName == "admin");
```

## 7. 执行分页

```csharp
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderByDescending(u => u.CreateTime)
          .Skip(0)
          .Take(10)
);
```

## 8. 完整调用闭环

### 8.1 在 Program.cs 中手动验证

下面的示例展示了一个更接近日常项目接入方式的完整流程。  
日常项目里，你既可以注入自定义的 `IUserService`，也可以直接注入泛型接口 `IEntityServiceAsync<User>` 与 `IEntityViewServiceAsync<User>`。

```csharp
using var scope = app.Services.CreateScope();

// 写法一：项目里已经定义了自定义服务
var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

// 写法二：直接使用框架提供的泛型服务
var entityService = scope.ServiceProvider.GetRequiredService<IEntityServiceAsync<User>>();
var viewService = scope.ServiceProvider.GetRequiredService<IEntityViewServiceAsync<User>>();

var user = new User
{
    UserName = "demo-user",
    Age = 26,
    CreateTime = DateTime.Now,
    DeptId = 2
};

// 1. 插入
// 两种写法二选一即可
await userService.InsertAsync(user);
// await entityService.InsertAsync(user);

// 2. 查询
var current = await userService.SearchOneAsync(u => u.Id == user.Id);
// var current = await viewService.SearchOneAsync(u => u.Id == user.Id);

// 3. 更新
current.UserName = "updated-demo-user";
await userService.UpdateAsync(current);
// await entityService.UpdateAsync(current);

// 4. 统计
var count = await userService.CountAsync(u => u.Age >= 18);
// var count = await viewService.CountAsync(u => u.Age >= 18);

// 5. 判断是否存在
var exists = await userService.ExistsAsync(u => u.UserName == "demo-user");
// var exists = await viewService.ExistsAsync(u => u.UserName == "demo-user");

// 6. 删除
if (exists)
{
    await userService.DeleteAsync(current);
    // await entityService.DeleteAsync(current);
}
```

### 8.2 在 Controller 中使用 LiteOrm

在 ASP.NET Core 项目中，更常见的做法是通过构造函数注入服务，然后在 Controller 中使用：

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("{id}")]
    public async Task<User?> GetById(int id)
    {
        return await _userService.SearchOneAsync(u => u.Id == id);
    }

    [HttpGet]
    public async Task<List<User>> List([FromQuery] string? keyword)
    {
        if (string.IsNullOrEmpty(keyword))
            return await _userService.SearchAsync();
        return await _userService.SearchAsync(u => u.UserName.Contains(keyword));
    }

    [HttpPost]
    public async Task<bool> Create(User user)
    {
        user.CreateTime = DateTime.Now;
        return await _userService.InsertAsync(user);
    }

    [HttpPut]
    public async Task<bool> Update(User user)
    {
        return await _userService.UpdateAsync(user);
    }

    [HttpDelete("{id}")]
    public async Task<bool> Delete(int id)
    {
        var user = await _userService.SearchOneAsync(u => u.Id == id);
        if (user == null) return false;
        return await _userService.DeleteAsync(user);
    }
}
```

如果你能顺利跑通这段代码，说明 LiteOrm 的基础接入已经完成。  
推荐做法是：业务层稳定后再逐步把泛型服务收敛到自定义 `IUserService` 中，方便承载事务、审计和组合业务逻辑。

当实体较多时，还可以使用[泛型 Controller 或动态 Controller 生成](../04-extensibility/07-generic-controller.md)来减少重复代码。

## 相关链接

- [返回目录](../README.md)
- [实体映射与数据源](../02-core-usage/01-entity-mapping.md)
- [查询指南](../02-core-usage/03-query-guide.md)
- [CRUD 指南](../02-core-usage/04-crud-guide.md)
- [关联查询](../02-core-usage/05-associations.md)

