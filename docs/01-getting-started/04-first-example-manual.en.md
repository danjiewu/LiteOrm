# First Full Example (Manual Construction, No DI)

This article walks through a minimal runnable example of LiteOrm **without any dependency injection container**: use `LiteOrmClient` to register data sources fluently and create a session, then build `ObjectDAO<User>` / `ObjectViewDAO<User>` straight from that session to run CRUD operations.

> **When to use**: console tools, batch jobs, unit tests, plugins — any host where pulling in a DI container is inconvenient.
>
> `LiteOrmClient` is a line **independent** of `AddLiteOrm()` and `RegisterLiteOrm()`: it registers no services, never reads `IConfiguration`, and never touches `SessionManager.Current`. If you need AOP transactions / permissions / logging, switch to [First Full Example (DI)](./05-first-example-di.en.md).

## 0. Project Setup

```bash
dotnet new console -n LiteOrmManualDemo
cd LiteOrmManualDemo
dotnet add package LiteOrm
dotnet add package Microsoft.Data.Sqlite
```

> `LiteOrm` automatically brings in `LiteOrm.Common`, so there is no need to install it separately. The example uses SQLite, so no database service is required.

## 1. Define the Entity

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
}
```

> - `[Table("Users")]`: maps to the `Users` table.
> - `[Column("Id", IsPrimaryKey = true, IsIdentity = true)]`: primary key and identity.
> - The entity does not need to inherit `ObjectBase`; a plain POCO is fine.

## 2. Create the Client

Every data source is fully configured **in a single `AddDataSource` call**: connection string, pool sizes, parameter limit, and table sync are all settled there. The client deliberately offers no follow-up configuration methods.

```csharp
using LiteOrm;
using Microsoft.Data.Sqlite;

var liteOrm = new LiteOrmClient()
    .AddDataSource<SqliteConnection>(
        name: "main",
        connectionString: "Data Source=LiteOrmManualDemo.db",
        @default: true,        // mark as the default source (the first one added also becomes the default)
        syncTable: true,       // create tables from entity definitions on startup
        poolSize: 8,           // cached connections
        maxPoolSize: 32,       // maximum concurrent connections
        paramCountLimit: 500); // max parameters per SQL statement; larger batches are split
```

The named parameters of `AddDataSource<TConnection>` map one-to-one onto `DataSourceConfig`; anything omitted falls back to the default:

| Parameter | Config property | Default | Notes |
| --- | --- | --- | --- |
| `name` | `Name` | `"DefaultConnection"` | Data source name |
| `connectionString` | `ConnectionString` | `null` | Connection string |
| `@default` | — | `false` | Whether to make it the default source |
| `sqlBuilder` | `SqlBuilder` | `null` | Custom SqlBuilder type name; rarely needed |
| `syncTable` | `SyncTable` | `false` | Whether to auto-create tables |
| `provider` | `Provider` | `TConnection`'s assembly-qualified name | Connection type string |
| `poolSize` | `PoolSize` | `16` | Cached connections |
| `maxPoolSize` | `MaxPoolSize` | `100` | Maximum concurrent connections |
| `paramCountLimit` | `ParamCountLimit` | `1000` | Max parameters per SQL statement |
| `keepAliveDuration` | `KeepAliveDuration` | 10 minutes | Idle-connection keep-alive duration |

Pools are only built on the first `CreateSession()`, so **all `AddDataSource` calls must happen before the first session is taken**. Adding a source after pools exist throws `InvalidOperationException`.

### Multiple data sources

```csharp
var liteOrm = new LiteOrmClient()
    .AddDataSource<SqliteConnection>("main", "Data Source=main.db", @default: true, syncTable: true, maxPoolSize: 32)
    .AddDataSource<MySqlConnection>("log", "Server=localhost;Database=log;", poolSize: 4);
```

> Each source is configured in full inside its own `AddDataSource` call; adding the same name again overwrites it, so a connection string left blank earlier can be filled in before the first session is taken.
>
> **Routing is decided by the entity, not the session.** Which database a DAO uses depends on `[Table(DataSource = "...")]` on the entity; entities without it always use the default data source. Calling `CreateSession()` again does not change that.

## 3. Create a Session and DAOs

```csharp
using var session = liteOrm.CreateSession();

var userDao = new ObjectDAO<User>(session);        // writes
var userViewDao = new ObjectViewDAO<User>(session); // reads
```

Each `CreateSession()` returns a new `SessionManager` that the caller owns. The session is passed to DAOs explicitly and never interferes with the DI line.

## 4. Full Call Loop

```csharp
using LiteOrm;
using LiteOrm.Common;
using Microsoft.Data.Sqlite;

using var liteOrm = new LiteOrmClient()
    .AddDataSource<SqliteConnection>("main", "Data Source=LiteOrmManualDemo.db", @default: true, syncTable: true, poolSize: 8, maxPoolSize: 32);

using var session = liteOrm.CreateSession();
var userDao = new ObjectDAO<User>(session);
var userViewDao = new ObjectViewDAO<User>(session);

// 1. Insert
var user = new User { UserName = "demo-user", Age = 26, CreateTime = DateTime.Now };
await userDao.InsertAsync(user);
Console.WriteLine($"Inserted, identity Id = {user.Id}");

