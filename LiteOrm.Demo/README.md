# LiteOrm.Demo

这是一个演示如何使用 LiteOrm 框架进行高效数据库开发的示例程序。

## 项目概述

`LiteOrm.Demo` 展示了 LiteOrm 在 .NET 8 / 10 环境下的核心特性，包括依赖注入集成、强大的表达式系统、自动化配置、分表查询、关联查询以及性能优化技巧。

## 如何运行

1. 确保已安装 .NET 8 或更高版本的 SDK。
2. 导航至项目根目录或 `LiteOrm.Demo` 目录。
3. 执行以下命令：
   ```bash
   dotnet run --project LiteOrm.Demo/LiteOrm.Demo.csproj
   ```

## 快速集成与初始化

`LiteOrm.Demo` 演示了如何通过简单的配置快速启动框架：

### 1. 宿主集成 (Program.cs)
在 `Program.cs` 中增加 `RegisterLiteOrm()` 调用即可自动完成服务扫描与多数据源注册：
```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm() // 自动扫描 [AutoRegister] 特性并初始化连接池
    .Build();
```

### 2. 配置说明 (appsettings.json)
连接池与数据源配置：
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
        "PoolSize": 16,
        "MaxPoolSize": 100,
        "ParamCountLimit": 2000,
        "SyncTable": true
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


## 核心功能演示与输出结果

---

## 基础用法 (Basic Usage)

### 1. 基础表达式构建 (Expr)
LiteOrm使用强类型属性名构建常见的 SQL 比较条件。

**代码示例：**
```csharp
Console.WriteLine("\n[BinaryExpr] 二元表达式:");
// 等于, 不等于, 大于, 小于, 大于等于, 小于等于
Expr e1 = Expr.Property(nameof(User.Age)) == 18;
Expr e2 = Expr.Property(nameof(User.Age)) >= 18;
Expr e3 = Expr.Property(nameof(User.UserName)) != "Admin";
Console.WriteLine($"  Age == 18: {e1}");
Console.WriteLine($"  Age >= 18: {e2}");
Console.WriteLine($"  UserName != 'Admin': {e3}");

Console.WriteLine("\n[ValueExpr] 值表达式:");
Expr v1 = (Expr)100; // 隐式转换
Expr v2 = Expr.Null;
Console.WriteLine($"  Value 100: {v1}");
Console.WriteLine($"  Value Null: {v2}");

Console.WriteLine("\n[PropertyExpr] 属性表达式:");
Expr p1 = Expr.Property("CreateTime");
Console.WriteLine($"  Property: {p1}");

Console.WriteLine("\n[UnaryExpr] 一元表达式:");
Expr u1 = !Expr.Property(nameof(User.UserName)).Contains("Guest");
Console.WriteLine($"  Not Contains Guest: {u1}");

Console.WriteLine("\n[ExprSet] 表达式集合 (And/Or):");
Expr set = (Expr.Property("Age") > 10 & Expr.Property("Age") < 20) | Expr.Property("DeptId") == 1;
Console.WriteLine($"  (Age > 10 AND Age < 20) OR DeptId == 1: {set}");

```

**示例输出：**
```text
[BinaryExpr] 二元表达式:
  Age == 18: [Age] = 18
  Age >= 18: [Age] >= 18
  UserName != 'Admin': [UserName] != Admin

[ValueExpr] 值表达式:
  Value 100: 100
  Value Null: NULL

[PropertyExpr] 属性表达式:
  Property: [CreateTime]

[UnaryExpr] 一元表达式:
  Not Contains Guest: [UserName] NotContains Guest

[ExprSet] 表达式集合 (And/Or):
  (Age > 10 AND Age < 20) OR DeptId == 1: (([Age] > 10 AND [Age] < 20) OR [DeptId] = 1)

```

### 2. Lambda 表达式转换
LiteOrm 支持通过 `EntityServiceExtensions` 将 C# Lambda 表达式自动转换为 `Expr` 对象，这种方式最接近原生的 LINQ 写法。

