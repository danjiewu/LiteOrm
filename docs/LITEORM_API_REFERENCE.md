# LiteOrm AI API 参考指南

## 项目概述

LiteOrm 是一个轻量级、高性能的 .NET ORM 框架，支持多种数据库（SQL Server, MySQL, Oracle, PostgreSQL, SQLite），提供完整的 CRUD 操作、灵活的查询表达式系统、声明式事务、自动化关联查询和动态分表功能。

**项目结构：**
- `LiteOrm.Common/` - 核心元数据、Expr表达式系统、接口定义
- `LiteOrm/` - ORM核心实现、DAO基类、SQL构建器
- `LiteOrm.ASPNetCore/` - ASP.NET Core 集成支持
- `LiteOrm.Demo/` - 示例项目
- `LiteOrm.Tests/` - 单元测试

---

## 一、快速入门

### 1.1 安装配置

在 `appsettings.json` 中配置数据库连接：

```json
{
    "LiteOrm": {
        "Default": "DefaultConnection",
        "DataSources": [
            {
                "Name": "DefaultConnection",
                "ConnectionString": "Data Source=demo.db",
                "Provider": "Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite",
                "KeepAliveDuration": "00:10:00",
                "PoolSize": 20,
                "MaxPoolSize": 100,
                "ParamCountLimit": 2000,
                "SyncTable": true,
                "ReadOnlyConfigs": [
                    {
                        "ConnectionString": "Server=readonly01;User ID=readonly;Password=xxxx;Database=OrmBench;"
                    },
                    {
                        "ConnectionString": "Server=readonly02;User ID=readonly;Password=xxxx;Database=OrmBench;",
                        "PoolSize": 10,
                        "KeepAliveDuration": "00:30:00"
                    }
                ]
            }
        ]
    }
}
```

**配置参数详解：**

| 参数名 | 默认值 | 说明 |
| :--- | :--- | :--- |
| **Default** | - | 默认数据源名称，如果实体未指定数据源则使用此项。 |
| **Name** | - | 必填，数据源名称。 |
| **ConnectionString** | - | 必填，物理连接字符串。 |
| **Provider** | - | 必填，DbConnection 实现类的类型全名（Assembly Qualified Name）。 |
| **PoolSize** | 16 | 基础连接池容量，超过此数量的数据库空闲连接会被释放。 |
| **MaxPoolSize** | 100 | 最大并发连接限制，防止耗尽数据库资源。 |
| **KeepAliveDuration** | 10min | 连接空闲存活时间，超过此时间后空闲连接将被物理关闭。 |
| **ParamCountLimit** | 2000 | 单条 SQL 支持的最大参数个数，批量操作时参数超过此限制会自动分批执行，避免触发 DB 限制。 |
| **SyncTable** | false | 是否在启动时自动检测实体类并尝试同步数据库表结构。 |
| **ReadOnlyConfigs** | - | 只读库配置 |

### ReadOnlyConfigs（只读从库配置）

LiteOrm 支持为每个主数据源配置若干只读从库，用于读写分离、负载均衡或故障切换。只读配置放在对应数据源对象的 `ReadOnlyConfigs` 数组中。

说明：

- `ReadOnlyConfigs`：可选数组，每项为只读数据源配置对象（可为空）。
- 每个只读项至少包含 `ConnectionString`，当只读库与主库使用不同驱动时也可指定 `Provider`。
- LiteOrm 在执行只读操作（例如 SELECT 查询）时会优先选择只读配置，从而减轻主库写入压力并实现读扩展。
- 如果所有只读配置不可用或未配置，LiteOrm 会回退到主数据源的连接。
- 可结合连接池与自定义路由策略实现更复杂的读写分离、负载均衡或高可用策略。

### 1.2 注册 LiteOrm

在 `Program.cs` 中注册 LiteOrm：

```csharp
// Generic Host / Console 应用
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()  // 自动扫描 AutoRegister 特性并初始化连接池
    .Build();
```

**注意：** LiteOrm 使用 `RegisterLiteOrm()` 进行注册，而非 `AddLiteOrm()`。

### 1.3 定义实体