// 2. Conditional query
var adults = await userViewDao.Search(Expr.Prop(nameof(User.Age)) >= 18).ToListAsync();
Console.WriteLine($"Adult users: {adults.Count}");

// 3. Single-record query
var current = await userViewDao.GetObject(user.Id).FirstOrDefaultAsync();
Console.WriteLine($"Found: {current?.UserName}, Age = {current?.Age}");

// 4. Count and existence check
var count = await userViewDao.Count(Expr.Prop(nameof(User.Age)) >= 18).GetResultAsync();
var exists = await userViewDao.ExistsKey(user.Id).GetResultAsync();

// 5. Update
current!.UserName = "updated-demo-user";
await userDao.UpdateAsync(current);

// 6. Delete
await userDao.DeleteAsync(Expr.Prop(nameof(User.Id)) == user.Id, CancellationToken.None);

Console.WriteLine($"Count={count}, Exists={exists}");

// Disposal: dispose the session before the client
liteOrm.Dispose();
```

> - **Writes live on `ObjectDAO<T>`**: `Insert` / `Update` / `DeleteAsync` / `DeleteByKeysAsync` / `UpdateOrInsert` and their batch versions.
> - **Reads live on `ObjectViewDAO<T>`**: `GetObject(keys)` / `Search(Expr?)` / `Count(expr)` / `Exists(...)` / `SearchAs<TResult>(...)`. `ObjectDAO<T>` has no `GetObject`.
> - **Result types**: `EnumerableResult<T>` offers `FirstOrDefault()` / `FirstOrDefaultAsync(ct)` / `ToList()` / `ToListAsync(ct)`; `ValueResult<T>` offers `GetResult()` / `GetResultAsync(ct)`.

## 5. Wrap Multiple Writes in a Transaction

```csharp
using var session = liteOrm.CreateSession();
var userDao = new ObjectDAO<User>(session);

session.BeginTransaction();
try
{
    await userDao.InsertAsync(new User { UserName = "a", Age = 20, CreateTime = DateTime.Now });
    await userDao.InsertAsync(new User { UserName = "b", Age = 30, CreateTime = DateTime.Now });

    session.Commit();
}
catch
{
    session.Rollback();
    throw;
}
```

> `BeginTransaction()` / `Commit()` / `Rollback()` all operate on the `SessionManager`, and the transaction context is shared by every DAO in that session. An `IsolationLevel` can be passed as well.

## 6. Boundary Between the Manual and DI Lines

| Capability | `LiteOrmClient` (this article) | `AddLiteOrm()` / `RegisterLiteOrm()` |
| --- | --- | --- |
| Entity mapping / CRUD / queries | ✅ | ✅ |
| Manual transactions | ✅ `session.BeginTransaction()` | ✅ |
| Declarative `[Transaction]` | ❌ | ✅ AOP interception |
| `[ServicePermission]` filtering | ❌ | ✅ AOP interception |
| `[ServiceLog]` logging | ❌ | ✅ AOP interception |
| Requires a DI container | ❌ no | ✅ yes |
| Reads `IConfiguration` | ❌ data sources are registered explicitly in code | ✅ `appsettings.json` auto-binding |
| Touches `SessionManager.Current` | ❌ | ✅ bound automatically |

> When you need AOP capabilities, swap `new LiteOrmClient()` for `AddLiteOrm()` in the host; the entity definitions and DAO usage stay identical.

## 7. Common Beginner Issues

### Issue 1: `SQLite Error 1: 'no such table: Users'`

**Cause**: The `Users` table does not exist.

**Solution**: Pass `syncTable: true` to `AddDataSource` (recommended during development), or run the table-creation SQL manually.

### Issue 2: `InvalidOperationException: The connection pool factory has already been created`

**Cause**: `AddDataSource` was called after `CreateSession()` had already built the pools.

**Solution**: Put every `AddDataSource` call at the front of the chain, before the first session is taken. Pool options must be supplied in full at `AddDataSource` time too.

### Issue 3: `ArgumentNullException`, or `SessionManager.Current` is null

**Cause**: The `LiteOrmClient` line never sets `SessionManager.Current`, so older code that relies on the static entry sees null.

**Solution**: Pass the value returned by `CreateSession()` to the DAO constructor explicitly: `new ObjectDAO<User>(session)`. Do not rely on `SessionManager.Current`.

## Run Verification Checklist

- [ ] `dotnet build` compiles without errors.
- [ ] Every `AddDataSource` call happens before the first `CreateSession()`.
- [ ] Entity classes are annotated with `[Table]` and `[Column]`.
- [ ] Reads go through `ObjectViewDAO<T>` (`ObjectDAO<T>` has no `GetObject`).
- [ ] Insert, query, update, and delete return the expected results.
- [ ] The session and client are disposed before exit (`using` or an explicit `Dispose`).

## Related Links

- [Back to docs hub](../README.md)
- [Installation](./02-installation.en.md)
- [First Full Example (Base Library Only)](./03-first-example.en.md)
- [First Full Example (DI)](./05-first-example-di.en.md)
- [Configuration Reference](../05-reference/01-configuration-reference.en.md)
