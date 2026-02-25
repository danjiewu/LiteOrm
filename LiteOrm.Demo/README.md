# LiteOrm.Demo

这是一个演示如何使用 LiteOrm 框架进行高效数据库开发的示例程序。

## 项目概述

`LiteOrm.Demo` 展示了 LiteOrm 在 .NET 8 环境下的核心特性，包括依赖注入集成、强大的表达式系统、Lambda 表达式查询（支持排序分页）、自动化关联查询、动态分表以及性能优化技巧。

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

## 核心功能演示

---

## 1. Lambda 表达式查询

LiteOrm 支持使用 `IQueryable` 形式的 Lambda 表达式进行查询，自动转换为 SQL，支持排序、分页、多条件合并等操作。

### 1.1 基础查询 (Where)

```csharp
var users = await userService.SearchAsync(
    q => q.Where(u => u.Age > 18)
);
```

### 1.2 排序 (OrderBy/OrderByDescending/ThenBy)

```csharp
// 按年龄升序
var sortedByAge = await userService.SearchAsync(
    q => q.Where(u => u.Age > 18).OrderBy(u => u.Age)
);

// 按年龄降序，再按用户名升序
var sortedComplex = await userService.SearchAsync(
    q => q.Where(u => u.Age > 18).OrderByDescending(u => u.Age).ThenBy(u => u.UserName)
);
```

### 1.3 分页 (Skip/Take)

```csharp
// 查询第 2 页（每页 10 条）
var pagedUsers = await userService.SearchAsync(
    q => q.Where(u => u.Age > 18)
          .OrderBy(u => u.CreateTime)
          .Skip(10)
          .Take(10)
);
```

### 1.4 多条件合并 (多个 Where)

多个 `Where()` 调用会自动合并为单个 WHERE 子句，条件之间用 AND 连接：

```csharp
var multiCondition = await userService.SearchAsync(
    q => q.Where(u => u.Age > 18)
          .Where(u => !string.IsNullOrEmpty(u.UserName))
          .Where(u => u.UserName.Contains("admin"))
);
// 等效于: WHERE (Age > 18 AND UserName IS NOT NULL AND UserName Contains admin)
```

**注意**: `SearchAsync` 的 Lambda 表达式目前支持 `Where`、`OrderBy`、`OrderByDescending`、`ThenBy`、`Skip`、`Take`，不支持 `GroupBy` 和 `Select`。

### 1.5 完整链式查询 (综合演示)

```csharp
var results = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18 && u.UserName.Contains("张"))
          .OrderByDescending(u => u.Id)
          .Skip(0).Take(10)
);
```

**示例输出：**
```text
=== Lambda 表达式查询演示 ===

[1] 基础 Lambda 查询 (Where):
  Lambda: q => q.Where(u => u.Age > 18)
  转换结果: UserView WHERE [Age] > 18

[2] Lambda 查询 + 排序 + 分页 (OrderBy + Skip + Take):
  说明: Skip(10) 跳过前10条，Take(20) 取20条（第2页，每页20条）
  Lambda: q => q.Where(u => u.Age > 18).OrderBy(u => u.Age).Skip(10).Take(20)
  转换结果: UserView WHERE [Age] > 18 ORDER BY [Age] ASC SKIP 10 TAKE 20

[5] Lambda 多条件查询 (多个 Where 自动合并为 AND):
  说明: 多个 Where() 调用会自动合并为一个 WHERE 子句
  转换结果: UserView WHERE ([Age] > 18 AND [UserName] != NULL AND [UserName] Contains Admin)
```

---

## 2. 基础表达式构建 (Expr)

LiteOrm 使用强类型属性名构建常见的 SQL 比较条件。

```csharp
// 二元表达式
Expr e1 = Expr.Prop(nameof(User.Age)) == 18;
Expr e2 = Expr.Prop(nameof(User.Age)) >= 18;
Expr e3 = Expr.Prop(nameof(User.UserName)) != "Admin";

// 表达式集合 (And/Or)
Expr set = (Expr.Prop("Age") > 10 & Expr.Prop("Age") < 20) | Expr.Prop("DeptId") == 1;

// Lambda 转换
var lambdaExpr = Expr.Exp<User>(u => u.Age > 18 && u.UserName.Contains("admin"));

// 逻辑表达式扩展功能 (直接从条件构建查询模型)
var queryModel = lambdaExpr.OrderBy(Expr.Prop("Id").Desc()).Section(0, 10);
```,oldString:
```

---

## 3. 自动化关联查询 (Join)

利用实体特性实现自动化的表连接，通过 `[ForeignType]` 标记外键关系，通过 `[ForeignColumn]` 在视图中获取关联表字段。

**实体定义示例：**
```csharp
// 主实体
public class SalesRecord : ObjectBase
{
    [Column("SalesUserId")]
    [ForeignType(typeof(User))]  // ForeignType 放在外键属性上
    public int SalesUserId { get; set; }
}