```csharp
using LiteOrm.Common;

[Table("USERS")]  // 表名特性
public class User
{
    [Column("ID", IsPrimaryKey = true, IsIdentity = true)]  // 主键自增
    public int Id { get; set; }

    [Column("USERNAME", IsUnique = true)]  // 唯一约束
    public string UserName { get; set; }

    [Column("EMAIL", CanBeNull = true)]  // 允许空值
    public string Email { get; set; }
    
    [Column("AGE")]
    public int Age { get; set; }
}
```

### 1.4 定义服务

```csharp
// 定义服务接口
public interface IUserService : IEntityService<User>, IEntityViewService<User>, 
    IEntityServiceAsync<User>, IEntityViewServiceAsync<User>
{
    // 可添加自定义方法
}

// 实现服务
public class UserService : EntityService<User>, IUserService
{
    // 继承所有基础 CRUD 和查询方法
}
```

### 1.5 使用服务

```csharp
// 插入
var user = new User { UserName = "admin", Age = 25 };
userService.Insert(user);

// 查询
var users = userService.Search(u => u.Age > 18);
var admin = userService.SearchOne(u => u.UserName == "admin");

// 更新
user.Age = 30;
userService.Update(user);

// 删除
userService.Delete(user);

// 异步操作
await userService.InsertAsync(user);
var users = await userService.SearchAsync(u => u.Age > 18);
```

---

## 二、实体映射 (Entity Mapping)

### 2.1 实体类定义

**文件位置：** `LiteOrm.Common/Attributes/ColumnAttribute.cs`

```csharp
using LiteOrm.Common;

[Table("USERS")]  // 表名特性
public class User
{
    [Column("ID", IsPrimaryKey = true, IsIdentity = true)]  // 主键自增
    public int Id { get; set; }

    [Column("USERNAME", IsUnique = true)]  // 唯一约束
    public string UserName { get; set; }

    [Column("EMAIL", CanBeNull = true)]  // 允许空值
    public string Email { get; set; }
    
    [Column("AGE")]
    public int Age { get; set; }
    
    [Column("CREATE_TIME")]
    public DateTime CreateTime { get; set; }
    
    [Column("DEPT_ID")]
    public int DeptId { get; set; }
}
```

### 2.2 视图模型定义

视图模型用于查询操作，可以包含关联表字段：

```csharp
// 视图模型继承自实体类
public class UserView : User
{
    // 关联部门名称
    [ForeignColumn(typeof(Dept), Property = "DeptName")]
    public string DeptName { get; set; }
}

// 部门实体
[Table("DEPT")]
public class Dept
{
    [Column("ID", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    
    [Column("DEPT_NAME")]
    public string DeptName { get; set; }
}
```

### 2.3 关联特性

```csharp
// 一对一关联
[Table("ORDERS")]
public class Order
{
    [Column("ID", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    
    [Column("USER_ID")]
    [ForeignType(typeof(User))]  // 外键关联到User
    public int UserId { get; set; }
    
    [Column("AMOUNT")]
    public decimal Amount { get; set; }
}

// 订单视图模型
public class OrderView : Order
{
    [ForeignColumn(typeof(User), Property = "UserName")]  // 从User表带出UserName
    public string UserName { get; set; }
}
```

### 2.4 分表支持

```csharp
public class Log : IArged  // 实现IArged接口
{
    [Column("ID", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    
    [Column("CONTENT")]
    public string Content { get; set; }
    
    [Column("CREATE_TIME")]
    public DateTime CreateTime { get; set; }
    
    // 自动路由到 Log_202401 格式的表
    string[] IArged.TableArgs => [CreateTime.ToString("yyyyMM")];
}
```

---

## 三、Service 层 API

### 3.1 IEntityService - 实体服务接口

**文件位置：** `LiteOrm.Common/Service/IEntityService.cs`

```csharp
// 基础 CRUD
bool Insert(T entity)
bool Update(T entity)
bool UpdateOrInsert(T entity)

// 批量操作（带事务支持）
void BatchInsert(IEnumerable<T> entities)
void BatchUpdate(IEnumerable<T> entities)
void BatchUpdateOrInsert(IEnumerable<T> entities)
void BatchDelete(IEnumerable<T> entities)

// 根据ID删除
bool DeleteID(object id, params string[] tableArgs)
void BatchDeleteID(IEnumerable ids, params string[] tableArgs)

// 根据条件删除（使用LogicExpr）
int Delete(LogicExpr expr, params string[] tableArgs)
```

