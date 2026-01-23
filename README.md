# LiteOrm

LiteOrm 是一个轻量级、高性能的 .NET ORM (对象关系映射) 框架，提供简单、灵活的数据库操作。

## 主要特性

*   **多数据库支持**：原生支持 SQL Server, MySQL (MariaDB), Oracle, 和 SQLite。
*   **灵活的查询引擎**：基于 `Expr` 的查询构建器，支持复杂的条件组合（And, Or, Not, In, Like 等）、连接查询（Join）、正则匹配等。
*   **动态查询生成**：支持根据条件动态生成查询语句，简化复杂查询的构建。
*   **自定义扩展**：允许注册自定义的 Lambda 表达式转换器和 SQL 函数映射，强大的扩展能力可以实现任意函数到 SQL 的映射。
*   **实体服务模式**：提供统一的 `ObjectDAO`、`ObjectViewDAO` 以及 `IEntityService<T>` 和 `IEntityViewService<T>` 接口及实现，封装三层常用的 CRUD 操作。
*   **异步支持**：所有核心操作均提供基于 `Task` 的异步版本。
*   **声明式映射**：使用 `[Table]`, `[Column]`, `[ForeignType]` 等特性定义实体与数据库表的映射关系。
*   **高性能批量操作**：支持大批量数据的插入、更新和删除。
*   **Autofac 与 ASP.NET Core 集成**：提供便捷的扩展方法，通过 Autofac 实现自动服务注册和拦截。


## 环境要求

*   .NET 8.0 或更高版本
*   .NET Standard 2.0 (兼容 .NET Framework 4.6.1+)

## 安装

可以通过 NuGet 包管理器安装 LiteOrm：

```bash
dotnet add package LiteOrm
```


## 快速入门 

使用 LiteOrm 进行基本的数据库操作简单易上手。以下是一个完整的示例，展示了如何定义实体、创建服务接口、配置 ASP.NET Core，并执行查询操作。

### 1. 定义实体

```csharp
[Table("USERS")]
public class User
{
    [Column("ID", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("USERNAME")]
    public string UserName { get; set; }

    [Column("EMAIL")]
    public string Email { get; set; }
}
```

### 2. 定义服务接口

```csharp
public interface IUserService : IEntityService<User>, IEntityViewService<User> 
{ 
    // 可以添加自定义业务方法
}

[AutoRegister(ServiceLifetime.Scoped)]
public class UserService : EntityService<User>, IUserService
{
    // 实现自定义业务逻辑
}
```

### 3. 在 ASP.NET Core 中启动配置

在 `Program.cs` 中注册 LiteOrm 服务：

```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm() // 注册 LiteOrm 和 Autofac
    .ConfigureServices(services => {
        // 其他配置
    })
    .Build();
```

### 4. 配置文件说明 (appsettings.json)

连接字符串与 Provider 配置：

```json
{
  "LiteOrm": {
    "Default": "DefaultConnection",
    "ConnectionStrings": [
      {
        "Name": "DefaultConnection",
        "ConnectionString": "Data Source=demo.db",
        "Provider": "Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite",
        "KeepAliveDuration": "00:10:00"
      }
    ]
  }
}
```

### 5. 使用服务进行查询

```csharp
public class MyController : ControllerBase
{
    private readonly IUserService _userService;

    public MyController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        // 1. 使用 Lambda 表达式查询 (推荐)
        var user = await _userService.SearchOneAsync(u => u.Id == id);
        
        // 2. 或者使用 Expr 构建器
        // var user = await _userService.SearchOneAsync(Expr.Property("UserName") == "admin");
        
        return Ok(user);
    }
}
```

## 查询表达式 (Expr) 

LiteOrm 提供了一套表达式构建工具，支持 Lambda 转换、JSON 序列化、SQL 自动生成。

### 1. 基础表达式构建
```csharp
// 组合逻辑条件 (And: & , Or: | , Not: !)
Expr condition = (Expr.Property("Age") > 18) & (Expr.Property("Status") == 1);

// IN 查询与集合查询
var ids = new[] { 1, 2, 3 };
Expr inCondition = Expr.Property("Id").In(ids);

// 模糊查询 (Contains, StartsWith, EndsWith)
Expr likeCondition = Expr.Property("UserName").Contains("admin");

// 正则匹配 (需数据库支持)
Expr regex = Expr.Property("Code").RegexpLike("^[A-Z][0-9]+$");
```

