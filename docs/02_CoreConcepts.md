# 基础概念

本文介绍 LiteOrm 的核心组件架构、实体定义、视图模型和数据源概念。

## 1. 核心组件架构

LiteOrm 由以下几个核心组件构成：

```
┌─────────────────────────────────────────────────────────┐
│                      Service 层                         │
│  EntityService<T> / EntityService<T, TView>            │
│  提供业务层封装，支持同步/异步、实体/视图查询               │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│                       DAO 层                            │
│  ObjectDAO<T>  - 增删改操作                              │
│  ObjectViewDAO<T> - 查询操作，返回实体列表               │
│  DataViewDAO<T> - 查询操作，返回 DataTable               │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│                    SqlBuilder 层                        │
│  MySqlBuilder / SqlServerBuilder / OracleBuilder 等    │
│  负责将 Expr 表达式转换为具体数据库的 SQL 语句            │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│                    DbAccess 层                          │
│  数据库连接管理、事务控制、连接池                         │
└─────────────────────────────────────────────────────────┘
```

### 组件职责

| 组件 | 职责 |
|------|------|
| `EntityService<T>` | 业务服务封装，组合多个 DAO，提供统一接口 |
| `ObjectDAO<T>` | 单表增删改，支持 Lambda/Expr 查询 |
| `ObjectViewDAO<T>` | 关联查询，返回 `List<T>` |
| `DataViewDAO<T>` | 关联查询，返回 `DataTable` |
| `SessionManager` | 会话上下文管理，通过 AsyncLocal 实现异步上下文隔离 |
| `DAOContext` | 数据库连接上下文，管理连接和事务 |
| `SqlBuilder` | SQL 方言构建器，将 Expr 转换为具体数据库 SQL |
| `LambdaExprConverter` | Lambda 表达式到 Expr 的转换器 |
| `LiteOrmLambdaHandlerInitializer` | 启动时注册默认的 Lambda 方法/成员处理器 |
| `LiteOrmSqlFunctionInitializer` | 启动时注册默认的 SQL 函数处理器 |
| `Expr` | 表达式对象模型，抽象 SQL 结构 |

## 2. 实体定义

实体类是 LiteOrm 操作数据库的基础，通过特性映射表和列。

### 2.1 基本结构

实体类可以使用普通类，无需继承任何基类：

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

    [Column("DeptId")]
    public int? DeptId { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }
}
```

> **注意**：`ObjectBase` 是可选的基类，仅用于增加一些常用方法（如 `Clone()`、`ToJson()` 等）。普通 POCO 类已完全满足 LiteOrm 的使用要求。

### 2.2 Table 特性

```csharp
[Table("表名")]
[Table("表名", DataSource = "数据源名称")]
```

| 参数 | 类型 | 说明 |
|------|------|------|
| `Name` | string | 数据库表名 |
| `DataSource` | string | 指定数据源名称（用于多数据源场景） |

### 2.3 Column 特性

```csharp
[Column("列名")]
[Column("列名", IsPrimaryKey = true)]
[Column("列名", IsIdentity = true)]
[Column("列名", IsPrimaryKey = true, IsIdentity = true)]
```

| 参数 | 类型 | 说明 |
|------|------|------|
| `Name` | string | 数据库列名 |
| `IsPrimaryKey` | bool | 是否为主键，默认 false |
| `IsIdentity` | bool | 是否自增列，默认 false |
| `DataType` | Type | 序列化类型（用于复杂对象存储） |

### 2.4 外键关联

```csharp
[Table("Orders")]
public class Order
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserId")]
    [ForeignType(typeof(User), Alias = "U")]  // 外键关联 User 表
    public int UserId { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }
}
```

| 参数 | 类型 | 说明 |
|------|------|------|
| `Type` | Type | 关联的实体类型 |
| `Alias` | string | 关联表别名，用于避免列名冲突 |

## 3. 视图模型

视图模型用于关联查询，继承实体类并添加 `[ForeignColumn]` 属性。

### 3.1 定义视图

```csharp
// 继承 Order 实体，添加 UserName 外键列
public class OrderView : Order
{
    [ForeignColumn(typeof(User), Property = "UserName")]
    public string? UserName { get; set; }

    [ForeignColumn(typeof(User), Property = "Email")]
    public string? UserEmail { get; set; }
}
```

### 3.2 ForeignColumn 特性

| 参数 | 类型 | 说明 |
|------|------|------|
| `Type` | Type | 关联的实体类型 |
| `Property` | string | 关联表中要获取的列名 |

### 3.3 自动 JOIN

定义好视图后，查询时会自动生成 JOIN 语句：

```csharp
var orders = await orderService.SearchAsync<OrderView>();
// 自动生成：SELECT o.*, u.UserName, u.Email FROM Orders o LEFT JOIN Users u ON o.UserId = u.Id
```

## 4. 数据源

LiteOrm 支持多数据源配置，用于读写分离或分库场景。

### 4.1 配置多个数据源

```json
{
  "LiteOrm": {
    "Default": "WriteDB",
    "DataSources": [
      {
        "Name": "WriteDB",
        "ConnectionString": "Server=master;Database=TestDb;...",
        "Provider": "MySqlConnector.MySqlConnection, MySqlConnector"
      },
      {
        "Name": "ReadDB",
        "ConnectionString": "Server=replica;Database=TestDb;...",
        "Provider": "MySqlConnector.MySqlConnection, MySqlConnector",
        "ReadOnlyConfigs": []
      }
    ]
  }
}
```

### 4.2 指定数据源

通过 `[Table]` 特性的 `DataSource` 参数指定：

```csharp
[Table("Users", DataSource = "ReadDB")]
public class User { ... }
```

## 5. 服务定义模式

### 5.1 实体与视图类型相同

```csharp
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<User>, IEntityViewServiceAsync<User>
{ }

