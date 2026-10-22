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

`ForeignColumn` supports declaring its own column-level converter: `[ForeignColumn(typeof(Department), Property = "Name", ConverterType = typeof(SomeValueConverter))]`. When reading, **it prefers its own `ConverterType` and otherwise falls back to the target column's converter**. `ConverterType` works like `ColumnAttribute.ConverterType` (provide a type implementing `IDbValueConverter`).

Whether a foreign column takes part in `SELECT` depends only on whether the property is writable: only a writable property needs a read-back. Written as a read-only property (`=> ...`) it stays out of `SELECT` and the property body computes the value. This is unrelated to the target column's own definition, so a target that is an unreadable computed column does not affect the value here (and `ForeignColumnAttribute` has no mode parameter to declare otherwise).

## Service Definition

### Same Entity and View Type

```csharp
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<User>, IEntityViewServiceAsync<User>
{ }

public class UserService : EntityService<User>, IUserService
{
    public UserService(IServiceProvider serviceProvider) : base(serviceProvider) { }
}
```

### Different Entity and View Types

```csharp
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<UserView>, IEntityViewServiceAsync<UserView>
{ }

public class UserService : EntityService<User, UserView>, IUserService
{
    public UserService(IServiceProvider serviceProvider) : base(serviceProvider) { }
}
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
    public UserWriteDao(SessionManager sessionManager) : base(sessionManager) { }

    public Task<bool> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        return InsertAsync(user, cancellationToken);
    }
}
```

```csharp
using static LiteOrm.Common.Expr;
public class UserViewDao : ObjectViewDAO<UserView>
{
    public UserViewDao(SessionManager sessionManager) : base(sessionManager) { }

    public Task<List<UserView>> GetActiveUsersAsync(CancellationToken cancellationToken = default)
    {
        return Search(Prop("Age") >= 18).ToListAsync(cancellationToken);
    }
}
```

```csharp
public class UserService : EntityService<User>
{
    public UserService(IServiceProvider serviceProvider) : base(serviceProvider) { }

    [Transaction]
    public async Task CreateUserWithDefaultRole(User user)
    {
        await InsertAsync(user);
        // Continue with role, audit log, etc.
    }
}
```

```csharp
using static LiteOrm.Common.Expr;

var summary = await userService.SearchAsAsync<UserSummary>(
    From<UserView>()
        .Where(Prop("Age") >= 18)
        .Select("Id", "UserName")
);
```

Important notes here:

- `ObjectDAO<T>` handles entity write operations and itself **does not** have `Search(...)` query entry points.
- `ObjectViewDAO<T>` exposes the broadest query surface, including `Search(...)`, `SearchAs(...)`, and `ExprString` entry points.
- The Service query side also exposes `Search(...)`, plus `SearchAs(...)` / `SearchAsAsync(...)` based on `SelectExpr`, and the `IQueryable<T>` overloads `SearchAs(...)` / `SearchOneAs(...)` / `SearchAsAsync(...)` / `SearchOneAsAsync(...)` for service-layer projections. Start the chain with `Expr.From<T>()` and pass it straight in, or keep using the IQueryable Lambda form.
- If you need both entity writes and view reads, you typically combine `ObjectDAO<T>` and `ObjectViewDAO<TView>` within a Service.

## When to Use Which

- DAO is usually more direct when there's only a single data access logic.
- Prefer using Service when transactions, auditing, or cross-table business encapsulation are needed.
- When the read result structure differs significantly from the write entity structure, use a separate `TView`.
- When you only need pure query encapsulation, start from `ObjectViewDAO<T>`.

## How DAO splits reads and writes

`DAOBase` does two things: turn "metadata plus expressions" into an executable command, and turn a reader into objects. It has two concrete implementations, one for reads and one for writes.

|  | `ObjectDAO<T>` (write) | `ObjectViewDAO<T>` (read) |
|:---|:---|:---|
| Base properties | `IsView = false`, `SingleTable = true` | `IsView = true`, `Table` comes from `GetTableView` |
| Main methods | `Insert` / `Update` / `Delete` / `UpdateOrInsert` / batch | `GetObject` / `Count` / `Exists*` / `Search` / `SearchAs` |
| Statement building | `MakeInsertSql` / `MakeUpdateSql` / `MakeDeleteSql` | `MakeGetObjectSql` / `MakeObjectExistsSql` |
| Command cache | `Insert` / `BatchInsert` always cached | Statements with a constant filter, and `GetObject` with a computed-column `Expr`, are rebuilt each time |

The split exists because the two do genuinely different work. Writing assembles `INSERT`, `UPDATE`, `DELETE` and handles identity columns and timestamps. Reading assembles complex `SELECT` with JOIN and fills one object from multiple tables. Put both in a single class and every method ends up with runtime checks for "operation not supported here".

Results are wrapped in lazy objects (`ResultTypes.cs`): the base is `CommandResult<T>`, `EnumerableResult<T>` is consumed with `ToList()` and `FirstOrDefault()`, and `ValueResult<T>` with `GetResult()`. When a result object is disposed it looks at where the command came from: cached commands are left alone, ones created for this call are released.

## What Service adds on top of DAO

`EntityService<T, TView>` (`EntityService.cs:58`) adds business semantics above DAO:

- It resolves one `ObjectDAO<T>` during construction.
- It raises `IEntityServiceEvent<T>` before and after every write; if any Before hook returns `false`, the operation is cancelled.
- The subscriber collection is lazy (`:76`), so constructing the service does not touch the container. Resolution happens at the first notification.
- If the entity implements `IArged`, operations are grouped by `TableArgs` automatically and routed to the matching shard.

## Related Links

- [Back to docs hub](../README.md)
- [Entity Mapping and Data Sources](./entity-mapping.en.md)
- [CRUD Guide](./crud-guide.en.md)
- [Transactions](../di/transactions.en.md)
