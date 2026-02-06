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

## 一、实体映射 (Entity Mapping)

### 1.1 实体类定义

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

### 1.2 关联特性

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

// 视图模型包含关联字段
public class OrderView : Order
{
    [ForeignColumn(typeof(User), Property = "UserName")]  // 从User表带出UserName
    public string UserName { get; set; }
}
```

### 1.3 分表支持

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

## 二、Expr 表达式系统 (核心查询引擎)

### 2.1 Expr 类型继承体系

**文件位置：** `LiteOrm.Common/Expr/`

```
Expr (基类)
├── LogicExpr (逻辑表达式，用于WHERE条件)
│   ├── LogicBinaryExpr (二元逻辑: And/Or)
│   ├── LogicSet (IN/NOT IN集合)
│   └── NotExpr (NOT否定)
├── ValueExpr (值表达式)
│   ├── ValueBinaryExpr (二元比较: >/</==/!=)
│   ├── ValueSet (值集合)
│   └── UnaryExpr (一元运算: +/-)
├── PropertyExpr (属性引用)
├── AggregateFunctionExpr (聚合函数: COUNT/SUM/AVG/MAX/MIN)
├── FunctionExpr (函数调用)
├── ForeignExpr (关联表字段)
└── LambdaExpr (Lambda转换结果)
```

### 2.2 Lambda 自动转换

**文件位置：** `LiteOrm.Common/Expr/ExprExtensions.cs`

```csharp
// Lambda 转 Expr（最常用的方式）
Expr expr = Expr.Exp<User>(u => u.Age > 18 && u.UserName.Contains("admin"));
// 生成 SQL: WHERE (Age > 18 AND UserName LIKE '%admin%')

// 使用 IQueryable 形式（推荐，支持排序和分页）
var users = await userViewService.SearchAsync(
    q => q.Where(u => u.Age > 18)
         .OrderBy(u => u.Id)
         .Skip(0)
         .Take(10)
);

// 多条件合并（多个Where自动合并为AND）
var result = await userViewService.SearchAsync(
    q => q.Where(u => u.Age > 18)
         .Where(u => u.UserName != null)
         .Where(u => u.UserName.Contains("admin"))
);
```

### 2.3 手动构建表达式

**文件位置：** `LiteOrm.Common/Expr/Expr.cs`

```csharp
// 属性引用
Expr prop = Expr.Property("Age");
Expr prop2 = Expr.Property(nameof(User.UserName));

// 比较运算
Expr cmp1 = Expr.Property("Age") > 18;
Expr cmp2 = Expr.Property("UserName") == "admin";
Expr cmp3 = Expr.Property("Age") >= 18 && Expr.Property("Age") <= 60;

// LIKE/IN/NULL
Expr like = Expr.Property("UserName").Contains("admin");
Expr startWith = Expr.Property("UserName").StartsWith("admin");
Expr endWith = Expr.Property("UserName").EndsWith("admin");
Expr inExpr = Expr.Property("Id").In(1, 2, 3);
Expr notIn = Expr.Property("Id").NotIn(new[] { 4, 5, 6 });
Expr isNull = Expr.Property("Email").IsNull();
Expr isNotNull = Expr.Property("Email").IsNotNull();

// 逻辑组合
Expr andExpr = Expr.And(cmp1, like);
Expr orExpr = Expr.Or(cmp1, cmp2);

// 聚合函数
Expr countExpr = Expr.Count();
Expr countProp = Expr.Count("Id");
Expr sumExpr = Expr.Sum("Amount");
Expr avgExpr = Expr.Avg("Price");
Expr maxExpr = Expr.Max("Score");
Expr minExpr = Expr.Min("Score");
```

### 2.4 查询片段表达式 (SqlSegment)

**文件位置：** `LiteOrm.Common/SqlSegment/`

```csharp
// 使用 Expr 拼接构造查询
var selectExpr = Expr.Table<User>()
    .Where(Expr.Property("Age") > 18)
    .GroupBy(Expr.Property("DeptId"))
    .Having(AggregateFunctionExpr.Count > 1)
    .Select(Expr.Property("DeptId"), AggregateFunctionExpr.Count)
    .OrderBy((Expr.Property("DeptId").Asc()))
    .Section(10, 20);
