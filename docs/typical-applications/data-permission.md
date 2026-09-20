# 数据权限

数据权限的本质，是把「当前用户是谁、什么角色」翻译成查询条件。LiteOrm 有两种实现方式，差别只在条件落在哪：

| 方式 | 条件落在哪 | 适合 |
| --- | --- | --- |
| 手动构造 `Expr` | 写进查询，随调用方传入 | 入口有限、规则需要经常调试与单测 |
| `GenericSqlExpr` 写入 `ConstFilter` | 挂到表定义，任何入口自动带上 | 规则在大量入口复用、相对稳定 |

两种方式都能表达同一条角色规则。下面先按一个真实需求把场景立起来，再用两种方式各自完整实现一遍。

## 需求场景

内部采购系统里的「订单查询」页面。`Orders` 表已存在：

| 列 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | `long` | 主键，自增 |
| `Title` | `string` | 订单标题 |
| `Amount` | `decimal` | 金额 |
| `OwnerId` | `int` | 下单人（归属人） |
| `DeptId` | `int` | 归属部门 |
| `Status` | `int` | 订单状态 |
| `CreateTime` | `DateTime` | 下单时间 |

访问角色与可见范围约定：

- **管理员**：看全部订单。
- **部门经理**：看本部门全部订单 + 自己在其他部门下的单。
- **普通员工**：只看自己下的单。

当前用户信息由认证体系给出，收口在一个 `CurrentUserContext` 里：

```csharp
public sealed class CurrentUser
{
    public int Id { get; init; }         // 用户 Id
    public int DeptId { get; init; }     // 所属部门
    public bool IsAdmin { get; init; }
    public bool IsManager { get; init; }
}

// 请求作用域：登录中间件写入，SQL 生成时读取
public static class CurrentUserContext
{
    private static readonly AsyncLocal<CurrentUser?> _current = new();
    public static CurrentUser? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
```

订单实体与两种方式共用的服务声明：

```csharp
[Table("Orders")]
public class Order
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("Title")] public string? Title { get; set; }
    [Column("Amount")] public decimal Amount { get; set; }
    [Column("OwnerId")] public int OwnerId { get; set; }
    [Column("DeptId")] public int DeptId { get; set; }
    [Column("Status")] public int Status { get; set; }
    [Column("CreateTime")] public DateTime CreateTime { get; set; }
}

// DI 注册（任选其一）
builder.Services.AddScoped<IEntityViewServiceAsync<Order>, EntityService<Order>>();
```

列表页一次查询要同时取「当页数据」和「总数」，还带业务条件（只看进行中的单）与排序分页。下面分别用两种方式实现。

## 方式一：手动构造 `Expr`

把角色规则收敛成一个条件拼装函数，列表、统计、导出共用：

```csharp
using static LiteOrm.Common.Expr;

public static class OrderScopes
{
    public static LogicExpr? For(CurrentUser user)
    {
        if (user.IsAdmin) return null;                 // 管理员：不限制

        var own = Prop(nameof(Order.OwnerId)) == user.Id;

        return user.IsManager
            ? own | (Prop(nameof(Order.DeptId)) == user.DeptId)
            : own;
    }
}
```

列表页在查询里显式带上：

```csharp
var user = CurrentUserContext.Current!;

var page = await orderService.SearchAsync(
    From<Order>()
        .Where(OrderScopes.For(user) & (Prop(nameof(Order.Status)) == 0))
        .OrderBy(Prop(nameof(Order.CreateTime)).Desc())
        .Section(0, 20));

var total = await orderService.CountAsync(OrderScopes.For(user) & (Prop(nameof(Order.Status)) == 0));
```

以员工 `Id=1001`（非管理员）为例，`SearchAsync` 最终生成的 SQL（以 SQL Server 方言示意）：

```sql
SELECT [T0].[Id], [T0].[Title], [T0].[Amount], [T0].[OwnerId],
       [T0].[DeptId], [T0].[Status], [T0].[CreateTime]
FROM [Orders] [T0]
WHERE ([T0].[OwnerId] = @0) AND ([T0].[Status] = @1)
ORDER BY [T0].[CreateTime] DESC
OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY
-- @0 = 1001, @1 = 0
```