### 3.2 IEntityServiceAsync - 实体服务异步接口

**文件位置：** `LiteOrm.Common/Service/IEntityServiceAsync.cs`

```csharp
Task<bool> InsertAsync(T entity, CancellationToken cancellationToken = default)
Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default)
Task<bool> UpdateOrInsertAsync(T entity, CancellationToken cancellationToken = default)

Task BatchInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
Task BatchUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
Task BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
Task BatchDeleteAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)

Task<bool> DeleteIDAsync(object id, CancellationToken cancellationToken = default, params string[] tableArgs)
Task BatchDeleteIDAsync(IEnumerable ids, CancellationToken cancellationToken = default, params string[] tableArgs)

Task<int> DeleteAsync(LogicExpr expr, CancellationToken cancellationToken = default, params string[] tableArgs)
```

### 3.3 IEntityViewService - 视图服务接口

**文件位置：** `LiteOrm.Common/Service/IEntityViewService.cs`

```csharp
// 查询视图模型
List<TView> Search<TView>(Expr expr, params string[] tableArgs)
List<TView> Search<TView>(Func<IQueryable<TView>, IQueryable<TView>> queryAction, params string[] tableArgs)

// 查询单个
TView SearchOne<TView>(Expr expr, params string[] tableArgs)

// 检查是否存在
bool Exists(Expr expr, params string[] tableArgs)

// 统计数量
int Count(Expr expr, params string[] tableArgs)
```

### 3.4 IEntityViewServiceAsync - 视图服务异步接口

**文件位置：** `LiteOrm.Common/Service/IEntityViewServiceAsync.cs`

```csharp
Task<List<TView>> SearchAsync<TView>(Expr expr, CancellationToken cancellationToken = default, params string[] tableArgs)
Task<List<TView>> SearchAsync<TView>(Func<IQueryable<TView>, IQueryable<TView>> queryAction, CancellationToken cancellationToken = default, params string[] tableArgs)

Task<TView> SearchOneAsync<TView>(Expr expr, CancellationToken cancellationToken = default, params string[] tableArgs)

Task<bool> ExistsAsync(Expr expr, CancellationToken cancellationToken = default, params string[] tableArgs)
Task<int> CountAsync(Expr expr, CancellationToken cancellationToken = default, params string[] tableArgs)
```

### 3.5 EntityService - 服务基类

**文件位置：** `LiteOrm/Service/EntityService.cs`

```csharp
// 泛型版本：实体类型和视图类型相同
public class UserService : EntityService<User>, IUserService
{
    // 继承所有基础 CRUD 方法
    // 可直接使用：Insert, Update, Delete, Search, SearchOne 等
}

// 泛型版本：实体类型和视图类型不同
public interface IUserService : IEntityService<User>, IEntityViewService<UserView>, 
    IEntityServiceAsync<User>, IEntityViewServiceAsync<UserView>
{
    // 可添加自定义方法
}

public class UserService : EntityService<User, UserView>, IUserService
{
    // 继承所有基础 CRUD 和查询方法
    // 可直接使用：Insert, Update, Delete, Search, SearchOne 等
}
```

### 3.6 扩展方法（Lambda查询）

**文件位置：** `LiteOrm.Common/Service/EntityServiceExtensions.cs`

IQueryable 形式的查询是通过扩展方法实现的：

```csharp
// Lambda 形式条件查询（仅条件）
var users = userViewService.Search(u => u.Age > 18);
var users = userViewService.Search(u => u.Age > 18 && u.UserName.Contains("admin"));

// Lambda 形式条件删除（使用LogicExpr）
int deleted = userService.Delete(u => u.Age < 18);

// IQueryable 形式查询（支持 Where/OrderBy/Skip/Take）- 扩展方法实现
var users = userViewService.Search(
    q => q.Where(u => u.Age > 18).OrderBy(u => u.Id).Skip(0).Take(10)
);

// 异步版本
var users = await userViewService.SearchAsync(
    q => q.Where(u => u.Age > 18).OrderBy(u => u.Id)
);

// 异步单个查询
var user = await userViewService.SearchOneAsync(u => u.Id == 1);

// 检查存在
var exists = userViewService.Exists(u => u.UserName == "admin");

// 统计数量
var count = userViewService.Count(u => u.Age > 18);
```