```

```csharp
// 使用Lambda 表达式构造查询
var selectExpr = Expr.Query<User>()
    .Where(u=>u.Age>18)
    .GroupBy
                
```

---

## 三、DAO 层 API

### 3.1 ObjectDAO - 实体数据访问

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
void BatchDeleteID(IEnumerable ids)
Task BatchDeleteIDAsync(IEnumerable ids, CancellationToken cancellationToken = default)

// 批量插入
void BatchInsert(IEnumerable<T> entities)
Task BatchInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)

// 批量更新
void BatchUpdate(IEnumerable<T> entities)
Task BatchUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)

// 批量插入或更新
void BatchUpdateOrInsert(IEnumerable<T> entities)
Task BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)

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

### 3.2 DataViewDAO - 视图查询（返回DataTable）

**文件位置：** `LiteOrm/DAO/DataViewDAO.cs`

```csharp
// 查询返回 DataTable
DataTable Search(Expr expr)
Task<DataTable> SearchAsync(Expr expr, CancellationToken cancellationToken = default)

// 指定字段查询
DataTable Search(string[] propertyNames, Expr expr)
Task<DataTable> SearchAsync(string[] propertyNames, Expr expr, CancellationToken cancellationToken = default)
```

### 3.3 ObjectViewDAO - 实体视图查询

**文件位置：** `LiteOrm/DAO/ObjectViewDAO.cs`

```csharp
// 查询视图模型（自动JOIN关联表）
List<TView> Search<TView>(Expr expr)
Task<List<TView>> SearchAsync<TView>(Expr expr, CancellationToken cancellationToken = default)

// 查询单个视图模型
TView SearchOne<TView>(Expr expr)
Task<TView> SearchOneAsync<TView>(Expr expr, CancellationToken cancellationToken = default)

// IQueryable 形式查询
List<T> Search<T>(Func<IQueryable<T>, IQueryable<T>> queryAction)
```

---

## 四、Service 层 API

### 4.1 IEntityService - 实体服务接口

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
void BatchDeleteID(IEnumerable ids)

// 根据条件删除（使用Lambda）
int Delete(LogicExpr expr, params string[] tableArgs)
```

### 4.2 IEntityViewService - 视图服务接口

**文件位置：** `LiteOrm.Common/Service/IEntityViewService.cs`

```csharp
// 查询视图模型
List<TView> Search<TView>(Expr expr, params string[] tableArgs)
List<TView> Search<TView>(Func<IQueryable<TView>, IQueryable<TView>> queryAction, params string[] tableArgs)

// 查询单个
TView SearchOne<TView>(Expr expr, params string[] tableArgs)

// 指定字段查询
DataTable SearchDataTable(string[] propertyNames, Expr expr, params string[] tableArgs)

// 检查是否存在
bool Exists(Expr expr, params string[] tableArgs)

// 统计数量
int Count(Expr expr, params string[] tableArgs)
```

### 4.3 EntityService - 服务基类

**文件位置：** `LiteOrm/Service/EntityService.cs`

```csharp
public class UserService : EntityService<User>, IUserService
{
    // 继承所有基础 CRUD 方法
    // 可直接使用：Insert, Update, Delete, Search, SearchOne 等
}
```

### 4.4 扩展方法（Lambda查询）

**文件位置：** `LiteOrm.Common/Service/EntityServiceExtensions.cs`

