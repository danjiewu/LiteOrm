# View Models and Services

LiteOrm decouples "entity writes" from "view queries". You can use DAO directly, or encapsulate business layer access logic through Service.

## View Models

View models typically inherit from entities and add association fields via `[ForeignColumn]`:

```csharp
public class UserView : User
{
    [ForeignColumn(typeof(Department), Property = "Name")]
    public string? DeptName { get; set; }
}
```

This way, when querying `UserView`, LiteOrm can automatically generate JOIN based on foreign key relationships.

## Service Definition

### Same Entity and View Type

```csharp
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<User>, IEntityViewServiceAsync<User>
{ }

public class UserService : EntityService<User>, IUserService
{ }
```

### Different Entity and View Types

```csharp
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<UserView>, IEntityViewServiceAsync<UserView>
{ }

public class UserService : EntityService<User, UserView>, IUserService
{ }
```

## DAO vs Service Responsibility

| Type | More Suitable Scenarios |
|------|------------------------|
| `ObjectDAO<T>` | Entity write operations like insert, update, delete, batch writes. |
| `ObjectViewDAO<T>` | `Search` / `SearchAs`, projections, association view reads. |
| `EntityService<T>` | Business layer encapsulation, transaction boundaries, combining multiple DAOs. |
| `EntityService<T, TView>` | Business models where entity write and view read are separated. |

## Usage Differences Between `ObjectDAO` and `EntityService`

```csharp
public class UserWriteDao : ObjectDAO<User>
{
    public Task<bool> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        return InsertAsync(user, cancellationToken);
    }
}
```

```csharp
public class UserViewDao : ObjectViewDAO<UserView>
{
    public Task<List<UserView>> GetActiveUsersAsync(CancellationToken cancellationToken = default)
    {
        return Search(Expr.Prop("Age") >= 18).ToListAsync(cancellationToken);
    }
}
```

```csharp
public class UserService : EntityService<User>
{
    [Transaction]
    public async Task CreateUserWithDefaultRole(User user)
    {
        await InsertAsync(user);
        // Continue with role, audit log, etc.
    }
}
```

Important notes here:

- `ObjectDAO<T>` handles entity write operations and itself **does not** have `Search(...)` query entry points.
- Query capabilities like `Search(...)` and `SearchAs(...)` are on `ObjectViewDAO<T>`.
- If you need both entity writes and view reads, you typically combine `ObjectDAO<T>` and `ObjectViewDAO<TView>` within a Service.

## When to Use Which

- DAO is usually more direct when there's only a single data access logic.
- Prefer using Service when transactions, auditing, or cross-table business encapsulation are needed.
- When the read result structure differs significantly from the write entity structure, use a separate `TView`.
- When you only need pure query encapsulation, start from `ObjectViewDAO<T>`.

## Related Links

- [Back to docs hub](../README.md)
- [Entity Mapping and Data Sources](./01-entity-mapping.en.md)
- [CRUD Guide](./04-crud-guide.en.md)
- [Transactions](../03-advanced-topics/01-transactions.en.md)