---

## 四、常用查询模式

### 4.1 基础 CRUD

```csharp
// 插入
var user = new User { UserName = "admin", Age = 25 };
userService.Insert(user);

// 更新
user.Age = 30;
userService.Update(user);

// 插入或更新
userService.UpdateOrInsert(user);

// 删除
userService.Delete(user);

// 根据ID删除
userService.DeleteID(1);
```

### 4.2 条件查询

```csharp
// 单一条件
var users = userService.Search(u => u.Age > 18);

// 多条件AND
var users = userService.Search(u => u.Age > 18 && u.UserName.Contains("admin"));

// IN 查询
var users = userService.Search(u => new[] { 1, 2, 3 }.Contains(u.Id));

// IS NULL
var users = userService.Search(u => u.Email == null);

// LIKE
var users = userService.Search(u => u.UserName.Contains("admin"));
```

### 4.3 排序与分页

```csharp
// 排序（IQueryable 形式）
var sorted = userService.Search(q => q.Where(u => u.Age > 18).OrderBy(u => u.Age));
var sortedDesc = userService.Search(q => q.Where(u => u.Age > 18).OrderByDescending(u => u.Age));

// 分页
var page1 = userService.Search(q => q.Where(u => u.Age > 18).OrderBy(u => u.Id).Skip(0).Take(10));
var page2 = userService.Search(q => q.Where(u => u.Age > 18).OrderBy(u => u.Id).Skip(10).Take(10));

// 组合排序
var users = userService.Search(
    q => q.Where(u => u.Age > 18)
         .OrderBy(u => u.Age)
         .ThenByDescending(u => u.Id)
         .Skip(0)
         .Take(10)
);
```

### 4.4 聚合查询

聚合查询需要使用 DataViewDAO，因为 EntityViewService 的 Search 方法不支持 GroupBy：

```csharp
// 使用 DataViewDAO 进行聚合查询
var dataViewDAO = serviceProvider.GetRequiredService<DataViewDAO<User>>();

// 创建带 GroupBy 的查询
var selectExpr = new SelectExpr(
    new GroupByExpr(
        new WhereExpr(Expr.From<User>(), Expr.Prop("Age") > 18),
        Expr.Prop("DeptId")
    ),
    Expr.Prop("DeptId"),
    Expr.Prop("Id").Count().As("UserCount")
);

// 查询返回 DataTable
var result = dataViewDAO.Search(selectExpr);
```

### 4.5 关联查询

```csharp
// 定义关联
public class Order
{
    [Column("ID", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    
    [Column("USER_ID")]
    [ForeignType(typeof(User))]
    public int UserId { get; set; }
}

public class OrderView : Order
{
    [ForeignColumn(typeof(User), Property = "UserName")]
    public string UserName { get; set; }
}

// 查询（自动JOIN）
var orders = orderViewService.Search<OrderView>(o => o.Amount > 100);
```

### 4.6 EXISTS 子查询

LiteOrm 支持通过 `Expr.Exists<T>` 进行高效的 EXISTS 子查询：

```csharp
// 基础 EXISTS 查询：查询拥有部门的所有用户
var users = userService.Search(u => Expr.Exists<Department>(d => d.Id == u.DeptId));

// EXISTS 与其他条件组合
var users = userService.Search(u => u.Age > 25 && Expr.Exists<Department>(d => d.Id == u.DeptId));

// 复杂的子查询条件
var users = userService.Search(u => Expr.Exists<Department>(d => d.Id == u.DeptId && d.Name == "IT"));

// 排序和分页
var users = await userService.SearchAsync(
    q => q.Where(u => Expr.Exists<Department>(d => d.Id == u.DeptId && d.Name == "IT"))
          .OrderByDescending(u => u.CreateTime)
          .Skip(0)
          .Take(10)
);

// NOT EXISTS：查询没有部门的用户
var orphans = userService.Search(u => !Expr.Exists<Department>(d => d.Id == u.DeptId));

// 多个 EXISTS 条件（AND）
var users = userService.Search(u => 
    Expr.Exists<Department>(d => d.Id == u.DeptId && d.Name == "IT") &&
    Expr.Exists<Department>(d => d.ParentId != null));
```

