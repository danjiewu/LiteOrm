# 第一个完整示例

本文通过一个最小可运行示例展示 LiteOrm 的典型使用流程：定义实体、注册服务、插入数据、查询数据和分页查询。

> **新手提示**：本文假设你已经完成了 [安装](./02-installation.md) 和 [配置](./03-configuration-and-registration.md)。如果你是第一次接触 LiteOrm，建议从头到尾跟着敲一遍代码，大约需要 15 分钟。本文使用 SQLite 作为演示数据库，无需额外安装数据库服务。

## 0. 项目准备

> 如果你还没有创建项目，请先执行以下命令：

```bash
dotnet new webapi -n LiteOrmDemo
cd LiteOrmDemo
dotnet add package LiteOrm
dotnet add package Microsoft.Data.Sqlite
```

然后在项目根目录创建 `Models` 文件夹，用于存放实体类。

## 1. 定义实体

> **什么是实体？** 实体就是一个 C# 类，通过 `[Table]` 和 `[Column]` 特性与数据库中的表和列建立映射关系。每个实体实例对应数据库中的一行数据。

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

> **逐行解释**：
> - `[Table("Users")]`：告诉 LiteOrm 这个类映射到数据库中的 `Users` 表。
> - `[Column("Id", IsPrimaryKey = true, IsIdentity = true)]`：`Id` 属性映射到 `Id` 列，是主键且自增。
> - `[Column("UserName")]`：`UserName` 属性映射到 `UserName` 列。
> - `int? DeptId`：可空类型，对应数据库中允许 NULL 的列。

## 2. 定义服务

> **什么是服务？** 服务是对实体操作的封装。LiteOrm 提供了两种使用方式：自定义服务（推荐用于正式项目）和直接使用泛型接口（适合快速原型）。

```csharp
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<User>, IEntityViewServiceAsync<User>
{ }

public class UserService : EntityService<User>, IUserService
{ }
```

> **逐行解释**：
> - `IEntityService<User>`：提供同步的增删改操作（Insert、Update、Delete）。
> - `IEntityServiceAsync<User>`：提供异步的增删改操作（InsertAsync、UpdateAsync、DeleteAsync）。
> - `IEntityViewService<User>`：提供同步的查询操作（Search、SearchOne、Count）。
> - `IEntityViewServiceAsync<User>`：提供异步的查询操作（SearchAsync、SearchOneAsync、CountAsync）。
> - `EntityService<User>`：框架提供的基类，已经实现了上述所有接口的方法。

如果你的项目暂时不准备定义自定义服务，也可以直接注入框架提供的泛型服务接口，后面的完整闭环里会同时演示两种写法。

## 3. 准备配置文件

> 在 `appsettings.json` 中添加 LiteOrm 配置节。以下使用 SQLite 作为示例：

```json
{
  "LiteOrm": {
    "Default": "DefaultConnection",
    "DataSources": [
      {
        "Name": "DefaultConnection",
        "ConnectionString": "Data Source=LiteOrmDemo.db",
        "Provider": "Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite"
      }
    ]
  }
}
```

> **说明**：SQLite 的连接字符串 `Data Source=LiteOrmDemo.db` 表示数据库文件将创建在项目运行目录下。如果文件不存在，SQLite 会自动创建。

## 4. 注册 LiteOrm

> 在 `Program.cs` 中注册 LiteOrm。**注意**：必须在 `builder.Host` 上调用，不是 `builder.Services`。

