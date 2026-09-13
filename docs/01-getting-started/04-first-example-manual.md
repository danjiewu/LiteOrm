# 第一个完整示例（手动构造，无 DI）

本文用一个最小可运行示例演示**完全不使用依赖注入容器**的 LiteOrm 用法：用 `LiteOrmClient` 链式登记数据源、创建会话，再由会话直接构造 `ObjectDAO<User>` / `ObjectViewDAO<User>` 完成增删改查。

> **适用场景**：控制台工具、批处理脚本、单元测试、插件，以及任何不方便引入 DI 容器的宿主。
>
> `LiteOrmClient` 与 `AddLiteOrm()`、`RegisterLiteOrm()` 是**三条相互独立的线路**：它不注册任何服务、不读取 `IConfiguration`、也不接管 `SessionManager.Current`。需要 AOP 事务/权限/日志时请改用 [第一个完整示例（DI 版）](./05-first-example-di.md)。

## 0. 项目准备

```bash
dotnet new console -n LiteOrmManualDemo
cd LiteOrmManualDemo
dotnet add package LiteOrm
dotnet add package Microsoft.Data.Sqlite
```

> 基础库 `LiteOrm` 会自动携带 `LiteOrm.Common`，无需单独安装。示例使用 SQLite，不必额外安装数据库服务。

## 1. 定义实体

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

> - `[Table("Users")]`：映射到数据库的 `Users` 表。
> - `[Column("Id", IsPrimaryKey = true, IsIdentity = true)]`：主键且自增。
> - 实体类不要求继承 `ObjectBase`，普通 POCO 即可。

## 2. 创建客户端

`LiteOrmClient` 的每一个数据源都在 `AddDataSource` 调用时**一次性配齐**：连接字符串、连接池大小、参数上限、是否自动建表，都在这一步定好，客户端不再提供后续的补充设置方法。

```csharp
using LiteOrm;
using Microsoft.Data.Sqlite;

var liteOrm = new LiteOrmClient()
    .AddDataSource<SqliteConnection>(
        name: "main",
        connectionString: "Data Source=LiteOrmManualDemo.db",
        @default: true,        // 设为默认数据源（第一个添加的也会自动成为默认）
        syncTable: true,       // 启动时按实体定义自动建表
        poolSize: 8,           // 连接池缓存数量
        maxPoolSize: 32,       // 最大并发连接数
        paramCountLimit: 500); // 单条 SQL 参数上限，超出会拆分批量语句
```

`AddDataSource<TConnection>` 的命名参数与 `DataSourceConfig` 一一对应，未填写的走默认值：

| 参数 | 配置项 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `name` | `Name` | `"DefaultConnection"` | 数据源名称 |
| `connectionString` | `ConnectionString` | `null` | 连接字符串 |
| `@default` | — | `false` | 是否设为默认数据源 |
| `sqlBuilder` | `SqlBuilder` | `null` | 自定义 SqlBuilder 类型名，一般不必填 |
| `syncTable` | `SyncTable` | `false` | 是否自动建表 |
| `provider` | `Provider` | `TConnection` 的限定程序集名 | 连接类型字符串 |
| `poolSize` | `PoolSize` | `16` | 连接池缓存数量 |
| `maxPoolSize` | `MaxPoolSize` | `100` | 最大并发连接数 |
| `paramCountLimit` | `ParamCountLimit` | `1000` | 单条 SQL 参数上限 |
| `keepAliveDuration` | `KeepAliveDuration` | 10 分钟 | 空闲连接保活时长 |

连接池在首次 `CreateSession()` 时才真正建好，因此**所有 `AddDataSource` 都必须在首次取会话之前调用**；建池之后再添加数据源会抛出 `InvalidOperationException`。

### 多数据源

```csharp
var liteOrm = new LiteOrmClient()
    .AddDataSource<SqliteConnection>("main", "Data Source=main.db", @default: true, syncTable: true, maxPoolSize: 32)
    .AddDataSource<MySqlConnection>("log", "Server=localhost;Database=log;", poolSize: 4);
```

> 每个数据源在自己的 `AddDataSource` 里配齐连接串与池参数；同名再次调用即覆盖，可以在真正取会话之前补齐先前留空的连接串。
>
> **数据源由实体决定，而不是由会话决定**。DAO 走哪个库取决于实体上的 `[Table(DataSource = "...")]`，未标注的实体一律落在默认数据源。多个 `CreateSession()` 本身不改变数据源归属。

## 3. 创建会话与 DAO

```csharp
using var session = liteOrm.CreateSession();

var userDao = new ObjectDAO<User>(session);        // 写入
var userViewDao = new ObjectViewDAO<User>(session); // 读取
```

`CreateSession()` 每次返回一个新的 `SessionManager`，由调用方负责释放；会话通过构造参数显式传给 DAO，与 DI 线路互不干扰。

## 4. 完整调用闭环