**性能优势**：
- EXISTS 仅检查是否存在，不返回关联表数据，性能优于 LEFT JOIN
- 关联表数据量大时，EXISTS 的短路优化效果明显
- 适合"存在性检查"场景，不适合需要返回关联数据的情况

详细信息见：[Expr.Exists 子查询演示](../LiteOrm.Demo/Demos/ExistsSubqueryDemo.cs) 和 [快速参考指南](./../.trae/documents/ExistsSubquery_Quick_Reference.md)

### 4.7 事务操作

```csharp
[Service]
public class BusinessService
{
    private readonly IUserService userService;
    private readonly IOrderService orderService;

    [Transaction]  // 声明式事务
    public async Task CreateUserWithOrder(User user, Order order)
    {
        await userService.InsertAsync(user);
        order.UserId = user.Id;
        await orderService.InsertAsync(order);
        // 自动提交事务
    }
}
```

---

## 五、Expr 表达式系统 (核心查询引擎)

### 5.1 Expr 类型继承体系

**文件位置：** `LiteOrm.Common/Expr/`

```
Expr (基类)
├── LogicExpr (逻辑表达式，用于WHERE条件)
│   ├── LogicBinaryExpr (二元逻辑: And/Or)
│   ├── LogicSet (IN/NOT IN集合)
│   ├── NotExpr (NOT否定)
│   └── ForeignExpr (EXISTS子查询)
├── ValueExpr (值表达式)
│   ├── ValueBinaryExpr (二元比较: >/</==/!=)
│   ├── ValueSet (值集合)
│   └── UnaryExpr (一元运算: -/~)
├── PropertyExpr (属性引用)
├── AggregateFunctionExpr (聚合函数: COUNT/SUM/AVG/MAX/MIN)
├── FunctionExpr (函数调用)
└── LambdaExpr (Lambda转换结果)
```

**ForeignExpr（EXISTS 子查询）特性说明：**
- `ForeignExpr` 是 `LogicExpr` 的子类，用于表示 EXISTS 子查询
- 支持关联表的条件过滤和复杂条件组合
- 可与其他逻辑条件使用 AND/OR 进行组合
- 支持 NOT 否定操作（实现 NOT EXISTS）

### 5.2 Lambda 自动转换

**文件位置：** `LiteOrm.Common/Expr/ExprExtensions.cs`

```csharp
// Lambda 转 Expr（最常用的方式）
Expr expr = Expr.Exp<User>(u => u.Age > 18 && u.UserName.Contains("admin"));
// 生成 SQL: WHERE (Age > 18 AND UserName LIKE '%admin%')

// 使用 IQueryable 形式（推荐，支持排序和分页）
var users = await userService.SearchAsync(
    q => q.Where(u => u.Age > 18)
         .OrderBy(u => u.Id)
         .Skip(0)
         .Take(10)
);

// 多条件合并（多个Where自动合并为AND）
var result = await userService.SearchAsync(
    q => q.Where(u => u.Age > 18)
         .Where(u => u.UserName != null)
         .Where(u => u.UserName.Contains("admin"))
);
```

### 5.3 手动构建表达式

**文件位置：** `LiteOrm.Common/Expr/Expr.cs`