### 2. Lambda 表达式转换
LiteOrm 支持将标准的 C# Lambda 表达式转换为 `Expr` 对象，这种方式最接近 LINQ 写法。
```csharp
// 转换 u => u.Age > 18 && u.UserName.Contains("admin")
Expr expr = Expr.Exp<User>(u => u.Age > 18 && u.UserName.Contains("admin"));
```

### 3. JSON 序列化支持
`Expr` 对象天然支持序列化为 JSON，这在跨服务传递查询条件或存储动态规则时非常有用。
```csharp
string json = JsonSerializer.Serialize(expr);
// 反序列化回 Expr
Expr deserialized = JsonSerializer.Deserialize<Expr>(json);
```

### 4. SQL 自动生成 (SqlGen)
可以手动使用 `SqlGen` 将逻辑表达式转换为 SQL，这在调试或需要自定义执行逻辑时非常有用。
```csharp
var expr = (Expr.Property("Age") > 18) & (Expr.Property("UserName").Contains("admin"));
var sqlGen = new SqlGen(typeof(User));
var result = sqlGen.ToSql(expr);

Console.WriteLine($"生成 SQL: {result.Sql}");
    // 获取参数化查询的参数映射
    foreach (var p in result.Params)
    {
        Console.WriteLine($"{p.Key} = {p.Value}");
    }
}
```

### 5. 注册自定义表达式扩展与 SQL方言构造器

您可以通过自定义 Lambda 转换逻辑及为特定数据库增加 SQL 函数映射来扩展 LiteOrm 的表达式能力。

#### 注册 Lambda 方法/属性转换
```csharp

// DateTime.Now 解析为 CURRENT_TIMESTAMP，需配合 SQL 函数注册使用
LambdaExprConverter.RegisterMemberHandler(typeof(DateTime), "Now");
// 注册 Math 类的所有方法（默认转换为对应的函数调用）
LambdaExprConverter.RegisterMethodHandler(typeof(Math));
// 注册 String.Contains 为 BinaryOperator.Contains 表达式
LambdaExprConverter.RegisterMethodHandler(typeof(string), "Contains", (node, converter) =>
{
    var left = converter.Convert(node.Object);
    var right = converter.Convert(node.Arguments[0]);
    return new BinaryExpr(left, BinaryOperator.Contains, right);
});
```

#### 注册数据库方言 SQL 函数
```csharp
// Now 函数映射为 CURRENT_TIMESTAMP（对应 DateTime.Now 解析结果）
BaseSqlBuilder.Instance.RegisterFunctionSqlHandler("Now", (functionName, args) => "CURRENT_TIMESTAMP");

// 特殊处理 IndexOf 和 Substring，支持 C# 到 SQL 的索引转换 (0-based -> 1-based)
BaseSqlBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (functionName, args) => args.Count > 2 ?
    $"INSTR({args[0].Key}, {args[1].Key}, {args[2].Key}+1)-1" : $"INSTR({args[0].Key}, {args[1].Key})-1");
BaseSqlBuilder.Instance.RegisterFunctionSqlHandler("Substring", (name, args) => args.Count > 2 ?
    $"SUBSTR({args[0].Key}, {args[1].Key}+1, {args[2].Key})" : $"SUBSTR({args[0].Key}, {args[1].Key}+1)");

// 为特定数据库（如 MySQL、SQLite）注册特定的日期加法逻辑
MySqlBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
    (functionName, args) => $"DATE_ADD({args[0].Key}, INTERVAL {args[1].Key} {functionName.Substring(3).ToUpper().TrimEnd('S')})");
SQLiteBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
    (functionName, args) => $"DATE({args[0].Key}, CAST({args[1].Key} AS TEXT)||' {functionName.Substring(3).ToLower()}')");
```

## 高级功能示例

### 1. 自动化关联查询 (Join)
利用实体特性实现自动化的表连接，无需手写 JOIN 语句。
```csharp
public class UserView : User
{
    // 自动关联查询 Departments 表，拉取别名为 "Dept" 的 Name 字段
    [ForeignColumn("Dept", Property = "Name")]
    public string? DeptName { get; set; }
}

// 查询时，LiteOrm 自动识别 [ForeignColumn] 并生成高效的 LEFT JOIN
var users = await userService.SearchAsync(u => u.Age > 20);
```

