# 视图模型与服务定义

LiteOrm 将“实体写入”和“视图查询”解耦。你可以直接使用 DAO，也可以通过 Service 封装业务层访问逻辑。

## 视图模型

视图模型通常继承实体，并通过 `[ForeignColumn]` 增加关联字段：

```csharp
public class UserView : User
{
    [ForeignColumn(typeof(Department), Property = "Name")]
    public string? DeptName { get; set; }
}
```

这样查询 `UserView` 时，LiteOrm 能根据外键关系自动生成 JOIN。

## Service 定义

### 实体类型与视图类型相同

```csharp
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<User>, IEntityViewServiceAsync<User>
{ }

public class UserService : EntityService<User>, IUserService
{ }
```

### 实体类型与视图类型不同

```csharp
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<UserView>, IEntityViewServiceAsync<UserView>
{ }

public class UserService : EntityService<User, UserView>, IUserService
{ }
```

## DAO 与 Service 的职责区分

| 类型 | 更适合的场景 |
| --- | --- |
| `ObjectDAO<T>` | 插入、更新、删除、批量写入等实体写操作。 |
| `ObjectViewDAO<T>` | `Search` / `SearchAs`、投影、关联视图读取。 |
| `EntityService<T>` | 业务层封装、事务边界、组合多个 DAO。 |
| `EntityService<T, TView>` | 写入实体与读取视图分离的业务模型。 |

## `ObjectDAO` 与 `EntityService` 的使用差异

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
        // 这里可以继续处理角色、审计日志等业务操作
    }
}
```

这里要特别注意：

- `ObjectDAO<T>` 负责实体写操作，本身**没有** `Search(...)` 查询入口。
- `Search(...)`、`SearchAs(...)` 这类查询能力在 `ObjectViewDAO<T>` 上。
- 如果你既要写入实体，又要读取视图，通常会在 Service 里同时组合 `ObjectDAO<T>` 和 `ObjectViewDAO<TView>`。

## 什么时候该用哪一种

- 只有单一数据访问逻辑时，DAO 通常更直接。
- 需要事务、审计、跨表业务封装时，优先使用 Service。
- 读取结果与写入实体结构差异明显时，使用独立 `TView`。
- 只想做纯查询封装时，优先从 `ObjectViewDAO<T>` 开始。

## 相关链接

- [返回目录](../SUMMARY.md)
- [实体映射与数据源](./01-entity-mapping.md)
- [CRUD 指南](./04-crud-guide.md)
- [事务管理](../03-advanced-topics/01-transactions.md)

