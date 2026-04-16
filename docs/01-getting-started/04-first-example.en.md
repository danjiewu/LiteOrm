# First Complete Example

This article demonstrates a minimal runnable example of LiteOrm's typical usage flow: defining entities, registering services, inserting data, querying data, and paginated queries.

## 1. Define Entity

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

## 2. Define Service

```csharp
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<User>, IEntityViewServiceAsync<User>
{ }

public class UserService : EntityService<User>, IUserService
{ }
```

If you're not ready to define custom services in your project yet, you can also directly inject the framework's generic service interfaces. The complete flow below demonstrates both approaches.

## 3. Prepare Configuration File

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

## 4. Register LiteOrm

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();
```

## 5. Insert a Record

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

## 6. Execute Queries

```csharp
var adults = await userService.SearchAsync(u => u.Age >= 18);
var admin = await userService.SearchOneAsync(u => u.UserName == "admin");
```

## 7. Execute Pagination

```csharp
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderByDescending(u => u.CreateTime)
          .Skip(0)
          .Take(10)
);
```

## 8. Complete End-to-End Flow

### 8.1 Manual verification in Program.cs

The example below demonstrates a complete flow closer to everyday project integration.
In daily projects, you can either inject your custom `IUserService` or directly inject the generic interfaces `IEntityServiceAsync<User>` and `IEntityViewServiceAsync<User>`.

```csharp
using var scope = app.Services.CreateScope();

// Approach 1: Custom service defined in the project
var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

// Approach 2: Use framework-provided generic services directly
var entityService = scope.ServiceProvider.GetRequiredService<IEntityServiceAsync<User>>();
var viewService = scope.ServiceProvider.GetRequiredService<IEntityViewServiceAsync<User>>();

var user = new User
{
    UserName = "demo-user",
    Age = 26,
    CreateTime = DateTime.Now,
    DeptId = 2
};

// 1. Insert
// Choose either approach 1 or 2
await userService.InsertAsync(user);
// await entityService.InsertAsync(user);

// 2. Query
var current = await userService.SearchOneAsync(u => u.Id == user.Id);
// var current = await viewService.SearchOneAsync(u => u.Id == user.Id);

// 3. Update
current.UserName = "updated-demo-user";
await userService.UpdateAsync(current);
// await entityService.UpdateAsync(current);

// 4. Count
var count = await userService.CountAsync(u => u.Age >= 18);
// var count = await viewService.CountAsync(u => u.Age >= 18);

// 5. Check existence
var exists = await userService.ExistsAsync(u => u.UserName == "demo-user");
// var exists = await viewService.ExistsAsync(u => u.UserName == "demo-user");

// 6. Delete
if (exists)
{
    await userService.DeleteAsync(current);
    // await entityService.DeleteAsync(current);
}
```

### 8.2 Using LiteOrm in a Controller

In ASP.NET Core projects, the more common approach is to inject services via constructor injection and use them in controllers:

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

If you can successfully run this code, your basic LiteOrm integration is complete.
The recommended approach is to gradually migrate generic services to custom `IUserService` after the business layer stabilizes, to accommodate transactions, auditing, and composite business logic.

When you have many entities, you can also use [Generic Controller or Dynamic Controller Generation](../04-extensibility/07-generic-controller.en.md) to reduce repetitive code.

## Related Links

- [Back to docs hub](../README.md)
- [Entity Mapping and Data Sources](../02-core-usage/01-entity-mapping.en.md)
- [Query Guide](../02-core-usage/03-query-guide.en.md)
- [CRUD Guide](../02-core-usage/04-crud-guide.en.md)
- [Associations](../02-core-usage/05-associations.en.md)
