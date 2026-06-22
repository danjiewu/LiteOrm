# First Complete Example

This article demonstrates a minimal runnable example of LiteOrm's typical usage flow: defining entities, registering services, inserting data, querying data, and paginated queries.

> **Beginner tip**: This article assumes you've completed [Installation](./02-installation.en.md) and [Configuration](./03-configuration-and-registration.en.md). If this is your first time with LiteOrm, follow along and type the code—it takes about 15 minutes. This article uses SQLite as the demo database, so no additional database server installation is needed.

## 0. Project Setup

> If you haven't created a project yet, run these commands first:

```bash
dotnet new webapi -n LiteOrmDemo
cd LiteOrmDemo
dotnet add package LiteOrm
dotnet add package Microsoft.Data.Sqlite
```

Then create a `Models` folder in the project root for your entity classes.

## 1. Define Entity

> **What is an entity?** An entity is a C# class mapped to a database table and columns through `[Table]` and `[Column]` attributes. Each entity instance corresponds to a row in the database.

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

> **Line-by-line explanation**:
> - `[Table("Users")]`: Tells LiteOrm this class maps to the `Users` table in the database.
> - `[Column("Id", IsPrimaryKey = true, IsIdentity = true)]`: The `Id` property maps to the `Id` column, which is the primary key and auto-increment.
> - `[Column("UserName")]`: The `UserName` property maps to the `UserName` column.
> - `int? DeptId`: Nullable type, corresponding to a column that allows NULL in the database.

## 2. Define Service

> **What is a service?** A service encapsulates entity operations. LiteOrm offers two approaches: custom services (recommended for production projects) and direct use of generic interfaces (great for rapid prototyping).

```csharp
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<User>, IEntityViewServiceAsync<User>
{ }

public class UserService : EntityService<User>, IUserService
{ }
```

> **Line-by-line explanation**:
> - `IEntityService<User>`: Provides synchronous write operations (Insert, Update, Delete).
> - `IEntityServiceAsync<User>`: Provides async write operations (InsertAsync, UpdateAsync, DeleteAsync).
> - `IEntityViewService<User>`: Provides synchronous read operations (Search, SearchOne, Count).
> - `IEntityViewServiceAsync<User>`: Provides async read operations (SearchAsync, SearchOneAsync, CountAsync).
> - `EntityService<User>`: Framework-provided base class that already implements all the above interface methods.

If you're not ready to define custom services in your project yet, you can also directly inject the framework's generic service interfaces. The complete flow below demonstrates both approaches.

## 3. Prepare Configuration File

> Add the LiteOrm configuration section to `appsettings.json`. The example below uses SQLite:

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

> **Note**: The SQLite connection string `Data Source=LiteOrmDemo.db` means the database file will be created in the project's runtime directory. If the file doesn't exist, SQLite creates it automatically.

## 4. Register LiteOrm

> In `Program.cs`, register LiteOrm. **Important**: Must be called on `builder.Host`, not `builder.Services`.