**代码示例：**
```csharp
Console.WriteLine("\n[LambdaExpr] Lambda 表达式转换演示:");
// 直接在 SearchAsync 中使用 Lambda，由扩展方法自动完成转换
var sales = await salesService.SearchAsync(s => (s.ShipTime ?? DateTime.Now) > s.SaleTime + TimeSpan.FromDays(3) && s.ProductName.Contains("电"));
Console.WriteLine($"  查询结果数量: {sales.Count}");
```

**示例输出：**
```text
[LambdaExpr] Lambda 表达式转换演示:
  C# Lambda: s => (s.ShipTime ?? DateTime.Now) > s.SaleTime + TimeSpan.FromDays(3) && s.ProductName.Contains("电")
  转换后的 Expr: (COALESCE([ShipTime],Now()) > [SaleTime] + 3.00:00:00 AND [ProductName] Contains 电)  
```

### 3. SQL 自动生成 (SqlGen)
演示如何使用 `SqlGen` 工具类将逻辑表达式手动转换为 SQL，可用于调试和验证表达式生成 SQL 的正确性。

**代码示例：**
```csharp
var expr = (Expr.Property(nameof(User.Age)) > 18) & (Expr.Property(nameof(User.UserName)).Contains("admin_"));
var sqlGen = new SqlGen(typeof(User));
var result = sqlGen.ToSql(expr);
Console.WriteLine($"  Expr: {expr}");
Console.WriteLine($"  生成 SQL: {result.Sql}");
Console.WriteLine("  参数列表:");
foreach (var p in result.Params)
{
    Console.WriteLine($"    - {p.Key} = {p.Value}");
}
```

**示例输出：**
```text
[SqlGen] 表达式生成 SQL 演示:
  Expr: ([Age] > 18 AND [UserName] Contains admin_)
  生成 SQL: ([User].[Age] > @0 AND [User].[UserName] LIKE @1 escape '/')
  参数列表:
    - 0 = 18
    - 1 = %admin/_%
```

### 4. 常用查询示例 (SearchAsync / SearchSectionAsync)
演示了通过 `Expr` 构建复杂条件、结合分页排序 (`PageSection`)、异步执行。

**示例 1: 使用 Lambda 异步查询（推荐）**
```csharp
var users1 = await userService.SearchAsync(u => u.Age > 25 && u.UserName.Length == 2);
Console.WriteLine($"\n[示例 1] 年龄 > 25 且名字长度为 2:");
foreach (var user in users1)
{
    Console.WriteLine($"    - ID:{user.Id}, 账号:{user.UserName}, 年龄:{user.Age}, 部门:{user.DeptName}");
}
```

**示例 2: 组合条件 + 分页 + 排序**
```csharp
var threeDaysAgo = DateTime.Now.AddDays(-3);
// 筛选 3 天内且未发货的订单，按金额降序取前 10 条
var section = new PageSection(0, 10).OrderByDesc(nameof(SalesRecord.Amount));
var sales2 = await salesService.SearchSectionAsync(s => s.SaleTime < threeDaysAgo && s.ShipTime == null, section, [currentMonth]);
Console.WriteLine($"\n[示例 2] 3天前销售且未发货的订单({currentMonth}月份)，并按金额降序取前10条:");
foreach (var sale in sales2)
{
    Console.WriteLine($"    - ID:{sale.Id}, 产品:{sale.ProductName}, 金额:{sale.Amount}, 销售员:{sale.UserName}, 销售时间:{sale.SaleTime:yyyy-MM-dd HH:mm} 发货时间:{sale.ShipTime:yyyy-MM-dd HH:mm}");
}
```

---

## 进阶与扩展功能 (Advanced & Extended)

### 1. 自动化关联查询 (Join)
利用实体特性 `[ForeignColumn]` 实现自动化的表连接，无需手写 JOIN 语句。只要模型定义了关联字段，查询主表时会自动带出。