以经理 `Id=2001, DeptId=20` 为例，`CountAsync` 生成的 SQL：

```sql
SELECT COUNT(*) FROM [Orders] [T0]
WHERE (([T0].[OwnerId] = @0) OR ([T0].[DeptId] = @1)) AND ([T0].[Status] = @2)
-- @0 = 2001, @1 = 20, @2 = 0
```

管理员 `IsAdmin = true` 时 `For` 返回 `null`，`&` 组合自动忽略，SQL 里只剩业务条件：

```sql
SELECT [T0].[Id], [T0].[Title], [T0].[Amount], [T0].[OwnerId],
       [T0].[DeptId], [T0].[Status], [T0].[CreateTime]
FROM [Orders] [T0]
WHERE ([T0].[Status] = @0)
ORDER BY [T0].[CreateTime] DESC
OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY
-- @0 = 0
```

要点：

- 一个函数按角色返回不同条件，调用方不再自搭分支。管理员返回 `null`，在 `&` 组合里被自动忽略。
- 经理分支是 `OR`，子条件都要各自带上部门或归属，别拆成「经理查部门、员工查自己」两个独立入口。
- 只限定真正写进查询的那张表。关联进来的表、`UpdateAll` / `DeleteAll` 的 `WHERE`、按主键的读路径都不自动带，需要另配；批量写入要把 `For(user)` 塞进同一个表达式。
- 条件就写在查询里，读代码一眼可见，断点、单测都落在 `OrderScopes.For` 这一个函数上。

## 方式二：`GenericSqlExpr` 写入 `ConstFilter`

规则一旦要覆盖几十个入口（列表、统计、导出、批量写、按主键读），逐入口带条件的写法漏一个就静默越权。把条件挂到表定义，任何入口自动带上。

先注册一个按角色返回片段的构件：

```csharp
using LiteOrm.Common;
using static LiteOrm.Common.Expr;

GenericSqlExpr.Register("OwnerScope", (context, _) =>
{
    var user = CurrentUserContext.Current
        ?? throw new InvalidOperationException("User not resolved.");
    if (user.IsAdmin) return null;                     // 管理员：不产生片段

    var own = Prop(nameof(Order.OwnerId)) == user.Id;

    var condition = user.IsManager
        ? own | (Prop(nameof(Order.DeptId)) == user.DeptId)
        : own;

    return condition.ToSql(context);
});
```

再把 `ConstFilter` 设为这个构件。`TableDefinition.ConstFilter` 是公开可写的 `get; set;` 属性，取到表定义直接赋值即可，不需要自定义元数据提供器：

```csharp
public static class OrderScopeSetup
{
    public static void Enable()
    {
        var tableDefinition = TableInfoProvider.Instance.GetTableDefinition(typeof(Order))
            ?? throw new InvalidOperationException("Table definition not found.");
        tableDefinition.ConstFilter &= Expr.Sql("OwnerScope");   // &= 保留已有条件，叠加当前规则
    }
}
```

启动时调用一次 `OrderScopeSetup.Enable()`。此后列表页只需写业务条件，权限条件自动注入：

```csharp
var page = await orderService.SearchAsync(
    From<Order>()
        .Where(Prop(nameof(Order.Status)) == 0)
        .OrderBy(Prop(nameof(Order.CreateTime)).Desc())
        .Section(0, 20));

var order = await orderService.GetObjectAsync(12345);   // 主键读同样受限
```

员工 `Id=1001` 的 `SearchAsync` 生成：

```sql
SELECT [T0].[Id], [T0].[Title], [T0].[Amount], [T0].[OwnerId],
       [T0].[DeptId], [T0].[Status], [T0].[CreateTime]
FROM [Orders] [T0]
WHERE (([T0].[Status] = @0) AND ([T0].[OwnerId] = @1))
ORDER BY [T0].[CreateTime] DESC
OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY
-- @0 = 0, @1 = 1001
```

经理 `Id=2001, DeptId=20` 时，片段展开为 `OR` 分支：