```csharp
// 属性引用
Expr prop = Expr.Prop("Age");
Expr prop2 = Expr.Prop(nameof(User.UserName));

// 比较运算
Expr cmp1 = Expr.Prop("Age") > 18;
Expr cmp2 = Expr.Prop("UserName") == "admin";
Expr cmp3 = Expr.Prop("Age") >= 18 && Expr.Prop("Age") <= 60;

// LIKE/IN/NULL
Expr like = Expr.Prop("UserName").Contains("admin");
Expr startWith = Expr.Prop("UserName").StartsWith("admin");
Expr endWith = Expr.Prop("UserName").EndsWith("admin");
Expr inExpr = Expr.Prop("Id").In(1, 2, 3);
Expr notIn = Expr.Prop("Id").NotIn(new[] { 4, 5, 6 });
Expr isNull = Expr.Prop("Email").IsNull();
Expr isNotNull = Expr.Prop("Email").IsNotNull();

// 逻辑组合
Expr andExpr = Expr.And(cmp1, like);
Expr orExpr = Expr.Or(cmp1, cmp2);

// EXISTS 子查询
Expr existsExpr = Expr.Exists<Department>(d => d.Id == 1);
Expr foreignExpr = Expr.Foreign<Department>(Expr.Prop("Id") == 1);

// 聚合函数
Expr countExpr = Expr.Prop("Id").Count();
Expr countProp = Expr.Prop("Id").Count();
Expr sumExpr = Expr.Prop("Amount").Sum();
Expr avgExpr = Expr.Prop("Price").Avg();
Expr maxExpr = Expr.Prop("Score").Max();
Expr minExpr = Expr.Prop("Score").Min();
```

### 5.4 查询片段表达式 (SqlSegment)

**文件位置：** `LiteOrm.Common/SqlSegment/`

```csharp
// 使用构造函数组合创建完整查询
var table = TableInfoProvider.Default.GetTableView(typeof(User));
var whereExpr = new WhereExpr(new FromExpr(table), Expr.Prop("Age") > 18);
var fullQuery = new SelectExpr(whereExpr,
    Expr.Prop("Id"),
    Expr.Prop("UserName")
);

// 聚合查询需要使用 GroupBy，并通过 DataViewDAO 执行
var groupByExpr = new GroupByExpr(whereExpr, Expr.Prop("DeptId"));
var aggregateQuery = new SelectExpr(groupByExpr,
    Expr.Prop("DeptId"),
    Expr.Prop("Id").Count().As("UserCount")
);
```

---

## 六、DAO 层 API

### 6.1 ObjectDAO - 实体数据访问

**文件位置：** `LiteOrm/DAO/ObjectDAO.cs`

```csharp
// 插入单个实体
int Insert(T entity)
Task<bool> InsertAsync(T entity, CancellationToken cancellationToken = default)

// 更新单个实体
bool Update(T entity)
Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default)

// 插入或更新（根据主键判断）
bool UpdateOrInsert(T entity)
Task<bool> UpdateOrInsertAsync(T entity, CancellationToken cancellationToken = default)

// 根据实体删除
int Delete(T entity)
Task<bool> DeleteAsync(T entity, CancellationToken cancellationToken = default)

// 根据条件删除（使用LogicExpr）
int Delete(LogicExpr expr)
Task<int> DeleteAsync(LogicExpr expr, CancellationToken cancellationToken = default)

// 根据ID删除
int DeleteID(object id)
Task<bool> DeleteIDAsync(object id, CancellationToken cancellationToken = default)

// 批量删除ID
void BatchDeleteByKeys(IEnumerable ids)
Task BatchDeleteByKeysAsync(IEnumerable ids, CancellationToken cancellationToken = default)

// 批量插入
void BatchInsert(IEnumerable<T> entities)
Task BatchInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)

// 批量更新（使用单条UPDATE语句）
void BatchUpdate(IEnumerable<T> entities)
Task BatchUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)

// 批量插入或更新
void BatchUpdateOrInsert(IEnumerable<T> entities)
Task BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)

// 批量删除
void BatchDelete(IEnumerable<T> entities)
Task BatchDeleteAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)

// 查询单个（根据条件）
T SearchOne(Expr expr)
Task<T> SearchOneAsync(Expr expr, CancellationToken cancellationToken = default)

// 查询列表（根据条件）
List<T> Search(Expr expr)
Task<List<T>> SearchAsync(Expr expr, CancellationToken cancellationToken = default)

// 查询所有
List<T> GetAll()
Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default)

// 对每个结果执行操作
void ForEach(Expr expr, Action<T> func)
```

### 6.2 DataViewDAO - 视图查询（返回DataTable）

**文件位置：** `LiteOrm/DAO/DataViewDAO.cs`

DataViewDAO 返回 DataTable 格式的结果，支持聚合查询和 GroupBy：