**实体类定义示例：**
```csharp
namespace LiteOrm.Common;

[Table("Departments")]
public class Department 
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    // 使用 ForeignType 标记关联类型，Alias 用于多重关联或同一表多次关联时的别名冲突
    [Column("ParentId")]
    [ForeignType(typeof(Department), Alias = "Parent")]
    public int? ParentId { get; set; }

    [Column("ManagerId")]
    [ForeignType(typeof(User))]
    public int? ManagerId { get; set; }
}

public class DepartmentView : Department
{
    // 使用 ForeignColumn 根据别名或类型自动拉取关联表的字段
    [ForeignColumn("Parent", Property = "Name")]
    public string? ParentName { get; set; }

    [ForeignColumn(typeof(User), Property = "UserName")]
    public string? ManagerName { get; set; }
}
```

**代码示例：**
```csharp
Console.WriteLine("\n--- 关联查询演示 (自动带出负责人和上级部门) ---");
// SearchAsync(null) 查询全表，LiteOrm 自动识别 [ForeignColumn] 并生成 JOIN
var depts = await deptService.SearchAsync(null);
foreach (var d in depts)
{
    Console.WriteLine($" ID: {d.Id}, 部门: {d.Name}, 负责人: {d.ManagerName ?? "未指定"}, 上级: {d.ParentName ?? "顶级"}");
}
```

### 2. 动态分表查询 (IArged)
通过实现 `IArged` 接口，支持按参数路由物理表。在进行数据库操作时通过 `tableArgs` 传递分表后缀（如按月分表）。

**代码示例：**
实体类定义示例：
```csharp
namespace LiteOrm.Common;

[Table("SALES_{0}")] // 物理表名占位符
public class SalesRecord : IArged
{
    public DateTime SaleTime { get; set; }
    // 自动返回分表参数，如 "202401"
    string[] IArged.TableArgs => [SaleTime.ToString("yyyyMM")];
}
```
**查询代码示例：**
```csharp
 Console.WriteLine("\n--- IArged 分表查询演示 ---");
 string currentMonth = DateTime.Now.ToString("yyyyMM");
 // 传入 [currentMonth] 参数，LiteOrm 将其路由至 Sales_202601 物理表
 var sales = await salesService.SearchAsync(null, [currentMonth]);
 Console.WriteLine($"{currentMonth} 账期销售记录条数: {sales.Count}");
```

### 3. 声明式事务控制 (Transaction Attribute)
演示如何使用 `[Transaction]` 特性配合 AOP 拦截器实现无侵入的事务控制。传统的事务代码（Begin/Commit/Rollback）被解耦到拦截器中。

**代码示例：**
1. 在接口中定义事务特性：
```csharp
namespace LiteOrm.Service;

public interface IUserService:IEntityService<User>,IEntityViewService<UserView>,IEntityServiceAsync<User>,IEntityViewServiceAsync<UserView>
{
    // 其他方法省略
}

public interface ISalesService:IEntityService<SalesRecord>, IEntityViewService<SalesRecordView>, IEntityServiceAsync<SalesRecord>, IEntityViewServiceAsync<SalesRecordView>
{
    // 其他方法省略
}

public interface IBusinessService
{
    /// <summary>
    /// 注册用户并初始化一条销售记录
    /// </summary>
    [Transaction] // 标记该方法需要事务支持
    Task<bool> RegisterUserWithInitialSaleAsync(User user, SalesRecord firstSale);
}
```