```sql
SELECT [T0].[Id], [T0].[Title], [T0].[Amount], [T0].[OwnerId],
       [T0].[DeptId], [T0].[Status], [T0].[CreateTime]
FROM [Orders] [T0]
WHERE (([T0].[Status] = @0) AND (([T0].[OwnerId] = @1) OR ([T0].[DeptId] = @2)))
ORDER BY [T0].[CreateTime] DESC
OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY
-- @0 = 0, @1 = 2001, @2 = 20
```

`GetObjectAsync(12345)` 走主键读路径，条件同样被带上，查不到归属不符的行：

```sql
SELECT [T0].[Id], [T0].[Title], [T0].[Amount], [T0].[OwnerId],
       [T0].[DeptId], [T0].[Status], [T0].[CreateTime]
FROM [Orders] [T0]
WHERE ([T0].[Id] = @0) AND ([T0].[OwnerId] = @1)
-- @0 = 12345, @1 = 1001
```

管理员不产生片段，SQL 与不带权限规则的语句完全一致；但只要表定义声明了 `ConstFilter`，该表就不复用预定义命令缓存，这一点与当前用户是谁无关。

要点：

- 角色分支在 SQL 生成那一刻重新计算，每次查询都重新读一遍当前用户，切换即生效，不是登录时的快照。
- 列引用用 `Prop(...)` 构造 `PropertyExpr`，交给 `ExprSqlConverter.ToSql` 渲染，列名自动带上当前表别名（如 `[T0].[OwnerId]`）。与具体类型（如 `user.Id`）比较走已实现的运算符重载，等价参数化，无需手写 `Value(...)`，也无需维护 `context.OutputParams` 下标。
- 构件返回的 SQL 走参数化，值不拼进文本；管理员返回 `null`，片段被忽略。
- `ConstFilter` 不只约束主表：本表作为 `JOIN` 的右表或 `EXISTS` 子查询的目标表时，其 `ConstFilter` 仍会叠加生效，范围条件随关联路径一起带上。
- 声明了 `ConstFilter` 的表不复用预定义命令缓存，每次操作重新拼接 SQL；两种方式的性能与 NativeAOT 差异详见[多租户隔离](./tenant-isolation.md)示例三。

## 两种方式怎么选

| 场景 | 手动构造 `Expr` | `GenericSqlExpr` + `ConstFilter` |
| --- | --- | --- |
| 列表、详情、统计共用一条范围规则，入口不多 | 合适 | 偏重：为一个入口挂全局表定义 |
| 同一条规则要覆盖几十个查询、批量写与主键读 | 每个入口都要带/另配，漏一个就静默越权 | 合适：挂表定义后自动全覆盖 |
| 规则常调，想断点、单测验证 | 合适：改的就是拼装函数 | 不方便：规则进入 SQL 生成路径，难单测 |
| 想让范围条件在查询里显式可见、可读 | 合适：条件就写在查询里 | 隐藏：规则藏在表定义里，读代码看不出限制 |
| 主键直接读（`GetObjectAsync`）也要受限 | 不覆盖，需另做对象级校验 | 自动带上主键读路径 |

一句话：入口少、规则常调、要显式可读，用手动 `Expr`；规则要被很多入口（含主键读、关联、批量写）稳定复用，用 `ConstFilter`。

## 按主键读取也落在范围内

挂在 `ConstFilter` 上的条件（方式二）会被 `GetObject` / `GetObjectAsync` / `ExistsKey` 等主键读路径自动识别并带上。方式一（手动拼 `Expr`）不覆盖主键读，`GetObjectAsync(id)` 拿到对象后仍要单独判断归属（`404` 与 `403` 分开返回）。对象级校验必须读主库，只读副本的滞后数据会误判归属，见[并发控制与读写分离](./concurrency-and-read-write-splitting.md)。

## 相关链接

- [返回目录](../README.md)
- [多租户隔离](./tenant-isolation.md)
- [软删除与历史数据](./soft-delete-and-archive.md)
- [审计与变更追踪](./audit-and-change-tracking.md)
- [安全性](../advanced-topics/security.md)