### 2. 动态分表路由 (IArged)
通过实现 `IArged` 接口，可以实现按月或按维度自动路由物理表。
```csharp
[Table("SALES_{0}")] // 物理表名占位符
public class SalesRecord : IArged
{
    public DateTime SaleTime { get; set; }
    // 自动返回分表参数，如 "202401"
    string[] IArged.TableArgs => [SaleTime.ToString("yyyyMM")];
}

// 操作时自动路由至 SALES_202401 表
await salesService.InsertAsync(new SalesRecord { SaleTime = DateTime.Now });
```

### 3. 事务处理与声明式事务
支持显式事务管理和基于特性的自动事务管理。

#### 声明式事务 (推荐)
通过 `[Transaction]` 特性配合 AOP 拦截器实现无侵入的事务控制，支持跨服务、跨数据源的事务一致性保证。
```csharp
public interface IOrderService : IEntityService<Order>,  IEntityViewService<OrderView>, 
    IEntityServiceAsync<Order>, IEntityViewServiceAsync<OrderView> 
{
    [Transaction] // 方法执行时自动开启、提交或回滚事务
    Task<bool> CreateOrderAsync(Order order, List<OrderItem> items);
}

public class OrderService : EntityService<Order,OrderView>, IOrderService 
{
    // 方法实现中无需书写事务控制代码，由拦截器自动处理
    public async Task<bool> CreateOrderAsync(Order order, List<OrderItem> items) { ... }
}
```

#### 手动事务管理 (SessionManager)
```csharp
await SessionManager.Current.ExecuteInTransactionAsync(async (sm) =>
{
    userService.Update(user);
    logService.Insert(new UserLog { UserId = user.Id, Action = "Update Profile" });
    // 异常自动回滚，正常结束自动提交
});
```

### 4. 批量操作性能优化
利用底层驱动的原生批量能力，比单条循环插入快数十倍。您还可以实现 `IBulkProvider` 来利用特定数据库的高速导入功能进一步提高效率（如 `MySqlBulkCopy`、`SqlServerBulkCopy`、`OracleBulkCopy`）。

**MySqlBulkCopy 示例实现：**
```csharp
[AutoRegister(Key = typeof(MySqlConnection))]
public class MySqlBulkCopyProvider : IBulkProvider
{  
    public int BulkInsert(DataTable dt, IDbConnection dbConnection, IDbTransaction transaction)
    {
        var bulkCopy = new MySqlBulkCopy(dbConnection as MySqlConnection, transaction as MySqlTransaction)
        {
            DestinationTableName = dt.TableName,
            ConflictOption = MySqlBulkLoaderConflictOption.Replace
        };
        for (int i = 0; i < dt.Columns.Count; i++)
        {
            bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i, dt.Columns[i].ColumnName));
        }
        return (int)bulkCopy.WriteToServer(dt).RowsInserted;
    }
}
```

### 5. 部分字段更新 (UpdateValues)
按需更新，避免加载整个大对象或更新不必要的列。
```csharp
var updates = new Dictionary<string, object> 
{ 
    { nameof(User.Email), "new@example.com" }, 
    { "LastLogin", DateTime.Now }
};
// 使用 Expr 更新
await userService.UpdateValuesAsync(updates, u=>u.Email == null);
```

### 6. 字符串与表达式双向转换 (ExprConvert)
非常适合处理前端传递的简单过滤语法。
```csharp
// 将 ">20" 解析为 [Age] > 20
var ageProp = Util.GetProperty(typeof(User), "Age");
Expr parsed = ExprConvert.Parse(ageProp, ">20");

// 将来自 QueryString 的多个参数一次性转换为 Expr 列表
var conditions = Util.ParseQueryCondition(context.Request.Query, typeof(User));
```

## 更多示例

请参考 [`LiteOrm.Demo`](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo) 项目了解更多实际场景下的用法，包括：
- 完整的表达式系统演示 (`ExprDemo.cs`)
- 数据库初始化方案 (`DbInitializer.cs`)
- 分层架构下的服务实现 (`Services/`)

## 开源协议

[MIT](LICENSE)


