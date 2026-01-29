# LiteOrm.Demo

这是一个演示如何使用 LiteOrm 框架进行高效数据库开发的示例程序。

## 项目概述

`LiteOrm.Demo` 展示了 LiteOrm 在 .NET 8 / 10 环境下的核心特性。本项目代码已按功能模块拆分至 `Demos/` 文件夹，方便查阅。

## 如何运行

1. 确保已安装 .NET 8 或更高版本的 SDK。
2. 导航至 `LiteOrm.Demo` 目录。
3. 执行以下命令：
   ```bash
   dotnet run
   ```

## 快速集成与初始化

### 1. 宿主集成 (Program.cs)
在 `Program.cs` 中增加 `RegisterLiteOrm()` 调用即可自动完成服务扫描与多数据源注册：
```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm() // 一键集成：扫描特性、配置连接池、注入服务
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

**配置字段详解：**
- **Default**: 指定默认使用的连接名称，未显式指定数据库的实体将使用此数据源。
- **Name**: 标识连接配置的 Key，在多数据源切换时使用。
- **Provider**: LiteOrm 的核心适配参数。格式为 `类的全路径, 程序集名称`。支持兼容 `IDbConnection` 的原生驱动，如 `Microsoft.Data.Sqlite` 或 `MySqlConnector`。
- **KeepAliveDuration**: 用于某些需要心跳维护或长时间开启的存储连接。

---

## 基础用法 (Basic Usage)

### 1. 基础表达式构建 (Expr) [参考 Demos\ExprBasicDemo.cs]
LiteOrm 使用强类型属性名构建常见的 SQL 过滤条件。

**代码示例：**
```csharp
// 二元表达式: ==, !=, >, <, >=, <=
Expr e1 = Expr.Property(nameof(User.Age)) == 18;
// 逻辑集合: & (And), | (Or), ! (Not)
Expr set = (Expr.Property("Age") > 10 & Expr.Property("Age") < 20) | Expr.Property("DeptId") == 1;
```

**演示输出：**
```text
[BinaryExpr] 二元表达式:
  Age == 18: [Age] = 18

[ExprSet] 表达式集合 (And/Or):
  (Age > 10 AND Age < 20) OR DeptId == 1: (([Age] > 10 AND [Age] < 20) OR [DeptId] = 1)
```

### 2. Lambda 表达式转换 [参考 Demos\ExprAdvancedDemo.cs]
LiteOrm 支持通过扩展方法直接使用 C# Lambda 表达式进行查询，这种方式最接近原生的 LINQ 写法。

**代码示例：**
```csharp
// 自动转换为：WHERE (COALESCE([ShipTime], Now()) > [SaleTime] + 3天 AND [ProductName] LIKE '%电%')
var sales = await salesService.SearchAsync(s => (s.ShipTime ?? DateTime.Now) > s.SaleTime + TimeSpan.FromDays(3) && s.ProductName.Contains("电"));
```

**演示输出：**
```text
[LambdaExpr] Lambda 表达式转换演示:
  C# Lambda: s => (s.ShipTime ?? DateTime.Now) > s.SaleTime + TimeSpan.FromDays(3) && s.ProductName.Contains("电")
  转换后的 Expr: (COALESCE([ShipTime],Now()) > [SaleTime] + 3.00:00:00 AND [ProductName] Contains 电)
```

### 3. JSON 序列化支持 [参考 Demos\ExprAdvancedDemo.cs]
`Expr` 对象天然支持 JSON 序列化，非常适合在分布式系统中传递查询条件。

**代码示例：**
```csharp
string json = JsonSerializer.Serialize((Expr)lambdaExpr, jsonOptions);
```

**序列化内容展示：**
```json
{
  "$": "set",
  "And": [
    { "$": ">", "Left": {"#":"Age"}, "Right": 18 },
    { "$": "contains", "Left": {"#":"UserName"}, "Right": {"@":"admin"} }
  ]
}
```

---

## 进阶功能 (Advanced Usage)

### 1. 自动化关联查询 (Join) [参考 Demos\QueryUsageDemo.cs]
利用 `[ForeignColumn]` 特性，LiteOrm 能在查询时自动识别关联关系并生成高效的 SQL JOIN。

**实体定义：**
```csharp
public class UserView : User {
    [ForeignColumn("Dept", Property = "Name")] // 关联 Dept 别名对应表的 Name 字段
    public string? DeptName { get; set; }
}
```

**演示输出：**
```text
--- 关联查询演示 (自动带出负责人和上级部门) ---
 ID: 1, 部门: 集团总部, 负责人: Admin, 上级: 顶级
```

### 2. 动态分表路由 (IArged) [参考 Demos\QueryUsageDemo.cs]
通过实现 `IArged` 接口，支持按参数（如月份）动态路由物理表（如 `SALES_202401`）。

**代码示例：**
```csharp
// 传入 tableArgs 参数 "202401"，SQL 将指向 SALES_202401 物理表
var sales = await salesService.SearchAsync(null, ["202401"]);
```

### 3. 三层架构与声明式事务 [参考 Demos\TransactionDemo.cs]
演示如何使用 `[Transaction]` 特性配合 AOP 拦截器实现无侵入的事务控制。

**异常回滚输出展示：**
```text
fail: LiteOrm.Service.ServiceInvokeInterceptor
      <Exception> BusinessService.RegisterUserWithInitialSaleAsync ... System.Exception: 模拟异常
事务执行失败并已回滚: 模拟异常
验证：通过 ID 查询不到用户记录，确认数据已完整回滚。
```

### 4. 批量操作性能优化 [参考 Demos\PerformanceDemo.cs]
对比 `BatchInsertAsync` 相比单条循环插入的性能提升。

**测试结果示例：**
```text
--- 批量插入 (BatchInsert) vs 循环插入 (Insert) 耗时对比 ---
循环插入 100 条: 551 ms
BatchInsert 批量插入 100 条: 10 ms (性能提升 50 倍+)
```

### 5. 自定义 DAO 与 SQL 扩展 [参考 Demos\CustomDaoDemo.cs]
展示如何通过自定义存储过程或 `GenericSqlExpr` 扩展逻辑。
- **GenericSqlExpr**: 注册复用度高的复杂 SQL 片段（如递归查询）。
- **Custom DAO**: 通过接口和实现类自由扩展复杂数据逻辑。

---

## 代码结构

- `Demos/`: 按功能分拆的演示代码实现。
- `Models/`: 实体与视图模型定义（含映射特性配置）。
- `Services/`: 基于 `EntityService<T>` 的业务封装服务及接口。
- `DAO/`: 自定义数据访问层接口及其实现。
- `Data/`: 数据库初始化脚本与种子数据生成逻辑。