```csharp
using LiteOrm;

var builder = WebApplication.CreateBuilder(args);

// 注册 LiteOrm
builder.Host.RegisterLiteOrm();

var app = builder.Build();

// 测试代码将放在这里

app.Run();
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

> **说明**：`InsertAsync` 会将实体插入数据库。如果 `Id` 是自增列（`IsIdentity = true`），插入后实体的 `Id` 属性会自动填充为数据库生成的值。

## 6. 执行查询

```csharp
var adults = await userService.SearchAsync(u => u.Age >= 18);
var admin = await userService.SearchOneAsync(u => u.UserName == "admin");
```

> **说明**：
> - `SearchAsync` 返回满足条件的列表。如果不传参数，返回所有记录。
> - `SearchOneAsync` 返回满足条件的第一条记录，如果没有匹配则返回 `null`。
> - Lambda 表达式 `u => u.Age >= 18` 会被自动转换为 SQL 的 `WHERE Age >= 18`。

## 7. 执行分页

```csharp
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderByDescending(u => u.CreateTime)
          .Skip(0)
          .Take(10)
);
```

> **说明**：
> - `Where`：过滤条件。
> - `OrderByDescending`：按指定字段降序排列（`OrderBy` 为升序）。
> - `Skip`：跳过前 N 条记录。
> - `Take`：取 N 条记录。
> - 以上组合实现了标准的分页查询：`SELECT ... WHERE Age >= 18 ORDER BY CreateTime DESC LIMIT 10 OFFSET 0`。

## 8. 完整调用闭环

### 8.1 在 Program.cs 中手动验证

下面的示例展示了一个更接近日常项目接入方式的完整流程。  
日常项目里，你既可以注入自定义的 `IUserService`，也可以直接注入泛型接口 `IEntityServiceAsync<User>` 与 `IEntityViewServiceAsync<User>`。

> **建议**：将以下代码放在 `Program.cs` 中 `app.Run()` 之前，用于快速验证 LiteOrm 是否正常工作。

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

> **代码解读**：
> - `using var scope = app.Services.CreateScope()`：创建一个 DI 作用域，用于解析服务。
> - `GetRequiredService<T>()`：从 DI 容器中获取指定类型的服务实例。
> - 注释中的"写法二"展示了不定义自定义 Service 也能使用 LiteOrm 的方式。

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

## 9. 新手常见问题排查

> 以下是在运行第一个示例时可能遇到的问题及解决方法：

### 问题一：`System.InvalidOperationException: Unable to resolve service for type 'IUserService'`

**原因**：自定义的 `IUserService` 和 `UserService` 没有被注册到 DI 容器中。

**解决方法**：确保 `UserService` 类在 `RegisterLiteOrm()` 扫描的程序集中。如果放在单独的项目中，需要通过 `options.Assemblies` 指定：

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.Assemblies = new[] { typeof(UserService).Assembly };
});
```

### 问题二：`Microsoft.Data.Sqlite.SqliteException: SQLite Error 1: 'no such table: Users'`

**原因**：数据库中没有 `Users` 表。

**解决方法**：
- 方案一：手动在数据库中创建 `Users` 表。
- 方案二：在配置中设置 `"SyncTable": true`，让 LiteOrm 自动根据实体定义创建表（仅开发环境推荐）。
- 方案三：使用 SQLite 时，确保数据库文件路径正确，且应用有写入权限。

### 问题三：插入后 `user.Id` 仍然是 0

**原因**：SQLite 的自增列需要在实体上正确配置 `IsIdentity = true`。

**解决方法**：检查 `[Column("Id", IsPrimaryKey = true, IsIdentity = true)]` 是否正确标注。如果使用其他数据库，确认表中该列确实是自增列。

### 问题四：`SearchAsync` 返回空列表

**可能原因**：
1. 表中确实没有数据——先确认插入操作是否成功。
2. Lambda 条件写错——检查字段名和比较运算符是否正确。
3. 数据库连接指向了错误的数据库文件。

**排查方法**：在代码中添加日志，或使用数据库管理工具直接查看表内容。

### 问题五：`RegisterLiteOrm` 方法找不到

**原因**：没有添加 `using LiteOrm;` 引用，或者安装的是 `LiteOrm.Common` 包而非 `LiteOrm` 包。

**解决方法**：确认安装了 `LiteOrm` NuGet 包，并在文件顶部添加 `using LiteOrm;`。

## 10. 运行验证清单

完成以上步骤后，按以下清单验证你的项目：

- [ ] `dotnet build` 编译通过，无错误。
- [ ] `appsettings.json` 中配置了正确的连接字符串和 Provider。
- [ ] `Program.cs` 中调用了 `builder.Host.RegisterLiteOrm()`。
- [ ] 实体类使用了 `[Table]` 和 `[Column]` 特性标注。
- [ ] 运行项目后，控制台没有异常输出。
- [ ] 插入和查询操作返回了预期的结果。

全部通过后，恭喜你！你已经成功完成了 LiteOrm 的基础接入。接下来可以继续学习 [实体映射与数据源](../02-core-usage/01-entity-mapping.md) 和 [查询总览](../02-core-usage/04-query-overview.md)。

## 相关链接

- [返回目录](../README.md)
- [实体映射与数据源](../02-core-usage/01-entity-mapping.md)
- [查询总览](../02-core-usage/04-query-overview.md)
- [Lambda 查询指南](../02-core-usage/05-lambda-guide.md)
- [Expr 使用指南](../02-core-usage/06-expr-guide.md)
- [CRUD 指南](../02-core-usage/03-crud-guide.md)
- [关联查询](../02-core-usage/08-associations.md)