```csharp
// 查询返回 DataTable
DataTable Search(Expr expr)
Task<DataTable> SearchAsync(Expr expr, CancellationToken cancellationToken = default)

// 指定字段查询
DataTable Search(string[] propertyNames, Expr expr)
Task<DataTable> SearchAsync(string[] propertyNames, Expr expr, CancellationToken cancellationToken = default)
```

**注意：** 聚合查询（使用 GroupBy 和聚合函数如 COUNT/SUM/AVG/MAX/MIN）必须使用 DataViewDAO，因为 EntityViewService 不支持 GroupBy。

### 6.3 ObjectViewDAO - 实体视图查询

**文件位置：** `LiteOrm/DAO/ObjectViewDAO.cs`

```csharp
// 查询视图模型（自动JOIN关联表）
List<TView> Search<TView>(Expr expr)
Task<List<TView>> SearchAsync<TView>(Expr expr, CancellationToken cancellationToken = default)

// 查询单个视图模型
TView SearchOne<TView>(Expr expr)
Task<TView> SearchOneAsync<TView>(Expr expr, CancellationToken cancellationToken = default)
```

---

## 七、文件结构速查

```
LiteOrm.Common/
├── Attributes/              # 特性定义（Table, Column, ForeignType等）
├── Classes/                 # 工具类（ExprConvert, Util等）
├── DAO/                     # 数据访问接口
│   ├── IObjectDAO.cs       # 实体DAO接口
│   ├── IObjectDAOAsync.cs  # 实体DAO异步接口
│   ├── IObjectViewDAO.cs   # 视图DAO接口
│   └── IObjectViewDAOAsync.cs
├── Expr/                   # 表达式系统（核心）
│   ├── Expr.cs             # Expr基类
│   ├── ExprExtensions.cs   # Expr扩展方法
│   ├── LogicExpr.cs        # 逻辑表达式
│   ├── PropertyExpr.cs     # 属性表达式
│   ├── LambdaExpr.cs       # Lambda转换
│   └── ...
├── MetaData/               # 元数据
│   ├── TableDefinition.cs  # 表定义
│   ├── SqlColumn.cs        # 列定义
│   └── TableInfoProvider.cs
├── Model/                   # 基础模型
│   └── ObjectBase.cs
├── Service/                 # 服务接口
│   ├── IEntityService.cs
│   ├── IEntityServiceAsync.cs
│   ├── IEntityViewService.cs
│   ├── IEntityViewServiceAsync.cs
│   └── EntityServiceExtensions.cs
├── SqlBuilder/              # SQL构建器接口
└── SqlSegment/              # SQL片段（Select/Where/OrderBy等）

LiteOrm/
├── Classes/                 # 核心类
│   ├── SqlGen.cs            # SQL生成器
│   ├── LiteOrmTableSyncInitializer.cs
│   ├── LiteOrmServiceExtensions.cs  # RegisterLiteOrm 扩展方法
│   └── ...
├── DAO/                     # DAO实现
│   ├── DAOBase.cs           # DAO基类
│   ├── ObjectDAO.cs         # 实体DAO实现
│   ├── ObjectViewDAO.cs     # 视图DAO实现
│   ├── DataViewDAO.cs       # DataTable DAO实现
│   └── ...
├── Service/                 # 服务实现
│   ├── EntityService.cs
│   └── EntityViewService.cs
├── DAOContext/              # DAO上下文
│   ├── DAOContext.cs
│   ├── DAOContextPool.cs
│   └── DAOContextPoolFactory.cs
└── SqlBuilder/               # SQL构建器实现
    ├── SqlBuilder.cs        # 默认SQL构建器
    ├── MySqlBuilder.cs      # MySQL
    ├── SqlServerBuilder.cs   # SQL Server
    ├── PostgreSqlBuilder.cs # PostgreSQL
    ├── OracleBuilder.cs      # Oracle
    └── SQLiteBuilder.cs     # SQLite
```

---

## 八、重要提示

1. **Expr vs LogicExpr**：
   - `Expr` 是所有表达式的基类
   - `LogicExpr` 是专用于 WHERE 条件的逻辑表达式
   - DAO 层的 Delete 方法参数是 `LogicExpr`
   - Service 层的 Delete 扩展方法也使用 `LogicExpr`