```csharp
// Lambda 形式条件查询（仅条件）
var users = userViewService.Search(u => u.Age > 18);
var users = userViewService.Search(u => u.Age > 18 && u.UserName.Contains("admin"));

// Lambda 形式条件删除
int deleted = userService.Delete(u => u.Age < 18);

// IQueryable 形式查询（支持 Where/OrderBy/Skip/Take）
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

## 五、配置与注册

### 5.1 appsettings.json 配置

```json
{
  "LiteOrm": {
    "Default": "DefaultConnection",
    "DataSources": [
      {
        "Name": "DefaultConnection",
        "ConnectionString": "Server=localhost;Database=mydb;Uid=root;Pwd=123456;",
        "Provider": "MySql.Data.MySqlConnection, MySql.Data",
        "KeepAliveDuration": "00:10:00",
        "PoolSize": 20,
        "MaxPoolSize": 100,
        "ParamCountLimit": 2000,
        "SyncTable": true
      }
    ]
  }
}
```

### 5.2 Generic Host / Console 注册

```csharp
// Program.cs
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()  // 自动扫描 AutoRegister 特性并初始化连接池
    .Build();
```

**注意：** LiteOrm 使用 `RegisterLiteOrm()` 进行注册，而非 `AddLiteOrm()`。

---

## 六、常用查询模式

### 6.1 基础 CRUD

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

### 6.2 条件查询

```csharp
// 单一条件
var users = userViewService.Search(u => u.Age > 18);

// 多条件AND
var users = userViewService.Search(u => u.Age > 18 && u.UserName.Contains("admin"));

// IN 查询
var users = userViewService.Search(u => new[] { 1, 2, 3 }.Contains(u.Id));

// IS NULL
var users = userViewService.Search(u => u.Email == null);

// LIKE
var users = userViewService.Search(u => u.UserName.Contains("admin"));
```

### 6.3 排序与分页

```csharp
// 排序（IQueryable 形式）
var sorted = userViewService.Search(q => q.Where(u => u.Age > 18).OrderBy(u => u.Age));
var sortedDesc = userViewService.Search(q => q.Where(u => u.Age > 18).OrderByDescending(u => u.Age));

// 分页
var page1 = userViewService.Search(q => q.Where(u => u.Age > 18).OrderBy(u => u.Id).Skip(0).Take(10));
var page2 = userViewService.Search(q => q.Where(u => u.Age > 18).OrderBy(u => u.Id).Skip(10).Take(10));

// 组合排序
var users = userViewService.Search(
    q => q.Where(u => u.Age > 18)
         .OrderBy(u => u.Age)
         .ThenByDescending(u => u.Id)
         .Skip(0)
         .Take(10)
);
```

### 6.4 聚合查询

```csharp
// 使用 Expr 聚合
var expr = new SelectExpr
{
    Source = new TableExpr(typeof(User)),
    Selects = new List<ValueTypeExpr>
    {
        Expr.Count("Id"),
        Expr.Avg("Age"),
        Expr.Sum("Amount")
    }
};
var result = userViewService.Search(expr);
```

### 6.5 关联查询

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

### 6.6 事务操作

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

## 七、文件结构速查

```
LiteOrm.Common/
├── Attributes/           # 特性定义（Table, Column, ForeignType等）
├── Classes/               # 工具类（ExprConvert, Util等）
├── DAO/                   # 数据访问接口
│   ├── IObjectDAO.cs     # 实体DAO接口
│   ├── IObjectDAOAsync.cs # 实体DAO异步接口
│   ├── IObjectViewDAO.cs # 视图DAO接口
│   └── IObjectViewDAOAsync.cs
├── Expr/                  # 表达式系统（核心）
│   ├── Expr.cs           # Expr基类
│   ├── ExprExtensions.cs # Expr扩展方法
│   ├── LogicExpr.cs      # 逻辑表达式
│   ├── PropertyExpr.cs    # 属性表达式
│   └── LambdaExpr.cs      # Lambda转换
├── MetaData/             # 元数据
│   ├── TableDefinition.cs # 表定义
│   ├── SqlColumn.cs       # 列定义
│   └── TableInfoProvider.cs
├── Model/                 # 基础模型
│   └── ObjectBase.cs
├── Service/               # 服务接口
│   ├── IEntityService.cs
│   ├── IEntityServiceAsync.cs
│   ├── IEntityViewService.cs
│   └── EntityServiceExtensions.cs
├── SqlBuilder/            # SQL构建器接口
└── SqlSegment/            # SQL片段（Select/Where/OrderBy等）