```csharp
using LiteOrm;
using LiteOrm.Common;
using Microsoft.Data.Sqlite;

using var liteOrm = new LiteOrmClient()
    .AddDataSource<SqliteConnection>("main", "Data Source=LiteOrmManualDemo.db", @default: true, syncTable: true, poolSize: 8, maxPoolSize: 32);

using var session = liteOrm.CreateSession();
var userDao = new ObjectDAO<User>(session);
var userViewDao = new ObjectViewDAO<User>(session);

// 1. 插入
var user = new User { UserName = "demo-user", Age = 26, CreateTime = DateTime.Now };
await userDao.InsertAsync(user);
Console.WriteLine($"插入成功，自增 Id = {user.Id}");

// 2. 条件查询
var adults = await userViewDao.Search(Expr.Prop(nameof(User.Age)) >= 18).ToListAsync();
Console.WriteLine($"成年用户数量：{adults.Count}");

// 3. 单条查询
var current = await userViewDao.GetObject(user.Id).FirstOrDefaultAsync();
Console.WriteLine($"查询到：{current?.UserName}, Age = {current?.Age}");

// 4. 统计与存在性判断
var count = await userViewDao.Count(Expr.Prop(nameof(User.Age)) >= 18).GetResultAsync();
var exists = await userViewDao.ExistsKey(user.Id).GetResultAsync();

// 5. 更新
current!.UserName = "updated-demo-user";
await userDao.UpdateAsync(current);

// 6. 删除
await userDao.DeleteAsync(Expr.Prop(nameof(User.Id)) == user.Id, CancellationToken.None);

Console.WriteLine($"Count={count}, Exists={exists}");

// 释放：会话先于客户端释放
liteOrm.Dispose();
```

> - **写操作在 `ObjectDAO<T>`**：`Insert` / `Update` / `DeleteAsync` / `DeleteByKeysAsync` / `UpdateOrInsert` 及批量版本。
> - **读操作在 `ObjectViewDAO<T>`**：`GetObject(keys)` / `Search(Expr?)` / `Count(expr)` / `Exists(...)` / `SearchAs<TResult>(...)`。`ObjectDAO<T>` 没有 `GetObject`。
> - **结果类型**：`EnumerableResult<T>` 提供 `FirstOrDefault()` / `FirstOrDefaultAsync(ct)` / `ToList()` / `ToListAsync(ct)`；`ValueResult<T>` 提供 `GetResult()` / `GetResultAsync(ct)`。

## 5. 用事务包住多条写操作

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

> `BeginTransaction()` / `Commit()` / `Rollback()` 都作用在 `SessionManager` 上，事务上下文由该会话内的 DAO 共享。也可以传 `IsolationLevel` 指定隔离级别。

## 6. 手动线路与 DI 线路的边界

| 能力 | `LiteOrmClient`（本文） | `AddLiteOrm()` / `RegisterLiteOrm()` |
| --- | --- | --- |
| 实体映射 / CRUD / 查询 | ✅ | ✅ |
| 手动事务 | ✅ `session.BeginTransaction()` | ✅ |
| 声明式事务 `[Transaction]` | ❌ | ✅ AOP 拦截 |
| 权限过滤 `[ServicePermission]` | ❌ | ✅ AOP 拦截 |
| 自动日志 `[ServiceLog]` | ❌ | ✅ AOP 拦截 |
| 需要 DI 容器 | ❌ 不需要 | ✅ 需要 |
| 读取 `IConfiguration` | ❌ 数据源全部代码里显式登记 | ✅ `appsettings.json` 自动绑定 |
| 修改 `SessionManager.Current` | ❌ | ✅ 自动绑定 |

> 需要 AOP 能力时，把 `new LiteOrmClient()` 换成宿主里的 `AddLiteOrm()` 即可，实体定义与 DAO 用法完全一致。

## 7. 新手常见问题

### 问题一：`SQLite Error 1: 'no such table: Users'`

**原因**：数据库中没有 `Users` 表。

**解决方法**：在 `AddDataSource` 时传 `syncTable: true`（开发环境推荐），或手动执行建表 SQL。

### 问题二：`InvalidOperationException: The connection pool factory has already been created`

**原因**：已经调用过 `CreateSession()`（连接池已建好）之后，又去 `AddDataSource`。

**解决方法**：把所有 `AddDataSource` 调用都放到链的前面，在首次取会话之前完成。连接池参数也必须在 `AddDataSource` 时一次给全。

### 问题三：`ArgumentNullException` 或 `SessionManager.Current` 为 null

**原因**：`LiteOrmClient` 线路不设置 `SessionManager.Current`，依赖静态入口的旧写法会取到 null。

**解决方法**：把 `CreateSession()` 的返回值显式传给 DAO 构造函数：`new ObjectDAO<User>(session)`。不要依赖 `SessionManager.Current`。

## 运行验证清单

- [ ] `dotnet build` 编译通过，无错误。
- [ ] 所有 `AddDataSource` 都在首次 `CreateSession()` 之前调用。
- [ ] 实体类使用了 `[Table]` 和 `[Column]` 特性标注。
- [ ] 读操作走 `ObjectViewDAO<T>`（`ObjectDAO<T>` 没有 `GetObject`）。
- [ ] 插入、查询、更新、删除返回了预期结果。
- [ ] 退出前释放了会话与客户端（`using` 或显式 `Dispose`）。

## 相关链接

- [返回目录](../README.md)
- [安装](./02-installation.md)
- [第一个完整示例（仅基础库）](./03-first-example.md)
- [第一个完整示例（DI 版）](./05-first-example-di.md)
- [配置参考](../05-reference/01-configuration-reference.md)