2. **Service vs ViewService**：
   - `IEntityService<T>` 用于基础 CRUD 操作（Insert/Update/Delete）
   - `IEntityViewService<T>` 用于查询操作（Search/Count/Exists）
   - `EntityService<T, TView>` 同时实现了实体服务和视图服务接口

3. **IQueryable 查询形式**：
   - 使用 `q => q.Where(...).OrderBy(...).Skip(...).Take(...)` 形式
   - 不支持 `orderBy` 和 `desc` 参数，应使用链式调用
   - 多个 `Where` 会自动合并为 AND 条件

4. **批量操作**：
   - `BatchUpdate` 使用单条 UPDATE 语句，而非 JOIN
   - `ParamCountLimit` 配置控制单条 SQL 最大参数数
   - 超过限制会自动分批执行

5. **SQL 注入防护**：始终使用参数化查询，不要拼接 SQL 字符串

6. **事务管理**：
   - 使用 `[Transaction]` 特性进行声明式事务
   - 避免在事务方法内使用异步操作

7. **异步方法**：
   - 所有 CRUD 操作都有对应的 Async 方法
   - 异步方法支持 `CancellationToken`

8. **服务继承**：
   - `EntityService<T>` 用于实体和视图类型相同的场景
   - `EntityService<T, TView>` 用于实体和视图类型不同的场景
   - 服务接口可以同时继承多个接口以获得完整功能

---

## 九、性能测试结果

基于 `LiteOrm.Benchmark` 项目（Windows 11, Intel Core i5-13400F 2.50GHz, .NET 10.0.103）的测试结果：

### 性能对比概览（BatchCount=100）

| 框架 | 插入性能 (ms) | 更新性能 (ms) | Upsert (ms) | 关联查询 (ms) | 内存分配 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **LiteOrm** | **3,743.9** | **4,684.3** | 5,535.7 | 974.9 | **295.97 KB** |
| FreeSql | 4,358.7 | 4,859.8 | **4,843.1** | 942.3 | 460.62 KB |
| SqlSugar | 4,126.6 | 5,377.7 | 9,355.1 | 1,664.3 | 476.13 KB |
| Dapper | 13,236.3 | 16,492.4 | 18,593.3 | **893.4** | 254.58 KB |
| EF Core | 21,973.8 | 21,571.2 | 22,967.5 | 6,680.8 | 1,965.32 KB |

### 性能对比概览（BatchCount=1000）

| 框架 | 插入性能 (ms) | 更新性能 (ms) | Upsert (ms) | 关联查询 (ms) | 内存分配 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **LiteOrm** | **10,711.9** | **16,472.2** | 16,733.4 | **6,061.1** | **870.27 KB** |
| FreeSql | 17,707.5 | 30,842.5 | **14,769.0** | 6,520.9 | 4,629.99 KB |
| SqlSugar | 15,775.0 | 35,522.5 | 66,357.1 | 12,304.3 | 4,571.36 KB |
| Dapper | 120,213.5 | 132,356.8 | 136,051.1 | 6,556.1 | 2,476.22 KB |
| EF Core | 169,846.8 | 149,932.5 | 157,037.7 | 12,422.7 | 18,118.07 KB |

### 性能对比概览（BatchCount=5000）

| 框架 | 插入性能 (ms) | 更新性能 (ms) | Upsert (ms) | 关联查询 (ms) | 内存分配 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **LiteOrm** | **40,268.4** | **68,069.3** | 60,711.4 | **39,060.2** | **4,082.59 KB** |
| FreeSql | 72,488.8 | 133,942.8 | **58,183.2** | 41,220.4 | 23,333.54 KB |
| SqlSugar | 76,643.9 | 194,130.4 | 885,872.8 | 63,744.0 | 23,196.37 KB |
| Dapper | 690,745.5 | 659,912.8 | 677,140.4 | 39,942.4 | 12,349.48 KB |
| EF Core | 824,700.5 | 749,069.8 | 794,845.9 | 49,403.4 | 80,230.09 KB |

完整测试报告请参考：[LiteOrm 性能评测报告](../LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md)

---

**文档版本：** 1.1  
**最后更新：** 2026年  
**适用版本：** LiteOrm >=8.0.7