2. 实现服务并启用拦截器：
```csharp
namespace LiteOrm.Service;

public class UserService : EntityService<User,UserView>, IUserService //继承 `EntityService` 基类可省略自动注册并注册拦截器特性
{
    // 其他方法省略
}
public class SalesService : EntityService<SalesRecord,SalesRecordView>, ISalesService
{
    // 其他方法省略
}

[AutoRegister(Lifetime = ServiceLifetime.Scoped)]
[Intercept(typeof(ServiceInvokeInterceptor))] // 启用自动注册并注册拦截器
public class BusinessService : IBusinessService
{
    private readonly IUserService _userService;
    private readonly ISalesService _salesService;

    public BusinessService(IUserService userService, ISalesService salesService)
    {
        _userService = userService;
        _salesService = salesService;
    }

    public async Task<bool> RegisterUserWithInitialSaleAsync(User user, SalesRecord firstSale)
    {
        // 1. 添加用户
        user.CreateTime = DateTime.Now;
        await _userService.InsertAsync(user);

        // 2. 设置销售记录的用户ID (Insert 后自增ID已填充到实体中)
        firstSale.SalesUserId = user.Id;
        firstSale.SaleTime = DateTime.Now;

        // 3. 添加销售记录
        await _salesService.InsertAsync(firstSale);

        // 如果需要测试回滚，可以取消下一行的注释
        // throw new Exception("模拟异常，测试事务回滚");

        // 如果其中任何一步失败，或者抛出异常，事务将自动由 ServiceInvokeInterceptor 回滚
        return true;
    }
}
```

**正常示例输出：**
```text
[2] 运行三层架构与事务控制演示...
正在尝试通过事务注册用户 ThreeTierUser 并创建初始订单...
事务执行成功！用户和订单已同时创建。
验证成功：用户 ID=31, 姓名=ThreeTierUser
```

**异常回滚示例输出：**

```text
[2] 运行三层架构与事务控制演示...
正在尝试通过事务注册用户 ThreeTierUser 并创建初始订单...
fail: LiteOrm.Service.ServiceInvokeInterceptor[0]
      <Exception>BusinessService.RegisterUserWithInitialSaleAsync({Id:19, UserName:ThreeTierUser, Age:25, CreateTime:2026-01-16 13:59:03},{Id:1866, ProductId:0, ProductName:Starter Pack, Amount:1, SaleTime:2026-01-16 13:59:03, SalesUserId:19}) System.Exception: 模拟异常，测试事务回滚
...
事务执行失败并已回滚: 模拟异常，测试事务回滚
回滚成功：用户未创建
```

### 4. 批量操作性能优化
对比底层驱动支持的原生 `BatchInsertAsync` 相比单条循环插入的显著性能提升，并可用 MySqlBulkCopy、SqlBulkCopy 等实现自定义的高性能批量插入提供器。

**代码示例：**
```csharp
// 循环插入
foreach (var item in testData) await salesService.InsertAsync(item);
// 原生批量插入
await salesService.BatchInsertAsync(testData);
```
**示例输出：**
```text
--- 批量插入 (BatchInsert) vs 循环插入 (Insert) 耗时对比 ---
循环插入 100 条: 551 ms
BatchInsert 批量插入 100 条: 10 ms
```

**MySqlBulkCopy方式批量插入方式示例代码：**
```csharp
[AutoRegister(Key = typeof(MySqlConnection))]
public class MySqlBulkCopyProvider : IBulkProvider
{  
    public int BulkInsert(DataTable dt, IDbConnection dbConnection, IDbTransaction transaction)
    {
        MySqlBulkCopy bulkCopy = new MySqlBulkCopy(dbConnection as MySqlConnection, transaction as MySqlTransaction);
        bulkCopy.DestinationTableName = dt.TableName;
        bulkCopy.ConflictOption = MySqlBulkLoaderConflictOption.Replace;
        for (int i = 0; i < dt.Columns.Count; i++)
        {
            bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i, dt.Columns[i].ColumnName));
        }
        return bulkCopy.WriteToServer(dt).RowsInserted;
    }
}
```

### 5. JSON 序列化
LiteOrm 支持 `Expr` 的 JSON 序列化与反序列化，适用于跨进程传输查询逻辑。