// 视图：包含关联的用户名
public class SalesRecordView : SalesRecord
{
    [ForeignColumn(typeof(User), Property = "UserName")]
    public string? UserName { get; set; }
}
```

**代码示例：**
```csharp
var sales = await salesService.SearchAsync(s => s.Amount > 100);
// 自动 JOIN User 表，结果中包含 UserName 字段
```

---

## 4. 动态分表查询 (IArged)

通过实现 `IArged` 接口，支持按参数路由物理表（如按月分表）。

**实体定义示例：**
```csharp
[Table("Sales_{0}")]  // 物理表名占位符
public class SalesRecord : ObjectBase, IArged
{
    [Column("SaleTime")]
    public DateTime SaleTime { get; set; }

    // 通过显式接口实现分表参数
    string[] IArged.TableArgs => [SaleTime.ToString("yyyyMM")];
}
```

**代码示例：**
```csharp
var record = new SalesRecord
{
    SaleTime = new DateTime(2024, 1, 15)  // 自动路由到 Sales_202401 表
};
await salesService.InsertAsync(record);
```

---

## 5. 声明式事务 (Transaction)

使用 `[Transaction]` 特性配合 AOP 拦截器实现无侵入的事务控制。

```csharp
public class BusinessService
{
    private readonly IUserService _userService;
    private readonly ISalesService _salesService;

    [Transaction]  // 自动事务管理
    public async Task<bool> RegisterUserWithInitialSaleAsync(User user, SalesRecord firstSale)
    {
        await _userService.InsertAsync(user);
        firstSale.SalesUserId = user.Id;
        await _salesService.InsertAsync(firstSale);
        return true;  // 自动提交
    }
}
```

---

## 6. 批量操作性能优化

```csharp
// 循环插入
foreach (var item in testData) await salesService.InsertAsync(item);
// 原生批量插入
await salesService.BatchInsertAsync(testData);

// 批量更新
await salesService.BatchUpdateAsync(testData);
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

## 7. JSON 序列化
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

## 8. 自定义 SQL 模板与 Lambda 扩展 (GenericSqlExpr & Lambda Handler & Function Sql Handler)

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

### 自定义 SQL 函数映射

```csharp
// Now 函数映射为 CURRENT_TIMESTAMP（对应 DateTime.Now 解析结果）
SqlBuilder.Instance.RegisterFunctionSqlHandler("Now", (functionName, args) => "CURRENT_TIMESTAMP");

// 特殊处理 IndexOf 和 Substring，支持 C# 到 SQL 的索引转换 (0-based -> 1-based)
SqlBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (functionName, args) => args.Count > 2 ?
    $"INSTR({args[0].Key}, {args[1].Key}, {args[2].Key}+1)-1" : $"INSTR({args[0].Key}, {args[1].Key})-1");
SqlBuilder.Instance.RegisterFunctionSqlHandler("Substring", (name, args) => args.Count > 2 ?
    $"SUBSTR({args[0].Key}, {args[1].Key}+1, {args[2].Key})" : $"SUBSTR({args[0].Key}, {args[1].Key}+1)");

// 为特定数据库（如 MySQL、SQLite）注册特定的日期加法逻辑
MySqlBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
    (functionName, args) => $"DATE_ADD({args[0].Key}, INTERVAL {args[1].Key} {functionName.Substring(3).ToUpper().TrimEnd('S')})");
SQLiteBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
    (functionName, args) => $"DATE({args[0].Key}, CAST({args[1].Key} AS TEXT)||' {functionName.Substring(3).ToLower()}')");
```

---

## 代码结构

```
LiteOrm.Demo/
├── Program.cs              # 演示程序入口，包含 DI 配置和初始化逻辑
├── ExprDemo.cs             # 表达式系统详尽示例
├── Demos/
│   ├── LambdaQueryDemo.cs  # Lambda 表达式查询演示（排序、分页、多条件）
│   └── BusinessDemo.cs     # 业务服务与事务控制演示
├── Models/
│   ├── User.cs             # 用户实体及视图（含关联配置）
│   ├── SalesRecord.cs      # 销售记录实体（含 IArged 分表配置）
│   └── Department.cs       # 部门实体及视图
└── Services/
    ├── UserService.cs      # 用户服务
    └── SalesService.cs     # 销售记录服务
```
