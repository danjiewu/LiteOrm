# 第一个完整示例（仅核心库）

本文通过一个最小可运行示例展示**仅使用 `LiteOrm` 核心库**（不引入 `LiteOrm.Framework`、Autofac、Castle 动态代理）的典型使用流程：手动初始化、定义实体、插入数据、查询数据和分页查询。

> **适用场景**：控制台应用、批处理脚本、不依赖 DI 容器的项目，或希望对生命周期完全自管理的场景。
>
> 如果你使用 ASP.NET Core 且需要 Autofac 集成、AOP 事务/权限/日志、动态 Controller 等能力，请参考 [第一个完整示例（Framework 版）](./05-first-example-framework.md)。

## 0. 项目准备

```bash
dotnet new console -n LiteOrmCoreDemo
cd LiteOrmCoreDemo
dotnet add package LiteOrm
dotnet add package Microsoft.Data.Sqlite
```

> 核心库 `LiteOrm` 会自动携带 `LiteOrm.Common`，无需单独安装。此处以 SQLite 为例，无需额外安装数据库服务。

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

> - `[Table("Users")]`：映射到数据库 `Users` 表。
> - `[Column("Id", IsPrimaryKey = true, IsIdentity = true)]`：主键且自增。
> - 实体类不要求继承 `ObjectBase`，普通 POCO 即可。

## 2. 手动初始化 LiteOrm

核心库不提供 `RegisterLiteOrm()`，需要手动构造依赖链。以下是完整的初始化代码：

```csharp
using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Data.Sqlite;

// 1. 配置数据源
var dataSourceProvider = new DataSourceProvider();
dataSourceProvider.AddDataSource(new DataSourceConfig
{
    Name = "DefaultConnection",
    ConnectionString = "Data Source=LiteOrmDemo.db",
    Provider = typeof(SqliteConnection).AssemblyQualifiedName,
    SyncTable = true  // 自动建表（开发阶段推荐）
});
dataSourceProvider.SetDefaultDataSource("DefaultConnection");

// 2. 初始化 SQL 函数映射（注册内置数据库函数处理器）
LiteOrmSqlFunctionInitializer.Initialize();

// 3. 创建连接池工厂
var poolFactory = new DAOContextPoolFactory(dataSourceProvider);
DAOContextPoolFactory.Set(() => poolFactory);

// 4. 创建会话管理器并设为当前会话
var sessionManager = new SessionManager(poolFactory);
SessionManager.SetCurrent(() => sessionManager);

// 5. 创建 DAO 和服务
var objectDAO = new ObjectDAO<User>();
var objectViewDAO = new ObjectViewDAO<User>();
var userService = new EntityService<User>(objectDAO, objectViewDAO);
```

> **逐行解释**：
> - `DataSourceProvider`：管理数据源配置。通过 `AddDataSource` 显式添加，`SetDefaultDataSource` 指定默认数据源。
> - `LiteOrmSqlFunctionInitializer.Initialize()`：注册各数据库方言的 SQL 函数映射（如 `NOW()`、`DATE_FORMAT` 等），必须调用。
> - `DAOContextPoolFactory`：根据数据源配置创建连接池，管理连接的获取与回收。通过 `Set` 设置为全局单例，使 DAO 内部可通过静态属性获取提供程序类型。
> - `SessionManager`：管理数据库会话、事务和异步上下文。通过 `SetCurrent` 设置为当前异步上下文的会话。
> - `ObjectDAO<T>` / `ObjectViewDAO<T>`：分别负责增删改和查询的数据访问对象。内部通过 `TableInfoProvider.Instance` 和 `BulkProviderFactory.Instance` 获取全局单例，无需手动传入。
> - `EntityService<T>`：封装了 DAO 的业务服务，提供 `InsertAsync`、`SearchAsync`、`UpdateAsync`、`DeleteAsync` 等方法。

## 3. 插入一条数据

```csharp
var user = new User
{
    UserName = "admin",
    Age = 30,
    CreateTime = DateTime.Now
};

await userService.InsertAsync(user);
Console.WriteLine($"插入成功，自增 Id = {user.Id}");
```

> `InsertAsync` 会将实体插入数据库。如果 `Id` 是自增列（`IsIdentity = true`），插入后实体的 `Id` 属性会自动填充。

## 4. 执行查询

```csharp
// 条件查询
var adults = await userService.SearchAsync(u => u.Age >= 18);
Console.WriteLine($"成年用户数量：{adults.Count}");

// 单条查询
var admin = await userService.SearchOneAsync(u => u.UserName == "admin");
Console.WriteLine($"查询到：{admin?.UserName}, Age = {admin?.Age}");
```

## 5. 执行分页

```csharp
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderByDescending(u => u.CreateTime)
          .Skip(0)
          .Take(10)
);
Console.WriteLine($"分页结果：{page.Count} 条");
```

## 6. 完整调用闭环