**代码示例：**
```csharp
var lambdaExpr = Expr.Exp<User>(u => u.Age > 18 && u.UserName.Contains("admin"));

var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
// 序列化结果
string json = JsonSerializer.Serialize((Expr)lambdaExpr, jsonOptions);
// 反序列化回 Expr 对象
var deserializedExpr = JsonSerializer.Deserialize<Expr>(json, jsonOptions);
```

**示例输出：**
```text
[LambdaExpr] Lambda 表达式转换演示:
  C# Lambda: u => u.Age > 18 && u.UserName.Contains("admin")
  转换后的 Expr: ([Age] > 18 AND [UserName] Contains admin)
  序列化结果: {
  "$": "set",
  "And": [
    {
      "$": ">",
      "Left": {"#":"Age"},
      "Right": 18
    },
    {
      "$": "contains",
      "Left": {"#":"UserName"},
      "Right": {"@":"admin"}
    }
  ]
}
  反序列化后的 Expr 类型: ExprSet
  反序列化后的 Expr 内容: ([Age] > 18 AND [UserName] Contains admin)
```

### 6. 自定义 SQL 模板与 Lambda 扩展 (GenericSqlExpr & Lambda Handler & Function Sql Handler)

**自定义 SQL 模板 (GenericSqlExpr)：**
演示如何通过 `GenericSqlExpr` 注册命名 SQL 片段（如包含 CTE 递归的复杂逻辑）并像普通条件一样组合使用。

```csharp
GenericSqlExpr.Register("DirectorDeptOrders", (ctx, builder, pms, arg) =>
{
    string paramName = pms.Count.ToString();
    pms.Add(new KeyValuePair<string, object>(paramName, arg));
    return $@"SalesUserId IN (
        SELECT u.Id FROM Users u 
        WHERE u.DeptId IN (
            WITH RECURSIVE SubDepts(Id) AS (
                SELECT Id FROM Departments WHERE ManagerId = {builder.ToSqlParam(paramName)}
                UNION ALL
                SELECT d.Id FROM Departments d JOIN SubDepts s ON d.ParentId = s.Id
            ) SELECT Id FROM SubDepts
        )
    )";
});
```

**自定义 Lambda 转换：**
您可以通过为特定数据库增加 SQL 函数映射或自定义成员处理器来扩展表达式能力（示例代码已在 LiteOrm 中默认实现，无需重复注册）。

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

**自定义 SQL 函数映射：**

```csharp
// Now 函数映射为 CURRENT_TIMESTAMP（对应 DateTime.Now 解析结果）


// 特殊处理 IndexOf 和 Substring，支持 C# 到 SQL 的索引转换 (0-based -> 1-based)
BaseSqlBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (functionName, args) => args.Count > 2 ?
    $"INSTR({args[0].Key}, {args[1].Key}, {args[2].Key}+1)-1" : $"INSTR({args[0].Key}, {args[1].Key})-1");
BaseSqlBuilder.Instance.RegisterFunctionSqlHandler("Substring", (name, args) => args.Count > 2 ?


// 为特定数据库（如 MySQL、SQLite）注册特定的日期加法逻辑
MySqlBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
    (functionName, args) => $"DATE_ADD({args[0].Key}, INTERVAL {args[1].Key} {functionName.Substring(3).ToUpper().TrimEnd('S')})");
SQLiteBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
    (functionName, args) => $"DATE({args[0].Key}, CAST({args[1].Key} AS TEXT)||' {functionName.Substring(3).ToLower()}')");
```
---

## 代码结构

- `Program.cs`: 演示程序入口，包含 DI 配置和初始化逻辑。
- `ExprDemo.cs`: 包含 `Expr` 表达式系统的详尽示例代码。
- `Demos/`: 按功能模块拆分的演示实现（基础、进阶、性能、事务等）。
- `Models/`: 包含实体定义及特性映射配置（`User`, `SalesRecord`, `Department`）。
- `Data/`: 数据库初始化及测试数据种子脚本。
- `Services`: 基于 `EntityService<T>` 的业务服务封装。