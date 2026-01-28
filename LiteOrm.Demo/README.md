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
    .RegisterLiteOrm() // 一键集成
    .Build();
```

### 2. 配置说明 (appsettings.json)
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

## 核心功能演示与输出结果

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

### 3.  JSON 序列化
LiteOrm 支持支持 `Expr` 的JSON 序列化与反序列化。

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

### 4. SQL 自动生成 (SqlGen)
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

### 5. 自动化关联查询 (Join)
利用实体特性 `[ForeignColumn]` 实现自动化的表连接，无需手写 JOIN 语句。只要模型定义了关联字段，查询主表时会自动带出。

**实体类定义示例：**
```csharp
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

**示例输出：**
```text
--- 关联查询演示 (自动带出负责人和上级部门) ---
 ID: 1, 部门: 集团总部, 负责人: Admin, 上级: 顶级
 ID: 2, 部门: 研发中心, 负责人: 研发主管, 上级: 集团总部
 ID: 3, 部门: 市场部, 负责人: 未指定, 上级: 集团总部
 ...
```

### 6. 动态分表查询 (IArged)
通过实现 `IArged` 接口，支持按参数路由物理表。在进行数据库操作时通过 `tableArgs` 传递分表后缀（如按月分表）。

**代码示例：**
实体类定义示例：
```csharp
[Table("SALES_{0}")] // 物理表名占位符
public class SalesRecord : IArged
{
    public DateTime SaleTime { get; set; }
    // 自动返回分表参数，如 "202401"
    string[] IArged.TableArgs => [SaleTime.ToString("yyyyMM")];
}
```
查询代码示例：
```csharp
 Console.WriteLine("\n--- IArged 分表查询演示 ---");
 string currentMonth = DateTime.Now.ToString("yyyyMM");
 // 传入 [currentMonth] 参数，LiteOrm 将其路由至 Sales_202601 物理表
 var sales = await salesService.SearchAsync(null, [currentMonth]);
 Console.WriteLine($"{currentMonth} 账期销售记录条数: {sales.Count}");
 foreach (var sale in sales)
 {
     Console.WriteLine($"    - ID:{sale.Id}, 产品:{sale.ProductName}, 金额:{sale.Amount}, 销售员:{sale.UserName}, 销售时间:{sale.SaleTime:yyyy-MM-dd HH:mm}");
 }
```

**示例输出：**
```text
--- IArged 分表查询演示 ---
202601 账期销售记录条数: 30
    - ID:1, 产品:降噪耳机, 金额:1599, 销售员:广州负责人, 销售时间:2026-01-03 18:35
    - ID:2, 产品:机械键盘, 金额:499, 销售员:李四, 销售时间:2026-01-07 18:27
    ...
```

### 7. 综合查询示例 (SearchAsync / SearchSectionAsync)
演示了通过 `Expr` 构建复杂条件、结合分页排序 (`PageSection`)、异步执行以及自定义 SQL 模板 (`GenericSqlExpr`)。

**代码示例：**
```csharp
string currentMonth = DateTime.Now.ToString("yyyyMM");

// 示例 1: 使用 Lambda 异步查询（推荐）
var users1 = await userService.SearchAsync(u => u.Age > 25 && u.UserName.Length == 2);
Console.WriteLine($"\n[示例 1] 年龄 > 25 且名字长度为 2:");
foreach (var user in users1)
{
    Console.WriteLine($"    - ID:{user.Id}, 账号:{user.UserName}, 年龄:{user.Age}, 部门:{user.DeptName}");
}

// 示例 2: 组合条件 + 分页 + 排序
var threeDaysAgo = DateTime.Now.AddDays(-3);
// 筛选 3 天内且未发货的订单，按金额降序取前 10 条
var section = new PageSection(0, 10).OrderByDesc(nameof(SalesRecord.Amount));
var sales2 = await salesService.SearchSectionAsync(s => s.SaleTime < threeDaysAgo && s.ShipTime == null, section, [currentMonth]);
Console.WriteLine($"\n[示例 2] 3天前销售且未发货的订单({currentMonth}月份)，并按金额降序取前10条:");
foreach (var sale in sales2)
{
    Console.WriteLine($"    - ID:{sale.Id}, 产品:{sale.ProductName}, 金额:{sale.Amount}, 销售员:{sale.UserName}, 销售时间:{sale.SaleTime:yyyy-MM-dd HH:mm} 发货时间:{sale.ShipTime:yyyy-MM-dd HH:mm}");
}

// 示例 3: 用 GenericSqlExpr 自定义SQL方式查询销售总监所负责部门及下级部门 3 天内的销售订单
// 注册名为 "DirectorDeptOrders" 的表达式
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

var directorId = 6; // 销售总监 ID
var threeDaysAgo = DateTime.Now.AddDays(-3);
// 组合 GenericSqlExpr 与时间过滤条件
var complexExpr = GenericSqlExpr.Get("DirectorDeptOrders", directorId) & Expr.Property(nameof(SalesRecord.SaleTime)) > threeDaysAgo;
var directorOrders = await salesService.SearchAsync(complexExpr, [currentMonth]);

Console.WriteLine($"\n[示例 3] 销售总监(ID:{directorId})负责部门及下级部门 3 天内的订单 ({currentMonth}):");
foreach (var sale in directorOrders)
{
    Console.WriteLine($"    - ID:{sale.Id}, 产品:{sale.ProductName}, 金额:{sale.Amount}, 销售员:{sale.UserName}, 销售时间:{sale.SaleTime:yyyy-MM-dd HH:mm} 发货时间:{sale.ShipTime:yyyy-MM-dd HH:mm}");
}
```