```csharp
// 1. 插入
var user = new User
{
    UserName = "demo-user",
    Age = 26,
    CreateTime = DateTime.Now
};
await userService.InsertAsync(user);

// 2. 查询
var current = await userService.SearchOneAsync(u => u.Id == user.Id);

// 3. 更新
current!.UserName = "updated-demo-user";
await userService.UpdateAsync(current);

// 4. 统计
var count = await userService.CountAsync(u => u.Age >= 18);

// 5. 判断是否存在
var exists = await userService.ExistsAsync(u => u.UserName == "updated-demo-user");

// 6. 删除
if (exists)
{
    await userService.DeleteAsync(current);
}

Console.WriteLine($"Count={count}, Exists={exists}");
```

## 7. 手动事务

核心库不提供 AOP 声明式事务（`[Transaction]` 特性需要 Framework 的 Castle 拦截器），但可以通过 `SessionManager` 手动控制：

```csharp
sessionManager.BeginTransaction();
try
{
    await userService.InsertAsync(new User { UserName = "user1", Age = 20, CreateTime = DateTime.Now });
    await userService.InsertAsync(new User { UserName = "user2", Age = 25, CreateTime = DateTime.Now });
    sessionManager.Commit();
}
catch
{
    sessionManager.Rollback();
    throw;
}
```

## 8. 资源释放

核心库场景下，`SessionManager` 和 `DAOContextPoolFactory` 持有数据库连接，使用完毕后需要释放：

```csharp
// 应用退出时
sessionManager.Dispose();
poolFactory.Dispose();

// 如果使用了 SyncTable=true，数据库文件会自动创建
// 如果是 SQLite in-memory（Data Source=:memory:），连接关闭后数据丢失
```

## 9. 核心库与 Framework 的能力对比

| 能力 | 仅核心库 (`LiteOrm`) | Framework (`LiteOrm.Framework`) |
|------|----------------------|--------------------------------|
| 实体映射 / CRUD / 查询 | ✅ | ✅ |
| 手动事务 | ✅ `SessionManager.BeginTransaction()` | ✅ |
| 声明式事务 `[Transaction]` | ❌ | ✅ AOP 拦截 |
| 权限过滤 `[ServicePermission]` | ❌ | ✅ AOP 拦截 |
| 自动日志 `[ServiceLog]` / `[Log]` | ❌ | ✅ AOP 拦截 |
| DI 容器自动注册 | ❌ 手动构造 | ✅ `RegisterLiteOrm()` |
| 动态 Controller 生成 | ❌ | ✅ |
| 配置文件绑定 | ❌ 手动 `AddDataSource` | ✅ `appsettings.json` 自动绑定 |
| 批量导入 `IBulkProvider` | ❌（Factory 可构造但不注册） | ✅ 自动注册 |

> 如果你后续需要 AOP 能力，可以从核心库平滑迁移到 Framework，实体定义和 DAO/Service 用法完全一致。

## 10. 新手常见问题

### 问题一：`SQLite Error 1: 'no such table: Users'`

**原因**：数据库中没有 `Users` 表。

**解决方法**：在数据源配置中设置 `SyncTable = true`，让 LiteOrm 自动根据实体定义创建表（开发环境推荐）。或手动执行建表 SQL。

### 问题二：`Object reference not set to instance` 或 `SessionManager.Current` 为 null

**原因**：忘记调用 `SessionManager.SetCurrent(() => sessionManager)`。

**解决方法**：确保在创建服务实例之前调用 `SessionManager.SetCurrent(() => sessionManager)`，否则 DAO 在执行 SQL 时无法获取数据库连接。

### 问题三：`Function 'XXX' is not supported` 异常

**原因**：忘记调用 `LiteOrmSqlFunctionInitializer.Initialize()`。

**解决方法**：在创建连接池工厂之前调用 `LiteOrmSqlFunctionInitializer.Initialize()`，注册内置 SQL 函数映射。

## 运行验证清单

- [ ] `dotnet build` 编译通过，无错误。
- [ ] 初始化代码中调用了 `LiteOrmSqlFunctionInitializer.Initialize()`。
- [ ] 初始化代码中调用了 `SessionManager.SetCurrent(() => sessionManager)`。
- [ ] 初始化代码中调用了 `DAOContextPoolFactory.Set(() => poolFactory)`。
- [ ] 实体类使用了 `[Table]` 和 `[Column]` 特性标注。
- [ ] 插入和查询操作返回了预期的结果。
- [ ] 应用退出前释放了 `SessionManager` 和 `DAOContextPoolFactory`。

## 相关链接

- [返回目录](../README.md)
- [安装](./02-installation.md)
- [配置与注册](./03-configuration-and-registration.md)
- [第一个完整示例（Framework 版）](./05-first-example-framework.md)
- [实体映射与数据源](../02-core-usage/01-entity-mapping.md)
- [查询总览](../02-core-usage/04-query-overview.md)
- [CRUD 指南](../02-core-usage/03-crud-guide.md)