LiteOrm/
├── Core/                   # 核心实现
│   ├── SqlGen.cs         # SQL生成器
│   ├── LiteOrmTableSyncInitializer.cs
│   └── LiteOrmServiceExtensions.cs  # RegisterLiteOrm 扩展方法
├── DAO/                   # DAO实现
│   ├── DAOBase.cs        # DAO基类
│   ├── ObjectDAO.cs      # 实体DAO实现
│   ├── ObjectViewDAO.cs   # 视图DAO实现
│   └── DataViewDAO.cs    # DataTable DAO实现
├── Service/               # 服务实现
│   ├── EntityService.cs
│   └── EntityViewService.cs
└── SqlBuilder/            # SQL构建器实现
    ├── SqlBuilder.cs     # 默认SQL构建器
    ├── MySqlBuilder.cs   # MySQL
    ├── SqlServerBuilder.cs # SQL Server
    ├── PostgreSqlBuilder.cs # PostgreSQL
    ├── OracleBuilder.cs   # Oracle
    └── SQLiteBuilder.cs   # SQLite
```

---

## 八、重要提示

1. **Expr vs LogicExpr**：
   - `Expr` 是所有表达式的基类
   - `LogicExpr` 是专用于 WHERE 条件的逻辑表达式
   - DAO 层的 Delete 方法参数是 `LogicExpr`

2. **Service vs ViewService**：
   - `IEntityService<T>` 用于基础 CRUD 操作（Insert/Update/Delete）
   - `IEntityViewService<T>` 用于查询操作（Search/Count/Exists）
   - Lambda 扩展方法主要在 `IEntityViewService` 上使用

3. **IQueryable 查询形式**：
   - 使用 `q => q.Where(...).OrderBy(...).Skip(...).Take(...)` 形式
   - 不支持 `orderBy` 和 `desc` 参数，应使用链式调用

4. **SQL 注入防护**：始终使用参数化查询，不要拼接 SQL 字符串

5. **事务管理**：
   - 使用 `[Transaction]` 特性进行声明式事务
   - 避免在事务方法内使用异步操作

6. **批量操作**：
   - `ParamCountLimit` 配置控制单条 SQL 最大参数数
   - 超过限制会自动分批执行

7. **异步方法**：
   - 所有 CRUD 操作都有对应的 Async 方法
   - 异步方法支持 `CancellationToken`

---

## 九、快速示例代码

```csharp
// 1. 定义实体
[Table("Users")]
public class User
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    
    [Column("Name")]
    public string Name { get; set; }
    
    [Column("Age")]
    public int Age { get; set; }
}

// 2. 定义服务接口
public interface IUserService : IEntityService<User>
{
    // 可添加自定义方法
}

public interface IUserViewService : IEntityViewService<User>
{
    // 可添加自定义查询方法
}

// 3. 实现服务
public class UserService : EntityService<User>, IUserService { }
public class UserViewService : EntityViewService<User>, IUserViewService { }

// 4. 使用
var user = new User { Name = "Admin", Age = 30 };
userService.Insert(user);

var admin = userViewService.SearchOne(u => u.Name == "Admin");
var youngUsers = userViewService.Search(u => u.Age < 30);

// 排序分页查询
var paged = userViewService.Search(
    q => q.Where(u => u.Age > 18)
         .OrderBy(u => u.Age)
         .Skip(0)
         .Take(10)
);

// 条件删除
userService.Delete(u => u.Age < 18);
```

---

**文档版本：** 1.1  
**最后更新：** 2025年  
**适用版本：** LiteOrm 2.x
