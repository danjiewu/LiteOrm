# First End-to-End Example

This page shows a minimal but practical LiteOrm flow: define an entity, register services, insert data, query it, and complete a full CRUD loop.

## 1. Define the entity

```csharp
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

## 2. Define a custom service

```csharp
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<User>, IEntityViewServiceAsync<User>
{ }

public class UserService : EntityService<User>, IUserService
{ }
```

If you do not want a custom service yet, you can also inject `IEntityServiceAsync<User>` and `IEntityViewServiceAsync<User>` directly.

## 3. Full request-to-database loop

```csharp
using var scope = app.Services.CreateScope();

var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
var entityService = scope.ServiceProvider.GetRequiredService<IEntityServiceAsync<User>>();
var viewService = scope.ServiceProvider.GetRequiredService<IEntityViewServiceAsync<User>>();

var user = new User
{
    UserName = "demo-user",
    Age = 26,
    CreateTime = DateTime.Now,
    DeptId = 2
};

await userService.InsertAsync(user);
// await entityService.InsertAsync(user);

var current = await userService.SearchOneAsync(u => u.Id == user.Id);
// var current = await viewService.SearchOneAsync(u => u.Id == user.Id);

current.UserName = "updated-demo-user";
await userService.UpdateAsync(current);
// await entityService.UpdateAsync(current);

var count = await userService.CountAsync(u => u.Age >= 18);
var exists = await userService.ExistsAsync(u => u.UserName == "demo-user");

if (exists)
    await userService.DeleteAsync(current);
```

## 4. Practical advice

- Use custom services such as `IUserService` once business logic starts to grow.
- Use the generic interfaces first if you only need quick integration.
- Keep write operations on entity services and query-oriented logic on view services.

## Related Links

- [Back to English docs hub](../README.md)
- [Query Guide](../02-core-usage/03-query-guide.en.md)
- [View Models and Services](../02-core-usage/02-view-models-and-services.en.md)
