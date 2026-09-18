# 数据权限

数据权限的本质，是把「当前用户是谁、什么角色」翻译成查询条件。LiteOrm 有两种实现方式，差别只在条件落在哪：

| 方式 | 条件落在哪 | 适合 |
| --- | --- | --- |
| 手动构造 `Expr` | 写进查询，随调用方传入 | 入口有限、规则需要经常调试与单测 |
| `GenericSqlExpr` 写入 `ConstFilter` | 挂到表定义，任何入口自动带上 | 规则在大量入口复用、相对稳定 |

两种方式都能表达同一条角色规则。全文用一组角色贯穿：**管理员看全部，经理看自己和本部门，普通员工只看自己的数据**。实体上有 `OwnerId`（归属人）与 `DeptId`（归属部门）两列，当前用户提供 `Id`、`DeptId`、`IsAdmin`、`IsManager` 四个值。

## 方式一：手动构造 `Expr`

把规则收敛成一个条件拼装函数，按角色返回不同条件，列表、统计、导出共用同一个函数：

```csharp
using static LiteOrm.Common.Expr;

public static class OrderScopes
{
    public static LogicExpr For(CurrentUser user)
    {
        if (user.IsAdmin) return null;                 // 管理员不限制

        var own = Prop(nameof(Order.OwnerId)) == user.Id;

        if (user.IsManager)
            return own | (Prop(nameof(Order.DeptId)) == user.DeptId);

        return own;
    }
}
```

```csharp
var page = await orderService.SearchAsync(
    From<Order>().Where(OrderScopes.For(user) & Prop(nameof(Order.IsDeleted)) == false)
                 .OrderBy(Prop(nameof(Order.CreateTime)).Desc())
                 .Section(0, 20));

var total = await orderService.CountAsync(OrderScopes.For(user));
```

要点：

- 一个函数按角色返回不同条件，调用方不再自搭分支。管理员返回 `null`，在 `&` 组合里被自动忽略，得到的就是全表。
- 经理分支是 `OR`，子条件都要各自带上部门或归属，别拆成「经理查部门、员工查自己」两个独立入口。
- 只限定真正写进查询的那张表。关联进来的表、`UpdateAll` / `DeleteAll` 的 `WHERE`、按主键的读路径都不自动带，需要另配；批量写入要把 `For(user)` 塞进同一个表达式。

## 方式二：`GenericSqlExpr` 写入 `ConstFilter`

条件挂到表定义，任何入口自动带上。先注册一个按角色返回片段的构件：

```csharp
using LiteOrm.Common;
using static LiteOrm.Common.Expr;

GenericSqlExpr.Register("OwnerScope", (context, sqlBuilder, outputParams, _) =>
{
    var user = UserContext.Current ?? throw new InvalidOperationException("User not resolved.");
    if (user.IsAdmin) return null;                     // 管理员：不产生片段

    // 内部构造 PropertyExpr（Prop(...)）组装相等条件；与具体类型比较已实现运算符重载
    var own = Prop(nameof(Order.OwnerId)) == user.Id;

    var condition = user.IsManager
        ? own | (Prop(nameof(Order.DeptId)) == user.DeptId)
        : own;

    return condition.ToSql(context, sqlBuilder, outputParams);
});
```

再把 `ConstFilter` 设为这个构件。`TableDefinition.ConstFilter` 是公开可写的 `get; set;` 属性，取到表定义直接赋值即可，不需要自定义元数据提供器：

```csharp
foreach (var type in typeof(Order).Assembly.GetTypes())
{
    var tableDefinition = TableInfoProvider.Instance.GetTableDefinition(type);
    if (tableDefinition is null || !type.IsAssignableTo(typeof(IUserScoped))) continue;

    tableDefinition.ConstFilter &= Expr.Sql("OwnerScope");   // &= 保留表定义已有条件，叠加当前规则
}
```

要点：

- 角色分支在 SQL 生成那一刻重新计算，每次查询都重新读一遍当前用户，切换即生效，不是登录时的快照。
- 列引用不再手拼裸列名，而是用 `Prop(...)` 构造 `PropertyExpr`，交给 `ExprSqlConverter.ToSql` 渲染，列名自动带上当前表别名（如 `"T0"."OwnerId"`）。与具体类型（如 `user.Id`）比较走已实现的运算符重载，等价参数化，无需手写 `Value(...)`，也无需维护 `outputParams` 下标。
- 构件返回的 SQL 走参数化，值不拼进文本；管理员返回 `null`，片段被忽略。

`GenericSqlExpr` 与 `ConstFilter` 的底层机制（生效范围：主键读、`JOIN ON`、`EXISTS`、`UPDATE`/`DELETE`，以及性能代价与 NativeAOT 差异）与[多租户隔离](./tenant-isolation.md)示例三一致，此处不再重复。

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