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

`ForeignColumn` 支持声明自己的列级转换器：`[ForeignColumn(typeof(Department), Property = "Name", ConverterType = typeof(SomeValueConverter))]`。读取时**优先使用自身声明的 `ConverterType`，否则回退目标列的转换器**；`ConverterType` 用法与 `ColumnAttribute.ConverterType` 一致（提供实现 `IDbValueConverter` 的转换器类型）。

外键列是否进 `SELECT` 只看属性可不可写：可写才有回填需求，读回来才有人接。写成只读属性（`=> ...`）时该列不进 `SELECT`，值由属性体自行计算。这条判定与目标列自身的定义无关，目标列是不可读的计算列也不影响本列取值（`ForeignColumnAttribute` 没有列模式参数，本来也没法在关联列上单独声明）。

## Service 定义

### 实体类型与视图类型相同

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

### 实体类型与视图类型不同

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
        // 这里可以继续处理角色、审计日志等业务操作
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

这里要特别注意：

- `ObjectDAO<T>` 负责实体写操作，本身**没有** `Search(...)` 查询入口。
- `ObjectViewDAO<T>` 提供最完整的查询能力，包括 `Search(...)`、`SearchAs(...)` 以及 `ExprString` 入口。
- Service 查询侧同样提供 `Search(...)`，并支持基于 `SelectExpr` 的 `SearchAs(...)` / `SearchAsAsync(...)`，以及接收 `IQueryable<T>` 的投影重载 `SearchAs(...)` / `SearchOneAs(...)` / `SearchAsAsync(...)` / `SearchOneAsAsync(...)`，适合在服务层做结果投影。查询用 `Expr.From<T>()` 起链，链尾直接传入。
- 如果你既要写入实体，又要读取视图，通常会在 Service 里同时组合 `ObjectDAO<T>` 和 `ObjectViewDAO<TView>`。

## 什么时候该用哪一种

- 只有单一数据访问逻辑时，DAO 通常更直接。
- 需要事务、审计、跨表业务封装时，优先使用 Service。
- 读取结果与写入实体结构差异明显时，使用独立 `TView`。
- 只想做纯查询封装时，优先从 `ObjectViewDAO<T>` 开始。

## DAO 的读写分工

`DAOBase` 干两件事：把"元数据 + 表达式"变成能执行的命令，再把读取器变成对象。它有两个具体实现，读一个写一个。

|  | `ObjectDAO<T>`（写） | `ObjectViewDAO<T>`（读） |
|:---|:---|:---|
| 基类属性 | `IsView = false`，`SingleTable = true` | `IsView = true`，`Table` 取 `GetTableView` |
| 主要方法 | `Insert` / `Update` / `Delete` / `UpdateOrInsert` / 批量 | `GetObject` / `Count` / `Exists*` / `Search` / `SearchAs` |
| 语句构造 | `MakeInsertSql` / `MakeUpdateSql` / `MakeDeleteSql` | `MakeGetObjectSql` / `MakeObjectExistsSql` |
| 命令缓存 | `Insert` / `BatchInsert` 恒缓存 | 带固定切片条件的语句、含计算列 `Expr` 的 `GetObject` 每次新建 |

拆成两个类，是因为它们能做的事根本不一样。写要拼 `INSERT`、`UPDATE`、`DELETE`，还要处理自增列和时间戳；读要拼复杂的 `SELECT` 和 JOIN，还要把多表结果填进一个对象。硬塞进同一个类，方法里会到处是"当前操作不支持"的运行时判断。

结果统一包成惰性对象（`ResultTypes.cs`）：基类 `CommandResult<T>`，`EnumerableResult<T>` 用 `ToList()` 和 `FirstOrDefault()` 取，`ValueResult<T>` 用 `GetResult()` 取。结果对象释放时看命令的来路，缓存里的不动，本次新建的才释放。

## Service 在 DAO 之上加了什么

`EntityService<T, TView>`（`EntityService.cs:58`）在 DAO 之上加业务语义：

- 构造时解析出一个 `ObjectDAO<T>`；
- 每次写操作前后触发 `IEntityServiceEvent<T>`，任何一个 Before 钩子返回 `false`，操作就取消；
- 订阅者集合是惰性的（`:76`），构造服务时不碰容器，第一次要通知了才解析；
- 实体实现了 `IArged` 的话，自动按 `TableArgs` 分组，走分表。

## 相关链接

- [返回目录](../README.md)
- [实体映射与数据源](./entity-mapping.md)
- [CRUD 指南](./crud-guide.md)
- [事务管理](../di/transactions.md)

