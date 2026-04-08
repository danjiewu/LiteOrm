# View Models and Services

LiteOrm separates write-oriented entity access from read-oriented view access. This helps keep models clean and makes association queries easier to organize.

## View models

View models usually inherit from the entity and add `[ForeignColumn]` properties.

```csharp
public class UserView : User
{
    [ForeignColumn(typeof(Department), Property = "Name")]
    public string? DeptName { get; set; }
}
```

## Service patterns

### Same entity and view type

```csharp
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<User>, IEntityViewServiceAsync<User>
{ }

public class UserService : EntityService<User>, IUserService
{ }
```

### Different entity and view type

```csharp
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<UserView>, IEntityViewServiceAsync<UserView>
{ }

public class UserService : EntityService<User, UserView>, IUserService
{ }
```

## DAO vs Service

| Type | Best fit |
|------|----------|
| `ObjectDAO<T>` | Insert, update, delete, batch write operations |
| `ObjectViewDAO<T>` | `Search`, `SearchAs`, projections, association queries |
| `EntityService<T>` | Business logic, transaction boundaries |
| `EntityService<T, TView>` | Separate write model and read model |

## Important distinction

`ObjectDAO<T>` is for entity writes and does **not** provide `Search(...)`.

If you need query APIs such as `Search(...)` or `SearchAs(...)`, use `ObjectViewDAO<T>` instead.

```csharp
public class UserWriteDao : ObjectDAO<User>
{
    public Task<bool> CreateAsync(User user, CancellationToken cancellationToken = default)
        => InsertAsync(user, cancellationToken);
}

public class UserViewDao : ObjectViewDAO<UserView>
{
    public Task<List<UserView>> GetActiveUsersAsync(CancellationToken cancellationToken = default)
        => Search(Expr.Prop("Age") >= 18).ToListAsync(cancellationToken);
}
```

## Related Links

- [Back to English docs hub](../README.md)
- [First Example](../01-getting-started/04-first-example.en.md)
- [Query Guide](./03-query-guide.en.md)