public class UserService : EntityService<User>, IUserService { }
```

### 5.2 实体与视图类型不同

```csharp
public interface IOrderService
    : IEntityService<Order>, IEntityServiceAsync<Order>,
      IEntityViewService<OrderView>, IEntityViewServiceAsync<OrderView>
{ }

public class OrderService : EntityService<Order, OrderView>, IOrderService { }
```

## 6. 生命周期与 SessionManager

LiteOrm 通过 Autofac 实现依赖注入。`RegisterLiteOrm()` 默认以 **Scoped** 方式注册。

### 6.1 RegisterScope 的作用

`RegisterScope` 用于**自动维护 Scope 与 `SessionManager.Current` 的绑定**：

- 当 Scope 开启时，自动将当前 Scope 的 `SessionManager` 实例绑定到 `SessionManager.Current`
- 当 Scope 结束时，自动恢复为父级 Scope 的 `SessionManager`（如果有）
- 这确保了在异步调用链中，`SessionManager.Current` 始终指向当前请求对应的会话

| 注册方式 | 生命周期 | 说明 |
|----------|----------|------|
| `RegisterLiteOrm()` | Scoped（默认） | 自动维护 SessionManager.Current 绑定 |
| `RegisterLiteOrm(options => options.RegisterScope = false)` | Singleton | 不维护绑定，需手动处理 SessionManager.Current |

### 6.2 SessionManager

`SessionManager` 是 LiteOrm 的核心会话管理类，负责：

| 功能 | 说明 |
|------|------|
| 会话上下文管理 | 通过 `AsyncLocal` 管理异步上下文中的会话 |
| 连接池管理 | 使用 `DAOContextPoolFactory` 获取和管理连接 |
| 事务处理 | 支持事务的开始、提交和回滚 |
| SQL 日志 | 记录执行的 SQL 语句用于调试和监控 |

**获取当前会话**：

```csharp
var session = SessionManager.Current;
```

**手动事务示例**：

```csharp
var session = SessionManager.Current;
session.BeginTransaction();
try
{
    await userService.InsertAsync(user);
    await orderService.InsertAsync(order);
    session.Commit();
}
catch
{
    session.Rollback();
    throw;
}
```

**异步事务扩展方法**：

```csharp
await SessionManager.Current.ExecuteInTransactionAsync(async sm =>
{
    await userService.InsertAsync(user);
    await orderService.InsertAsync(order);
});
```

## 7. Service 与 DAO 查询对比

LiteOrm 提供 Service 和 DAO 两层 API：

| 对比项 | Service | DAO |
|--------|---------|-----|
| 查询方式 | Lambda 表达式 + Expr | Lambda 表达式 + Expr + ExprString |
| 返回类型 | 强类型 `List<T>` | `EnumerableResult<T>`（支持流式枚举） |
| 异步调用 | 直接返回 `Task<T>` | 通过 `ToListAsync()` / `FirstOrDefaultAsync()` 等扩展方法 |
| 批量操作 | ✅ 支持 | ✅ 支持 |
| 关联查询 | ✅ 通过 View 类型 | ✅ 通过 View 类型 |

**Service 查询示例**：

```csharp
// Lambda 表达式 - 直接返回 Task<List<T>>
var users = await userService.SearchAsync(u => u.Age >= 18);

// Expr 对象 - 直接返回 Task<List<T>>
var users = await userService.SearchAsync(Expr.Prop("Age") >= 18);
```

**DAO 查询示例**：

```csharp
// Expr - 需要调用扩展方法获取结果
var result = userViewDAO.Search(Expr.Prop("Age") >= 18);
var users = await result.ToListAsync();

// ExprString 插值字符串
var result = userViewDAO.Search($"WHERE {Expr.Prop("Age")} > {minAge}");
var users = await result.ToListAsync();

// 流式枚举
await foreach (var user in userViewDAO.Search(Expr.Prop("Status") == 1))
{
    // 逐条处理，避免一次性加载到内存
}
```

**核心区别**：
- **Service**：异步方法直接返回 `Task<T>`，使用方便
- **DAO**：`Search()` 返回 `EnumerableResult<T>`，需要调用 `ToListAsync()`、`FirstOrDefaultAsync()` 等扩展方法获取结果，支持流式处理

## 8. 下一步

- 掌握查询方式：[查询指南](./03_QueryGuide.md)
- 学习完整操作：[增删改查](./04_CrudGuide.md)
- 事务处理：[EXP_Transaction](./EXP/EXP_Transaction.md)