```csharp
using LiteOrm;

var builder = WebApplication.CreateBuilder(args);

// Register LiteOrm
builder.Host.RegisterLiteOrm();

var app = builder.Build();

// Test code goes here

app.Run();
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

> **Note**: `InsertAsync` inserts the entity into the database. If `Id` is an auto-increment column (`IsIdentity = true`), the entity's `Id` property is automatically populated with the database-generated value after insertion.

## 6. Execute Queries

```csharp
var adults = await userService.SearchAsync(u => u.Age >= 18);
var admin = await userService.SearchOneAsync(u => u.UserName == "admin");
```

> **Note**:
> - `SearchAsync` returns a list of matching records. If called without parameters, it returns all records.
> - `SearchOneAsync` returns the first matching record, or `null` if no match is found.
> - The Lambda expression `u => u.Age >= 18` is automatically converted to SQL `WHERE Age >= 18`.

## 7. Execute Pagination

```csharp
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderByDescending(u => u.CreateTime)
          .Skip(0)
          .Take(10)
);
```

> **Note**:
> - `Where`: Filter condition.
> - `OrderByDescending`: Sort descending by the specified field (`OrderBy` for ascending).
> - `Skip`: Skip the first N records.
> - `Take`: Take N records.
> - Together, these implement standard pagination: `SELECT ... WHERE Age >= 18 ORDER BY CreateTime DESC LIMIT 10 OFFSET 0`.

## 8. Complete End-to-End Flow

### 8.1 Manual verification in Program.cs

The example below demonstrates a complete flow closer to everyday project integration.
In daily projects, you can either inject your custom `IUserService` or directly inject the generic interfaces `IEntityServiceAsync<User>` and `IEntityViewServiceAsync<User>`.

> **Tip**: Place the following code in `Program.cs` before `app.Run()` to quickly verify that LiteOrm is working correctly.

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

> **Code explanation**:
> - `using var scope = app.Services.CreateScope()`: Creates a DI scope for resolving services.
> - `GetRequiredService<T>()`: Gets a service instance of the specified type from the DI container.
> - The commented "Approach 2" shows how to use LiteOrm without defining a custom Service class.

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

## 9. Common Beginner Troubleshooting

> Here are issues you might encounter when running your first example, along with solutions:

### Issue 1: `System.InvalidOperationException: Unable to resolve service for type 'IUserService'`

**Cause**: Your custom `IUserService` and `UserService` are not registered in the DI container.

**Solution**: Make sure the `UserService` class is in an assembly scanned by `RegisterLiteOrm()`. If it's in a separate project, specify it via `options.Assemblies`:

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.Assemblies = new[] { typeof(UserService).Assembly };
});
```

### Issue 2: `Microsoft.Data.Sqlite.SqliteException: SQLite Error 1: 'no such table: Users'`

**Cause**: The `Users` table doesn't exist in the database.

**Solutions**:
- Option 1: Manually create the `Users` table in the database.
- Option 2: Set `"SyncTable": true` in the configuration to let LiteOrm auto-create tables from entity definitions (recommended for development only).
- Option 3: When using SQLite, make sure the database file path is correct and the app has write permissions.

### Issue 3: `user.Id` is still 0 after insertion

**Cause**: SQLite auto-increment columns require `IsIdentity = true` to be correctly configured on the entity.

**Solution**: Check that `[Column("Id", IsPrimaryKey = true, IsIdentity = true)]` is correctly annotated. For other databases, verify that the column is indeed auto-increment in the table.

### Issue 4: `SearchAsync` returns an empty list

**Possible causes**:
1. The table is genuinely empty—first confirm that the insert operation succeeded.
2. The Lambda condition is wrong—check that field names and comparison operators are correct.
3. The database connection points to the wrong database file.

**Troubleshooting**: Add logging to your code, or use a database management tool to directly inspect the table contents.

### Issue 5: `RegisterLiteOrm` method not found

**Cause**: Missing `using LiteOrm;` reference, or the `LiteOrm.Common` package was installed instead of `LiteOrm`.

**Solution**: Confirm that the `LiteOrm` NuGet package is installed, and add `using LiteOrm;` at the top of your file.

## 10. Verification Checklist

After completing the steps above, verify your project against this checklist:

- [ ] `dotnet build` compiles successfully with no errors.
- [ ] `appsettings.json` has the correct connection string and Provider.
- [ ] `Program.cs` calls `builder.Host.RegisterLiteOrm()`.
- [ ] Entity classes use `[Table]` and `[Column]` attributes.
- [ ] No exceptions appear in the console when running the project.
- [ ] Insert and query operations return expected results.

If all items pass—congratulations! You've successfully completed the basic LiteOrm integration. Next, continue with [Entity Mapping and Data Sources](../02-core-usage/01-entity-mapping.en.md) and [Expr Guide](../02-core-usage/03-expr-guide.en.md).

## Related Links

- [Back to docs hub](../README.md)
- [Entity Mapping and Data Sources](../02-core-usage/01-entity-mapping.en.md)
- [Expr Guide](../02-core-usage/03-expr-guide.en.md)
- [Query Guide](../02-core-usage/04-query-guide.en.md)
- [CRUD Guide](../02-core-usage/05-crud-guide.en.md)
- [Associations](../02-core-usage/06-associations.en.md)