**示例输出：**
```text
[示例 1] 年龄 > 25 且名字长度为 2:
  查询结果数量: 3
    - ID:4, 账号:李四, 年龄:28, 部门:研发中心, 创建日期:2026-01-15
    ...

[示例 2] 3天前销售且未发货的订单(202601月份)，并按金额降序取前10条:
  Expr 序列化结果: {
  "$": "set",
  "And": [
    {
      "$": "<",
      "Left": {"#":"SaleTime"},
      "Right": {"@":"2026-01-12T07:58:12.3609501+08:00"}
    },
    {
      "$": "==",
      "Left": {"#":"ShipTime"},
      "Right": null
    }
  ]
}
  查询结果数量: 5
    - ID:34, 产品:升降桌, 金额:3299, 销售员:上海负责人, 销售时间:2026-01-05 12:34 发货时间:
    ...

[示例 3] 销售总监(ID:6)负责部门及下级部门 3 天内的订单 (202601):
  Expr 序列化结果: {
  "$": "set",
  "And": [
    {
      "$": "sql",
      "Key": "DirectorDeptOrders",
      "Arg": 6
    },
    {
      "$": ">",
      "Left": {"#":"SaleTime"},
      "Right": {"@":"2026-01-12T07:58:12.3609501+08:00"}
    }
  ]
}
  结果数量: 14
    - ID:3, 产品:电竞主机, 金额:8999, 销售员:销售总监, 销售时间:2026-01-24 08:18 发货时间:2026-01-26 04:18
    ...
```

### 8. 批量操作性能优化
对比了底层驱动支持的原生 `BatchInsertAsync` 相比单条循环插入的显著性能提升，并可用 MySqlBulkCopy、SqlBulkCopy 等实现自定义的高性能批量插入提供器。

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
        for (int i = 0; i &lt; dt.Columns.Count; i++)
        {
            bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i, dt.Columns[i].ColumnName));
        }
        return bulkCopy.WriteToServer(dt).RowsInserted;
    }
}
```

### 9. 声明式事务控制 (Transaction Attribute)
演示如何使用 `[Transaction]` 特性配合 AOP 拦截器实现无侵入的事务控制。传统的事务代码（Begin/Commit/Rollback）被解耦到拦截器中。

**代码示例：**

1. 在接口中定义事务特性：
```csharp
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
[AutoRegister(Lifetime = ServiceLifetime.Scoped), Intercept(typeof(ServiceInvokeInterceptor))] // 启用自动注册并注册拦截器，如果继承 `EntityService`、`EntityViewService` 基类可省略此特性
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

### 10.自定义 Lambda 表达式转换与 SQL 函数映射扩展

您可以通过自定义 Lambda 转换逻辑及为特定数据库增加 SQL 函数映射来扩展 LiteOrm 的表达式能力。（示例代码已在 LiteOrm 中默认实现，无需重复注册）

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

## 代码结构

- `Program.cs`: 演示程序入口，包含 DI 配置和初始化逻辑。
- `ExprDemo.cs`: 包含 `Expr` 表达式系统的详尽示例代码。
- `Models/`: 包含实体定义及特性映射配置（`User`, `SalesRecord`, `Department`）。
- `Data/`: 数据库初始化及测试数据种子脚本。
- `Services/`: 基于 `EntityService<T>` 的业务服务封装。